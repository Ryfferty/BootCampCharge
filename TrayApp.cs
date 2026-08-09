using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace BootCampCharge;

/// <summary>
/// WinForms system-tray application.  Hosts a <see cref="NotifyIcon"/> with a context
/// menu for charge-limit selection, over-temperature protection, sail mode, auto-start,
/// and a semi-transparent status popup shown on left-click.
/// </summary>
public sealed class TrayApp : IDisposable
{
    private readonly BatteryManager _battery;
    private readonly AppConfig _config;

    private readonly NotifyIcon _notify;
    private readonly System.Windows.Forms.Timer _timer;
    private StatusForm? _statusForm;
    private bool _disposed;

    // Cached menu item references for dynamic updates.
    private readonly ToolStripMenuItem _limitMenu;
    private readonly ToolStripMenuItem _overtempMenu;
    private readonly ToolStripMenuItem _sailModeItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _topUpItem;
    private readonly ToolStripMenuItem _calibrateItem;
    private readonly ToolStripMenuItem _statusLabel;

    // Charge-limit choices presented in the menu.
    private static readonly int[] LimitChoices = { 60, 70, 80, 85, 90, 100 };
    private static readonly int[] OvertempChoices = { 0, 35, 40, 45 };

    public TrayApp(BatteryManager battery, AppConfig config)
    {
        _battery = battery;
        _config = config;

        // ── Build context menu ───────────────────────────────────────────
        _statusLabel = new ToolStripMenuItem("加载中…")
        {
            Enabled = false
        };

        _limitMenu = new ToolStripMenuItem("充电限制");
        BuildLimitMenu();

        _overtempMenu = new ToolStripMenuItem("过热保护");
        BuildOvertempMenu();

        _sailModeItem = new ToolStripMenuItem("航行模式", null, (s, e) =>
        {
            _config.SailMode.Enabled = !_config.SailMode.Enabled;
            _sailModeItem.Checked = _config.SailMode.Enabled;
            _config.Save();
        });

        _topUpItem = new ToolStripMenuItem("临时充满", null, (s, e) => ToggleTopUp());

        _calibrateItem = new ToolStripMenuItem("校准模式…", null, (s, e) => ToggleCalibration());

        _autoStartItem = new ToolStripMenuItem("开机自启", null, (s, e) =>
        {
            _config.StartWithWindows = !_config.StartWithWindows;
            _autoStartItem.Checked = _config.StartWithWindows;
            _config.ApplyAutoStart();
            _config.Save();
        });
        _autoStartItem.Checked = _config.StartWithWindows;

        var exitItem = new ToolStripMenuItem("退出（恢复满充）", null, (s, e) => ExitApplication());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusLabel);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_limitMenu);
        menu.Items.Add(_overtempMenu);
        menu.Items.Add(_sailModeItem);
        menu.Items.Add(_topUpItem);
        menu.Items.Add(_calibrateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        // ── NotifyIcon ───────────────────────────────────────────────────
        _notify = new NotifyIcon
        {
            Icon = CreateIcon(TrayIconColor.Yellow),
            Visible = true,
            Text = "BootCampCharge 充电管理",
            ContextMenuStrip = menu
        };
        _notify.MouseClick += OnTrayClick;
        _notify.MouseDoubleClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowStatusForm(); };

        // ── Timer ────────────────────────────────────────────────────────
        _timer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(5, _config.PollIntervalSeconds) * 1000
        };
        _timer.Tick += (s, e) => Poll();
        _timer.Start();

        // Apply initial config.
        ApplyInitialConfig();

        // Do an immediate poll.
        Poll();
    }

    // ── Menu construction ────────────────────────────────────────────────

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
            var item = new ToolStripMenuItem(label, null, (s, e) => SetChargeLimit(limit))
            {
                Checked = _config.ChargeLimit == limit
            };
            _limitMenu.DropDownItems.Add(item);
        }

        // Add an enable/disable toggle at the top.
        var enableItem = new ToolStripMenuItem("启用充电限制", null, (s, e) =>
        {
            _config.ChargeLimitEnabled = !_config.ChargeLimitEnabled;
            if (_config.ChargeLimitEnabled)
                SetChargeLimit(_config.ChargeLimit);
            else
            {
                _battery.DisableLimit();
                _config.Save();
            }
            UpdateMenuChecks();
        })
        {
            Checked = _config.ChargeLimitEnabled
        };
        _limitMenu.DropDownItems.Insert(0, enableItem);
        _limitMenu.DropDownItems.Insert(1, new ToolStripSeparator());
    }

    private void BuildOvertempMenu()
    {
        _overtempMenu.DropDownItems.Clear();
        string[] labels = { "关闭", "35°C", "40°C", "45°C" };
        for (int i = 0; i < OvertempChoices.Length; i++)
        {
            int threshold = OvertempChoices[i];
            var item = new ToolStripMenuItem(labels[i], null, (s, e) =>
            {
                if (threshold == 0)
                {
                    _config.Overtemp.Enabled = false;
                }
                else
                {
                    _config.Overtemp.Enabled = true;
                    _config.Overtemp.ThresholdCelsius = threshold;
                }
                _config.Save();
                UpdateMenuChecks();
            })
            {
                Checked = threshold == 0 ? !_config.Overtemp.Enabled
                    : _config.Overtemp.Enabled && _config.Overtemp.ThresholdCelsius == threshold
            };
            _overtempMenu.DropDownItems.Add(item);
        }
    }

    private void UpdateMenuChecks()
    {
        // Limit submenu.
        if (_limitMenu.DropDownItems[0] is ToolStripMenuItem enableItem)
            enableItem.Checked = _config.ChargeLimitEnabled;
        for (int i = 0; i < LimitChoices.Length; i++)
        {
            if (_limitMenu.DropDownItems[i + 2] is ToolStripMenuItem item) // +2 for enable + separator
                item.Checked = _config.ChargeLimit == LimitChoices[i];
        }

        // Over-temp submenu.
        for (int i = 0; i < OvertempChoices.Length; i++)
        {
            if (_overtempMenu.DropDownItems[i] is ToolStripMenuItem item)
            {
                int t = OvertempChoices[i];
                item.Checked = t == 0 ? !_config.Overtemp.Enabled
                    : _config.Overtemp.Enabled && _config.Overtemp.ThresholdCelsius == t;
            }
        }

        _sailModeItem.Checked = _config.SailMode.Enabled;
        _topUpItem.Checked = _battery.IsTopUpActive;
    }

    // ── Config application ───────────────────────────────────────────────

    private void ApplyInitialConfig()
    {
        if (_config.ChargeLimitEnabled && _config.ChargeLimit < 100)
            _battery.SetChargeLimit(_config.ChargeLimit);

        if (_config.TopUpActive)
            _battery.StartTopUp();

        _sailModeItem.Checked = _config.SailMode.Enabled;
        _topUpItem.Checked = _battery.IsTopUpActive;
    }

    // ── Actions ──────────────────────────────────────────────────────────

    private void SetChargeLimit(int percent)
    {
        _config.ChargeLimit = percent;
        _config.ChargeLimitEnabled = percent < 100;

        if (percent >= 100)
            _battery.DisableLimit();
        else
            _battery.SetChargeLimit(percent);

        _config.Save();
        UpdateMenuChecks();

        _notify.BalloonTipText = percent >= 100
            ? "充电限制已取消 — 电池将充满至 100%"
            : $"Charge limit set to {percent}%.";
        _notify.ShowBalloonTip(1500);
    }

    private void ToggleTopUp()
    {
        if (_battery.IsTopUpActive)
        {
            _battery.StopTopUp();
            _config.TopUpActive = false;
            // Re-apply the configured limit.
            if (_config.ChargeLimitEnabled && _config.ChargeLimit < 100)
                _battery.SetChargeLimit(_config.ChargeLimit);
            _notify.BalloonTipText = "临时充满已取消 — 充电限制已恢复";
        }
        else
        {
            _battery.StartTopUp();
            _config.TopUpActive = true;
            _notify.BalloonTipText = "临时充满已启动 — 电池将充至 100%";
        }
        _config.Save();
        UpdateMenuChecks();
        _notify.ShowBalloonTip(1500);
    }

    private void ToggleCalibration()
    {
        if (_battery.CalibrationState != CalibrationPhase.Idle &&
            _battery.CalibrationState != CalibrationPhase.Done)
        {
            _battery.CancelCalibration();
            _notify.BalloonTipText = "校准已取消";
        }
        else
        {
            var result = MessageBox.Show(
                "校准模式将执行完整的 充满→静置(1小时)→放电 循环。\n" +
                "这可能需要数小时。是否继续？",
                "校准模式",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (result == DialogResult.OK)
            {
                _battery.StartCalibration();
                _notify.BalloonTipText = "校准已启动 — 完整充放循环";
            }
        }
        _notify.ShowBalloonTip(2000);
    }

    // ── Polling ──────────────────────────────────────────────────────────

    private void Poll()
    {
        try
        {
            var status = _battery.GetStatus();

            // 1. Over-temperature protection (highest priority).
            bool overtemp = false;
            if (_config.Overtemp.Enabled)
                overtemp = _battery.EvaluateOvertempProtection(_config.Overtemp.ThresholdCelsius);

            // 2. Calibration state machine.
            if (_battery.CalibrationState != CalibrationPhase.Idle &&
                _battery.CalibrationState != CalibrationPhase.Done)
            {
                _battery.StepCalibration();
                _calibrateItem.Checked = true;
                _calibrateItem.Text = $"Calibration: {_battery.CalibrationState}…";
            }
            else
            {
                _calibrateItem.Checked = false;
                _calibrateItem.Text = "校准模式…";
            }

            // 3. Sail mode.
            if (!overtemp && _battery.CalibrationState == CalibrationPhase.Idle && _config.SailMode.Enabled)
            {
                int floor = _config.SailMode.Floor;
                int limit = _config.ChargeLimitEnabled ? _config.ChargeLimit : 100;
                _battery.EvaluateSailMode(limit, floor);
            }

            // 4. Update tray icon + text.
            UpdateTray(status, overtemp);

            // 5. Update status popup if open.
            _statusForm?.UpdateStatus(status, overtemp, _battery.CalibrationState);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
            _notify.Icon = CreateIcon(TrayIconColor.Red);
        }
    }

    private void UpdateTray(BatteryStatus status, bool overtemp)
    {
        // Status-bar text: 电量 | 充电中/已停充 | 限制值 | CPU温度
        string charge = status.ChargePercent.HasValue ? $"{status.ChargePercent}%" : "--%";
        string charging = status.IsCharging ? "充电中" : "已停充";
        string limitText = status.ChargeLimit.HasValue && status.ChargeLimit < 100
            ? $" | Limit {status.ChargeLimit}%"
            : "";
        string tempText = status.CpuTemp.HasValue ? $" | {status.CpuTemp:0}°C" : "";
        _statusLabel.Text = $"{charge} | {charging}{limitText}{tempText}";

        // Tooltip (NotifyIcon.Text max 63 chars).
        string tip = $"{charge} — {charging}";
        if (status.ChargeLimit.HasValue && status.ChargeLimit < 100)
            tip += $" (limit {status.ChargeLimit}%)";
        if (overtemp)
            tip = $"OVERTEMP PROTECTION — {charge}";
        _notify.Text = Truncate(tip, 63);

        // Icon colour.
        TrayIconColor color;
        if (overtemp)
            color = TrayIconColor.Red;
        else if (status.IsCharging)
            color = TrayIconColor.Yellow;
        else
            color = TrayIconColor.Green;

        _notify.Icon = CreateIcon(color);
    }

    // ── Tray click ───────────────────────────────────────────────────────

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ShowStatusForm();
    }

    private void ShowStatusForm()
    {
        if (_statusForm == null || _statusForm.IsDisposed)
        {
            _statusForm = new StatusForm(_battery);
            _statusForm.FormClosed += (s, e) => _statusForm = null;
        }
        var status = _battery.GetStatus();
        _statusForm.UpdateStatus(status, _battery.IsOvertempEngaged, _battery.CalibrationState);
        _statusForm.ShowNearMouse();
    }

    // ── Exit ─────────────────────────────────────────────────────────────

    internal void OnExit() => RestoreFullCharge();

    private void RestoreFullCharge()
    {
        try
        {
            _battery.StopTopUp();
            _battery.CancelCalibration();
            _battery.EnableCharging();
            _battery.DisableLimit();
        }
        catch { }
    }

    private void ExitApplication()
    {
        RestoreFullCharge();
        _notify.Visible = false;
        Application.Exit();
    }

    // ── Icon generation ──────────────────────────────────────────────────

    private enum TrayIconColor { Green, Yellow, Red }

    private static Icon CreateIcon(TrayIconColor color) => color switch
    {
        TrayIconColor.Green => CreateSolidIcon(Color.FromArgb(34, 180, 79), 'G'),
        TrayIconColor.Yellow => CreateSolidIcon(Color.FromArgb(245, 200, 20), 'C'),
        TrayIconColor.Red => CreateSolidIcon(Color.FromArgb(220, 50, 47), 'R'),
        _ => CreateSolidIcon(Color.Gray, '?')
    };

    /// <summary>
    /// Draws a 16×16 rounded square with a lightning bolt, returns it as an Icon.
    /// </summary>
    private static Icon CreateSolidIcon(Color bg, char label)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.FromArgb(0, 0, 0, 0));

            // Rounded background.
            using var path = RoundedRect(0.5f, 0.5f, 15, 15, 3);
            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);
            using (var pen = new Pen(Color.FromArgb(180, 0, 0, 0), 0.5f))
                g.DrawPath(pen, path);

            // Lightning bolt overlay (always white for visibility).
            var bolt = new PointF[]
            {
                new(9, 2),
                new(4, 9),
                new(7, 9),
                new(6, 14),
                new(12, 7),
                new(9, 7)
            };
            g.FillPolygon(Brushes.White, bolt);
        }
        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        float d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];

    // ── Dispose ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _notify.Visible = false;
        _notify.Dispose();
        _statusForm?.Dispose();
        _battery.Dispose();
    }
}

