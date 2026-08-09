using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BootCampCharge;

/// <summary>
/// 应用配置，JSON 持久化。
/// 精简版：只有充电限制 + 开机自启。
/// </summary>
public sealed class AppConfig
{
    /// <summary>充电限制百分比 (0-100, 100=不限制)。</summary>
    public int ChargeLimit { get; set; } = 80;

    /// <summary>是否启用充电限制。</summary>
    public bool ChargeLimitEnabled { get; set; } = true;

    /// <summary>开机自启。</summary>
    public bool StartWithWindows { get; set; } = false;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "BootCampCharge.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOpts);
                if (cfg != null) return cfg;
            }
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(ConfigPath) ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(dir);
            string tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOpts));
            File.Move(tmp, ConfigPath, overwrite: true);
        }
        catch { }
    }

    // ── 开机自启 ──

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BootCampCharge";

    public void ApplyAutoStart()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (StartWithWindows)
            {
                string exe = Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath;
                key!.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                key!.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch { }
    }
}
