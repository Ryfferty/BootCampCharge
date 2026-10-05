using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace BootCampCharge;

/// <summary>电池健康度信息。</summary>
public sealed class BatteryHealth
{
    public int? DesignCapacity { get; init; }
    public int? FullChargeCapacity { get; init; }
    public double? HealthPercent =>
        DesignCapacity.HasValue && FullChargeCapacity.HasValue && DesignCapacity > 0
            ? Math.Round(FullChargeCapacity.Value * 100.0 / DesignCapacity.Value, 1) : null;
    public int? DesignVoltage { get; init; }
    public int? CycleCount { get; init; }
}

/// <summary>电池当前状态快照。</summary>
public sealed class BatteryStatus
{
    public int? ChargePercent { get; init; }
    public int? ChargeLimit { get; init; }
    public double? CpuTemp { get; init; }
    public double? GpuTemp { get; init; }
    public bool AcConnected { get; init; }
    public bool IsCharging { get; init; }
    public BatteryHealth? Health { get; init; }
}

// ═══════════════════════════════════════════════════════════════════════════
// BatteryManager — 精简版
//
// Boot Camp 下 BCLM 是唯一可靠的充电控制方式（SmcDiag 实测验证）：
// - BCLM 写入是硬件级持久操作，固件自动执行，不需要程序持续运行
// - CH0B/放电/TopUp 在 Boot Camp 下不可靠（缺 IOPMAssertion 等价物）
//
// 设计原则：
// - BCLM 写入是事件驱动（用户改滑块时一次性写入），不在轮询中重复写
// - 轮询只读取状态用于 UI 显示，不写任何 SMC
// ═══════════════════════════════════════════════════════════════════════════

public sealed class BatteryManager : IDisposable
{
    private readonly Smc _smc;
    private bool _disposed;

    private const string KeyBclm = "BCLM";   // 充电上限 — ui8, R/W, 硬件持久
    private const string KeyTc0p = "TC0P";   // CPU 温度 — sp78, R only
    private const string KeyTg0d = "TG0D";   // GPU 温度 — sp78, R only

    private int _chargeLimit = 80;
    private bool _chargeLimitEnabled = true;
    private BatteryHealth? _healthCache;

    public BatteryManager(Smc smc) => _smc = smc ?? throw new ArgumentNullException(nameof(smc));

    public static BatteryManager Open(Action<string>? log = null) => new(Smc.Open(log));

