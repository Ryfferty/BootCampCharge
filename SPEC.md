# BootCampCharge — 功能规格文档 (SPEC)

> 基于 AlDente 开源源码 (AppHouseKitchen/AlDente-Battery_Care_and_Monitoring) 逆向分析 +
> 官网文档 (apphousekitchen.com) 功能说明 + Windows Boot Camp SMC 实测验证。

---

## 目录

1. [架构总览](#1-架构总览)
2. [SMC 键完整参考](#2-smc-键完整参考)
3. [UI 设计规格](#3-ui-设计规格)
4. [功能规格：充电限制 (Charge Limiter)](#4-功能规格充电限制-charge-limiter)
5. [功能规格：放电模式 (Discharge)](#5-功能规格放电模式-discharge)
6. [功能规格：航行模式 (Sailing Mode)](#6-功能规格航行模式-sailing-mode)
7. [功能规格：过热保护 (Heat Protection)](#7-功能规格过热保护-heat-protection)
8. [功能规格：校准模式 (Calibration Mode)](#8-功能规格校准模式-calibration-mode)
9. [功能规格：临时充满 (Top Up)](#9-功能规格临时充满-top-up)
10. [功能规格：硬件电量百分比 (Hardware Battery Percentage)](#10-功能规格硬件电量百分比-hardware-battery-percentage)
11. [主轮询循环规格](#11-主轮询循环规格)
12. [状态优先级矩阵](#12-状态优先级矩阵)
13. [Intel MacBook 特殊行为](#13-intel-macbook-特殊行为)
14. [功能可行性与实现状态总表](#14-功能可行性与实现状态总表)

---

## 1. 架构总览

### AlDente 原版架构 (macOS)

```
AlDente.app (SwiftUI)
├── AppDelegate.swift     — 菜单栏图标 + NSPopover 弹出窗口 + 5秒轮询定时器
├── ContentView.swift     — SwiftUI 弹窗布局: 滑块 + 文本输入 + 按钮
├── Helper.swift          — SMC 操作封装 + IOPMAssertion 睡眠控制
├── PersistanceManager.swift — UserDefaults 配置持久化 (chargeVal, oldKey, launchOnLogin)
└── com.davidwernhart.Helper (特权守护进程, 通过 NSXPC 通信)
    ├── main.swift        — 守护进程入口, 10秒检查 AlDente 是否运行, 退出时恢复默认
    ├── HelperTool.swift  — XPC 协议实现: setSMCByte / readSMCByte / readSMCUInt32 / createAssertion / releaseAssertion / reset
    └── SMC.swift         — IOKit AppleSMC 驱动通信层 (SMCParamStruct 80字节结构体)
```

### 关键架构决策

- **特权分离**: GUI 进程无 root 权限, 通过 SMJobBless 安装特权 Helper 守护进程, 经 NSXPC 通信执行 SMC 写入
- **两种充电控制模式**:
  - **Charge Inhibit 模式** (默认, Apple Silicon + 较新 Intel): 周期性读写 `CH0B` (0=允许充电, 2=禁止充电), 配合 `IOPMAssertion` 防止睡眠
  - **BCLM 模式** (Classic / Intel oldKey): 直接写 `BCLM` 键设置充电上限, 硬件自动执行, 不需要持续运行 app
- **Helper 自愈**: 守护进程每 10 秒检查 AlDente 是否在运行; 如果 AlDente 退出则 `reset()` 恢复所有修改的 SMC 键并退出

### BootCampCharge 对应架构 (Windows)

```
BootCampCharge.exe (C# .NET 8 WinForms)
├── Program.cs          — 入口, 管理员权限检查, InpOut 驱动启动
├── TrayApp.cs          — NotifyIcon 系统托盘 + ContextMenuStrip 右键菜单 + StatusForm 左键弹窗
├── BatteryManager.cs   — 全部充电管理逻辑 (限制/放电/航行/过热保护/校准/Top Up)
├── AppConfig.cs        — JSON 配置持久化
├── SMC/                — SMC 通信库 (I/O 端口 0x300/0x304, InpOut 驱动)
│   ├── Smc.cs          — SMC 核心类
│   ├── PortTransport.cs — pre-T2 I/O 端口传输
│   └── Native.cs       — InpOut32/InpOut64 P/Invoke
└── drivers/
    └── inpoutx64.dll   — I/O 端口内核驱动
```

---

## 2. SMC 键完整参考

### 核心充电控制键

| SMC 键 | 类型 | 读/写 | 功能 | Boot Camp 验证状态 |
|--------|------|-------|------|-------------------|
| `BCLM` | ui8 | 读写 | 充电上限百分比 (0-100)。硬件固件级执行, 不需要软件持续运行。值=0 表示无限制。 | ✅ 写入 77 生效 |
| `CH0B` | ui8 | 读写 | 充电开关。0=允许充电, 2=禁止充电。软件级控制, 需持续运行。 | ✅ 读取/写入成功 |
| `CH0C` | ui8 | 读写 | CH0B 的备用键 (部分机型)。与 CH0B 同步写入。 | ⚠️ 未单独验证 |
| `BRSC` | ui32 | 只读 | 电池剩余容量。高 16 位是百分比 (value >> 16)。 | ✅ 读取成功 |

### 温度传感器键

| SMC 键 | 类型 | 功能 | Boot Camp 验证状态 |
|--------|------|------|-------------------|
| `TC0P` | sp78 | CPU 近邻温度 (°C) | ✅ 读取成功 |
| `TC0D` | sp78 | CPU 核心二极管温度 | ⚠️ 未验证 |
| `TC0F` | sp78 | CPU Die 温度 | ⚠️ 未验证 |
| `TC0H` | sp78 | CPU 散热器温度 | ⚠️ 未验证 |
| `TG0D` | sp78 | GPU 二极管温度 | ✅ 读取成功 |
| `TG0H` | sp78 | GPU 散热器温度 | ⚠️ 未验证 |
| `TA0P` | sp78 | 环境空气温度 | ⚠️ 未验证 |
| `TB0T`-`TB3T` | sp78 | 电池外壳温度 (4个传感器) | ⚠️ 未验证, AlDente Pro 过热保护可能使用 |

### 电池信息键

| SMC 键 | 类型 | 功能 | Boot Camp 验证状态 |
|--------|------|------|-------------------|
| `ACIN` | flag | 充电器连接状态 | ✅ 读取成功 |
| `BATP` | flag | 电池存在状态 | ✅ 读取成功 |
| `BNum` | ui8 | 电池循环次数 | ⚠️ 不准确 (Boot Camp 显示 1, macOS 显示 105) |
| `B0RM` | ui16 | 电池剩余容量 (mAh) | ⚠️ 未验证 |
| `B0FC` | ui16 | 电池满充容量 (mAh) | ⚠️ 未验证 |
| `B0DC` | ui16 | 电池设计容量 (mAh) | ⚠️ 未验证 |
| `BSIn` | ui8 | 电池状态信息 (位掩码: bit0=充电中, bit1=AC连接, bit6=电池正常) | ⚠️ 未验证 |

### SP78 温度解码

```
SP78 是 2 字节有符号定点数:
  byte[0] = 高字节 (7位整数 + 1位符号), byte[1] = 低字节 (8位小数, 通常忽略)
  解码: sign = (byte[0] & 0x80) ? -1 : 1
        value = sign * (byte[0] & 0x7F)  // 取整数值, 忽略小数
```

源码参考 (SMC.swift L88-92):
```swift
extension Double {
    init(fromSP78 bytes: SP78) {
        let sign = bytes.0 & 0x80 == 0 ? 1.0 : -1.0
        self = sign * Double(bytes.0 & 0x7F)
    }
}
```

### C# 实现指引

```csharp
// BCLM 读写 (ui8)
smc.WriteKey("BCLM", new byte[] { 77 });   // 设置充电上限 77%
byte[] data = smc.ReadKey("BCLM", 1);       // 读取当前充电上限
int limit = data[0];

// CH0B 读写 (ui8)
smc.WriteKey("CH0B", new byte[] { 2 });     // 禁止充电
smc.WriteKey("CH0B", new byte[] { 0 });     // 允许充电

// BRSC 读取 (ui32)
byte[] brsc = smc.ReadKey("BRSC", 4);
int chargePercent = (brsc[0] << 8) | brsc[1]; // 高16位 = 百分比

// TC0P 读取 (sp78)
byte[] temp = smc.ReadKey("TC0P", 2);
double celsius = (temp[0] & 0x80) != 0 ? -(temp[0] & 0x7F) : temp[0];
```

---

## 3. UI 设计规格

### 3.1 AlDente 原版 UI (macOS)

#### 菜单栏图标

- NSStatusItem 放置在系统菜单栏 (右上角)
- 图标: `menubaricon` (一个小的叉子图标, 代表 "AlDente" 意大利面)
- **左键点击**: 切换显示/隐藏 NSPopover 弹出窗口

#### 弹出窗口 (NSPopover)

- **不是独立窗口**, 是从菜单栏图标向下弹出的 NSPopover
- 尺寸: 400 × 100 (收起) / 400 × 275 (展开设置)
- 行为: `.transient` — 点击窗口外部自动关闭
- 包含 NSHostingController 包装的 SwiftUI ContentView

#### ContentView 布局 (400px 宽)

```
┌──────────────────────────────────────────────────┐
│ Max. Battery Charge: [ 80 ]    [Settings] [Quit] │  ← HStack: 标签 + TextField + 按钮
│ ━━━━━━━━━━━━━━━━━●━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │  ← Slider (20...100)
│                                                    │
│ ── 展开设置后 (showSettings=true) ───────────────── │
│                                                    │
│ ☑ Launch at login          [Reinstall Helper]     │  ← Toggle + Button
│ ☑ Use Classic SMC Key (Intel)                      │  ← Toggle (仅 Intel 显示)
│                                                    │
│ AlDente 1.x 🍝                                     │  ← 版本信息
│ github.com/davidwernhart/AlDente                   │  ← 链接按钮
│ Cooked up in 2021 by AppHouseKitchen               │
│                              [Get Pro 🍜]          │  ← Pro 推广按钮
│                                                    │
│ Status: Charge Inhibit: true | Prevent Sleep: ... │  ← 底部状态文本
└──────────────────────────────────────────────────┘
```

**控件清单**:
1. **Slider** — 范围 20...100, 绑定 chargeVal, 实时调用 `presenter.setValue()`
2. **TextField** — 数字输入框, 限制 20-100, 与 Slider 双向绑定
3. **Button "Settings"** — 切换 showSettings, 展开/收起设置面板
4. **Button "Quit"** — 终止应用, 退出时恢复充电 (`enableCharging()` + `enableSleep()`)
5. **Toggle "Launch at login"** — 开机自启
6. **Toggle "Use Classic SMC Key (Intel)"** — BCLM 模式 vs Charge Inhibit 模式
7. **Button "Reinstall Helper"** — 重新安装特权守护进程
8. **Button "Get Pro"** — 打开 Pro 购买页面
9. **Text "Status"** — 底部状态字符串

#### AlDente Pro 弹出窗口 (参考)

Pro 版使用更丰富的 Dashboard 布局:
- **Charge Limit Slider** — 带刻度, 可拖动设置上限
- **Sailing Mode 区间指示** — 滑块下方的虚线区域表示航行区间
- **Discharge 按钮** — 手动触发放电
- **Top Up 按钮** — 临时充满
- **Calibration 启动** — 在 Settings → Dashboard → Start Calibration
- **Heat Protection 设置** — Settings → Features → 启用 + 温度选择
- **Power Flow Sankey 图** — 实时功率流向图
- **Live Status Icons** — 当前状态的图标指示

### 3.2 BootCampCharge UI 规格 (WinForms)

#### 系统托盘

- **NotifyIcon** — 系统托盘图标
- **左键单击**: 显示/隐藏 StatusForm 弹出窗口
- **右键单击**: 显示 ContextMenuStrip 上下文菜单
- **动态图标颜色**: 根据 charging 状态变色

#### ContextMenuStrip 右键菜单布局

```
┌──────────────────────────────┐
│ 80% | 充电中 | Limit 80% | 45°C │  ← 状态标签 (禁用, 仅显示)
├──────────────────────────────┤
│ 充电限制 ►                    │  ← 子菜单
│   ☑ 启用充电限制              │
│   ──────                      │
│   ☐ 60%                       │
│   ☐ 70%                       │
│   ☑ 80% （推荐）              │
│   ☐ 85%                       │
│   ☐ 90%                       │
│   ☐ 100%（不限制）            │
├──────────────────────────────┤
│ 过热保护 ►                    │  ← 子菜单
│   ☐ 关闭                      │
│   ☑ 35°C                      │
│   ☐ 40°C                      │
│   ☐ 45°C                      │
├──────────────────────────────┤
│ ☐ 航行模式                    │  ← Toggle
│ ☐ 临时充满                    │  ← Toggle
│   校准模式…                   │  ← 对话框确认
├──────────────────────────────┤
│ ☐ 开机自启                    │  ← Toggle (注册表)
├──────────────────────────────┤
│ 退出（恢复满充）              │  ← 恢复默认并退出
└──────────────────────────────┘
```

#### StatusForm 弹出窗口 (左键单击)

半透明状态窗口, 实时显示:
- 当前电量百分比
- 充电状态 (充电中/已停充)
- 当前充电限制值
- CPU 温度
- 校准模式进度 (如正在校准)

#### WinForms 控件映射

| AlDente (SwiftUI) | BootCampCharge (WinForms) |
|---|---|
| NSSlider | TrackBar (Range=20-100) |
| TextField | NumericUpDown 或 TextBox |
| NSButton | Button / ToolStripMenuItem |
| NSToggle | CheckBox / ToolStripMenuItem.Checked |
| NSPopover | Form (Show/Hide) 或 ToolTip |
| NSStatusItem | NotifyIcon |

---

## 4. 功能规格：充电限制 (Charge Limiter)

### 4.1 功能描述 (用户视角)

设置 MacBook 电池在充电时不会超过的上限百分比。当连接充电器时, 电池充电至上限后停止充电, 改为由充电器直接供电。推荐范围: 50%-80%。

### 4.2 AlDente macOS 实现方式

#### 模式 1: Charge Inhibit (默认模式)

源码: `AppDelegate.swift` L76-116

```swift
// 5秒轮询定时器
Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { timer in
    Helper.instance.getChargingInfo { (Name, Capacity, IsCharging, MaxCapacity) in
        if (Capacity < SMCPresenter.shared.value) {
            // 当前电量 < 目标 → 需要充电
            if (Helper.instance.chargeInhibited) {
                Helper.instance.enableCharging()  // CH0B = 0
            }
            Helper.instance.disableSleep()        // 防止睡眠 (充电中)
        } else {
            // 当前电量 >= 目标 → 停止充电
            if (!Helper.instance.chargeInhibited) {
                Helper.instance.disableCharging() // CH0B = 2
            }
            Helper.instance.enableSleep()
        }
    }
}
```

**工作原理**:
1. 每 5 秒读取系统电量 (`IOPSCopyPowerSourcesInfo`)
2. 比较 `Capacity < chargeLimit`
3. 低于限制 → `SMCWriteByte("CH0B", 0)` 允许充电 + 禁止睡眠
4. 达到限制 → `SMCWriteByte("CH0B", 2)` 禁止充电 + 允许睡眠
5. **关键**: 必须持续运行, 否则 CH0B 会恢复默认 (0 = 充电)

**SMC 操作** (Helper.swift L90-106):
```swift
func enableCharging() {
    SMCWriteByte(key: "CH0B", value: 00)   // 允许充电
    self.chargeInhibited = false
}
func disableCharging() {
    SMCWriteByte(key: "CH0B", value: 02)   // 禁止充电
    self.chargeInhibited = true
}
```

#### 模式 2: BCLM (Classic / Intel oldKey 模式)

源码: `Helper.swift` L215-218, `ContentView.swift` L252-257

```swift
func writeMaxBatteryCharge(setVal: UInt8) {
    SMCWriteByte(key: "BCLM", value: setVal)  // 直接写充电上限
}
```

**工作原理**:
1. 用户拖动滑块 → `writeMaxBatteryCharge(setVal: 77)`
2. 直接写 `BCLM = 77`
3. **硬件固件自动执行**: 电池充电到 77% 后自动停止
4. **不需要 app 持续运行** — BCLM 值持久保存在 SMC 中
5. 精度: 官方说明实际停止充电可能偏差 ±5%
6. **退出时不需要恢复** — BCLM 值保留 (除非显式设为 0)

### 4.3 UI 交互流程

1. 用户左键点击菜单栏图标 → 弹出窗口
2. 窗口显示: "Max. Battery Charge: [80]" + 滑块
3. 用户拖动滑块到 60% (或直接在文本框输入 60)
4. `presenter.setValue(value: 60)` 被调用:
   - `PersistanceManager.instance.chargeVal = 60` (持久化)
   - `PersistanceManager.instance.save()`
   - `writeValue()` → BCLM 模式下调用 `writeMaxBatteryCharge(setVal: 60)`
5. 如果是 Charge Inhibit 模式: 下一轮 5 秒轮询会自动比较并停止充电

### 4.4 Windows Boot Camp 复现方案

**推荐策略**: 双写 BCLM + CH0B

| 层面 | 实现 |
|------|------|
| SMC 键 | `BCLM` (ui8, 读写) — 硬件级执行, 持久化<br>`CH0B` (ui8, 读写) — 软件级控制, 作为补充 |
| 核心逻辑 | `SetChargeLimit(percent)` → 写 `BCLM=percent` + `CH0B=0` (允许充电到上限) |
| 轮询 | 30 秒间隔读取电量, 验证是否到达上限 |
| 配置持久化 | JSON: `ChargeLimit` (0-100), `ChargeLimitEnabled` (bool) |
| WinForms 控件 | `ToolStripMenuItem` 子菜单 (60/70/80/85/90/100), 单选逻辑 |

**关键差异**:
- Windows Boot Camp 下 BCLM 精确停止 (写 77 停在 77%, 不多充 ~3%)
- macOS 下 BCLM 写 77 会充到 ~80% (有 +3% buffer), Windows 无此 buffer

**C# 实现要点**:
```csharp
// 设置充电限制
public void SetChargeLimit(int percent) {
    byte val = (byte)percent;
    if (percent >= 100) { DisableLimit(); return; }
    WriteKeySafe("BCLM", new byte[] { val });  // 硬件级上限
    EnableCharging();                           // CH0B = 0
}

// 取消限制
public void DisableLimit() {
    WriteKeySafe("BCLM", new byte[] { 0 });    // 0 = 无限制
    EnableCharging();
}
```

---

## 5. 功能规格：放电模式 (Discharge)

### 5.1 功能描述 (用户视角)

当当前电量高于设定的充电上限时, 让电池在**插着充电器的情况下**放电, 直到降至充电上限。放电完成后自动恢复充电器供电。

### 5.2 AlDente macOS 实现方式

**放电原理**:
- 设置 `CH0B = 2` 禁止充电 → 系统改为由电池供电 (即使充电器连接)
- 电池持续放电直到达到充电上限
- 到达上限后 `CH0B = 0` 恢复充电 → 但此时已在上限, 硬件不再充入

**关键操作**:
1. `disableCharging()` — `CH0B = 2` (模拟拔掉充电器)
2. `disableSleep()` — 防止睡眠中断放电 (Clamshell 模式必须)
3. 轮询监控电量, 达到上限后 `enableCharging()` — `CH0B = 0`

**AlDente Pro 增强**:
- **Automatic Discharge**: 当 Charge Limit < 当前电量时自动放电, 不需要手动按按钮
- **Discharge in Clamshell Mode**: 合盖状态下也能放电 (Pro 独有)

### 5.3 UI 交互流程

1. 当前电量 90%, Charge Limit = 80%
2. 充电已自动暂停 (电量在上限之上), 电池停留在 90%
3. 用户点击 "Discharge" 按钮
4. AlDente 设置 `CH0B = 2` + 禁用睡眠
5. 电池开始放电 (即使插着充电器, 系统从电池取电)
6. 电量降至 80% → AlDente 设置 `CH0B = 0`, 恢复充电器供电
7. Discharge 功能自动关闭

### 5.4 Windows Boot Camp 复现方案

| 层面 | 实现 |
|------|------|
| SMC 键 | `CH0B` (ui8) — 写 2 禁止充电触发放电, 写 0 恢复 |
| 防止睡眠 | `SetThreadExecutionState(ES_CONTINUOUS \| ES_SYSTEM_REQUIRED \| ES_DISPLAY_REQUIRED)` 替代 IOPMAssertion |
| 轮询监控 | 30 秒间隔读 BRSC, 到达上限后恢复 CH0B=0 |
| WinForms 控件 | `ToolStripMenuItem("手动放电")` Toggle, 或自动触发 |

**C# 实现要点**:
```csharp
// 放电
public void StartDischarge(int targetPercent) {
    InhibitCharging();  // CH0B = 2
    PreventSleep();     // SetThreadExecutionState
    _dischargeTarget = targetPercent;
    _discharging = true;
}

// 轮询中检查
public void PollDischarge() {
    if (!_discharging) return;
    int? charge = GetChargePercent();
    if (charge.HasValue && charge.Value <= _dischargeTarget) {
        EnableCharging();   // CH0B = 0
        AllowSleep();
        _discharging = false;
    }
}

// 防止睡眠
[DllImport("kernel32.dll")]
static extern uint SetThreadExecutionState(uint esFlags);
const uint ES_CONTINUOUS = 0x80000000;
const uint ES_SYSTEM_REQUIRED = 0x00000001;
const uint ES_DISPLAY_REQUIRED = 0x00000002;

void PreventSleep() {
    SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
}
void AllowSleep() {
    SetThreadExecutionState(ES_CONTINUOUS);
}
```

**注意事项**:
- MagSafe LED 在放电时可能闪烁橙绿色 (macOS 认为充电器异常), 这是正常行为
- 部分第三方 Thunderbolt Dock 在放电时可能断连

---

## 6. 功能规格：航行模式 (Sailing Mode)

### 6.1 功能描述 (用户视角)

航行模式是充电限制的补充功能。设置一个低于充电上限的**下限**, 允许电池在这个区间内自然放电后再充电, 避免频繁的"微充电"。

**核心目的**: 减少充电循环次数。当电池因瞬时高功耗微小放电 (或长时间自放电) 而略低于上限时, 不立即充电, 而是等到跌至下限才充回上限。

**重要认知**: 航行模式**不是**主动让电池在区间内来回充放。保持电池在稳定百分比 (如 80%) 比在 30%-70% 区间循环更健康。

### 6.2 AlDente macOS 实现方式

**工作流程** (基于 Charge Inhibit 模式):

```
上限 = 80% (Charge Limit)
区间 = 5-10% (Sailing Mode interval)
下限 = 上限 - 区间 = 70-75%

状态机:
  电量 >= 上限 (80%)  → CH0B = 2 (停止充电)
  电量在下限~上限之间 → 不充电 (等待, 这是"航行"状态)
  电量 <= 下限 (70%)  → CH0B = 0 (开始充电, 直到上限)
```

**在 AlDente 源码 (免费版) 中**: Sailing Mode 不存在于开源代码中 (它是 Pro 功能)。但可以通过修改 AppDelegate 的轮询逻辑实现:

```swift
// 修改后的轮询逻辑 (概念):
Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { timer in
    Helper.instance.getChargingInfo { (Name, Capacity, IsCharging, MaxCapacity) in
        let upperLimit = SMCPresenter.shared.value  // e.g. 80
        let lowerLimit = upperLimit - sailingInterval  // e.g. 70

        if (Capacity >= upperLimit) {
            // 达到上限 → 停止充电
            Helper.instance.disableCharging()
        } else if (Capacity <= lowerLimit) {
            // 跌至下限 → 开始充电
            Helper.instance.enableCharging()
        } else {
            // 在区间内 → 保持当前状态 (不操作)
        }
    }
}
```

### 6.3 UI 交互流程

1. 用户在 Settings 中启用 Sailing Mode
2. 设置区间值 (5-10%, 推荐 5%)
3. 主界面滑块下方出现**虚线区域**, 可视化显示航行区间 (如 75%-80%)
4. 电量在上限时: 显示 "Charging Paused" (暂停充电)
5. 电量微降至 78%: 显示 "Sailing" (航行中, 不充电)
6. 电量降至 75% (下限): 自动开始充电至 80%

**示例场景**:
- 上限 80%, 区间 10%, 范围 = 70%-80%
- 当前 77% 且在充电 → 停止充电 (77% 在航行范围内)
- 拔下充电器使用, 电量降至 62% → 重新插上 → 开始充电 (62% < 70% 下限)
- 充至 80% → 停止充电, 回到航行状态

### 6.4 Windows Boot Camp 复现方案

| 层面 | 实现 |
|------|------|
| SMC 键 | `CH0B` — 在下限时写 0, 在上限时写 2, 区间内不操作 |
| 电量读取 | `BRSC` >> 16 (硬件百分比) |
| 配置 | JSON: `SailMode.Enabled`, `SailMode.Floor`, `SailMode.Hysteresis` |
| WinForms 控件 | `ToolStripMenuItem("航行模式")` Toggle + NumericUpDown 设置区间 |

**C# 实现要点** (已在 BatteryManager.cs 中实现):
```csharp
public enum SailAction { Hold, Inhibit, Resume }

public SailAction EvaluateSailMode(int limit, int floor) {
    int? charge = GetChargePercent();
    if (!charge.HasValue) return SailAction.Hold;

    int c = charge.Value;
    int? ch0b = GetChargerStatus();

    if (c >= limit) {
        // 达到上限 → 停止充电
        if (ch0b != 2) InhibitCharging();
        return SailAction.Inhibit;
    }
    if (c <= floor) {
        // 跌至下限 → 恢复充电
        if (ch0b != 0) EnableCharging();
        return SailAction.Resume;
    }
    return SailAction.Hold;  // 区间内, 保持不动
}
```

**优先级**: 航行模式在过热保护和校准模式不活跃时才执行。

---

## 7. 功能规格：过热保护 (Heat Protection)

### 7.1 功能描述 (用户视角)

当电池/CPU 温度超过设定阈值时, 自动暂停充电以防止电池过热损坏。温度回落后恢复充电。

### 7.2 AlDente macOS 实现方式

**温度传感器**: AlDente Pro 使用 `TC0P` (CPU 近邻温度) 作为主要判断依据。部分版本也读取电池温度传感器 (`TB0T`-`TB3T`)。

**SMC 温度读取** (SMC.swift L609-623):
```swift
public static func temperature(_ sensorCode: FourCharCode) throws -> Double {
    let data = try readData(SMCKey(code: sensorCode, info: DataTypes.SP78))
    let temperatureInCelius = Double(fromSP78: (data.0, data.1))
    return temperatureInCelius
}
```

**迟滞机制 (Hysteresis)** — 这是 AlDente Pro 的关键设计:

```
温度超过阈值 → 停止充电, 启动 5 分钟倒计时
  ↓ 5 分钟后
  ├─ 温度仍超阈值 → 继续停止, 再启 5 分钟倒计时
  └─ 温度已降 → 恢复充电, 至少充 5 分钟
                  ↓ 5 分钟后
                  ├─ 温度仍超 → 停止充电
                  └─ 温度正常 → 继续充电
```

**目的**: 避免在阈值附近频繁切换充电状态, 同时限制长时间充电。

### 7.3 UI 交互流程

1. Settings → Features → 启用 Heat Protection
2. 点击 "Max Temp." 文本框, 选择温度限制 (推荐 35°C)
3. 正常充电中, CPU 温度 32°C → 正常
4. CPU 温度升至 35°C+ → 立即暂停充电, 显示 "Heat Protection Active"
5. 温度降至 35°C 以下, 等 5 分钟 → 恢复充电
6. 恢复后至少充 5 分钟 (即使温度再升也不停)

**校准模式期间**: 过热保护自动禁用 (避免干扰校准循环)。

### 7.4 Windows Boot Camp 复现方案

| 层面 | 实现 |
|------|------|
| 温度传感器 | `TC0P` (sp78, CPU 近邻温度) — 已验证 ✅<br>`TG0D` (sp78, GPU 温度) — 已验证 ✅ |
| 充电控制 | `CH0B` — 超温写 2, 降温写 0 |
| 迟滞逻辑 | 5 分钟计时器 (300 秒), 实现 AlDente 的迟滞状态机 |
| 配置 | JSON: `Overtemp.Enabled`, `Overtemp.ThresholdCelsius` |
| WinForms 控件 | `ToolStripMenuItem("过热保护")` 子菜单: 关闭/35°C/40°C/45°C |

**C# 实现要点** (基础版已在 BatteryManager.cs 中实现, 需增强迟滞):

```csharp
// 基础版 (已有, 简单迟滞 2°C)
public bool EvaluateOvertempProtection(double thresholdCelsius) {
    double? temp = GetCpuTemperature();
    if (!temp.HasValue) return _overtempEngaged;

    if (temp.Value > thresholdCelsius && !_overtempEngaged) {
        InhibitCharging();
        _overtempEngaged = true;
    } else if (temp.Value < thresholdCelsius - 2 && _overtempEngaged) {
        EnableCharging();
        _overtempEngaged = false;
    }
    return _overtempEngaged;
}

// 增强版 (AlDente 风格 5 分钟迟滞)
private DateTime? _overtempCooldownUntil;
private DateTime? _chargingMinUntil;

public bool EvaluateOvertempPro(double thresholdCelsius) {
    double? temp = GetCpuTemperature();
    if (!temp.HasValue) return _overtempEngaged;

    if (temp.Value > thresholdCelsius) {
        if (!_overtempEngaged) {
            InhibitCharging();
            _overtempEngaged = true;
            _overtempCooldownUntil = DateTime.UtcNow.AddMinutes(5);
        }
    } else {
        // 温度已降
        if (_overtempEngaged && _overtempCooldownUntil.HasValue &&
            DateTime.UtcNow >= _overtempCooldownUntil.Value) {
            EnableCharging();
            _overtempEngaged = false;
            _chargingMinUntil = DateTime.UtcNow.AddMinutes(5); // 至少充 5 分钟
        }
    }
    return _overtempEngaged;
}
```

**温度阈值建议**: Apple 建议环境温度不超过 35°C, 因此 35°C 是推荐阈值。

---

## 8. 功能规格：校准模式 (Calibration Mode)

### 8.1 功能描述 (用户视角)

自动执行完整的电池校准循环, 修复电池管理系统与实际电量的偏差。解决长时间保持中低电量导致的电量报告不准确问题 (如 50% 突然关机、电量显示异常)。

### 8.2 为什么需要校准

**症状**:
- 50% 电量突然关机
- 电量百分比跳跃不稳定
- 电量快速下降
- 满充后长时间显示 100%

**原因**: 电池管理系统 (BMS) 在长期不进行完整充放循环后, 会丢失对电池真实高低点的跟踪。

**标准校准流程**:
1. 放电至接近 0%
2. 充满至 100%
3. 保持 100% 静置 1-2 小时

### 8.3 AlDente Pro 校准流程

**完整自动化循环**:

```
Settings → Dashboard → Start Calibration

阶段 1: CHARGE   — 从当前电量充电到 100%
阶段 2: DISCHARGE — 从 100% 放电到 10% (即使插着充电器)
阶段 3: CHARGE   — 从 10% 充电回 100%
阶段 4: PAUSE    — 在 100% 保持 1 小时 (让电芯平衡)
阶段 5: RESTORE  — 放电回预设充电上限 (如 80%)

特殊行为:
- 校准期间 Heat Protection 自动禁用
- 校准期间 Sailing Mode 自动禁用
- 校准期间 Top Up 自动禁用
- 校准期间禁止睡眠 (保证循环不中断)
```

**推荐频率**:
- 不常使用电池的用户: 每 4-6 周
- 一直插电的用户: 每 3 周

### 8.4 Windows Boot Camp 复现方案

| 层面 | 实现 |
|------|------|
| 充电控制 | `CH0B` — 放电阶段写 2, 充电阶段写 0 |
| 电量监控 | `BRSC` >> 16 |
| 上限控制 | `BCLM` — 充电阶段写 100 (移除限制), 恢复阶段写回原值 |
| 防止睡眠 | `SetThreadExecutionState` |
| 状态机 | CalibrationPhase enum: Idle → Charging → Resting → Discharging → Done → Idle |
| 配置 | JSON: 无持久化配置, 运行时状态 |
| WinForms 控件 | `ToolStripMenuItem("校准模式…")` + MessageBox 确认对话框 |

**C# 实现要点** (已在 BatteryManager.cs 中实现):

```csharp
public enum CalibrationPhase { Idle, Charging, Resting, Discharging, Done }

public void StartCalibration() {
    _calSavedLimit = GetChargeLimit();  // 保存当前限制
    DisableLimit();                      // BCLM = 0 (移除上限)
    EnableCharging();                    // CH0B = 0
    _calPhase = CalibrationPhase.Charging;
    PreventSleep();
}

public CalibrationPhase StepCalibration() {
    switch (_calPhase) {
        case Charging:
            if (charge >= 100) {
                _calRestStart = DateTime.UtcNow;
                _calPhase = CalibrationPhase.Resting;
            }
            break;
        case Resting:
            if (DateTime.UtcNow - _calRestStart >= TimeSpan.FromHours(1)) {
                InhibitCharging();  // CH0B = 2 → 开始放电
                _calPhase = CalibrationPhase.Discharging;
            }
            break;
        case Discharging:
            if (charge <= CalibrationFloor) {  // 10-15%
                EnableCharging();
                RestoreLimit();
                _calPhase = CalibrationPhase.Done;
            }
            break;
        case Done:
            _calPhase = CalibrationPhase.Idle;
            AllowSleep();
            break;
    }
    return _calPhase;
}
```

**注意**: AlDente Pro 的校准是两轮充放 (100→10→100→静置→恢复), 当前实现是简化版 (一轮)。完整版需要添加第二个充电阶段。

---

## 9. 功能规格：临时充满 (Top Up)

### 9.1 功能描述 (用户视角)

临时移除充电限制, 让电池充满至 100%。适用于出门前需要满电的场景。充满后可以手动恢复限制, 或由 app 自动恢复 (放电回上限)。

### 9.2 AlDente macOS 实现方式

**工作原理**:
1. 记住当前 Charge Limit (如 80%)
2. 设置 Charge Limit = 100% (移除限制)
3. `enableCharging()` — CH0B = 0 (允许充电)
4. 电池充电至 100%
5. 用户手动关闭 Top Up, 或 AlDente 自动放电回原上限

**AlDente Pro 增强**:
- Top Up 完成后可自动放电回原充电上限
- Top Up 期间 Sailing Mode 和 Calibration Mode 暂停

### 9.3 UI 交互流程

1. 用户点击 "Top Up" 按钮
2. AlDente 移除充电限制 → 电池开始充电至 100%
3. 充电至 100% 后, MagSafe LED 变绿
4. 用户再次点击 "Top Up" (或 "Stop Top Up") → AlDente 恢复原充电限制
5. 如果启用了 Discharge, 电池会放电回原上限

### 9.4 Windows Boot Camp 复现方案

| 层面 | 实现 |
|------|------|
| SMC 键 | `BCLM` — 临时写 0 (移除限制), 恢复时写回保存值<br>`CH0B` — 写 0 允许充电 |
| 状态保存 | 运行时变量 `_topUpSavedLimit`, 持久化到 JSON `TopUpActive` |
| WinForms 控件 | `ToolStripMenuItem("临时充满")` Toggle |

**C# 实现要点** (已在 BatteryManager.cs 中实现):
```csharp
private int? _topUpSavedLimit;
private bool _topUpActive;

public void StartTopUp() {
    if (_topUpActive) return;
    _topUpSavedLimit = GetChargeLimit();  // 保存当前限制
    DisableLimit();                        // BCLM = 0
    EnableCharging();                      // CH0B = 0
    _topUpActive = true;
}

public void StopTopUp() {
    if (!_topUpActive) return;
    if (_topUpSavedLimit.HasValue && _topUpSavedLimit.Value < 100)
        SetChargeLimit(_topUpSavedLimit.Value);  // 恢复原限制
    else
        DisableLimit();
    _topUpActive = false;
}
```

---

## 10. 功能规格：硬件电量百分比 (Hardware Battery Percentage)

### 10.1 功能描述 (用户视角)

macOS 显示的电量百分比**不是**电池管理系统 (BMS) 报告的真实值。两者通常有 2-7% 的偏差。启用硬件电量百分比后, AlDente 直接读取 BMS 报告的值用于所有功能判断, 更精确。

### 10.2 为什么有两个百分比

**Apple 的设计意图** (推测):
- **电池寿命**: macOS 显示 100% 时, BMS 可能只有 95%。用户会更早拔掉充电器。
- **充电速度**: 最后 5% 充电很慢。显示 100% (实际 95%) 避免用户等待。
- **便利性**: 用户更喜欢看到 100% 而不是 95%。

**哪个更准确**: 硬件电量百分比 (BMS 直接报告) 更准确, 但也不是 100% 精确。

### 10.3 AlDente macOS 实现方式

**macOS 电量读取** (Helper.swift L119-130):
```swift
func getChargingInfo(withReply reply: (String,Int,Bool,Int) -> Void) {
    let snapshot = IOPSCopyPowerSourcesInfo().takeRetainedValue()
    let sources = IOPSCopyPowerSourcesList(snapshot).takeRetainedValue() as Array
    let info = IOPSGetPowerSourceDescription(snapshot, sources[0]).takeUnretainedValue()
    // capacity = info[kIOPSCurrentCapacityKey]  ← macOS 显示的百分比
}
```

**硬件电量读取** (Helper.swift L132-137):
```swift
func getSMCCharge(withReply reply: @escaping (Float)->Void) {
    Helper.instance.SMCReadUInt32(key: "BRSC") { value in
        let smcval = Float(value >> 16)  // 高 16 位 = 硬件百分比
        reply(smcval)
    }
}
```

### 10.4 Windows Boot Camp 复现方案

| 层面 | 实现 |
|------|------|
| 硬件百分比 | `BRSC` (ui32) >> 16 — 直接从 BMS 读取, 已验证 ✅ |
| 系统百分比 | `GetSystemPowerStatus()` Win32 API (Windows 显示值) |
| WinForms 控件 | CheckBox "使用硬件电量百分比" |

**关键优势**: Windows Boot Camp 下, `GetSystemPowerStatus` 报告的百分比与 BMS 基本一致 (Windows 没有 Apple 的"美化"逻辑)。因此**默认应使用 BRSC 硬件百分比**。

**C# 实现**:
```csharp
// 硬件电量百分比 (推荐)
public int? GetHardwareChargePercent() {
    byte[] data = ReadKeySafe("BRSC", 4);
    if (data != null && data.Length >= 2)
        return (data[0] << 8) | data[1];  // 高 16 位
    return null;
}

// Windows API 电量 (备用)
[DllImport("kernel32.dll")]
static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

public int? GetWindowsChargePercent() {
    GetSystemPowerStatus(out var status);
    return status.BatteryLifePercent > 100 ? (int?)null : status.BatteryLifePercent;
}
```

---

## 11. 主轮询循环规格

### 11.1 AlDente 轮询架构

源码: `AppDelegate.swift` L76-116

```swift
// 5 秒间隔定时器
Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { timer in
    if (Helper.instance.isInitialized) {
        // 1. 读取电量信息
        Helper.instance.getChargingInfo { (Name, Capacity, IsCharging, MaxCapacity) in
            // 2. Charge Inhibit 逻辑 (非 BCLM 模式)
            if (!PersistanceManager.instance.oldKey) {
                if (Capacity < target) → enableCharging() + disableSleep()
                else → disableCharging() + enableSleep()
            }
        }
        // 3. 更新状态字符串
        Helper.instance.setStatusString()
    }
}
```

### 11.2 BootCampCharge 轮询架构

源码: `TrayApp.cs` Poll() 方法

```
每 30 秒执行一次 Poll():
  1. 读取 BatteryStatus (电量/温度/充电状态)
  2. 过热保护评估 (最高优先级)
     → 如果超温: 停止充电, 跳过后续
  3. 校准状态机推进 (如果正在校准)
     → 如果校准中: 跳过航行模式
  4. 航行模式评估 (如果启用且无更高优先级功能活跃)
  5. 更新托盘图标颜色和文字
  6. 更新状态弹窗 (如果打开)
```

**轮询间隔**: AlDente = 5 秒, BootCampCharge = 30 秒 (可配置)。30 秒已足够因为 BCLM 是硬件执行, CH0B 切换延迟可接受。

### 11.3 状态优先级

```
过热保护 (Heat Protection)     [最高 — 温度安全第一]
    ↓ 不活跃时
校准模式 (Calibration Mode)    [校准期间独占控制]
    ↓ 不活跃时
Top Up                         [临时充满期间独占]
    ↓ 不活跃时
航行模式 (Sailing Mode)        [区间充放电管理]
    ↓ 不活跃时
充电限制 (Charge Limiter)      [基础限制, 由 BCLM 硬件执行]
```

---

## 12. 状态优先级矩阵

| 当前活跃功能 | 充电控制 | SMC 操作 | 附加行为 |
|---|---|---|---|
| 无 | 由 BCLM 硬件决定 | BCLM = 用户设定值 | — |
| 过热保护 | 停止充电 | CH0B = 2 | 5 分钟迟滞冷却 |
| 过热保护恢复 | 恢复充电 | CH0B = 0 | 至少充 5 分钟 |
| 校准-充电阶段 | 充满 | BCLM = 0, CH0B = 0 | 禁止睡眠 |
| 校准-静置阶段 | 保持 | 不操作 | 等待 1 小时 |
| 校准-放电阶段 | 放电 | CH0B = 2 | 禁止睡眠 |
| Top Up | 充满 | BCLM = 0, CH0B = 0 | — |
| 航行模式-区间内 | 保持当前 | 不操作 | — |
| 航行模式-达到上限 | 停止充电 | CH0B = 2 | — |
| 航行模式-跌至下限 | 恢复充电 | CH0B = 0 | — |
| 手动放电 | 放电 | CH0B = 2 | 禁止睡眠 |

---

## 13. Intel MacBook 特殊行为

### 13.1 充电控制在睡眠/关机时仍然生效

Intel MacBook 使用 BCLM 硬件级充电控制。一旦写入 BCLM 值, 即使 macOS 处于睡眠或关机状态, 硬件固件仍会执行充电限制。

**Boot Camp 关键优势**: 在 macOS 中设置 BCLM 后, 重启到 Windows Boot Camp, 充电限制**仍然生效** (BCLM 值持久保存在 SMC 中)。

### 13.2 精度差异

- macOS 下 BCLM 写 77 实际停在 ~80% (有 +3% buffer)
- Windows Boot Camp 下 BCLM 写 77 精确停在 77%
- bclm 文档的 +3% 规则在 Windows 下不适用

### 13.3 两种模式选择

| 模式 | SMC 键 | 需要 App 运行 | 精度 | 睡眠时生效 |
|------|--------|-------------|------|-----------|
| BCLM (Classic) | BCLM | 否 | ±5% | ✅ 是 |
| Charge Inhibit | CH0B | 是 | 精确 | ❌ 否 |

**Boot Camp 推荐**: 使用 BCLM 为主 (硬件执行, 睡眠/重启后仍然生效), CH0B 为辅 (软件级精确控制)。

### 13.4 MagSafe LED 行为

放电时 MagSafe LED 可能闪烁橙绿色。这是 macOS 认为充电器异常的正常反应, 不影响功能。

---

## 14. 功能可行性与实现状态总表

| # | 功能 | AlDente SMC 键 | Boot Camp 可复刻 | 当前实现状态 | WinForms 控件 |
|---|------|---------------|-----------------|-------------|--------------|
| 1 | 充电限制 (Charge Limiter) | BCLM (ui8) | ✅ 完全可行 | ✅ 已实现 | 子菜单 (60/70/80/85/90/100) |
| 2 | 充电开关 (Charge Inhibit) | CH0B (ui8) | ✅ 完全可行 | ✅ 已实现 | 内部逻辑 |
| 3 | 放电模式 (Discharge) | CH0B=2 + 防睡眠 | ✅ 可行 | ⚠️ 部分 (缺手动触发 UI) | 待添加 Toggle |
| 4 | 航行模式 (Sailing Mode) | CH0B 区间逻辑 | ✅ 完全可行 | ✅ 已实现 | Toggle |
| 5 | 过热保护 (Heat Protection) | TC0P + CH0B | ✅ 完全可行 | ✅ 已实现 (基础迟滞) | 子菜单 (关/35/40/45°C) |
| 6 | 校准模式 (Calibration) | CH0B + BRSC 状态机 | ✅ 可行 | ✅ 已实现 (简化版) | 菜单项 + 对话框 |
| 7 | 临时充满 (Top Up) | BCLM=0 + CH0B=0 | ✅ 完全可行 | ✅ 已实现 | Toggle |
| 8 | 硬件电量百分比 | BRSC >> 16 | ✅ 完全可行 | ✅ 已实现 | 内部逻辑 |
| 9 | 自动放电 (Auto Discharge) | CH0B + 自动触发 | ✅ 可行 | ❌ 未实现 | 待添加 |
| 10 | 防止睡眠 | IOPMAssertion | ⚠️ 平台差异 | ⚠️ SetThreadExecutionState | 内部逻辑 |
| 11 | 开机自启 | LaunchAtLogin | ✅ 可行 | ✅ 已实现 | Toggle (注册表) |
| 12 | 配置持久化 | UserDefaults | ✅ 可行 | ✅ 已实现 | JSON 文件 |
| 13 | 退出恢复默认 | applicationWillTerminate | ✅ 可行 | ✅ 已实现 | 退出菜单项 |
| 14 | MagSafe LED 控制 | BFCL | ❌ 不可复刻 | — | — |
| 15 | Apple Shortcuts | — | ❌ macOS 专属 | — | — |
| 16 | Power Flow 图 | — | ❌ UI 重写 | — | — |
| 17 | 定时任务 (Schedule) | — | ✅ 可行 | ❌ 未实现 | 待添加 |

### 实现完成度

- **核心电池管理**: 7/7 功能可复刻, 6/7 已实现 (85%)
- **辅助功能**: 开机自启 ✅, 配置持久化 ✅, 退出恢复 ✅
- **未实现**: 自动放电, 定时任务, Power Flow 可视化
- **不可复刻**: MagSafe LED 控制, Apple Shortcuts

---

## 附录 A: SMC 通信协议要点 (Windows Boot Camp)

### I/O 端口

- **数据端口**: `0x300`
- **命令端口**: `0x304`
- 驱动: `inpoutx64.dll` (InpOut32/64, MIT 许可, highrez.co.uk)

### SMC 通信流程

```
1. 打开 SMC: 写 0x300 端口
2. 写键名 (4字符 ASCII → 4字节) 到数据端口
3. 写命令码到命令端口:
   - 0x10 = Get Key Info
   - 0x11 = Read Key
   - 0x12 = Write Key
4. 读/写数据端口获取/发送数据
5. 关闭 SMC
```

### SMCParamStruct 结构 (80 字节)

Apple SMC 驱动通过此结构与内核通信:

```c
struct SMCParamStruct {  // 80 bytes total
    UInt32 key;           // 4-char SMC key code
    SMCVersion vers;      // 6 bytes
    SMCPLimitData pLimit; // 16 bytes
    SMCKeyInfoData keyInfo; // 9 bytes (dataSize + dataType + attributes)
    UInt16 padding;       // 2 bytes
    UInt8 result;         // 1 byte
    UInt8 status;         // 1 byte
    UInt8 data8;          // 1 byte (selector)
    UInt32 data32;        // 4 bytes
    UInt8 bytes[32];      // 32 bytes (data payload)
};
```

---

## 附录 B: 配置文件规格 (BootCampCharge.json)

```json
{
  "ChargeLimit": 80,
  "ChargeLimitEnabled": true,
  "SailMode": {
    "Enabled": false,
    "Floor": 50,
    "Hysteresis": 5
  },
  "Overtemp": {
    "Enabled": false,
    "ThresholdCelsius": 40
  },
  "StartWithWindows": false,
  "PollIntervalSeconds": 30,
  "TopUpActive": false
}
```

---

## 附录 C: 运行环境

- **目标设备**: MacBook Pro 14,3 (2017 15寸, i7-7820HQ, pre-T2)
- **操作系统**: Windows 10 + WSL2
- **运行时**: .NET 8
- **驱动**: InpOut x64 (`sc start inpoutx64`)
- **安全软件注意**: 火绒行为检测可能拦截 InpOut 驱动加载, 需手动启动或加白名单
- **权限要求**: 管理员权限 (I/O 端口访问需要)

---

*文档生成日期: 2026-08-09*
*源码版本: AlDente 开源版 (AlDente Classic, GitHub)*
*参考文档: apphousekitchen.com/aldente-overview/features, /pricing, /faq, /aldentes-behavior-on-intel-macbooks*
