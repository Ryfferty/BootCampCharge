using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows;
using Forms = System.Windows.Forms;

namespace BootCampCharge;

/// <summary>
/// WPF 应用入口。管理托盘图标 + 后台轮询 + 主窗口。
///
/// 精简版：轮询只读状态刷新 UI，不写 SMC。
/// BCLM 是事件驱动（用户改滑块时一次性写入），不在轮询中写。
/// </summary>
public partial class App : System.Windows.Application
{
    private BatteryManager? _battery;
    private AppConfig? _config;
    private NotifyIconController? _tray;
    private MainWindow? _mainWindow;
    private System.Windows.Threading.DispatcherTimer? _timer;
    private int _healthCounter;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 管理员检查
        if (!IsRunningAsAdmin())
        {
            RelaunchAsAdmin();
            Shutdown();
            return;
        }

        // 初始化驱动 + SMC
        try
        {
            if (!InpOut.DriverOpen())
            {
                Forms.MessageBox.Show(
                    "InpOut 驱动未加载。请以管理员身份运行。",
                    "驱动错误", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                Shutdown();
                return;
            }

            _battery = BatteryManager.Open();
            _config = AppConfig.Load();
        }
        catch (Exception ex)
        {
            Forms.MessageBox.Show(
                $"启动失败：\n\n{ex.Message}\n\n此工具仅适用于 Intel Mac 的 Boot Camp。",
                "启动错误", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
            Shutdown();
            return;
        }

        // 从硬件同步 + 应用配置
        _battery!.SyncFromHardware();
        if (_config!.ChargeLimitEnabled)
            _battery.SetChargeLimit(_config.ChargeLimit);
        _battery.RefreshBatteryHealth();
        _config.ApplyAutoStart();

        // 托盘
        _tray = new NotifyIconController(_battery, _config, ShowMainWindow);

        // 轮询定时器 — 5 秒刷新 UI
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += OnPoll;
        _timer.Start();
        OnPoll(null, EventArgs.Empty);
    }

    /// <summary>主轮询 — 只读状态刷新 UI，不写 SMC。</summary>
    private void OnPoll(object? sender, EventArgs e)
    {
        if (_battery == null || _config == null) return;
        try
        {
            // 健康信息每 ~60 秒刷新一次
            if (++_healthCounter >= 12)
            {
                _healthCounter = 0;
                _battery.RefreshBatteryHealth();
            }

            var status = _battery.GetStatus();
            _tray?.Update(status);
            if (_mainWindow?.IsLoaded == true)
                _mainWindow.UpdateStatus(status);
        }
        catch (Exception ex)
        {
            _tray?.UpdateError(ex.Message);
        }
    }

    /// <summary>用户操作后立即刷新 UI。</summary>
    public void RefreshNow()
    {
        _timer?.Stop();
        _timer?.Start();
        OnPoll(null, EventArgs.Empty);
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
            _mainWindow = new MainWindow(_battery!, _config!, OnConfigChanged, RefreshNow);
        _mainWindow.Show();
        _mainWindow.Activate();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
    }

    private void OnConfigChanged(AppConfig cfg) => _config = cfg;

    protected override void OnExit(ExitEventArgs e)
    {
        // BCLM 保持用户设定值（持久保护，Boot Camp 核心优势）
        _tray?.Dispose();
        _battery?.Dispose();
        base.OnExit(e);
    }

    // ── 管理员 ──

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var id = new System.Security.Principal.WindowsIdentity(
                System.Security.Principal.WindowsIdentity.GetCurrent().Token);
            return new System.Security.Principal.WindowsPrincipal(id)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static void RelaunchAsAdmin()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Forms.Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 托盘图标
// ═══════════════════════════════════════════════════════════════════════════

internal sealed class NotifyIconController : IDisposable
{
    private readonly BatteryManager _battery;
    private readonly AppConfig _config;
    private readonly Action _onLeftClick;
    private readonly Forms.NotifyIcon _notify;
    private readonly Forms.ToolStripMenuItem _statusLabel;
    private readonly Forms.ToolStripMenuItem _limitMenu;
    private readonly Forms.ToolStripMenuItem _autoStartItem;

    private static readonly int[] LimitChoices = { 60, 70, 80, 85, 90, 100 };

    public NotifyIconController(BatteryManager battery, AppConfig config, Action onLeftClick)
    {
        _battery = battery;
        _config = config;
        _onLeftClick = onLeftClick;

        _statusLabel = new("(加载中…)") { Enabled = false };

        _limitMenu = new("充电限制");
        BuildLimitMenu();

        _autoStartItem = new("开机自启", null, (_, _) =>
        {
            _config.StartWithWindows = !_config.StartWithWindows;
            _autoStartItem.Checked = _config.StartWithWindows;
            _config.ApplyAutoStart();
            _config.Save();
        });
        _autoStartItem.Checked = _config.StartWithWindows;

        var exitItem = new Forms.ToolStripMenuItem("退出", null, (_, _) =>
        {
            _notify.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_statusLabel);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_limitMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notify = new Forms.NotifyIcon
        {
            Icon = CreateIcon(TrayColor.Yellow),
            Visible = true,
            Text = "BootCampCharge",
            ContextMenuStrip = menu
        };
        _notify.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) _onLeftClick();
        };
    }

    private void BuildLimitMenu()
    {
        _limitMenu.DropDownItems.Clear();
        foreach (int limit in LimitChoices)
        {
            string label = limit switch
            {
                80 => "80%（推荐）",
                100 => "100%（不限制）",
                _ => $"{limit}%"
            };
            _limitMenu.DropDownItems.Add(new Forms.ToolStripMenuItem(label, null, (_, _) =>
            {
                _config.ChargeLimit = limit;
                _config.ChargeLimitEnabled = limit < 100;
                _battery.SetChargeLimit(limit);
                _config.Save();
                UpdateMenuChecks();
            })
            { Checked = _battery.ChargeLimit == limit });
        }
    }

    public void UpdateMenuChecks()
    {
        for (int i = 0; i < LimitChoices.Length; i++)
            if (_limitMenu.DropDownItems[i] is Forms.ToolStripMenuItem item)
                item.Checked = _battery.ChargeLimit == LimitChoices[i];
        _autoStartItem.Checked = _config.StartWithWindows;
    }

    public void Update(BatteryStatus s)
    {
        string charge = s.ChargePercent.HasValue ? $"{s.ChargePercent}%" : "--%";
        string mode = s.IsCharging ? "充电中" : s.AcConnected ? "已停充" : "未接电源";
        string limitText = s.ChargeLimit.HasValue && s.ChargeLimit < 100 ? $" | 限制 {s.ChargeLimit}%" : "";
        string tempText = s.CpuTemp.HasValue ? $" | {s.CpuTemp:0}°C" : "";
        _statusLabel.Text = $"{charge} | {mode}{limitText}{tempText}";

        string tip = $"{charge} — {mode}";
        if (s.ChargeLimit.HasValue && s.ChargeLimit < 100) tip += $" (限制 {s.ChargeLimit}%)";
        _notify.Text = tip.Length > 63 ? tip[..63] : tip;

        var color = s.IsCharging ? TrayColor.Yellow
            : s.AcConnected ? TrayColor.Green
            : TrayColor.Gray;
        _notify.Icon = CreateIcon(color);
    }

    public void UpdateError(string msg)
    {
        _statusLabel.Text = $"错误: {msg}";
        _notify.Icon = CreateIcon(TrayColor.Red);
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
    }

    // ── 图标 ──

    private enum TrayColor { Green, Yellow, Red, Gray }

    private static Icon CreateIcon(TrayColor color) => color switch
    {
        TrayColor.Green => CreateSolidIcon(Color.FromArgb(34, 180, 79)),
        TrayColor.Yellow => CreateSolidIcon(Color.FromArgb(245, 200, 20)),
        TrayColor.Red => CreateSolidIcon(Color.FromArgb(220, 50, 47)),
        TrayColor.Gray => CreateSolidIcon(Color.FromArgb(150, 150, 150)),
        _ => CreateSolidIcon(Color.Gray)
    };

    private static Icon CreateSolidIcon(Color bg)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.FromArgb(0, 0, 0, 0));
            using var path = RoundedRect(0.5f, 0.5f, 15, 15, 3);
            using (var brush = new SolidBrush(bg)) g.FillPath(brush, path);
            using var pen = new Pen(Color.FromArgb(180, 0, 0, 0), 0.5f);
            g.DrawPath(pen, path);
            g.FillPolygon(Brushes.White, new PointF[]
            {
                new(9, 2), new(4, 9), new(7, 9), new(6, 14), new(12, 7), new(9, 7)
            });
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
