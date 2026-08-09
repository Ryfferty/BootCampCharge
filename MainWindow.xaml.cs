using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;

namespace BootCampCharge;

/// <summary>
/// WPF 主窗口。精简版：只有充电限制滑块 + 开机自启。
/// </summary>
public partial class MainWindow : Window
{
    private readonly BatteryManager _battery;
    private AppConfig _config;
    private readonly Action<AppConfig> _onConfigChanged;
    private readonly Action _onRefresh;
    private bool _suppress;

    public MainWindow(BatteryManager battery, AppConfig config, Action<AppConfig> onConfigChanged, Action onRefresh)
    {
        _battery = battery;
        _config = config;
        _onConfigChanged = onConfigChanged;
        _onRefresh = onRefresh;

        InitializeComponent();

        // 事件绑定（InitializeComponent 之后，避免 NullRef）
        LimitSlider.ValueChanged += OnLimitChanged;
        LimitInput.TextChanged += OnLimitInputChanged;
        AutoStartChk.Checked += OnAutoStartChk;
        AutoStartChk.Unchecked += OnAutoStartChk;

        // 初始化 UI
        _suppress = true;
        LimitSlider.Value = _config.ChargeLimit;
        LimitInput.Text = _config.ChargeLimit.ToString();
        AutoStartChk.IsChecked = _config.StartWithWindows;
        _suppress = false;

        UpdateStatus(_battery.GetStatus());
    }

    // ── 状态更新（由 App 轮询调用）──

    public void UpdateStatus(BatteryStatus s)
    {
        Dispatcher.Invoke(() =>
        {
            // 电量
            ChargePercentLabel.Text = s.ChargePercent.HasValue ? $"{s.ChargePercent}%" : "--%";
            BatteryPercentText.Text = s.ChargePercent.HasValue ? $"{s.ChargePercent}" : "--";

            // 电池动画
            if (s.ChargePercent.HasValue)
            {
                double fillH = s.ChargePercent.Value / 100.0 * 62;
                double curH = BatteryFill.Height;
                if (Math.Abs(curH - fillH) > 0.5)
                {
                    var hAnim = new DoubleAnimation(curH, fillH, TimeSpan.FromMilliseconds(600))
                    { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                    var tAnim = new DoubleAnimation(62 - curH, 62 - fillH, TimeSpan.FromMilliseconds(600))
                    { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                    BatteryFill.BeginAnimation(Canvas.TopProperty, tAnim);
                    BatteryFill.BeginAnimation(System.Windows.Shapes.Rectangle.HeightProperty, hAnim);
                }
            }

            // 颜色
            Color fillColor = s.ChargePercent > 80 ? Color.FromRgb(52, 199, 89)
                : s.ChargePercent > 40 ? Color.FromRgb(255, 204, 0)
                : Color.FromRgb(255, 99, 71);
            BatteryFillBrush.Color = fillColor;
            ChargePercentLabel.Foreground = new SolidColorBrush(fillColor);

            // 状态文字
            string mode = s.IsCharging ? "充电中" : s.AcConnected ? "已停充" : "未接电源";
            if (s.ChargeLimit.HasValue && s.ChargeLimit < 100) mode += $" | 限制 {s.ChargeLimit}%";
            StatusLabel.Text = mode;

            // 温度
            TempLabel.Text = s.CpuTemp.HasValue
                ? $"CPU {s.CpuTemp:0.0}°C{(s.GpuTemp.HasValue ? $" | GPU {s.GpuTemp:0.0}°C" : "")} | 适配器 {(s.AcConnected ? "已连接" : "未连接")}"
                : $"适配器 {(s.AcConnected ? "已连接" : "未连接")}";

            // 健康信息
            if (s.Health != null)
            {
                var h = s.Health;
                if (h.HealthPercent.HasValue)
                {
                    HealthLabel.Text = $"{h.HealthPercent.Value:0.0}%";
                    HealthBar.Width = Math.Max(0, h.HealthPercent.Value / 100.0 * ActualHealthBarWidth);
                    HealthBarBrush.Color = h.HealthPercent > 80 ? Color.FromRgb(52, 199, 89)
                        : h.HealthPercent > 50 ? Color.FromRgb(255, 204, 0)
                        : Color.FromRgb(255, 99, 71);
                }
                if (h.DesignCapacity.HasValue) DesignCapLabel.Text = $"{h.DesignCapacity} mWh";
                if (h.FullChargeCapacity.HasValue) FullCapLabel.Text = $"{h.FullChargeCapacity} mWh";
                if (h.DesignVoltage.HasValue) VoltageLabel.Text = $"{h.DesignVoltage} mV";
            }
            AdapterLabel.Text = s.AcConnected ? "已连接" : "未连接";
        });
    }

    private double ActualHealthBarWidth => Math.Max(100, (ActualWidth > 0 ? ActualWidth : Width) - 80);

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        var h = _battery.GetStatus().Health;
        if (h?.HealthPercent.HasValue == true)
            HealthBar.Width = Math.Max(0, h.HealthPercent.Value / 100.0 * ActualHealthBarWidth);
    }

    // ── 充电限制 ──

    private void OnLimitChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress || LimitInput == null) return;
        int val = (int)Math.Round(LimitSlider.Value);
        _suppress = true;
        LimitInput.Text = val.ToString();
        _suppress = false;
        ApplyChargeLimit(val);
    }

    private void OnLimitInputChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        if (int.TryParse(LimitInput.Text, out int val))
        {
            val = Math.Clamp(val, 20, 100);
            _suppress = true;
            LimitSlider.Value = val;
            _suppress = false;
            ApplyChargeLimit(val);
        }
    }

    private void ApplyChargeLimit(int val)
    {
        _config.ChargeLimit = val;
        _config.ChargeLimitEnabled = val < 100;
        _battery.SetChargeLimit(val);
        _config.Save();
        _onConfigChanged?.Invoke(_config);
        _onRefresh?.Invoke();
    }

    // ── 开机自启 ──

    private void OnAutoStartChk(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        _config.StartWithWindows = AutoStartChk.IsChecked == true;
        _config.ApplyAutoStart();
        _config.Save();
    }

    // ── 关闭=隐藏到托盘 ──

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
