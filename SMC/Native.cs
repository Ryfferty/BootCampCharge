using System.Runtime.InteropServices;

namespace BootCampCharge;

/// <summary>
/// Interop with the InpOut helper driver (https://www.highrez.co.uk/downloads/inpout32/),
/// which provides both x86 port I/O and physical-memory mapping from user mode.
/// The DLL installs its signed kernel driver on first use from an elevated process;
/// afterwards any user can use it.
/// </summary>
internal static class InpOut
{
    private const string Dll = "inpoutx64.dll";

    [DllImport(Dll)]
    private static extern bool IsInpOutDriverOpen();

    [DllImport(Dll)]
    internal static extern byte DlPortReadPortUchar(ushort port);

    [DllImport(Dll)]
    internal static extern void DlPortWritePortUchar(ushort port, byte value);

    [DllImport(Dll)]
    internal static extern IntPtr MapPhysToLin(IntPtr physAddr, uint size, out IntPtr physMemHandle);

    [DllImport(Dll)]
    internal static extern bool UnmapPhysicalMemory(IntPtr physMemHandle, IntPtr linAddr);

    internal static bool DriverOpen()
    {
        try
        {
            return IsInpOutDriverOpen();
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                $"{Dll} not found; it must sit next to fancamp.exe.");
        }
    }
}

/// <summary>
/// Reads the I/O port and memory ranges the firmware assigned to a device, via the
/// Configuration Manager API. Used to locate the SMC rather than hardcoding addresses.
/// </summary>
internal static class DeviceResources
{
    private const uint CrSuccess = 0;
    private const uint FilterEnumerator = 0x1;
    private const uint FilterPresent = 0x100;
    // Which logical configuration to read addresses from. Firmware devices such as the
    // SMC often expose an empty ALLOC config, so fall back to BOOT and then FILTERED/BASIC.
    private static readonly uint[] LogConfTypes = [2 /*ALLOC*/, 3 /*BOOT*/, 1 /*FILTERED*/, 0 /*BASIC*/];
    private const uint ResTypeAll = 0;
    private const uint ResTypeMem = 1;
    private const uint ResTypeIo = 2;
    private const uint ResTypeMemLarge = 7;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_List_SizeW(out uint len, string? filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_ListW(string? filter, char[] buffer, uint bufferLen, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_First_Log_Conf(out IntPtr logConf, uint devInst, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Next_Res_Des(out IntPtr resDes, IntPtr prev, uint forResource,
        out uint resourceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Res_Des_Data_Size(out uint size, IntPtr resDes, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Res_Des_Data(IntPtr resDes, byte[] buffer, uint bufferLen, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Free_Res_Des_Handle(IntPtr resDes);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Free_Log_Conf_Handle(IntPtr logConf);

    internal sealed record Range(ulong Start, ulong End)
    {
        internal uint Length => (uint)(End - Start + 1);
    }

    internal sealed record Resources(string InstanceId, List<Range> Io, List<Range> Memory);

    /// <summary>Finds present devices whose ID starts with the given ACPI hardware ID.</summary>
    internal static IEnumerable<string> FindInstances(string enumeratorFilter)
    {
        uint flags = FilterEnumerator | FilterPresent;
        if (CM_Get_Device_ID_List_SizeW(out uint len, enumeratorFilter, flags) != CrSuccess || len == 0)
            yield break;
        var buffer = new char[len];
        if (CM_Get_Device_ID_ListW(enumeratorFilter, buffer, len, flags) != CrSuccess)
            yield break;
        foreach (var id in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries))
            yield return id;
    }

    internal static Resources? Query(string instanceId)
    {
        if (CM_Locate_DevNodeW(out uint devInst, instanceId, 0) != CrSuccess)
            return null;
        foreach (uint logConfType in LogConfTypes)
        {
            var resources = ReadLogConf(instanceId, devInst, logConfType);
            // An empty or all-zero configuration means "not assigned here"; keep looking.
            if (resources != null && (resources.Io.Count > 0 || resources.Memory.Count > 0))
                return resources;
        }
        return null;
    }

    private static Resources? ReadLogConf(string instanceId, uint devInst, uint logConfType)
    {
        if (CM_Get_First_Log_Conf(out IntPtr logConf, devInst, logConfType) != CrSuccess)
            return null;

        var io = new List<Range>();
        var mem = new List<Range>();
        try
        {
            IntPtr prev = logConf;
            while (CM_Get_Next_Res_Des(out IntPtr resDes, prev, ResTypeAll, out uint type, 0) == CrSuccess)
            {
                if (prev != logConf)
                    CM_Free_Res_Des_Handle(prev);
                prev = resDes;

                if (CM_Get_Res_Des_Data_Size(out uint size, resDes, 0) != CrSuccess || size < 24)
                    continue;
                var data = new byte[size];
                if (CM_Get_Res_Des_Data(resDes, data, size, 0) != CrSuccess)
                    continue;

                // Both IO_DES and MEM_DES start: DWORD count, DWORD type, then
                // 8-byte-aligned base and end addresses.
                ulong start = BitConverter.ToUInt64(data, 8);
                ulong end = BitConverter.ToUInt64(data, 16);
                if (end <= start)
                    continue;
                if (type == ResTypeIo)
                    io.Add(new Range(start, end));
                else if (type is ResTypeMem or ResTypeMemLarge)
                    mem.Add(new Range(start, end));
            }
            if (prev != logConf)
                CM_Free_Res_Des_Handle(prev);
        }
        finally
        {
            CM_Free_Log_Conf_Handle(logConf);
        }
        return new Resources(instanceId, io, mem);
    }
}
