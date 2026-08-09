using System.Text;

namespace BootCampCharge;

/// <summary>
/// Memory-mapped SMC interface used by T2 Macs (2018 and later Intel models).
/// The register window is the ACPI memory resource of device APP0001; a transaction
/// writes the key name, an SMC id and a command byte, then polls a status byte.
/// See docs/PROTOCOL.md for the register map.
/// </summary>
public sealed unsafe class MmioTransport : ISmcTransport
{
    private const int OffData = 0x00;
    private const int OffKeyName = 0x78;
    private const int OffDataLen = 0x7D;
    private const int OffSmcId = 0x7E;
    private const int OffCmd = 0x7F;
    private const int OffStatus = 0x4005;
    internal const uint MinSize = 0x4006;

    // Offsets within the data window for a key-info reply.
    private const int OffTypeCode = 0x00;
    private const int OffTypeLen = 0x05;
    private const int OffTypeFlags = 0x06;

    private const byte StatusComplete = 0x20;

    private readonly byte* _base;
    private readonly IntPtr _handle;
    private readonly IntPtr _linear;
    private readonly ulong _physical;
    private readonly object _gate = new();
    private bool _disposed;

    private MmioTransport(IntPtr linear, IntPtr handle, ulong physical)
    {
        _linear = linear;
        _handle = handle;
        _physical = physical;
        _base = (byte*)linear;
    }

    public string Name => $"mmio 0x{_physical:X}";

    /// <summary>
    /// Maps the register window. Returns null when the region cannot be mapped or the
    /// SMC does not answer; callers should then fall back to the port interface.
    /// </summary>
    public static MmioTransport? TryOpen(ulong physicalAddress, uint size, out string failure)
    {
        failure = "";
        if (size < MinSize)
        {
            failure = $"memory resource too small ({size} < {MinSize})";
            return null;
        }
        IntPtr linear = InpOut.MapPhysToLin((IntPtr)(long)physicalAddress, size, out IntPtr handle);
        if (linear == IntPtr.Zero)
        {
            failure = "MapPhysToLin failed (is the InpOut driver installed?)";
            return null;
        }

        var transport = new MmioTransport(linear, handle, physicalAddress);

        // Apple's own driver sanity-checks that the status byte is not all-ones.
        byte status = transport.Read8(OffStatus);
        if (status == 0xFF)
        {
            failure = "status register reads 0xFF (no SMC behind this window)";
            transport.Dispose();
            return null;
        }

        // LDKN is the SMC key-interface version; the MMIO protocol needs at least 2.
        try
        {
            Span<byte> ldkn = stackalloc byte[1];
            transport.ReadKey("LDKN", ldkn);
            if (ldkn[0] < 2)
            {
                failure = $"LDKN version {ldkn[0]} is below the minimum of 2";
                transport.Dispose();
                return null;
            }
        }
        catch (SmcException e)
        {
            failure = $"LDKN probe failed ({e.Message})";
            transport.Dispose();
            return null;
        }
        return transport;
    }

    public void ReadKey(string key, Span<byte> buffer)
        => Command(SmcProtocol.CmdRead, SmcProtocol.KeyBytes(key), buffer, $"read {key}");

    public string GetKeyByIndex(uint index)
    {
        Span<byte> name = stackalloc byte[4];
        Command(SmcProtocol.CmdKeyByIndex, SmcProtocol.IndexBytes(index), name, $"key at {index}");
        return Encoding.ASCII.GetString(name);
    }

    public SmcKeyInfo GetKeyInfo(string key)
    {
        lock (_gate)
        {
            EnsureOpen();
            Submit(SmcProtocol.CmdKeyInfo, SmcProtocol.KeyBytes(key), $"key info {key}");
            Span<byte> type = stackalloc byte[4];
            for (int i = 0; i < 4; i++)
                type[i] = Read8(OffData + OffTypeCode + i);
            return new SmcKeyInfo(key, Read8(OffData + OffTypeLen),
                Encoding.ASCII.GetString(type), Read8(OffData + OffTypeFlags));
        }
    }

    public void WriteKey(string key, ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            EnsureOpen();
            ClearStatus();
            var name = SmcProtocol.KeyBytes(key);
            for (int i = 0; i < 4; i++)
                Write8(OffKeyName + i, name[i]);
            for (int i = 0; i < data.Length; i++)
                Write8(OffData + i, data[i]);
            Write8(OffDataLen, (byte)data.Length);
            Write8(OffSmcId, 0);
            Write8(OffCmd, SmcProtocol.CmdWrite);
            Complete($"write {key}");
        }
    }

    private void Command(byte cmd, ReadOnlySpan<byte> keyOrIndex, Span<byte> buffer, string what)
    {
        lock (_gate)
        {
            EnsureOpen();
            Submit(cmd, keyOrIndex, what);
            if (cmd == SmcProtocol.CmdRead)
            {
                byte remoteLen = Read8(OffDataLen);
                if (remoteLen != buffer.Length)
                    throw new SmcException(
                        $"{what}: length mismatch (SMC says {remoteLen}, asked for {buffer.Length})");
            }
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = Read8(OffData + i);
        }
    }

    private void Submit(byte cmd, ReadOnlySpan<byte> keyOrIndex, string what)
    {
        ClearStatus();
        for (int i = 0; i < 4; i++)
            Write8(OffKeyName + i, keyOrIndex[i]);
        Write8(OffSmcId, 0);
        Write8(OffCmd, cmd);
        Complete(what);
    }

    private void Complete(string what)
    {
        if (!WaitComplete())
            throw new SmcException($"{what}: SMC timed out");
        byte err = Read8(OffCmd);
        if (err != 0)
            throw new SmcException($"{what}: SMC returned error 0x{err:X2}");
    }

    private bool WaitComplete()
    {
        int us = 8;
        for (int i = 0; i < 24; i++)
        {
            if ((Read8(OffStatus) & StatusComplete) != 0)
                return true;
            SmcProtocol.MicroDelay(us);
            if (i > 9)
                us <<= 1;
        }
        return false;
    }

    private void ClearStatus()
    {
        if (Read8(OffStatus) != 0)
            Write8(OffStatus, 0);
    }

    private byte Read8(int offset) => Volatile.Read(ref _base[offset]);

    private void Write8(int offset, byte value) => Volatile.Write(ref _base[offset], value);

    /// <summary>
    /// Guards every transaction: touching the mapping after it is unmapped would
    /// dereference freed memory and take the process down with an access violation
    /// rather than a catchable exception.
    /// </summary>
    private void EnsureOpen()
    {
        if (_disposed)
            throw new SmcException("the SMC connection is already closed");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            InpOut.UnmapPhysicalMemory(_handle, _linear);
        }
    }
}