/// <summary>
/// Semi-transparent borderless popup that shows live battery information.
/// Appears near the cursor on left-click of the tray icon.
/// </summary>
internal sealed class StatusForm : Form
{
    private readonly BatteryManager _battery;
    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public StatusForm(BatteryManager battery)
    {
        _battery = battery;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Opacity = 0.92;
        BackColor = Color.FromArgb(30, 30, 35);
        ForeColor = Color.WhiteSmoke;
        Size = new Size(260, 140);
        Font = new Font("微软雅黑", 9f);

        _label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 8, 12, 8),
            Font = new Font("微软雅黑", 9.5f),
            ForeColor = Color.WhiteSmoke,
            BackColor = Color.Transparent
        };
        Controls.Add(_label);

        // Close on click-away.
        Deactivate += (s, e) => Hide();

        // Continuous refresh while visible.
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _refreshTimer.Tick += (s, e) =>
        {
            if (Visible)
            {
                var st = _battery.GetStatus();
                UpdateStatus(st, _battery.IsOvertempEngaged, _battery.CalibrationState);
            }
        };
        _refreshTimer.Start();
    }

    public void ShowNearMouse()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        var loc = new Point(
            cursor.X - Width / 2,
            screen.WorkingArea.Bottom - Height - 10);
        // Keep on screen.
        if (loc.X < screen.WorkingArea.Left) loc.X = screen.WorkingArea.Left + 5;
        if (loc.X + Width > screen.WorkingArea.Right) loc.X = screen.WorkingArea.Right - Width - 5;
        Location = loc;
        Show();
        Activate();
    }

    public void UpdateStatus(BatteryStatus s, bool overtemp, CalibrationPhase cal)
    {
        string charge = s.ChargePercent.HasValue ? $"{s.ChargePercent}%" : "暂无";
        string limit = s.ChargeLimit.HasValue ? $"{s.ChargeLimit}%" : "无";
        string temp = s.CpuTemp.HasValue ? $"{s.CpuTemp:0.0} °C" : "暂无";
        string ac = s.AcConnected ? "已连接" : "未连接";
        string charging = s.IsCharging ? "⚡ 充电中" : "⏸ 已停充";

        string calText = cal switch
        {
            CalibrationPhase.Charging => $"\n  校准：正在充满…",
            CalibrationPhase.Resting => $"\n  校准：静置中（1小时）…",
            CalibrationPhase.Discharging => $"\n  校准：放电至 {BatteryManager.CalibrationFloor}%…",
            CalibrationPhase.Done => "\n  校准：已完成 ✓",
            _ => ""
        };

        string overtempText = overtemp ? "\n  ⚠ 过热保护 — 充电已暂停" : "";

        _label.Text =
            $"BootCampCharge\n" +
            $"────────────────────\n" +
            $"  Battery:   {charge}\n" +
            $"  Status:    {charging}\n" +
            $"  Limit:     {limit}\n" +
            $"  CPU Temp:  {temp}\n" +
            $"  Adapter:   {ac}" +
            $"{calText}{overtempText}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _label.Dispose();
        }
        base.Dispose(disposing);
    }
}
