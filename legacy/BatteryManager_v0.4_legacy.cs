// ═══════════════════════════════════════════════════════════════════════════
// BootCampCharge v0.4 三层架构 — 放电/TopUp/航行/校准/过热保护
// ═══════════════════════════════════════════════════════════════════════════
//
// ⚠️ 这些功能在 Boot Camp 下经过 SmcDiag 实测验证后确认【不可用】。
// 保留此文件供未来参考——如果限制解除或有绕过方法，可直接复用。
//
// ═══════════════════════════════════════════════════════════════════════════
// 失败原因（SmcDiag 2026-08-09 实测）
// ═══════════════════════════════════════════════════════════════════════════
//
// 实验 A: BCLM=80 限制 86% 停充后，改 BCLM=0    → 充电=False ❌
// 实验 B: BCLM=80 限制后，改 BCLM=100          → 充电=False ❌
// 实验 C: BCLM=80 限制后，拔插充电器+BCLM=0    → 充电=False ❌
//
// 根因：macOS AlDente 放电/TopUp 依赖 IOPMAssertionCreateWithName（IOKit API）
// 来"模拟拔线"（simulate unplugging）。Windows Boot Camp 没有等价 API。
// SMC CH0B=2 只阻止充电器给电池充，不阻止充电器给系统供电 → 电池不掉电。
// BCLM 一旦触发限制（电量>BCLM 值），SMC 固件锁定"已停充"状态，
// 后续改 BCLM 无法恢复充电——只有电池自然跌到阈值以下才恢复。
//
// ═══════════════════════════════════════════════════════════════════════════
// 如果未来要恢复这些功能，可能的技术路线
// ═══════════════════════════════════════════════════════════════════════════
//
// 路线 A: 写 Windows 内核驱动模拟 IOPMAssertion
//   - WDM/WDF 内核驱动，拦截 ACPI 电池设备，向 Windows 电源管理器报告"AC 已断开"
//   - 需要内核驱动开发 + 数字签名 + 蓝屏风险
//
// 路线 B: ACPI DSDT/SSDT 补丁
//   - 修改 MacBook ACPI 表中电池设备的 _PSR（Power Source）方法
//   - 让 Windows 以为在用电池供电
//   - 非常危险，刷错不能开机
//
// 路线 C: 等 Apple Boot Camp 驱动更新
//   - Apple 的 AppleSMC.sys 驱动可能未来暴露更多接口
//   - 目前只暴露了 BCLM/CH0B 读写，没有放电控制接口
//
// ═══════════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace BootCampCharge.Legacy;

// ── 枚举 ──

public enum CalibrationPhase { Idle, Charging, Discharging, Recharging, Resting, Done }
public enum ChargingDecision { Charge, Inhibit, Hold }
internal enum OvertempState { Normal, Cooling, ChargingRecovery }

// ── 数据结构 ──

public sealed class BatteryHealth_Legacy
{
    public int? DesignCapacity { get; init; }
    public int? FullChargeCapacity { get; init; }
    public double? HealthPercent =>
        DesignCapacity.HasValue && FullChargeCapacity.HasValue && DesignCapacity > 0
            ? Math.Round(FullChargeCapacity.Value * 100.0 / DesignCapacity.Value, 1) : null;
    public int? DesignVoltage { get; init; }
    public int? CycleCount { get; init; }
}

public sealed class BatteryStatus_Legacy
{
    public int? ChargePercent { get; init; }
    public int? ChargeLimit { get; init; }
    public double? CpuTemp { get; init; }
    public bool AcConnected { get; init; }
    public bool IsInhibited { get; init; }
    public bool IsCharging => AcConnected && !IsInhibited;
}

// ═══════════════════════════════════════════════════════════════════════════
// BatteryManager v0.4 — 三层架构完整实现
//
// 第一层：功能方法（只设请求标志，不写 SMC）
// 第二层：EvaluateChargingState()（按优先级裁决）
// 第三层：ApplyDecision()（唯一写 SMC 的地方）
//
// Boot Camp 铁律（实测验证）：
// 1. BCLM 是唯一可靠的持久充电限制方式
// 2. CH0B 在 Boot Camp 下：BCLM 非零时 CH0B 被忽略
// 3. 用 CH0B 前必须先 BCLM=0，用完恢复 BCLM
// 4. BCLM 一旦触发限制后无法通过改 BCLM 恢复充电（固件锁定）
// ═══════════════════════════════════════════════════════════════════════════

