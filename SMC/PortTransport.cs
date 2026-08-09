using System.Text;

namespace BootCampCharge;

/// <summary>
/// Legacy port-based SMC interface (pre-T2 Intel Macs): data on base+0,
/// command/status on base+4, base normally 0x300.
/// </summary>
public sealed class PortTransport(ushort portBase) : ISmcTransport
{
    private const byte StatusDataReady = 0x01;
    private const byte StatusSettling = 0x02;
    private const byte StatusAccepted = 0x04;
    private const int Retries = 3;

    private readonly ushort _data = portBase;
    private readonly ushort _cmd = (ushort)(portBase + 4);
    private readonly object _gate = new();

    public string Name => $"port 0x{_data:X3}";

    public void Dispose() { }

    public void ReadKey(string key, Span<byte> buffer)
        => Transact(SmcProtocol.CmdRead, SmcProtocol.KeyBytes(key), buffer, default, $"read {key}");

    public void WriteKey(string key, ReadOnlySpan<byte> data)
        => Transact(SmcProtocol.CmdWrite, SmcProtocol.KeyBytes(key), default, data, $"write {key}");

    public SmcKeyInfo GetKeyInfo(string key)
    {
        Span<byte> info = stackalloc byte[6];
        Transact(SmcProtocol.CmdKeyInfo, SmcProtocol.KeyBytes(key), info, default, $"key info {key}");
        return new SmcKeyInfo(key, info[0], Encoding.ASCII.GetString(info.Slice(1, 4)), info[5]);
    }

    public string GetKeyByIndex(uint index)
    {
        Span<byte> name = stackalloc byte[4];
        Transact(SmcProtocol.CmdKeyByIndex, SmcProtocol.IndexBytes(index), name, default, $"key at {index}");
        return Encoding.ASCII.GetString(name);
    }

    private void Transact(byte cmd, ReadOnlySpan<byte> argument, Span<byte> readBuffer,
        ReadOnlySpan<byte> writeBuffer, string what)
    {
        lock (_gate)
        {
            for (int attempt = 0; attempt < Retries; attempt++)
            {
                if (TryTransact(cmd, argument, readBuffer, writeBuffer))
                    return;
                Drain();
                Thread.Sleep(2 * (attempt + 1));
            }
        }
        throw new SmcException($"SMC transaction failed: {what}");
    }

    private bool TryTransact(byte cmd, ReadOnlySpan<byte> argument, Span<byte> readBuffer,
        ReadOnlySpan<byte> writeBuffer)
    {
        if (!SendByte(cmd, _cmd))
            return false;
        foreach (byte b in argument)
            if (!SendByte(b, _data))
                return false;

        int payload = readBuffer.Length > 0 ? readBuffer.Length : writeBuffer.Length;
        if (!SendByte((byte)payload, _data))
            return false;
        foreach (byte b in writeBuffer)
            if (!SendByte(b, _data))
                return false;

        for (int i = 0; i < readBuffer.Length; i++)
        {
            if (!WaitRead())
                return false;
            readBuffer[i] = InpOut.DlPortReadPortUchar(_data);
        }
        Drain();
        return true;
    }

    /// <summary>
    /// Write a byte and wait for it to be accepted. The SMC ignores bytes that arrive
    /// while it is busy, so resend once per iteration after backing off.
    /// </summary>
    private bool SendByte(byte value, ushort port)
    {
        InpOut.DlPortWritePortUchar(port, value);
        for (int i = 0; i < 16; i++)
        {
            SmcProtocol.MicroDelay(16);
            byte status = InpOut.DlPortReadPortUchar(_cmd);
            if ((status & StatusSettling) != 0)
                continue;
            if ((status & StatusAccepted) != 0)
                return true;
            if (i == 15)
                break;
            SmcProtocol.MicroDelay(256);
            InpOut.DlPortWritePortUchar(port, value);
        }
        return false;
    }

    private bool WaitRead()
    {
        for (int us = 16; us < 0x8000; us <<= 1)
        {
            SmcProtocol.MicroDelay(us);
            if ((InpOut.DlPortReadPortUchar(_cmd) & StatusDataReady) != 0)
                return true;
        }
        return false;
    }

    private void Drain()
    {
        for (int i = 0; i < 16; i++)
        {
            SmcProtocol.MicroDelay(16);
            if ((InpOut.DlPortReadPortUchar(_cmd) & StatusDataReady) == 0)
                break;
            _ = InpOut.DlPortReadPortUchar(_data);
        }
    }
}
