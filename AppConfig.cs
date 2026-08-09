using System.Text.Json;
using System.Text.Json.Serialization;

namespace BootCampCharge;

/// <summary>Sail-mode (charge band) configuration.</summary>
public sealed class SailModeSettings
{
    /// <summary>Enable sail-mode (keep battery oscillating between floor and limit).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Lower bound of the charge band (percent). Below this, charging resumes.</summary>
    public int Floor { get; set; } = BatteryManager.DefaultSailFloor;

    /// <summary>Hysteresis below the limit before stopping charge (percent points).</summary>
    public int Hysteresis { get; set; } = BatteryManager.DefaultSailHysteresis;
}

/// <summary>Over-temperature protection configuration.</summary>
public sealed class OvertempSettings
{
    /// <summary>Enable over-temperature charge protection.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Temperature threshold in °C. Charging stops above this.</summary>
    public int ThresholdCelsius { get; set; } = 40;
}

/// <summary>
/// Application configuration, persisted as JSON next to the executable.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Battery charge limit percent (0–100, where 100 = no limit).</summary>
    public int ChargeLimit { get; set; } = 80;

    /// <summary>Whether charge limiting is enabled.</summary>
    public bool ChargeLimitEnabled { get; set; } = true;

    /// <summary>Sail-mode settings.</summary>
    public SailModeSettings SailMode { get; set; } = new();

    /// <summary>Over-temperature protection settings.</summary>
    public OvertempSettings Overtemp { get; set; } = new();

    /// <summary>Start with Windows (registry Run key under HKCU).</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>Polling interval in seconds for background monitoring.</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>Last saved Top Up state (restored on launch if still active).</summary>
    public bool TopUpActive { get; set; } = false;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Full path to the config file beside the executable.</summary>
    public static string ConfigPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "BootCampCharge.json");

    /// <summary>Loads config from disk, or returns defaults if the file is absent/corrupt.</summary>
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null)
                {
                    cfg.SailMode ??= new SailModeSettings();
                    cfg.Overtemp ??= new OvertempSettings();
                    return cfg;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable config — fall back to defaults.
        }
        return new AppConfig();
    }

    /// <summary>Persists config to disk atomically (temp file + replace).</summary>
    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, JsonOpts);
            string dir = Path.GetDirectoryName(ConfigPath) ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(dir);
            string tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, ConfigPath, overwrite: true);
        }
        catch
        {
            // Non-fatal: config is best-effort.
        }
    }

    // ── Windows auto-start registry helpers ──────────────────────────────

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BootCampCharge";

    /// <summary>
    /// Sets or clears the HKCU Run-key entry that launches this app at logon.
    /// </summary>
    public void ApplyAutoStart()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (StartWithWindows)
            {
                string exe = Environment.ProcessPath ?? Application.ExecutablePath;
                key!.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                key!.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-fatal.
        }
    }
}