public sealed class BatteryManagerLegacy : IDisposable
{
    private readonly Smc _smc;

    private const string KeyBclm = "BCLM";
    private const string KeyBrsc = "BRSC";
    private const string KeyTc0p = "TC0P";
    private const string KeyCh0B = "CH0B";
    private const string KeyCh0C = "CH0C";

    public const int DefaultSailFloor = 50;
    public const int CalibrationFloor = 10;
    public static readonly TimeSpan CalibrationRestPeriod = TimeSpan.FromHours(1);
    private static readonly TimeSpan OvertempWindow = TimeSpan.FromMinutes(5);

    // ── 第一层：请求标志 ──
    private int _chargeLimit = 80;
    private bool _chargeLimitEnabled = true;
    private bool _dischargeRequested;
    private int _dischargeTarget;
    private bool _topUpRequested;
    private bool _heatProtectionEnabled;
    private double _heatThreshold;
    private bool _sailingEnabled;
    private int _sailingFloor = DefaultSailFloor;
    private bool _autoDischargeEnabled;
    private int _autoDischargeMargin = 5;

    // ── 校准状态机 ──
    private CalibrationPhase _calPhase = CalibrationPhase.Idle;
    private int _calSavedLimit;
    private DateTime _calRestStart;

    // ── 第二层：运行时状态 ──
    private bool _chargeInhibited;
    private bool _ch0bMode;
    private int _savedBclm;
    private bool _sleepPrevented;

    // ── 过热状态机 ──
    private OvertempState _overtempState = OvertempState.Normal;
    private DateTime _overtempSince;

    public BatteryManagerLegacy(Smc smc) => _smc = smc ?? throw new ArgumentNullException(nameof(smc));
    public static BatteryManagerLegacy Open(Action<string>? log = null) => new(Smc.Open(log));

    public void Dispose() => _smc.Dispose();