    public void Dispose()
    {
        if (!_disposed) { _disposed = true; _smc.Dispose(); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BCLM 充电限制 — 唯一的写操作
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 设置充电限制。直接写 BCLM（一次性硬件写入，固件接管）。
    /// 100% = 清除限制（写 BCLM=100）。
    /// ⚠️ 2026-10-05 实测修正：本机固件语义 BCLM=0 = 「上限 0% = 停止充电」而非「无限制」。
    /// 旧版此处写 0 导致用户关闭限制后机器插电不充（今天 15:30 实际发生）。
    /// </summary>
    public void SetChargeLimit(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        _chargeLimit = percent;
        _chargeLimitEnabled = percent < 100;

        // BCLM: 1-100 = 充电上限，100 = 无限制（绝不能写 0 —— 0 = 停止充电）
        WriteBclm(percent >= 100 ? 100 : percent);
    }

    /// <summary>启动时从硬件同步当前 BCLM 值。</summary>
    public void SyncFromHardware()
    {
        int bclm = ReadBclm();
        if (bclm > 0 && bclm <= 100)
        {
            _chargeLimit = bclm;
            _chargeLimitEnabled = bclm < 100;
        }
        else if (bclm == 0)
        {
            // 遗留的 0 值（旧版本写入或异常状态）——主动修复为 100，防止「插电不充」复发
            _chargeLimit = 100;
            _chargeLimitEnabled = false;
            WriteBclm(100);
        }
    }

    public int ChargeLimit => _chargeLimit;
    public bool ChargeLimitEnabled => _chargeLimitEnabled;

    // ═══════════════════════════════════════════════════════════════════════
    // 只读状态（轮询调用，不写 SMC）
    // ═══════════════════════════════════════════════════════════════════════

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, Reserved1;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(ref SYSTEM_POWER_STATUS s);

    public BatteryStatus GetStatus()
    {
        int? charge = null;
        bool ac = false;
        bool charging = false;
        try
        {
            var sps = new SYSTEM_POWER_STATUS();
            if (GetSystemPowerStatus(ref sps))
            {
                if (sps.BatteryLifePercent <= 100) charge = sps.BatteryLifePercent;
                ac = sps.ACLineStatus == 1;
                charging = sps.BatteryFlag == 8;
            }
        }
        catch { }

        double? cpuTemp = null, gpuTemp = null;
        try { cpuTemp = _smc.ReadNumber(KeyTc0p); } catch { }
        try { gpuTemp = _smc.ReadNumber(KeyTg0d); } catch { }

        return new BatteryStatus
        {
            ChargePercent = charge,
            ChargeLimit = _chargeLimitEnabled ? _chargeLimit : null,
            CpuTemp = cpuTemp,
            GpuTemp = gpuTemp,
            AcConnected = ac,
            IsCharging = charging,
            Health = _healthCache
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 电池健康（WMI + powercfg）
    // ═══════════════════════════════════════════════════════════════════════

    public BatteryHealth? RefreshBatteryHealth()
    {
        try
        {
            int? dc = null, fc = null, v = null, cycles = null;

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                try { dc = obj["DesignCapacity"] as int?; } catch { }
                try { fc = obj["FullChargeCapacity"] as int?; } catch { }
                try { v = obj["DesignVoltage"] as int?; } catch { }
            }

            if (!fc.HasValue || !dc.HasValue || !cycles.HasValue)
            {
                var (d2, f2, c2) = ReadHealthFromPowerCfg();
                dc ??= d2; fc ??= f2; cycles ??= c2;
            }

            _healthCache = new BatteryHealth
            {
                DesignCapacity = dc,
                FullChargeCapacity = fc,
                DesignVoltage = v,
                CycleCount = cycles
            };
            return _healthCache;
        }
        catch { return _healthCache; }
    }

    private static (int? designCap, int? fullCap, int? cycles) ReadHealthFromPowerCfg()
    {
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"bcc_{Guid.NewGuid():N}.xml");
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = $"/batteryreport /output \"{tmp}\" /xml",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            if (!File.Exists(tmp)) return (null, null, null);
            var xml = File.ReadAllText(tmp);
            File.Delete(tmp);
            return (
                ExtractXmlInt(xml, "DesignCapacity"),
                ExtractXmlInt(xml, "FullChargeCapacity"),
                ExtractXmlInt(xml, "CycleCount")
            );
        }
        catch { return (null, null, null); }
    }

    private static int? ExtractXmlInt(string xml, string tag)
    {
        int bs = xml.IndexOf("<Battery>", StringComparison.OrdinalIgnoreCase);
        if (bs < 0) bs = 0;
        int ts = xml.IndexOf($"<{tag}>", bs, StringComparison.OrdinalIgnoreCase);
        if (ts < 0) return null;
        int vs = ts + tag.Length + 2, ve = xml.IndexOf('<', vs);
        if (ve <= vs) return null;
        return int.TryParse(xml.AsSpan(vs, ve - vs).Trim(), out var v) ? v : null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SMC 原始读写
    // ═══════════════════════════════════════════════════════════════════════

    private void WriteBclm(int value)
    {
        try { if (_smc.KeyExists(KeyBclm)) _smc.WriteKey(KeyBclm, new byte[] { (byte)value }); }
        catch { }
    }

    private int ReadBclm()
    {
        try
        {
            if (_smc.KeyExists(KeyBclm))
            {
                var d = _smc.ReadKey(KeyBclm, 1);
                if (d.Length >= 1) return d[0];
            }
        }
        catch { }
        return 0;
    }
}
