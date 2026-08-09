using System.Buffers.Binary;
using System.Text;

namespace BootCampCharge;

/// <summary>
/// Encoding/decoding of SMC value types. Multi-byte integers and fixed-point values
/// are big-endian on the wire; "flt " (IEEE 754 single) is little-endian.
/// Fixed-point families: fpXY = unsigned, spXY = signed with a sign bit; the last
/// type character is the number of fractional bits in hex (e.g. fpe2 = 14.2, sp78 = 7.8).
/// </summary>
public static class SmcValue
{
    public static double? ToNumber(string type, byte[] data)
    {
        switch (type)
        {
            case "flt " when data.Length == 4:
                return BitConverter.ToSingle(data);
            case "ui8 " when data.Length >= 1:
                return data[0];
            case "ui16" when data.Length == 2:
                return BinaryPrimitives.ReadUInt16BigEndian(data);
            case "ui32" when data.Length == 4:
                return BinaryPrimitives.ReadUInt32BigEndian(data);
            case "si8 " when data.Length >= 1:
                return (sbyte)data[0];
            case "si16" when data.Length == 2:
                return BinaryPrimitives.ReadInt16BigEndian(data);
            case "flag" when data.Length >= 1:
                return data[0];
        }

        if (data.Length == 2 && type.Length == 4)
        {
            int frac = HexDigit(type[3]);
            if (frac >= 0 && type[0] == 'f' && type[1] == 'p')
                return BinaryPrimitives.ReadUInt16BigEndian(data) / (double)(1 << frac);
            if (frac >= 0 && type[0] == 's' && type[1] == 'p')
                return BinaryPrimitives.ReadInt16BigEndian(data) / (double)(1 << frac);
        }
        return null;
    }

    public static byte[] FromNumber(string type, int length, double value)
    {
        switch (type)
        {
            case "flt ":
                return BitConverter.GetBytes((float)value);
            case "ui8 ":
                return [(byte)value];
            case "ui16":
                {
                    var b = new byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)value);
                    return b;
                }
            case "ui32":
                {
                    var b = new byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(b, (uint)value);
                    return b;
                }
            case "flag":
                return [(byte)(value != 0 ? 1 : 0)];
        }

        if (type.Length == 4)
        {
            int frac = HexDigit(type[3]);
            if (frac >= 0 && type[0] == 'f' && type[1] == 'p')
            {
                var b = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)Math.Round(value * (1 << frac)));
                return b;
            }
            if (frac >= 0 && type[0] == 's' && type[1] == 'p')
            {
                var b = new byte[2];
                BinaryPrimitives.WriteInt16BigEndian(b, (short)Math.Round(value * (1 << frac)));
                return b;
            }
        }
        throw new SmcException($"don't know how to encode SMC type '{type}' ({length} bytes)");
    }

    public static string Format(string type, byte[] data)
    {
        var number = ToNumber(type, data);
        if (number != null)
            return number.Value.ToString("0.##");
        if (type.StartsWith("ch8"))
            return Encoding.ASCII.GetString(data).TrimEnd('\0');
        return Convert.ToHexString(data);
    }

    private static int HexDigit(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };
}
