using System.Text;

namespace BootCampCharge;

/// <summary>
/// Battery information snapshot returned by <see cref="BatteryManager.GetStatus"/>.
/// </summary>
public sealed class BatteryStatus
{
    /// <summary>Battery charge percent (0–100), or null if unavailable.</summary>
    public int? ChargePercent { get; init; }

    /// <summary>Current charge limit set via BCLM (0–100), or null if not set/readable.</summary>
    public int? ChargeLimit { get; init; }

    /// <summary>CPU proximity temperature in °C (TC0P), or null if unavailable.</summary>
    public double? CpuTemp { get; init; }

    /// <summary>True when the charger is connected (ACIN != 0).</summary>
    public bool AcConnected { get; init; }

    /// <summary>True when the battery is physically present (BATP != 0).</summary>
    public bool BatteryPresent { get; init; }

    /// <summary>Raw CH0B value: 0 = charging enabled, 2 = charging inhibited.</summary>
    public int? ChargerStatus { get; init; }

    /// <summary>Derived: true when charging is actively being inhibited by CH0B=2.</summary>
    public bool ChargingInhibited => ChargerStatus == 2;

    /// <summary>Derived: true when the battery is currently charging (AC connected, not inhibited).</summary>
    public bool IsCharging => AcConnected && !ChargingInhibited;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(ChargePercent.HasValue ? $"{ChargePercent}%" : "--%");
        sb.Append(" | ");
        sb.Append(IsCharging ? "Charging" : "Stopped");
        if (ChargeLimit.HasValue && ChargeLimit < 100)
            sb.Append($" | Limit {ChargeLimit}%");
        if (CpuTemp.HasValue)
            sb.Append($" | {CpuTemp:0}°C");
        return sb.ToString();
    }
}

/// <summary>
/// Calibration state-machine phases for the full charge/discharge cycle.
/// </summary>
public enum CalibrationPhase
{
    Idle,
    /// <summary>Charging to 100% (limit removed).</summary>
    Charging,
    /// <summary>Holding at full charge to let the cells balance.</summary>
    Resting,
    /// <summary>Discharging to the floor percentage.</summary>
    Discharging,
    /// <summary>Calibration cycle finished; restore previous limit.</summary>
    Done
}

/// <summary>
/// High-level battery management over the Apple SMC.  Wraps the raw SMC key reads/writes
/// used by AlDente-style charge limiting on Intel Macs running Windows Boot Camp.
/// </summary>
public sealed class BatteryManager : IDisposable
{
    private readonly Smc _smc;
    private bool _disposed;

    // SMC keys (4-char codes).  See Apple's SMC reference and the BootCampCharge docs.
    private const string KeyBclm = "BCLM";  // Battery Charge Level Limit (Mac)
    private const string KeyBrsc = "BRSC";  // Battery Charge Level Limit (Boot Camp / Windows)
    private const string KeyTc0p = "TC0P";  // CPU Proximity Temperature
    private const string KeyCh0B = "CH0B";  // Charger 0B — 0=charge, 2=inhibit
    private const string KeyCh0C = "CH0C";  // Charger 0C — alternate inhibit key on some models
    private const string KeyAcin = "ACIN";  // AC attached flag (some models expose this)
    private const string KeyBatp = "BATP";  // Battery present
    private const string KeyBrws = "BRWS";  // Battery remaining capacity (Wh, sp78 sometimes)
    private const string KeyB0rm = "B0RM";  // Battery remaining mAh
    private const string KeyB0fc = "B0FC";  // Battery full charge capacity mAh
    private const string KeyB0dc = "B0DC";  // Battery design capacity mAh
    private const string KeyB0ps = "B0PS";  // Battery power status (bit 0 = charging)

    /// <summary>Default floor for sail-mode (lower bound of the charge band).</summary>
    public const int DefaultSailFloor = 50;
    /// <summary>Default ceiling offset below the limit for sail-mode hysteresis.</summary>
    public const int DefaultSailHysteresis = 5;

    /// <summary>Discharge floor for calibration mode.</summary>
    public const int CalibrationFloor = 15;