    // ═══════════════════════════════════════════════════════════════════════
    // 启动同步
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 从硬件同步状态。处理程序崩溃后重启的场景。
    /// </summary>
    public void SyncStateFromHardware()
    {
        int bclm = ReadBclm();
        if (bclm > 0 && bclm <= 100)
        {
            _chargeLimit = bclm;
            _chargeLimitEnabled = bclm < 100;
        }

        var ch0bData = ReadKeySafe(KeyCh0B, 1);
        bool ch0bIsInhibited = ch0bData != null && ch0bData.Length == 1 && ch0bData[0] == 2;

        if (bclm == 0 && ch0bIsInhibited)
        {
            // 残留 CH0B 模式（上次崩溃了）→ 安全恢复
            _ch0bMode = true;
            _chargeInhibited = true;
            WriteCh0B(0);
            _chargeInhibited = false;
            int restoreVal = _chargeLimitEnabled ? _chargeLimit : 0;
            WriteBclm(restoreVal > 0 ? restoreVal : 80);
            _ch0bMode = false;
        }
        else
        {
            _ch0bMode = false;
            _chargeInhibited = ch0bIsInhibited && bclm == 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 第一层：功能方法（设请求标志）
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 设置充电限制。直接写 BCLM（一次性硬件写入）。
    /// ✅ 已验证可用。
    /// </summary>
    public void SetChargeLimit(int percent)
    {
        _chargeLimit = Math.Clamp(percent, 0, 100);
        _chargeLimitEnabled = percent < 100;
        if (_ch0bMode) ExitCh0BMode();
        WriteBclm(percent >= 100 ? 0 : percent);
    }

    /// <summary>
    /// 请求手动放电。
    /// ❌ Boot Camp 下不可用——CH0B=2 只阻止充电，充电器仍供系统电，电池不掉。
    /// </summary>
    public void RequestDischarge(int target = 0)
    {
        _topUpRequested = false;
        _dischargeRequested = true;
        _dischargeTarget = Math.Clamp(target, 0, 99);
    }

    public void CancelDischarge() => _dischargeRequested = false;

    /// <summary>
    /// 请求临时充满（BCLM=0 + CH0B=0）。
    /// ❌ Boot Camp 下不可用——BCLM 触发限制后改 0 不恢复充电（固件锁定）。
    /// </summary>
    public void RequestTopUp()
    {
        _dischargeRequested = false;
        _topUpRequested = true;
        if (_ch0bMode) ExitCh0BMode();
        WriteBclm(0);
        WriteCh0B(0);
    }

    /// <summary>
    /// 取消临时充满：恢复 BCLM。
    /// </summary>
    public void CancelTopUp()
    {
        if (!_topUpRequested) return;
        _topUpRequested = false;
        int restoreVal = _chargeLimitEnabled ? _chargeLimit : 0;
        WriteBclm(restoreVal > 0 ? restoreVal : 0);
    }

    /// <summary>
    /// 过热保护。
    /// ⚠️ 间接可用——降低 BCLM 值间接减少充电热量，但无法精确控制。
    /// </summary>
    public void SetHeatProtection(bool enabled, double threshold = 40)
    {
        _heatProtectionEnabled = enabled;
        _heatThreshold = threshold;
    }

    /// <summary>
    /// 航行模式（施密特触发器：上限停充 / 下限充回）。
    /// ❌ 依赖 CH0B 区间控制，Boot Camp 下不可用。
    /// </summary>
    public void SetSailingMode(bool enabled, int floor = DefaultSailFloor)
    {
        _sailingEnabled = enabled;
        _sailingFloor = Math.Clamp(floor, 10, 95);
    }

    /// <summary>
    /// 自动放电。
    /// ❌ 依赖 CH0B，Boot Camp 下不可用。
    /// </summary>
    public void SetAutoDischarge(bool enabled, int margin = 5)
    {
        _autoDischargeEnabled = enabled;
        _autoDischargeMargin = Math.Clamp(margin, 1, 20);
    }

    /// <summary>
    /// 校准模式（5 阶段状态机）。
    /// ❌ 放电阶段依赖 CH0B，Boot Camp 下不可用。
    /// </summary>
    public void StartCalibration()
    {
        _calSavedLimit = _chargeLimit;
        _calPhase = CalibrationPhase.Charging;
    }

    public void CancelCalibration()
    {
        if (_calPhase == CalibrationPhase.Idle) return;
        _calPhase = CalibrationPhase.Idle;
        _chargeLimit = _calSavedLimit;
    }

    // ── 状态查询 ──
    public bool IsDischarging => _dischargeRequested;
    public bool IsTopUpActive => _topUpRequested;
    public CalibrationPhase CalibrationState => _calPhase;
    public bool IsOvertempEngaged => _overtempState != OvertempState.Normal;
    public int ChargeLimit => _chargeLimit;
    public bool ChargeLimitEnabled => _chargeLimitEnabled;
    public bool IsInhibited => _chargeInhibited;

    // ═══════════════════════════════════════════════════════════════════════
    // 第二层：决策 — 按优先级链裁决
    // 优先级：过热 > 校准 > TopUp > 手动放电 > 自动放电 > 航行 > 充电限制
    // ═══════════════════════════════════════════════════════════════════════

    public ChargingDecision EvaluateChargingState()
    {
        int capacity = GetChargePercent() ?? 100;
        double? temp = GetCpuTemperature();
        bool ac = IsAcConnected();

        if (!ac) return ChargingDecision.Hold;

        // ① 过热保护（5 分钟迟滞状态机）
        if (_heatProtectionEnabled && temp.HasValue)
        {
            var od = EvaluateOvertemp(temp.Value);
            if (od.HasValue) return od.Value;
        }

        // ② 校准模式
        if (_calPhase != CalibrationPhase.Idle)
        {
            StepCalibration(capacity);
            return _calPhase switch
            {
                CalibrationPhase.Discharging => ChargingDecision.Inhibit,
                CalibrationPhase.Charging or CalibrationPhase.Recharging => ChargingDecision.Charge,
                CalibrationPhase.Resting => ChargingDecision.Charge,
                _ => ChargingDecision.Hold
            };
        }

        // ③ Top Up
        if (_topUpRequested)
            return capacity < 100 ? ChargingDecision.Charge : ChargingDecision.Hold;

        // ④ 手动放电
        if (_dischargeRequested)
            return capacity > _dischargeTarget ? ChargingDecision.Inhibit : ChargingDecision.Hold;

        // ⑤ 自动放电
        if (_autoDischargeEnabled && _chargeLimitEnabled)
        {
            if (capacity >= _chargeLimit + _autoDischargeMargin)
                return ChargingDecision.Inhibit;
        }

        // ⑥ 航行模式（施密特触发器）
        if (_sailingEnabled && _chargeLimitEnabled)
        {
            if (capacity >= _chargeLimit) return ChargingDecision.Inhibit;
            if (capacity <= _sailingFloor) return ChargingDecision.Charge;
            return ChargingDecision.Hold;
        }

        // ⑦ BCLM 模式：固件在管
        return ChargingDecision.Hold;
    }

    // ── 过热保护 5 分钟状态机 ──
    // Normal →(超温)→ Cooling(禁止充电) →(5min后温度降了)→ ChargingRecovery →(5min后正常)→ Normal
    private ChargingDecision? EvaluateOvertemp(double temp)
    {
        if (_heatThreshold <= 0) { _overtempState = OvertempState.Normal; return null; }

        bool over = temp > _heatThreshold;
        var now = DateTime.UtcNow;
        bool expired = (now - _overtempSince) >= OvertempWindow;

        switch (_overtempState)
        {
            case OvertempState.Normal:
                if (over) { _overtempState = OvertempState.Cooling; _overtempSince = now; return ChargingDecision.Inhibit; }
                return null;

            case OvertempState.Cooling:
                if (expired)
                {
                    if (over) { _overtempSince = now; return ChargingDecision.Inhibit; }
                    _overtempState = OvertempState.ChargingRecovery; _overtempSince = now;
                    return ChargingDecision.Charge;
                }
                return ChargingDecision.Inhibit;

            case OvertempState.ChargingRecovery:
                if (expired)
                {
                    if (over) { _overtempState = OvertempState.Cooling; _overtempSince = now; return ChargingDecision.Inhibit; }
                    _overtempState = OvertempState.Normal; return null;
                }
                return ChargingDecision.Charge;

            default: return null;
        }
    }

    // ── 校准状态机（5 阶段）──
    // Charging →(100%)→ Discharging →(10%)→ Recharging →(100%)→ Resting →(1h)→ Done
    private void StepCalibration(int capacity)
    {
        switch (_calPhase)
        {
            case CalibrationPhase.Charging:
                if (capacity >= 100) _calPhase = CalibrationPhase.Discharging;
                break;
            case CalibrationPhase.Discharging:
                if (capacity <= CalibrationFloor) _calPhase = CalibrationPhase.Recharging;
                break;
            case CalibrationPhase.Recharging:
                if (capacity >= 100) { _calPhase = CalibrationPhase.Resting; _calRestStart = DateTime.UtcNow; }
                break;
            case CalibrationPhase.Resting:
                if (DateTime.UtcNow - _calRestStart >= CalibrationRestPeriod)
                { _calPhase = CalibrationPhase.Done; _chargeLimit = _calSavedLimit; }
                break;
            case CalibrationPhase.Done:
                _calPhase = CalibrationPhase.Idle;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 第三层：执行 — 唯一写 CH0B 的地方
    // ═══════════════════════════════════════════════════════════════════════

    public void ApplyDecision(ChargingDecision decision)
    {
        switch (decision)
        {
            case ChargingDecision.Charge:
                if (_chargeInhibited) { WriteCh0B(0); _chargeInhibited = false; AllowSleep(); }
                break;

            case ChargingDecision.Inhibit:
                if (!_chargeInhibited)
                {
                    EnsureBclmCleared();
                    WriteCh0B(2);
                    _chargeInhibited = true;
                    PreventSleep();
                }
                break;

            case ChargingDecision.Hold:
                // Hold 时如果没功能需要 CH0B 了，退出恢复 BCLM
                if (_ch0bMode && !NeedsCh0BControl()) ExitCh0BMode();
                break;
        }
    }

    private bool NeedsCh0BControl()
    {
        if (_dischargeRequested) return true;
        if (_calPhase is not (CalibrationPhase.Idle or CalibrationPhase.Done)) return true;
        if (_overtempState != OvertempState.Normal) return true;
        if (_sailingEnabled && _chargeLimitEnabled && (GetChargePercent() ?? 0) >= _chargeLimit) return true;
        if (_autoDischargeEnabled && _chargeLimitEnabled && (GetChargePercent() ?? 0) >= _chargeLimit + _autoDischargeMargin) return true;
        return false;
    }

    /// <summary>安全网：Hold 时恢复 BCLM（修复 81% 不充电 bug）。</summary>
    public void RestoreBclmIfIdle()
    {
        if (_ch0bMode && !NeedsCh0BControl()) ExitCh0BMode();
    }

    private void EnsureBclmCleared()
    {
        if (!_ch0bMode) { _savedBclm = ReadBclm(); WriteBclm(0); _ch0bMode = true; }
    }

    private void ExitCh0BMode()
    {
        if (!_ch0bMode) return;
        WriteCh0B(0);
        _chargeInhibited = false;
        AllowSleep();
        int restoreVal = _chargeLimitEnabled ? _chargeLimit : 0;
        WriteBclm(restoreVal > 0 ? restoreVal : 80);
        _ch0bMode = false;
    }

    /// <summary>退出程序时恢复（不清零 BCLM）。</summary>
    public void RestoreOnExit()
    {
        ExitCh0BMode();
        WriteCh0B(0);
        _chargeInhibited = false;
        AllowSleep();
        if (_topUpRequested)
        {
            int restoreVal = _chargeLimitEnabled ? _chargeLimit : 0;
            WriteBclm(restoreVal > 0 ? restoreVal : 0);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 读取方法（Windows API + SMC fallback）
    // ═══════════════════════════════════════════════════════════════════════

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, Reserved1;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(ref SYSTEM_POWER_STATUS s);

    public int? GetChargePercent()
    {
        try
        {
            var sps = new SYSTEM_POWER_STATUS();
            if (GetSystemPowerStatus(ref sps) && sps.BatteryLifePercent <= 100) return sps.BatteryLifePercent;
        }
        catch { }
        var brsc = ReadKeySafe(KeyBrsc, 4);
        if (brsc != null && brsc.Length >= 2)
        {
            int hi = (brsc[0] << 8) | brsc[1];
            if (hi > 0 && hi <= 100) return hi;
        }
        return null;
    }

    public double? GetCpuTemperature()
    {
        try { return _smc.ReadNumber(KeyTc0p); } catch { return null; }
    }

    public bool IsAcConnected()
    {
        try
        {
            var sps = new SYSTEM_POWER_STATUS();
            if (GetSystemPowerStatus(ref sps)) return sps.ACLineStatus == 1;
        }
        catch { }
        var d = ReadKeySafe("ACIN", 1);
        return d != null && d.Length == 1 && d[0] != 0;
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
        var d = ReadKeySafe(KeyBclm, 1);
        return d != null && d.Length == 1 ? d[0] : 0;
    }

    private void WriteCh0B(int value)
    {
        try
        {
            if (_smc.KeyExists(KeyCh0B)) _smc.WriteKey(KeyCh0B, new byte[] { (byte)value });
            if (_smc.KeyExists(KeyCh0C)) _smc.WriteKey(KeyCh0C, new byte[] { (byte)value });
        }
        catch { }
    }

    private byte[]? ReadKeySafe(string key, int length)
    {
        try { if (!_smc.KeyExists(key)) return null; return _smc.ReadKey(key, length); }
        catch { return null; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 防睡眠（SetThreadExecutionState）
    // ═══════════════════════════════════════════════════════════════════════

    [Flags]
    private enum EXECUTION_STATE : uint
    {
        ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1, ES_DISPLAY_REQUIRED = 0x2
    }
    [DllImport("kernel32.dll")]
    private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE f);

    private bool _sleepPrevented;
    private void PreventSleep()
    {
        if (_sleepPrevented) return;
        SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
        _sleepPrevented = true;
    }
    private void AllowSleep()
    {
        if (!_sleepPrevented) return;
        SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        _sleepPrevented = false;
    }
}
