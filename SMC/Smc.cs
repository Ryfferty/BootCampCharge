using System.Text;

namespace BootCampCharge;

/// <summary>
/// The SMC as a keyed store, over whichever transport this Mac provides.
/// </summary>
public sealed class Smc(ISmcTransport transport) : IDisposable
{
    /// <summary>ACPI hardware id of the SMC on every Intel Mac.</summary>
    public const string AcpiId = @"ACPI\APP0001";

    private readonly Dictionary<string, SmcKeyInfo?> _infoCache = [];

    public ISmcTransport Transport => transport;

    /// <summary>
    /// Locates the SMC and opens it, preferring the memory-mapped interface (T2 Macs)
    /// and falling back to the legacy port interface. <paramref name="log"/> receives
    /// a line per step, for the diag command.
    /// </summary>
    public static Smc Open(Action<string>? log = null)
    {
        log ??= _ => { };
        if (!InpOut.DriverOpen())
            throw new InvalidOperationException(
                "the InpOut kernel driver is not loaded. Run 'fancamp install-driver' once (it asks for admin).");

        var instances = DeviceResources.FindInstances(AcpiId).ToList();
        if (instances.Count == 0)
            throw new SmcException(
                $"no {AcpiId} device found. This tool only works on Intel Macs running Windows.");

        foreach (string instance in instances)
        {
            log($"device {instance}");
            var res = DeviceResources.Query(instance);
            if (res == null)
            {
                log("  no allocated resources");
                continue;
            }
            foreach (var m in res.Memory)
                log($"  memory 0x{m.Start:X}-0x{m.End:X} ({m.Length} bytes)");
            foreach (var io in res.Io)
                log($"  io     0x{io.Start:X}-0x{io.End:X}");

            foreach (var m in res.Memory)
            {
                var mmio = MmioTransport.TryOpen(m.Start, m.Length, out string failure);
                if (mmio != null)
                {
                    log($"  using {mmio.Name}");
                    return new Smc(mmio);
                }
                log($"  mmio at 0x{m.Start:X} unusable: {failure}");
            }

            foreach (var io in res.Io)
            {
                var port = new PortTransport((ushort)io.Start);
                try
                {
                    port.GetKeyInfo("#KEY");
                    log($"  using {port.Name}");
                    return new Smc(port);
                }
                catch (SmcException e)
                {
                    log($"  port 0x{io.Start:X} unusable: {e.Message}");
                    port.Dispose();
                }
            }
        }
        throw new SmcException(
            "found the SMC device but no usable interface. Run 'fancamp diag' and open an issue with the output.");
    }

    public uint KeyCount()
    {
        Span<byte> data = stackalloc byte[4];
        transport.ReadKey("#KEY", data);
        return (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
    }

    public byte[] ReadKey(string key, int length)
    {
        var buffer = new byte[length];
        transport.ReadKey(key, buffer);
        return buffer;
    }

    public void WriteKey(string key, ReadOnlySpan<byte> data) => transport.WriteKey(key, data);

    public string GetKeyByIndex(uint index) => transport.GetKeyByIndex(index);

    /// <summary>Key metadata, cached; null when the key does not exist on this machine.</summary>
    public SmcKeyInfo? Info(string key)
    {
        if (!_infoCache.TryGetValue(key, out var info))
        {
            try
            {
                info = transport.GetKeyInfo(key);
            }
            catch (SmcException)
            {
                info = null;
            }
            _infoCache[key] = info;
        }
        return info;
    }

    public bool KeyExists(string key) => Info(key) != null;

    public double? ReadNumber(string key)
    {
        var info = Info(key);
        if (info == null || info.Length == 0)
            return null;
        return SmcValue.ToNumber(info.Type, ReadKey(key, info.Length));
    }

    public string ReadString(string key)
    {
        var info = Info(key);
        if (info == null)
            return "";
        return Encoding.ASCII.GetString(ReadKey(key, info.Length)).Trim('\0', ' ');
    }

    public void Dispose() => transport.Dispose();
}