    // ── Construction / lifecycle ──────────────────────────────────────────

    public BatteryManager(Smc smc)
    {
        _smc = smc ?? throw new ArgumentNullException(nameof(smc));
    }

    /// <summary>Opens the SMC and wraps it in a BatteryManager.</summary>
    public static BatteryManager Open(Action<string>? log = null)
    {
        var smc = Smc.Open(log);
        return new BatteryManager(smc);
    }

    public Smc Smc => _smc;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _smc.Dispose();
        }
    }

    // ── Charge limiting ───────────────────────────────────────────────────

    /// <summary>
    /// Sets the battery charge limit. Writes to both BCLM (macOS side) and BRSC
    /// (Boot Camp side) when available. Pass 100 to remove the limit.
    /// </summary>
    public void SetChargeLimit(int percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "must be 0–100");

        byte val = (byte)percent;

        if (percent >= 100)
        {
            // Remove limit: restore default (0 = no limit on most firmware).
            DisableLimit();
            return;
        }

        // BCLM is the primary key on Intel Macs.
        WriteKeySafe(KeyBclm, new byte[] { val });
        // BRSC is used on some models when booted via Boot Camp.
        WriteKeySafe(KeyBrsc, new byte[] { val });

        // Make sure charging isn't artificially inhibited.
        EnableCharging();
    }

    /// <summary>Disables the charge limit (full charge).</summary>
    public void DisableLimit()
    {
        // BCLM value 0 means "no limit" / always charge.
        WriteKeySafe(KeyBclm, new byte[] { 0 });
        WriteKeySafe(KeyBrsc, new byte[] { 0 });
        EnableCharging();
    }

    /// <summary>Reads the current charge limit from BCLM, falling back to BRSC.</summary>
    public int? GetChargeLimit()
    {
        var bclm = ReadKeySafe(KeyBclm, 1);
        if (bclm != null && bclm.Length == 1 && bclm[0] != 0)
            return bclm[0];

        var brsc = ReadKeySafe(KeyBrsc, 1);
        if (brsc != null && brsc.Length == 1 && brsc[0] != 0)
            return brsc[0];

        return null;
    }

    // ── Manual discharge ──────────────────────────────────────────────────

    /// <summary>Inhibits charging (CH0B = 2) so the battery discharges on AC power.</summary>
    public void InhibitCharging()
    {
        WriteKeySafe(KeyCh0B, new byte[] { 2 });
        WriteKeySafe(KeyCh0C, new byte[] { 2 });
    }

    /// <summary>Re-enables charging (CH0B = 0).</summary>
    public void EnableCharging()
    {
        WriteKeySafe(KeyCh0B, new byte[] { 0 });
        WriteKeySafe(KeyCh0C, new byte[] { 0 });
    }

    /// <summary>Reads CH0B: 0 = charging enabled, 2 = charging inhibited.</summary>
    public int? GetChargerStatus()
    {
        var data = ReadKeySafe(KeyCh0B, 1);
        return data != null && data.Length == 1 ? data[0] : null;
    }

    // ── Status readout ────────────────────────────────────────────────────

    /// <summary>Reads a complete battery status snapshot from the SMC.</summary>
    public BatteryStatus GetStatus()
    {
        int? charge = GetChargePercent();
        int? limit = GetChargeLimit();
        double? temp = GetCpuTemperature();
        bool ac = IsAcConnected();
        bool present = IsBatteryPresent();
        int? ch0b = GetChargerStatus();

        return new BatteryStatus
        {
            ChargePercent = charge,
            ChargeLimit = limit,
            CpuTemp = temp,
            AcConnected = ac,
            BatteryPresent = present,
            ChargerStatus = ch0b
        };
    }

    /// <summary>Battery charge percent 0–100, computed from B0RM/B0FC.</summary>
    public int? GetChargePercent()
    {
        try
        {
            // Try B0PS first — some models return a direct percentage.
            var b0rm = ReadKeySafe(KeyB0rm, 2);
            var b0fc = ReadKeySafe(KeyB0fc, 2);
            if (b0rm != null && b0fc != null && b0fc.Length == 2 && b0rm.Length == 2)
            {
                int remaining = (b0rm[0] << 8) | b0rm[1];
                int full = (b0fc[0] << 8) | b0fc[1];
                if (full > 0)
                    return (int)Math.Round(remaining * 100.0 / full);
            }
        }
        catch { /* fall through */ }

        // Fall back to BRWS (sp78 fixed-point on some models).
        try
        {
            double? brws = _smc.ReadNumber(KeyBrws);
            if (brws.HasValue && brws.Value > 0 && brws.Value <= 100)
                return (int)Math.Round(brws.Value);
        }
        catch { }

        return null;
    }

    /// <summary>CPU proximity temperature (TC0P) in °C, or null.</summary>
    public double? GetCpuTemperature()
    {
        try
        {
            return _smc.ReadNumber(KeyTc0p);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True when AC adapter is attached (ACIN byte != 0).</summary>
    public bool IsAcConnected()
    {
        var data = ReadKeySafe(KeyAcin, 1);
        return data != null && data.Length == 1 && data[0] != 0;
    }

    /// <summary>True when the battery is physically present (BATP != 0).</summary>
    public bool IsBatteryPresent()
    {
        var data = ReadKeySafe(KeyBatp, 1);
        return data != null && data.Length == 1 && data[0] != 0;
    }

    // ── Top Up (temporary full charge) ───────────────────────────────────

    private int? _topUpSavedLimit;
    private bool _topUpActive;

    /// <summary>Removes the charge limit temporarily to top-up to 100%, remembering the old value.</summary>
    public void StartTopUp()
    {
        if (_topUpActive)
            return;
        _topUpSavedLimit = GetChargeLimit();
        DisableLimit();
        EnableCharging();
        _topUpActive = true;
    }

    /// <summary>Restores the charge limit that was active before Top Up.</summary>
    public void StopTopUp()
    {
        if (!_topUpActive)
            return;
        if (_topUpSavedLimit.HasValue && _topUpSavedLimit.Value < 100)
            SetChargeLimit(_topUpSavedLimit.Value);
        else
            DisableLimit();
        _topUpActive = false;
    }

    public bool IsTopUpActive => _topUpActive;

    // ── Over-temperature protection ──────────────────────────────────────

    private double _overtempThreshold = double.MaxValue;
    private bool _overtempEngaged;

    /// <summary>
    /// If temperature exceeds threshold, charging is inhibited until it drops.
    /// Set threshold to 0 or double.MaxValue to disable.
    /// </summary>
    public bool EvaluateOvertempProtection(double thresholdCelsius)
    {
        if (thresholdCelsius <= 0)
        {
            // Disabled — make sure we're not stuck in overtemp inhibit.
            if (_overtempEngaged)
            {
                EnableCharging();
                _overtempEngaged = false;
            }
            return false;
        }

        double? temp = GetCpuTemperature();
        if (!temp.HasValue)
            return _overtempEngaged;

        if (temp.Value > thresholdCelsius && !_overtempEngaged)
        {
            InhibitCharging();
            _overtempEngaged = true;
        }
        else if (temp.Value < thresholdCelsius - 2 && _overtempEngaged)
        {
            // 2 °C hysteresis to avoid rapid toggling.
            EnableCharging();
            _overtempEngaged = false;
        }

        return _overtempEngaged;
    }

    public bool IsOvertempEngaged => _overtempEngaged;

    // ── Sail mode (charge band cycling) ──────────────────────────────────

    /// <summary>Sail-mode control decision returned by <see cref="EvaluateSailMode"/>.</summary>
    public enum SailAction
    {
        /// <summary>No action needed; battery is within the band.</summary>
        Hold,
        /// <summary>Stop charging (drop below limit).</summary>
        Inhibit,
        /// <summary>Start charging (dropped below floor).</summary>
        Resume
    }

    /// <summary>
    /// Evaluates sail mode: keep the battery between floor and limit by toggling CH0B.
    /// Returns the action taken. Pass in the configured limit and a lower floor.
    /// </summary>
    public SailAction EvaluateSailMode(int limit, int floor)
    {
        int? charge = GetChargePercent();
        if (!charge.HasValue)
            return SailAction.Hold;

        int c = charge.Value;
        int? ch0b = GetChargerStatus();

        if (c >= limit)
        {
            // At or above ceiling — stop charging.
            if (ch0b != 2)
                InhibitCharging();
            return SailAction.Inhibit;
        }

        if (c <= floor)
        {
            // At or below floor — resume charging.
            if (ch0b != 0)
                EnableCharging();
            return SailAction.Resume;
        }

        return SailAction.Hold;
    }

    // ── Calibration mode ─────────────────────────────────────────────────

    private CalibrationPhase _calPhase = CalibrationPhase.Idle;
    private int? _calSavedLimit;
    private DateTime _calRestStart;
    private readonly object _calLock = new();

    /// <summary>Begins a calibration cycle: full charge → rest → discharge → done.</summary>
    public void StartCalibration()
    {
        lock (_calLock)
        {
            _calSavedLimit = GetChargeLimit();
            DisableLimit();
            EnableCharging();
            _calPhase = CalibrationPhase.Charging;
        }
    }

    /// <summary>Cancels calibration and restores the previous limit.</summary>
    public void CancelCalibration()
    {
        lock (_calLock)
        {
            if (_calPhase == CalibrationPhase.Idle)
                return;
            EnableCharging();
            if (_calSavedLimit.HasValue && _calSavedLimit.Value < 100)
                SetChargeLimit(_calSavedLimit.Value);
            else
                DisableLimit();
            _calPhase = CalibrationPhase.Idle;
        }
    }

    /// <summary>Current calibration phase.</summary>
    public CalibrationPhase CalibrationState => _calPhase;

    /// <summary>
    /// Advances the calibration state machine. Call periodically (e.g. every 30 s).
    /// </summary>
    public CalibrationPhase StepCalibration()
    {
        lock (_calLock)
        {
            switch (_calPhase)
            {
                case CalibrationPhase.Charging:
                    {
                        int? charge = GetChargePercent();
                        if (charge.HasValue && charge.Value >= 100)
                        {
                            // Begin the rest period (let cells balance) — 1 hour.
                            _calRestStart = DateTime.UtcNow;
                            _calPhase = CalibrationPhase.Resting;
                        }
                        break;
                    }
                case CalibrationPhase.Resting:
                    {
                        if (DateTime.UtcNow - _calRestStart >= TimeSpan.FromHours(1))
                        {
                            // Begin discharge.
                            InhibitCharging();
                            _calPhase = CalibrationPhase.Discharging;
                        }
                        break;
                    }
                case CalibrationPhase.Discharging:
                    {
                        int? charge = GetChargePercent();
                        if (charge.HasValue && charge.Value <= CalibrationFloor)
                        {
                            // Done — restore charging and previous limit.
                            EnableCharging();
                            if (_calSavedLimit.HasValue && _calSavedLimit.Value < 100)
                                SetChargeLimit(_calSavedLimit.Value);
                            else
                                DisableLimit();
                            _calPhase = CalibrationPhase.Done;
                        }
                        break;
                    }
                case CalibrationPhase.Done:
                    {
                        _calPhase = CalibrationPhase.Idle;
                        break;
                    }
            }
            return _calPhase;
        }
    }

    // ── Safe SMC helpers (swallow missing-key exceptions) ────────────────

    /// <summary>Reads a key, returning null if the key doesn't exist or fails.</summary>
    private byte[]? ReadKeySafe(string key, int length)
    {
        try
        {
            if (!_smc.KeyExists(key))
                return null;
            return _smc.ReadKey(key, length);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Writes a key only if it exists on this machine (silent skip otherwise).</summary>
    private void WriteKeySafe(string key, byte[] data)
    {
        try
        {
            if (_smc.KeyExists(key))
                _smc.WriteKey(key, data);
        }
        catch
        {
            // Some keys are write-protected or transient; ignore.
        }
    }
}
