using System.Diagnostics;
using System.Text;

namespace BootCampCharge;

public sealed class SmcException(string message) : Exception(message);

public sealed record SmcKeyInfo(string Key, int Length, string Type, byte Flags);

/// <summary>
/// One way of talking to the SMC. Intel Macs use either the legacy port interface
/// or, on T2 machines, a memory-mapped one. See docs/PROTOCOL.md.
/// </summary>
public interface ISmcTransport : IDisposable
{
    string Name { get; }
    void ReadKey(string key, Span<byte> buffer);
    void WriteKey(string key, ReadOnlySpan<byte> data);
    SmcKeyInfo GetKeyInfo(string key);
    string GetKeyByIndex(uint index);
}

internal static class SmcProtocol
{
    internal const byte CmdRead = 0x10;
    internal const byte CmdWrite = 0x11;
    internal const byte CmdKeyByIndex = 0x12;
    internal const byte CmdKeyInfo = 0x13;

    internal static byte[] KeyBytes(string key)
    {
        if (key.Length != 4)
            throw new ArgumentException($"SMC keys are exactly 4 characters, got '{key}'");
        return Encoding.ASCII.GetBytes(key);
    }

    internal static byte[] IndexBytes(uint index) =>
        [(byte)(index >> 24), (byte)(index >> 16), (byte)(index >> 8), (byte)index];

    /// <summary>Busy-wait with microsecond granularity; Thread.Sleep is far too coarse.</summary>
    internal static void MicroDelay(int microseconds)
    {
        long ticks = microseconds * Stopwatch.Frequency / 1_000_000;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedTicks < ticks)
            Thread.SpinWait(20);
    }
}
