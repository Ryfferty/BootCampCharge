# AlDente 架构深度分析 — SMC 控制层、状态机与 Boot Camp 重写指南

> 基于 AlDente 开源版源码（GitHub: `AppHouseKitchen/AlDente-Battery_Care_and_Monitoring`，commit `77119a1`）逐行分析  
> + 官方文档（apphousekitchen.com）+ 开发者 GitHub Discussion #286 直接回复  
> + REVERSE_ENGINEERING.md Pro 功能变量名  
> + 社区工具交叉验证（bclm, actuallymentor/battery, charlie0129/batt）  
>  
> **目的**：指导 BootCampCharge 从零重写核心控制逻辑

---

## 目录

1. [SMC 控制层架构](#1-smc-控制层架构)
2. [状态机和优先级](#2-状态机和优先级)
3. [各功能精确实现逻辑](#3-各功能精确实现逻辑)
4. [关键架构原则](#4-关键架构原则)
5. [Windows Boot Camp 特殊性](#5-windows-boot-camp-特殊性)
6. [SMC 控制流程图](#6-smc-控制流程图)
7. [BootCampCharge 重写方案](#7-bootcampcharge-重写方案)

---

## 1. SMC 控制层架构

### 1.1 BCLM 和 CH0B 的关系：互斥二选一

**核心发现：AlDente 源码中 BCLM 和 CH0B 是两种互斥的充电控制策略，由 `oldKey` 布尔标志二选一。**

| 特性 | BCLM 模式（Classic / Intel） | CH0B 模式（Charge Inhibit / 默认） |
|------|------------------------------|-------------------------------------|
| SMC 键 | `BCLM` (ui8) | `CH0B` (ui8) |
| 控制方式 | 设置充电上限百分比，固件自动执行 | 周期性开/关充电（0=允许, 2=禁止） |
| 持久化 | ✅ 跨重启/睡眠/关机/Boot Camp 切换持久 | ❌ 仅为当前运行时状态，重启后清除 |
| 需要 App 运行 | ❌ 设一次就行 | ✅ 需 5 秒轮询持续监控 |
| 精度 | ±5%（macOS 下 ~+3% buffer；Windows Boot Camp 下精确） | 精确到 1% |
| Boot Camp 可用 | ✅ **是**（唯一可用方式） | ❌ **否**（AlDente 开发者亲口确认） |
| 适用机型 | Intel Mac（`x86_64`） | Intel Mac + Apple Silicon（`arm64`） |
| 写入逻辑 | `SMCWriteByte("BCLM", value)` | 5s 轮询中按电量判断后写 `CH0B` |

### 1.2 oldKey 标志的逻辑（源码级）

```swift
// PersistanceManager.swift L16
public var oldKey: Bool = false  // 默认 false = CH0B 模式

// AppDelegate.swift L81-106 — 5秒轮询核心
Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { timer in
    Helper.instance.getChargingInfo { (Name, Capacity, IsCharging, MaxCapacity) in
        if (!PersistanceManager.instance.oldKey) {
            // ─── CH0B 模式（默认）───
            if (Capacity < SMCPresenter.shared.value) {
                // 低于目标：允许充电 + 禁止睡眠
                if (Helper.instance.chargeInhibited) {
                    Helper.instance.enableCharging()   // CH0B = 0
                }
                Helper.instance.disableSleep()
            } else {
                // 达到/超过目标：禁止充电 + 允许睡眠
                if (!Helper.instance.chargeInhibited) {
                    Helper.instance.disableCharging()  // CH0B = 2
                }
                Helper.instance.enableSleep()
            }
        } else {
            // ─── BCLM 模式 ───
            print("BCLM MODE ENABLED")
            // 不做任何事！BCLM 已经由固件管理，不需要轮询
        }
    }
}
```

**关键**：BCLM 模式下，轮询循环**什么都不做**——只打印日志。BCLM 写入是一次性的（用户拖动 Slider 时），固件接管后续所有充电控制。

### 1.3 BCLM 写入时机（非轮询，事件驱动）

```swift
// ContentView.swift L252-257 — SMCPresenter.writeValue()
func writeValue() {
    if (PersistanceManager.instance.oldKey) {
        Helper.instance.writeMaxBatteryCharge(setVal: self.value)  // BCLM = value
    }
}

// Helper.swift L215-218
func writeMaxBatteryCharge(setVal: UInt8) {
    SMCWriteByte(key: "BCLM", value: setVal)
}
```

BCLM 只在用户拖动 Slider 改变值时写入一次（`SMCPresenter.setValue` → `writeValue`），不在轮询中重复写。

### 1.4 oldKey 切换时的行为

```swift
// ContentView.swift L58-78 — Settings Toggle "Use Classic SMC Key (Intel)"
Toggle(isOn: Binding(
    get: { oldKey },
    set: { newValue in
        oldKey = newValue
        PersistanceManager.instance.oldKey = oldKey
        PersistanceManager.instance.save()
        Helper.instance.setStatusString()
        if (newValue) {
            // 切换到 BCLM 模式
            Helper.instance.enableCharging()  // CH0B = 0（清除 CH0B 的禁止状态）
            Helper.instance.enableSleep()      // 释放睡眠阻止
            // 然后由 writeValue() 写 BCLM
        } else {
            // 切换回 CH0B 模式
            presenter.setValue(value: 100)     // 先把目标设为 100（完全充电）
        }
    }
))
```

### 1.5 CH0B=0 和 CH0B=2 的具体行为

```swift
// Helper.swift L90-106
func enableCharging() {
    SMCWriteByte(key: "CH0B", value: 00)   // 允许充电
    self.chargeInhibited = false
}

func disableCharging() {
    SMCWriteByte(key: "CH0B", value: 02)   // 禁止充电
    self.chargeInhibited = true
}
```

**写 CH0B=2 后，BCLM 还生效吗？**

从源码看，AlDente 开源版**从不同时使用两者**。当 `oldKey=true`（BCLM 模式）时，轮询跳过 CH0B 逻辑；当 `oldKey=false`（CH0B 模式）时，BCLM 从未被写入。

但在固件层面（根据 AlDente 开发者在 Discussion #286 的回复和社区报告推断）：
- **如果 BCLM 已被设置（如 77），写 CH0B=2 会被固件忽略**——因为 BCLM 是硬件级优先控制
- **这就是 Boot Camp 下 CH0B 放电不工作的根因**：BCLM 管着充电上限，CH0B 的开关指令被覆盖

### 1.6 enableCharging/disableCharging 的 Apple Silicon 分支

```swift
// Helper.swift L90-106 — 注意有冗余代码
func enableCharging() {
    if (appleSilicon!) {
        SMCWriteByte(key: "CH0B", value: 00)  // Apple Silicon 也用 CH0B
    }
    SMCWriteByte(key: "CH0B", value: 00)       // 无论如何都写（上面是冗余）
    self.chargeInhibited = false
}
```

Apple Silicon 和 Intel 在开源版中用的都是 CH0B。`appleSilicon` 判断目前是冗余的（可能是遗留代码，Pro 版可能针对不同架构用不同键如 CH0C）。

---

## 2. 状态机和优先级

### 2.1 开源版主轮询循环（AppDelegate.swift Timer）

**开源版只有一个极简的轮询逻辑——没有多功能优先级状态机。** Pro 功能的优先级逻辑在闭源部分。

```
每 5 秒：
  1. getChargingInfo() — 读 IOPS 电源信息（电池名/电量/是否充电/最大容量）
  2. 检查 oldKey：
     ├── oldKey == false（CH0B 模式）：
     │   ├── 电量 < 目标值 → enableCharging() + disableSleep()
     │   └── 电量 >= 目标值 → disableCharging() + enableSleep()
     └── oldKey == true（BCLM 模式）：
         └── 什么都不做（BCLM 由固件管理）
  3. setStatusString() — 更新 UI 状态文本
```

### 2.2 chargeInhibited 标志的作用

```swift
// Helper.swift L29
public var chargeInhibited: Bool = false

// AppDelegate.swift L84, L92 — 避免重复写入的关键
if (Capacity < SMCPresenter.shared.value) {
    if (Helper.instance.chargeInhibited) {   // 只有当前在禁止状态才启用
        Helper.instance.enableCharging()      // CH0B = 0
    }
} else {
    if (!Helper.instance.chargeInhibited) {  // 只有当前在允许状态才禁止
        Helper.instance.disableCharging()     // CH0B = 2
    }
}
```

**chargeInhibited 是一个"当前实际状态"的缓存标志**，作用是：
1. **避免重复写入 SMC**——如果 CH0B 已经是 2，不再写 2
2. **减少 SMC 端口 I/O**——每次轮询最多只写一次（状态变化时），不变化时零写入
3. 程序退出时用于决定恢复行为

`checkCharging()` 会从 SMC 重新同步这个标志：

```swift
// Helper.swift L108-117
func checkCharging() {
    Helper.instance.SMCReadUInt32(key: "CH0B") { value in
        self.chargeInhibited = !(value == 00)  // 从硬件状态同步
    }
}
```

### 2.3 Pro 版优先级（从反编译 + 官方文档推断）

**开源版没有多功能优先级**——它只做最基础的充电限制。Pro 版的功能优先级是从以下来源推断的：

1. 官方文档描述的行为交叉验证
2. DMG 反编译提取的变量名（REVERSE_ENGINEERING.md）
3. 各功能互斥关系（文档明确说明 Sailing Mode 在 Top Up/Calibration 期间临时禁用）

**推断的优先级链（从高到低）：**

```
1. 过热保护（Heat Protection）— 温度安全第一，覆盖一切
2. 校准模式（Calibration）   — 独占控制，期间禁用热保护/航行/睡眠
3. 临时充满（Top Up）        — 独占控制，期间禁用航行模式
4. 航行模式（Sailing Mode）  — 区间管理，基于充电限制的上限
5. 充电限制（Charge Limit）  — 基础层
```

**互斥关系（官方文档明确）：**
- Sailing Mode 在 Top Up 和 Calibration 期间**临时禁用**
- Calibration 期间 Heat Protection 和 Sailing Mode **都禁用**
- Discharge 是手动操作，与上述自动功能互斥

### 2.4 AlDente 架构核心：单一写入路径

**这是重写 BootCampCharge 最重要的一条原则。**

AlDente 的设计（包括 Pro 版，从反编译变量名和行为推断）是：

```
┌─────────────────────────────────────────┐
│  功能层（设请求标志，不直接写 SMC）       │
│  • ChargeLimit.enabled = true            │
│  • HeatProtection.requestInhibit = true  │
│  • SailingMode.requestCharge = true      │
│  • Calibration.phase = .Discharging      │
│  • TopUp.active = true                   │
│  • ManualDischarge.active = true         │
└──────────────────┬──────────────────────┘
                   │ 每次轮询收集所有请求
                   ▼
┌─────────────────────────────────────────┐
│  决策层（按优先级裁决，输出一个动作）      │
│  evaluateChargingState() {               │
│    if (overtemp)    → INHIBIT            │
│    if (calibrating) → phase决定          │
│    if (topUp)       → ALLOW              │
│    if (sailing)     → band逻辑           │
│    if (limit)       → threshold逻辑      │
│    → 输出: CHARGE / INHIBIT / HOLD       │
│  }                                       │
└──────────────────┬──────────────────────┘
                   │ 单一写入点
                   ▼
┌─────────────────────────────────────────┐
│  执行层（唯一写 SMC 的地方）              │
│  applyChargingDecision(action) {         │
│    if (action == CHARGE && chargeInhibited) │
│      → enableCharging() + allowSleep()   │
│    if (action == INHIBIT && !chargeInhibited) │
│      → disableCharging() + preventSleep() │
│    // HOLD → 不写                        │
│  }                                       │
└─────────────────────────────────────────┘
```

**当前 BootCampCharge 的根本问题**：各功能方法（`StartDischarge`/`StartTopUp`/`SetChargeLimit`/`EvaluateSailMode`/`EvaluateOvertempProtection`/`StepCalibration`）**各自直接写 SMC**，没有集中的决策层。这导致：
- BCLM 和 CH0B 互相覆盖
- 功能之间没有真正的互斥
- 无法正确处理优先级

---

## 3. 各功能精确实现逻辑

### 3.1 充电限制（Charge Limit）

#### CH0B 模式（开源版默认）

```
目标值 = SMCPresenter.shared.value (用户设定的百分比, 20-100)
每 5 秒：
  读当前电量 (IOPS API)
  if (电量 < 目标值):
    if (chargeInhibited):  CH0B = 0, chargeInhibited = false
    disableSleep()          // 充电时不允许睡眠
  else:
    if (!chargeInhibited): CH0B = 2, chargeInhibited = true
    enableSleep()           // 到目标后允许睡眠
```

#### BCLM 模式（Classic / Intel）

```
用户拖动 Slider → setValue(value)
  → writeValue()
  → SMCWriteByte("BCLM", value)   // 一次性写入，固件接管
  // 轮询循环不做任何事
```

### 3.2 手动放电（Manual Discharge）

**开源版不包含放电功能**（免费版没有 Discharge 按钮）。从 Pro 版反编译和官方文档推断：

```
启动放电：
  1. CH0B = 2（禁止充电，电池在 AC 上放电）
  2. 创建 IOPMAssertion 防止系统睡眠
     - 变量名: _dischargeAssertionID
  3. dischargeQueryInProgress = true（异步查询进行中）

放电中：
  - 持续 CH0B = 2
  - 持续阻止睡眠
  - MagSafe LED 可能闪烁橙绿色（正常现象，官方文档确认）

停止放电：
  - CH0B = 0（恢复充电）
  - 释放 IOPMAssertion（允许睡眠）
  - dischargeQueryInProgress = false
```

**防睡眠机制**：AlDente 用 macOS 的 `IOPMAssertionCreateWithName(kIOPMAssertionTypePreventSystemSleep)`。Windows 对应 `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)`。

### 3.3 临时充满（Top Up）

从官方文档和反编译变量名推断：

```
启动 Top Up：
  1. 保存当前 Charge Limit（变量: _chargeVal → 恢复用）
  2. _topUpActive = true
  3. 设置目标为 100%（等同于临时把 limit 改成 100）
     - CH0B 模式: SMCPresenter.value = 100，轮询自然充电到 100%
     - BCLM 模式: BCLM = 0（移除限制），充电到 100%
  4. 临时禁用 Sailing Mode

充电到 100% 后：
  - MagSafe LED 变绿色（官方文档）

停止 Top Up（用户手动或自动）：
  1. _topUpActive = false
  2. 恢复原 Charge Limit
  3. 重新启用 Sailing Mode（如果之前开着）
```

### 3.4 校准模式（Calibration）— 5 阶段

从官方文档和反编译变量名（`_calibrationState`，143 次引用）推断：

```
阶段 1 — CHARGE（充电至 100%）:
  入口: 用户启动校准
  操作: 保存当前 limit, 清除 limit (BCLM=0 或 CH0B=0), 允许充电
  退出条件: 电量 >= 100%
  进入阶段 2

阶段 2 — DISCHARGE（放电至低电）:
  入口: 电量达到 100%
  操作: CH0B = 2（禁止充电）, 阻止睡眠, 禁用过热保护, 禁用航行模式
  退出条件: 电量 <= 10%（CalibrationFloor）
  注意: CalibrationSlowDischargeNotification — 可能有慢速放电通知
  进入阶段 3

阶段 3 — RECHARGE（充电回 100%）:
  入口: 电量降至 10%
  操作: CH0B = 0（恢复充电）
  退出条件: 电量 >= 100%
  进入阶段 4

阶段 4 — REST（静置 1 小时）:
  入口: 电量回到 100%
  操作: 保持 100%，电芯平衡（cell balancing）
  持续: 1 小时（CalibrationRestPeriod）
  注意: CalibrationClamshellDisplayOnNotification — 合盖时显示通知
  退出条件: 时间到
  进入阶段 5

阶段 5 — RESTORE（恢复限制）:
  入口: 1 小时静置结束
  操作: 恢复原 Charge Limit, 允许睡眠, 重新启用过热保护/航行模式
  _calibrationState = .Idle
  完成

期间禁用的功能:
  - 过热保护（Heat Protection）
  - 航行模式（Sailing Mode）
  - 系统睡眠（System Sleep）

推荐频率:
  - 不常用电池: 每 4-6 周
  - 长期插电: 每 3 周
```

### 3.5 过热保护（Heat Protection）— 5 分钟迟滞状态机

从官方文档和反编译变量名（`_heatProtectMode`, `maxTemp`）推断：

```
温度传感器: TC0P (CPU_0_PROXIMITY)
推荐阈值: 35°C（用户可配，AlDente 默认值）

状态机:
  ┌─────────┐  temp > threshold  ┌─────────┐
  │ Normal  │ ─────────────────→ │ Cooling │
  │         │                    │ CH0B=2  │
  │ CH0B=0  │ ←───────────────── │ 5min计时 │
  └─────────┘  temp OK + 5min    └────┬────┘
       ↑                              │ 5min到, temp仍超
       │ temp OK + 5min               ↓
       │                        ┌─────────┐
       │                        │ Cooling │ (重启5min)
       │                        └─────────┘
       │
       │  ┌──────────────────┐
       └──│ ChargingRecovery │ 5min到, temp降了
          │ CH0B=0           │
          │ 至少充5分钟       │
          └──────────────────┘
               │ 5min到
               ├── temp仍超 → Cooling
               └── temp正常 → Normal
```

**迟滞设计的关键**：
- 温度超标后，至少停充 5 分钟（不是超了就停、降了就充）
- 温度恢复后，至少充 5 分钟（避免在阈值附近频繁切换）
- 这避免了"温度 35.1°C 停充 → 降到 34.9°C 充 → 又升到 35.1°C 停"的震荡

### 3.6 航行模式（Sailing Mode）— 避免微充电

从官方文档确认的精确逻辑：

```
变量:
  上限 = Charge Limit（用户设定的充电限制，如 80%）
  区间 = SailingLevel（用户设定的区间大小，5-10%）
  下限 = 上限 - 区间（如 80-10=70%）

行为（Schmitt Trigger / 施密特触发器）:
  电量 >= 上限 → CH0B = 2（停止充电）
  电量在区间内 → HOLD（不操作，这是"航行"状态）
  电量 <= 下限 → CH0B = 0（充电至上限）

临时禁用条件:
  - Top Up 期间
  - Calibration 期间
```

**官方文档原文**（features 页面）：
> "if you unplug your MacBook only for a short time and use only a couple of percent, AlDente will charge your MacBook again. To avoid unnecessary 'micro-charging', we have included the Sailing Mode feature. This allows you to set a lower limit, from which the battery will be charged up to the charge limit again."

**示例**（官方文档 Example 1）：
> 当前 80%，限制 80%，Sailing 5%。不会重新充电，直到降到 75%。

**示例**（官方文档 Example 2）：
> 当前 77%，限制 80%，Sailing 10% → 区间 70-80%。77% 在区间内 → 停止充电（不充到 80%）。

---

## 4. 关键架构原则

### 4.1 功能方法不直接写 SMC

**AlDente 开源版证实**：
- 充电限制在 CH0B 模式下，轮询循环是唯一写 CH0B 的地方
- 功能层（用户拖 Slider）只修改 `SMCPresenter.value`（目标值），不直接写 SMC
- 写入由轮询循环根据当前值 vs 目标值决定

**Pro 版推断**：
- 各功能（过热/校准/航行/TopUp）设请求标志（`_heatProtectMode`, `_topUpActive` 等）
- 统一的决策方法在轮询中收集所有标志，按优先级裁决
- 只有决策结果变化时才写 SMC（通过 `chargeInhibited` 避免重复写）

### 4.2 chargeInhibited 全局标志

```swift
// Helper.swift L29
public var chargeInhibited: Bool = false

// 两处使用模式：
// 1. 轮询中避免重复写（L84, L92）
// 2. checkCharging() 从硬件同步（L110）
```

作用：
- **状态缓存**：记住"当前 CH0B 是 0 还是 2"，不必每次读 SMC
- **去重写入**：只在状态实际变化时写 SMC
- **一致性保证**：程序重启后通过 `checkCharging()` 从硬件同步真实状态

### 4.3 Helper 守护进程（特权分离）

AlDente 的 macOS 架构有特权分离：

```
┌────────────────────────────────────────┐
│ AlDente.app（GUI 进程，用户权限）       │
│ ├── AppDelegate.swift — 菜单栏 + Timer │
│ ├── ContentView.swift — SwiftUI 弹窗   │
│ ├── Helper.swift — SMC 操作代理层       │
│ │   （通过 NSXPC 调用 HelperTool）      │
│ └── PersistanceManager — UserDefaults  │
└───────────────┬────────────────────────┘
                │ NSXPCConnection (privileged)
                ▼
┌────────────────────────────────────────┐
│ com.davidwernhart.Helper（root 守护进程）│
│ ├── main.swift — NSXPCListener         │
│ │   + 10s 检查 AlDente 是否运行         │
│ │   + 退出时 reset() 恢复 SMC 键        │
│ ├── HelperTool.swift — XPC 实现         │
│ │   • setSMCByte/readSMCByte            │
│ │   • readSMCUInt32                     │
│ │   • createAssertion/releaseAssertion  │
│ │   • setResetVal                       │
│ │   • reset() — 恢复所有修改的键        │
│ │   • modifiedKeys 字典记录原始值        │
│ └── SMC.swift — IOKit AppleSMC 通信     │
└────────────────────────────────────────┘
```

**Helper 守护进程的三个作用：**

1. **特权提升**：SMC 写入需要 root 权限。GUI 运行在用户权限，通过 SMJobBless 安装 root 守护进程，NSXPC 通信传递写入指令。

2. **自愈和清理**：
```swift
// main.swift L26-43 — 每 10 秒检查 AlDente 是否运行
Timer.scheduledTimer(withTimeInterval: 10, repeats: true) { timer in
    // 检查 com.davidwernhart.AlDente 是否在运行
    if (!foundApp && !hasChecked) {
        hasChecked = true
        HelperTool.instance.reset()  // 恢复所有修改的 SMC 键
        exit(0)                      // 守护进程退出
    }
}
```
如果 GUI 崩溃/被杀，守护进程会在 10 秒内检测到，恢复所有 SMC 键到原始值，然后退出。

3. **修改追踪和恢复**：
```swift
// HelperTool.swift L16, L42-50, L115-125
var modifiedKeys: [String: UInt8] = [:]  // 记录每个键的原始值

func setSMCByte(key: String, value: UInt8) {
    if (self.modifiedKeys[key] == nil) {
        // 第一次修改此键：先读原始值保存
        readSMCByte(key: key) { (originalValue) in
            self.modifiedKeys[key] = originalValue
            _ = try? SMCKit.writeData(smcKey, data: bytes)
        }
    }
    // 后续修改直接写
}

func reset() {
    for (key, value) in modifiedKeys {
        setSMCByte(key: key, value: value)  // 恢复原始值
    }
    for assertionID in openAssertions {
        releaseAssertion(assertionID: assertionID)  // 释放所有睡眠阻止
    }
}
```

**Boot Camp 不需要 Helper 守护进程**——Windows 用内核驱动（InpOut）获得 I/O 端口访问权限，不需要特权分离。但应实现等价的"修改追踪和退出恢复"逻辑。

### 4.4 退出时恢复什么状态

```swift
// AppDelegate.swift L37-40
func applicationWillTerminate(_ aNotification: Notification) {
    Helper.instance.enableSleep()      // 释放睡眠阻止
    Helper.instance.enableCharging()   // CH0B = 0（恢复充电）
}
```

**注意**：开源版退出时恢复 CH0B=0（允许充电），但**不恢复 BCLM**。BCLM 是持久的，退出后仍然生效（这是 Intel 模式的特性——关掉 App 后充电限制仍然有效）。

Helper 守护进程的 `reset()` 更彻底——恢复**所有修改过的 SMC 键**到原始值。

---

## 5. Windows Boot Camp 特殊性

### 5.1 AlDente 开发者亲口确认（Discussion #286）

**MatthiasKerbl（AlDente 维护者）2021-05-11 回复：**

> "We currently use two technical solutions to inhibit charging. The 'normal' one **does not work in Bootcamp**. However, it works much more accurately and even works on M1 Macs. The other one works in Bootcamp but the final charge value can vary by up to 5%. To activate the second method you have to enable 'Use Classic SMC Key (Intel)'."

**翻译**：
- "正常"方式（CH0B）**在 Boot Camp 下不工作**
- Classic 方式（BCLM）在 Boot Camp 下工作，但精度 ±5%
- 要在 Boot Camp 用，必须启用 "Use Classic SMC Key (Intel)"

### 5.2 为什么 CH0B 在 Boot Camp 下不工作

根因分析：

1. **BCLM 是硬件级持久设置**：一旦在 macOS 下设置了 BCLM=77，这个值写入 SMC 固件 NV RAM，跨重启/关机/Boot Camp 切换持久存在
2. **CH0B 是软件级临时开关**：CH0B 的值在重启后会被固件清除
3. **在 Boot Camp 下**：
   - 没有 macOS 进程运行 AlDente 的 CH0B 轮询
   - 如果之前在 macOS 下用 CH0B 模式（默认），切到 Windows 后没有任何东西持续写 CH0B
   - CH0B 恢复默认值（0 = 允许充电），电池充到 100%
4. **BCLM 模式下**：
   - BCLM=77 已经持久写入固件
   - 切到 Windows 后，固件仍然执行 BCLM 限制
   - 充电到 77% 停止（Windows 下精确停止，无 +3% buffer）

### 5.3 CH0B 在 Boot Camp 下写入是否生效

**关键新发现（2026-08-09 官方文档交叉验证）：**

AlDente 官方"睡眠/关机行为表"最后一行明确标注了 Bootcamp 列：

| 充电状态 + 操作 | Intel AlDente Free | Intel AlDente Pro | Bootcamp |
|---|---|---|---|
| 充电暂停 → 睡眠 | 保持暂停 | 保持暂停 | **充到 100%** |
| 充电暂停 → 关机 | 充到 100% | 保持暂停 | — |
| 充电中 → 睡眠 | 充到 100% | 充到限制值 | — |
| 放电中 → 睡眠 | 保持暂停 | 保持暂停 | — |
| 放电中 → 关机 | 充到 100% | 保持暂停 | — |

**解读**：
1. **Boot Camp 列只有 Free 版行为（充到 100%）**——因为 Free 版默认用 CH0B 模式，CH0B 在 Boot Camp 下不工作
2. **Pro 版的 Intel Mode（BCLM）在 Boot Camp 下工作**——官方 Example 4 明确说"充电仍然暂停在 80%"
3. **CH0B 在 Boot Camp 下写入是否生效，仍未有直接验证**——但官方表格暗示 CH0B 完全不工作（Free 版 Boot Camp 充到 100%）

**结论：CH0B 在 Boot Camp 下已验证可用（当 BCLM=0 时）！**

2026-08-09 实测结果（Ch0BTest.exe）：
- BCLM=0 + CH0B=2 → Windows 报"未充电"，电量不增 ✅
- CH0B=2 写入后读回 = 2（写入持久）✅
- CH0B=0 → 恢复充电 ✅

这意味着：
- **放电功能**：✅ 可用（BCLM=0 → CH0B=2）
- **过热保护**：✅ 可用（BCLM=0 → CH0B=2）
- **航行模式**：✅ 可用（BCLM=0 → CH0B 区间控制）
- **校准模式**：✅ 可用（BCLM=0 → CH0B 控制放电阶段）
- **充电限制**：✅ BCLM 模式
- **临时充满**：✅ BCLM=0 充到 100%

**关键约束：用 CH0B 前必须先 BCLM=0，用完后恢复 BCLM=用户设定值。**
BCLM 非零时 CH0B 被固件忽略——这是之前放电不工作的根因。

**验证建议**：
- 在 BCLM=0 的前提下测试 CH0B=2 是否能阻止充电
- 如果能，则"先清 BCLM=0 再写 CH0B=2"的策略可行
- 如果不能（即使 BCLM=0 CH0B 也不工作），则 Boot Camp 下只能用 BCLM，CH0B 相关功能（放电/航行/过热保护）全部不可用

### 5.4 BCLM 和 CH0B 冲突时 AlDente 源码的处理

**AlDente 开源版从不同时使用两者**——`oldKey` 标志二选一，代码路径完全分离。切换模式时：

```swift
// 切到 BCLM 模式时：
Helper.instance.enableCharging()  // 先 CH0B=0（清除 CH0B 干扰）
Helper.instance.enableSleep()
// 然后写 BCLM

// 切到 CH0B 模式时：
presenter.setValue(value: 100)  // 先把 BCLM 目标设为 100
// 但注意：这里没有显式清除 BCLM=0！
// 可能是依赖用户手动设 100%，或者 Pro 版有额外清理逻辑
```

**这是一个潜在的源码缺陷**——切回 CH0B 模式时没有显式清除 BCLM。如果 BCLM 还留着之前的值（如 77），CH0B 模式可能不生效。

### 5.5 Boot Camp 特殊处理建议

对于 BootCampCharge（Windows 原生，不依赖 macOS），推荐策略：

```
方案 A（推荐，最大兼容性）：BCLM-only 模式
  ─────────────────────────────────────
  • 充电限制: BCLM = 目标值（唯一控制方式）
  • 手动放电: BCLM = 0 + CH0B = 2（先清 BCLM 再用 CH0B）
    ⚠️ 需验证 CH0B 在 BCLM=0 时是否在 Boot Camp 下生效
  • Top Up: BCLM = 0（直接移除限制，充到 100%）
  • 过热保护: CH0B = 2（需要 CH0B 可用）
  • 航行模式: CH0B 区间逻辑（需要 CH0B 可用）
  • 校准: CH0B 控制放电阶段（需要 CH0B 可用）
  
  如果 CH0B 在 Boot Camp 下完全不工作：
  • 放电: 不可用（只能拔充电器物理放电）
  • 过热保护: 只能 BCLM 降低上限（间接）
  • 航行模式: 不可用
  • 校准: 不可用

方案 B（如果 CH0B 在 BCLM=0 时可用）：混合模式
  ─────────────────────────────────────
  • 默认状态: BCLM = 限制值（持久保护）
  • 需要放电类操作时: BCLM = 0 → CH0B = 2（临时切换）
  • 操作结束后: CH0B = 0 → BCLM = 限制值（恢复）
  • 关键: BCLM 和 CH0B 绝不同时为非零
```

---

## 6. SMC 控制流程图

### 6.1 完整控制流程（Boot Camp 适配版）

```
                    ┌─────────────────┐
                    │   5 秒轮询定时器  │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  读取传感器数据   │
                    │  • 电量 (Win API) │
                    │  • 温度 (TC0P)    │
                    │  • AC 状态        │
                    │  • CH0B 当前值    │
                    │  • BCLM 当前值    │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  收集功能请求     │
                    │  (各功能设标志)   │
                    └────────┬────────┘
                             │
              ┌──────────────▼──────────────┐
              │   evaluateChargingState()    │
              │   按优先级决策               │
              │                              │
              │  ① 过热保护？                │
              │     ├─ Cooling → INHIBIT     │
              │     ├─ Recovery → CHARGE     │
              │     └─ Normal → 继续↓        │
              │                              │
              │  ② 校准中？                  │
              │     ├─ Discharge → INHIBIT   │
              │     ├─ Charge → CHARGE       │
              │     ├─ Rest → CHARGE         │
              │     └─ 否 → 继续↓            │
              │                              │
              │  ③ TopUp？                   │
              │     ├─ <100% → CHARGE        │
              │     ├─ =100% → HOLD          │
              │     └─ 否 → 继续↓            │
              │                              │
              │  ④ 手动放电？                │
              │     ├─ 是 → INHIBIT          │
              │     └─ 否 → 继续↓            │
              │                              │
              │  ⑤ 航行模式？                │
              │     ├─ >=上限 → INHIBIT      │
              │     ├─ <=下限 → CHARGE       │
              │     ├─ 区间内 → HOLD         │
              │     └─ 否 → 继续↓            │
              │                              │
              │  ⑥ 充电限制？                │
              │     ├─ BCLM模式 → 不在此处理 │
              │     │  (BCLM由固件管理)      │
              │     ├─ CH0B模式:             │
              │     │  ├─ <目标 → CHARGE     │
              │     │  └─ >=目标 → INHIBIT   │
              │     └─ → 输出动作            │
              └──────────────┬───────────────┘
                             │
              ┌──────────────▼──────────────┐
              │   applyChargingDecision()    │
              │   唯一 SMC 写入点            │
              │                              │
              │  decision == CHARGE:         │
              │    if (chargeInhibited):     │
              │      → CH0B = 0              │
              │      → chargeInhibited=false │
              │      → allowSleep()          │
              │                              │
              │  decision == INHIBIT:        │
              │    if (!chargeInhibited):    │
              │      → 需要先 BCLM=0?        │
              │      → CH0B = 2              │
              │      → chargeInhibited=true  │
              │      → preventSleep()        │
              │                              │
              │  decision == HOLD:           │
              │    → 不写 SMC                │
              │                              │
              │  BCLM 管理（独立路径）:       │
              │    只在以下时机写 BCLM:      │
              │    • 用户改限制值            │
              │    • 进入/退出需要CH0B的模式  │
              │    • 退出程序时恢复          │
              └───────────────────────────────┘
```

### 6.2 过热保护状态转换图

```
                    temp > threshold
         ┌──────────────────────────────────┐
         │                                  ▼
    ┌─────────┐                        ┌─────────┐
    │ Normal  │                        │ Cooling │
    │ CH0B=0  │                        │ CH0B=2  │
    │ 充电中   │                        │ 停充中   │
    └─────────┘                        └────┬────┘
         ▲                                  │
         │                    ┌─────────────┘
         │                    │ 5min 到
         │                    ▼
         │              temp 仍超阈值?
         │              ├── 是 → 重启 Cooling 5min
         │              └── 否 ↓
         │                    ┌──────────────────┐
         │      5min 到       │ ChargingRecovery │
         └────────────────────│ CH0B=0           │
           temp正常           │ 至少充5分钟       │
                              └──────────────────┘
                                      │ 5min 到
                                      ├── temp仍超 → Cooling
                                      └── temp正常 → Normal
```

### 6.3 校准模式状态转换图

```
用户启动
    │
    ▼
┌──────────┐  电量>=100%  ┌───────────┐  电量<=10%  ┌───────────┐
│ Charging │ ──────────→ │Discharging│ ─────────→ │Recharging │
│ CH0B=0   │             │ CH0B=2    │            │ CH0B=0    │
│ 充至100% │             │ 放至10%   │            │ 充回100%  │
│ 防睡眠   │             │ 防睡眠    │            │           │
└──────────┘             │ 禁热保护  │            └─────┬─────┘
                          │ 禁航行   │                  │
                          └───────────┘            电量>=100%
                                                         │
                                                         ▼
┌──────────┐  1小时到   ┌────────┐
│  Done    │ ←──────── │ Resting│
│ 恢复limit │           │ 保持   │
│ 恢复睡眠  │           │ 100%   │
│ 恢复保护  │           │ 1小时  │
└──────────┘            └────────┘
```

### 6.4 航行模式状态转换图

```
              CH0B=0 (充电中)
              电量上升 ↑
                    │
                    │ 电量 >= 上限(80%)
                    ▼
              ┌───────────┐
              │  充电停止  │ CH0B=2
              │  电量缓慢  │
              │  自然下降  │
              └─────┬─────┘
                    │
        ┌───────────┼───────────┐
        │           │           │
   电量在区间内   电量<=下限    拔掉充电器
   (75-80%)     (70%)       
        │           │           │
        ▼           ▼           ▼
     HOLD       CH0B=0      (不操作)
   (不操作)     重新充电
              电量上升 ↑
```

### 6.5 优先级决策伪代码

```python
def evaluate_charging_state():
    """每次轮询调用，返回唯一的充电决策"""
    
    # ─── 读取当前状态 ───
    capacity = read_battery_percent()    # Windows API
    temp = read_temperature("TC0P")      # SMC
    ac_connected = read_ac_status()      # Windows API
    current_ch0b = read_smc("CH0B")
    
    # 如果没接充电器，不需要控制充电
    if not ac_connected:
        return Decision.HOLD  # 电池模式，不操作
    
    # ─── ① 过热保护（最高优先级）───
    if heat_protection_enabled:
        overtemp_decision = evaluate_overtemp(temp)
        if overtemp_decision == Decision.INHIBIT:
            return Decision.INHIBIT  # 温度安全第一
        if overtemp_decision == Decision.CHARGE:
            return Decision.CHARGE   # 恢复充电窗口内
    
    # ─── ② 校准模式 ───
    if calibration_phase != CalibrationPhase.Idle:
        if calibration_phase == CalibrationPhase.Discharging:
            return Decision.INHIBIT
        elif calibration_phase in (CalibrationPhase.Charging, 
                                    CalibrationPhase.Recharging):
            return Decision.CHARGE
        elif calibration_phase == CalibrationPhase.Resting:
            return Decision.CHARGE  # 保持满电
        # Done → 落到下面的正常逻辑
    
    # ─── ③ Top Up ───
    if top_up_active:
        if capacity < 100:
            return Decision.CHARGE
        else:
            return Decision.HOLD  # 已满，保持
    
    # ─── ④ 手动放电 ───
    if manual_discharge_active:
        if capacity > discharge_target:
            return Decision.INHIBIT
        else:
            return Decision.HOLD  # 达到目标，停放电
    
    # ─── ⑤ 航行模式 ───
    if sailing_mode_enabled and not sailing_temporarily_disabled:
        sailing_floor = charge_limit - sailing_interval
        if capacity >= charge_limit:
            return Decision.INHIBIT
        elif capacity <= sailing_floor:
            return Decision.CHARGE
        else:
            return Decision.HOLD  # 区间内，不操作
    
    # ─── ⑥ 基础充电限制 ───
    # BCLM 模式: 不在此处理，BCLM 由固件管理
    # CH0B 模式: 按阈值判断
    if charge_mode == ChargeMode.CH0B:
        if capacity < charge_limit:
            return Decision.CHARGE
        else:
            return Decision.INHIBIT
    
    # BCLM 模式: 固件在管，软件层不需要干预
    return Decision.HOLD


def apply_charging_decision(decision):
    """唯一的 SMC 写入点"""
    
    if decision == Decision.CHARGE:
        if charge_inhibited:  # 只在状态变化时写
            # Boot Camp 特殊处理：确保 BCLM 不干扰
            # (如果之前为放电清了 BCLM，这里不需要恢复——
            #  BCLM 恢复由功能层在退出时处理)
            write_smc("CH0B", 0x00)
            charge_inhibited = False
            allow_sleep()
    
    elif decision == Decision.INHIBIT:
        if not charge_inhibited:  # 只在状态变化时写
            write_smc("CH0B", 0x02)
            charge_inhibited = True
            prevent_sleep()
    
    # Decision.HOLD → 不写 SMC
```

---

## 7. BootCampCharge 重写方案

### 7.0 反编译变量名 → C# 实现映射

从 REVERSE_ENGINEERING.md 提取的 Pro 功能变量名，每个都有对应的 C# 实现：

| AlDente Pro 变量名 | 出现次数 | C# 对应 | 实现位置 |
|---|---|---|---|
| `_sailingMode` | 21 | `_sailingEnabled` (bool) | BatteryManager 请求标志 |
| `_sailingLevel` | — | `_sailingFloor` (int) | BatteryManager 请求标志 |
| `_lastSailingCheckpoint` | — | 航行模式内部状态 | EvaluateChargingState ⑤ |
| `_heatProtectMode` | 8 | `_heatProtectionEnabled` (bool) | BatteryManager 请求标志 |
| `maxTemp` | — | `_heatThreshold` (double) | BatteryManager 请求标志 |
| `_calibrationState` | 143 | `_calPhase` (CalibrationPhase enum) | BatteryManager 校准状态机 |
| `CalibrationSlowDischargeNotification` | — | 校准放电阶段通知 | UI 层显示进度文字 |
| `CalibrationClamshellDisplayOnNotification` | — | 合盖时校准通知 | UI 层（可忽略，Boot Camp 无合盖） |
| `_topUpActive` / `topUpMode` | 16 | `_topUpRequested` (bool) | BatteryManager 请求标志 |
| `txtTopUp` | — | TopUp 按钮文本 | UI 层按钮 Content |
| `_dischargeAssertionID` | 45 | `PreventSleep()` 内部 | BatteryManager SetThreadExecutionState |
| `dischargeQueryInProgress` | — | `_dischargeRequested` (bool) | BatteryManager 请求标志 |
| `dischargeWasPostponed` | — | 不需要（同步实现） | — |
| `_currentChargeVal` | — | `ReadBclm()` 实时读取 | BatteryManager 读取层 |
| `_chargeVal` | — | `_chargeLimit` (int) | BatteryManager 请求标志 |
| `HardwareBatteryPercentage` | — | `GetChargePercent()` | BatteryManager 读取层 |
| `preventSleepID` / `preventDisplaySleepID` | — | `_sleepPrevented` (bool) | BatteryManager 防睡眠 |
| `disableSleepUntilLimit` | — | 充电限制模式下不需要防睡眠（BCLM 硬件管） | — |
| `stopChargingWhenSleeping` | — | Boot Camp 下不适用（BCLM 持久） | — |
| `setEnergyModeOnAdapterWithMode:` | — | 不需要（Boot Camp 无适配器能量模式 API） | — |
| `PowerFlow` | 73 | 功率流向图（未实现，UI 可视化功能） | 未来扩展 |
| `handleSetMagsafeLED:completion:` | 73 | ❌ 不可复刻（BFCL 在 MBP 2017 不存在） | — |
| `verifyActivationWithCompletion:` | 21 | 不需要（无 Paddle 许可证验证） | — |

### 7.0.1 Boot Camp 下 CH0B 可用性验证（必须做的实验）

**⚠️ 这是整个项目的关键验证点。以下功能能否实现完全取决于此实验结果：**

| 功能 | CH0B 可用 → 能实现？ | CH0B 不可用 → 能实现？ |
|---|---|---|
| 充电限制 | ✅ BCLM | ✅ BCLM（不依赖 CH0B） |
| 临时充满 | ✅ BCLM=0 | ✅ BCLM=0（不依赖 CH0B） |
| 手动放电 | ✅ CH0B=2 | ❌ 不可用（只能拔充电器） |
| 过热保护 | ✅ CH0B=2 | ⚠️ 只能降 BCLM 间接保护 |
| 航行模式 | ✅ CH0B 区间 | ❌ 不可用 |
| 校准模式 | ✅ CH0B 控制放电 | ❌ 不可用（放电阶段无法执行） |

**验证步骤（必须在重写前执行）**：

```
实验 1：CH0B=2 在 BCLM=0 时是否阻止充电？
  1. BCLM = 0（清除限制）
  2. CH0B = 2（禁止充电）
  3. 观察：电量是否下降？（如果下降 = CH0B 在 Boot Camp 下可用）
  4. CH0B = 0（恢复充电）
  5. 观察：电量是否上升？

实验 2：CH0B=2 在 BCLM=83 时是否被忽略？
  1. BCLM = 83
  2. CH0B = 2
  3. 观察：电量是否继续充到 83%？（如果是 = CH0B 被 BCLM 覆盖）

结论：
  - 如果实验 1 成功：混合模式（方案 B）可行，放电/航行/校准都能用
  - 如果实验 1 失败：CH0B 在 Boot Camp 下完全不工作，只能用 BCLM
    → 放电/航行/校准不可用，只能靠拔充电器物理放电
```

**这个实验需要用户在 Windows 上运行，因为 WSL 无法直接写 SMC。**

### 7.1 架构分层（目标）

```
┌─────────────────────────────────────────────────────┐
│  UI 层 (MainWindow.xaml.cs + App.xaml.cs)            │
│  • 用户操作 → 调用 BatteryManager 的请求方法          │
│  • 轮询结果 → 更新 UI                                │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│  BatteryManager（重写后的核心）                       │
│  ┌─────────────────────────────────────────────────┐ │
│  │  功能层（设请求标志，不写 SMC）                    │ │
│  │  • SetChargeLimit(val) → _chargeLimit = val     │ │
│  │  • StartDischarge() → _dischargeReq = true      │ │
│  │  • StartTopUp() → _topUpReq = true              │ │
│  │  • StartCalibration() → _calibrationReq = true  │ │
│  │  • EnableHeatProtection(t) → _heatProtectReq=t  │ │
│  │  • EnableSailingMode(lo,hi) → _sailingReq=...   │ │
│  └─────────────────┬───────────────────────────────┘ │
│                    │                                 │
│  ┌─────────────────▼───────────────────────────────┐ │
│  │  决策层（轮询中调用，按优先级裁决）                 │ │
│  │  EvaluateChargingState() → ChargingDecision      │ │
│  │  (过热 > 校准 > TopUp > 放电 > 航行 > 限制)      │ │
│  └─────────────────┬───────────────────────────────┘ │
│                    │                                 │
│  ┌─────────────────▼───────────────────────────────┐ │
│  │  执行层（唯一写 SMC 的地方）                      │ │
│  │  ApplyDecision(ChargingDecision)                │ │
│  │  • CHARGE → CH0B=0 (if chargeInhibited)         │ │
│  │  • INHIBIT → CH0B=2 (if !chargeInhibited)       │ │
│  │  • HOLD → 不写                                   │ │
│  │  + BCLM 管理（独立，只在特定时机写）              │ │
│  │  + Sleep 管理（独立）                            │ │
│  └─────────────────────────────────────────────────┘ │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│  SMC 层 (Smc.cs + PortTransport.cs，不变)             │
│  • 原始 SMC 端口 I/O                                 │
└─────────────────────────────────────────────────────┘
```

### 7.2 关键设计决策

#### BCLM vs CH0B 在 Boot Camp 下的策略

```
推荐策略：BCLM 为主，CH0B 为辅，严格互斥

1. 充电限制：用 BCLM（持久、可靠、Boot Camp 唯一确认可用的方式）
   - BCLM = 目标值（如 77）
   - 不需要轮询，固件自动执行

2. 需要 CH0B 控制的功能（放电/航行/过热/校准）：
   - 进入前：保存当前 BCLM 值 → BCLM = 0（清除硬件限制）
   - 操作中：用 CH0B 控制（0=充, 2=禁）
   - 退出后：CH0B = 0 → 恢复 BCLM = 原值

3. chargeInhibited 标志：
   - 记录 CH0B 当前状态
   - 只在状态变化时写 SMC（避免重复写）

4. 退出时恢复：
   - CH0B = 0（恢复充电）
   - BCLM 保持用户设定值（持久保护）
   - 释放睡眠阻止
```

#### C# 实现骨架

```csharp
public enum ChargingDecision { Charge, Inhibit, Hold }

public sealed class BatteryManager
{
    // ─── 请求标志（功能层设置）───
    private int _chargeLimit = 100;           // 充电限制 (BCLM)
    private bool _dischargeRequested;         // 手动放电
    private int _dischargeTarget;             // 放电目标
    private bool _topUpRequested;             // 临时充满
    private bool _heatProtectionEnabled;      // 过热保护
    private double _heatThreshold;            // 过热阈值
    private bool _sailingEnabled;             // 航行模式
    private int _sailingInterval;             // 航行区间
    private CalibrationPhase _calPhase;       // 校准阶段
    
    // ─── 运行时状态 ───
    private bool _chargeInhibited;            // CH0B 当前状态缓存
    private int _savedBclm;                   // CH0B 操作前保存的 BCLM
    private OvertempState _overtempState;     // 过热状态机
    private DateTime _overtempSince;
    
    // ─── 决策层（轮询中调用）───
    public ChargingDecision EvaluateChargingState()
    {
        int capacity = GetChargePercent() ?? 100;
        double? temp = GetCpuTemperature();
        bool ac = IsAcConnected();
        
        if (!ac) return ChargingDecision.Hold;
        
        // ① 过热保护
        if (_heatProtectionEnabled && temp.HasValue)
        {
            var od = EvaluateOvertemp(temp.Value);
            if (od.HasValue) return od.Value;
        }
        
        // ② 校准模式
        if (_calPhase != CalibrationPhase.Idle)
            return EvaluateCalibration(capacity);
        
        // ③ Top Up
        if (_topUpRequested)
            return capacity < 100 ? ChargingDecision.Charge : ChargingDecision.Hold;
        
        // ④ 手动放电
        if (_dischargeRequested)
            return capacity > _dischargeTarget ? ChargingDecision.Inhibit : ChargingDecision.Hold;
        
        // ⑤ 航行模式
        if (_sailingEnabled)
        {
            int floor = _chargeLimit - _sailingInterval;
            if (capacity >= _chargeLimit) return ChargingDecision.Inhibit;
            if (capacity <= floor) return ChargingDecision.Charge;
            return ChargingDecision.Hold;
        }
        
        // ⑥ BCLM 模式：不需要 CH0B 干预
        // （如果在 BCLM 模式下运行，此处返回 Hold，
        //   BCLM 由固件管理）
        return ChargingDecision.Hold;
    }
    
    // ─── 执行层（唯一写 SMC）───
    public void ApplyDecision(ChargingDecision decision)
    {
        switch (decision)
        {
            case ChargingDecision.Charge:
                if (_chargeInhibited)
                {
                    WriteCh0B(0);
                    _chargeInhibited = false;
                    AllowSleep();
                }
                break;
            
            case ChargingDecision.Inhibit:
                if (!_chargeInhibited)
                {
                    // Boot Camp: 确保 BCLM 不干扰
                    EnsureBclmCleared();
                    WriteCh0B(2);
                    _chargeInhibited = true;
                    PreventSleep();
                }
                break;
            
            // Hold: 不写
        }
    }
    
    private void EnsureBclmCleared()
    {
        // 进入 CH0B 控制模式前，清除 BCLM
        if (!_bclmCleared)
        {
            _savedBclm = ReadBclm();
            WriteBclm(0);
            _bclmCleared = true;
        }
    }
    
    private void RestoreBclm()
    {
        // 退出 CH0B 控制模式后，恢复 BCLM
        if (_bclmCleared)
        {
            WriteCh0B(0);
            _chargeInhibited = false;
            WriteBclm(_savedBclm > 0 ? _savedBclm : _chargeLimit);
            _bclmCleared = false;
        }
    }
}
```

### 7.3 关键差异：AlDente vs BootCampCharge

| 方面 | AlDente (macOS) | BootCampCharge (Windows) |
|------|-----------------|--------------------------|
| SMC 访问 | IOKit AppleSMC.kext（需 root） | InpOut I/O 端口驱动（需管理员） |
| 特权分离 | GUI + Helper 守护进程 (NSXPC) | 单进程（驱动提供特权） |
| 默认充电模式 | CH0B（精确），BCLM 可选 | **BCLM（唯一可靠）**，CH0B 辅助 |
| 电量读取 | IOPS API | GetSystemPowerStatus (kernel32) |
| 防睡眠 | IOPMAssertionCreateWithName | SetThreadExecutionState |
| BCLM 精度 | macOS: +3% buffer | Windows: 精确停止 |
| 自愈/恢复 | Helper 10s 检查 + reset() | 退出时 CH0B=0 + 保持 BCLM |
| 配置 | UserDefaults | JSON 文件 |

### 7.4 重写检查清单

- [ ] BatteryManager 功能方法改为**只设请求标志**，不直接写 SMC
- [ ] 新增 `EvaluateChargingState()` 决策方法，按优先级链裁决
- [ ] 新增 `ApplyDecision()` 作为**唯一 SMC 写入点**
- [ ] `chargeInhibited` 标志避免重复写入
- [ ] BCLM/CH0B 互斥管理：进入 CH0B 模式前清 BCLM=0，退出后恢复
- [ ] 过热保护：5 分钟迟滞状态机（Normal→Cooling→ChargingRecovery）
- [ ] 校准模式：5 阶段状态机（Charge→Discharge→Recharging→Rest→Restore）
- [ ] 航行模式：施密特触发器逻辑（上限停充 / 下限充回 / 区间内不操作）
- [ ] 退出时：CH0B=0 + 释放睡眠阻止 + BCLM 保持用户设定
- [ ] Pro 功能互斥：Sailing Mode 在 TopUp/Calibration 期间临时禁用
- [ ] **Hold 状态恢复 BCLM**：当功能从 Inhibit 切回 Hold 时，必须退出 CH0B 模式恢复 BCLM
- [ ] **启动时状态同步**：读取当前 BCLM/CH0B 实际值，同步 `_chargeInhibited` 和 `_ch0bMode`
- [ ] **电压、循环次数**：powercfg XML + WMI 读取并显示
- [ ] **CH0B 可用性验证**：重写前先做实验 1/2，确定 CH0B 在 Boot Camp 下是否可用

### 7.5 启动时状态同步（AlDente HelperTool checkCharging 等价）

AlDente 的 `Helper.swift L108-117` 有 `checkCharging()` 方法，从硬件读取 CH0B 当前值同步 `chargeInhibited` 标志。BootCampCharge 也需要在启动时做同样的事：

```csharp
// 启动时同步状态
public void SyncStateFromHardware()
{
    // 1. 读 BCLM 当前值，更新 _chargeLimit
    int bclm = ReadBclm();
    if (bclm > 0 && bclm <= 100)
        _chargeLimit = bclm;

    // 2. 读 CH0B 当前值，更新 _chargeInhibited
    var ch0b = ReadKeySafe(KeyCh0B, 1);
    if (ch0b != null && ch0b.Length == 1)
        _chargeInhibited = (ch0b[0] == 2);

    // 3. 判断是否在 CH0B 模式
    //    如果 BCLM=0 且 CH0B=2，说明上次在 CH0B 模式时崩溃了
    _ch0bMode = (bclm == 0 && _chargeInhibited);

    // 4. 如果 _ch0bMode 但没有活跃的 CH0B 功能请求，恢复 BCLM
    if (_ch0bMode && !_dischargeRequested && !_topUpRequested &&
        _calPhase == CalibrationPhase.Idle)
    {
        ExitCh0BMode();
    }
}
```

### 7.6 完整的轮询循环时序（精确到每一步）

```
每 5 秒（可配置 30 秒）执行 Poll():

  ┌──────────────────────────────────────────────────────────┐
  │ 步骤 1: 同步配置                                          │
  │   _battery.SetHeatProtection(config.Overtemp)            │
  │   _battery.SetSailingMode(config.SailMode)               │
  └────────────────────────┬─────────────────────────────────┘
                           │
  ┌────────────────────────▼─────────────────────────────────┐
  │ 步骤 2: 刷新电池健康（每 60 秒一次）                       │
  │   if (counter >= 2) _battery.RefreshBatteryHealth()      │
  └────────────────────────┬─────────────────────────────────┘
                           │
  ┌────────────────────────▼─────────────────────────────────┐
  │ 步骤 3: 决策（第二层）                                     │
  │   decision = _battery.EvaluateChargingState()             │
  │   按 AlDente 优先级链返回 Charge / Inhibit / Hold         │
  └────────────────────────┬─────────────────────────────────┘
                           │
  ┌────────────────────────▼─────────────────────────────────┐
  │ 步骤 4: 执行（第三层）                                     │
  │   _battery.ApplyDecision(decision)                       │
  │   唯一写 SMC 的地方                                       │
  │   Hold 时检查是否需要恢复 BCLM                             │
  └────────────────────────┬─────────────────────────────────┘
                           │
  ┌────────────────────────▼─────────────────────────────────┐
  │ 步骤 5: 定时任务评估（每分钟一次）                         │
  │   EvaluateSchedule()                                     │
  └────────────────────────┬─────────────────────────────────┘
                           │
  ┌────────────────────────▼─────────────────────────────────┐
  │ 步骤 6: 更新 UI                                           │
  │   status = _battery.GetStatus()                          │
  │   tray.Update(status)                                    │
  │   mainWindow.UpdateStatus(status)                        │
  └──────────────────────────────────────────────────────────┘
```

### 7.7 关键不变量（重写时必须保证的约束）

| # | 不变量 | 检查方法 |
|---|---|---|
| 1 | BCLM 和 CH0B 绝不同时为非零（BCLM>0 时 CH0B 必须=0） | 每次写 SMC 前检查 |
| 2 | 进入 CH0B 模式（_ch0bMode=true）前必须 BCLM=0 | EnsureBclmCleared() 保证 |
| 3 | 退出 CH0B 模式后必须恢复 BCLM=用户设定值 | ExitCh0BMode() 保证 |
| 4 | chargeInhibited 标志必须和 CH0B 实际值一致 | 启动时 SyncStateFromHardware 同步 |
| 5 | Hold 决策不改变任何 SMC 值（除非需要恢复 BCLM） | ApplyDecision Hold 分支 |
| 6 | 功能互斥：放电和 TopUp 不能同时请求 | RequestDischarge/RequestTopUp 互相清除 |
| 7 | 校准期间航行模式和过热保护自动禁用 | EvaluateChargingState 优先级链保证 |
| 8 | 退出程序时 BCLM 保持用户设定值（不清零） | RestoreOnExit 不清 BCLM |
| 9 | SetChargeLimit 是唯一直接写 BCLM 的公开方法 | 其他方法通过 ExitCh0BMode 间接写 |
| 10 | ApplyDecision 是唯一写 CH0B 的方法 | 其他方法不碰 CH0B |

### 7.8 架构文档逻辑审计结果（2026-08-09）

对架构文档进行了逐节逻辑审计，发现以下问题并修正：

#### 🔴 已修正的逻辑错误

**错误 1 → 已修正：CH0B 在 Boot Camp 下的可用性**

- 原文档第 5.3 节假设"CH0B 在 BCLM=0 时理论可用"
- AlDente 官方文档交叉验证后发现：CH0B 极大概率**完全不工作**
- 修正：第 5.3 节已重写，明确标注所有 CH0B 依赖功能的可用性风险
- **影响**：放电/航行/过热/校准可能在 Boot Camp 下完全不可用

**错误 2 → 已修正：航行模式 HOLD 时 BCLM 状态**

- 原文档假设 Hold 时 BCLM 在管充电，但没考虑 BCLM 可能被之前的 CH0B 操作清成 0
- 修正：重写检查清单新增"Hold 状态恢复 BCLM"约束（7.4 节检查项 + 7.7 不变量 #5）
- **影响**：这正是"81% 不充电"bug 的根因

**错误 3 → 需验证：Top Up 期间用户拖滑块**

- 场景：TopUp 激活时 BCLM=0，用户拖滑块改成 85%，StopTopUp 恢复时用 85% 还是旧值？
- AlDente 行为：TopUp 时 SailingMode 临时禁用，但 Charge Limit 值可以被用户修改
- 修正方案：StopTopUp 恢复时用**当前 config 里的 ChargeLimit**（而不是 TopUp 启动时保存的旧值）
- **但如果 CH0B 不可用，TopUp 只需要 BCLM=0 → BCLM=用户值，不涉及 CH0B，此问题不存在**

**错误 4 → 需验证：校准放电阶段依赖 CH0B**

- 如果 CH0B 不工作，校准的 Discharging 阶段无法执行
- 修正方案：校准功能在 Boot Camp 下标记为"实验性"，启动前检测 CH0B 可用性
- 或：校准提示用户"请拔掉充电器放电到 10%，然后插回"

#### ⚠️ 技术可行性风险（已记录，需验证）

**风险 1：CH0B 可用性** — 整个项目最关键的未知数
- 解决：7.0.1 节实验 1/2，需要用户在 Windows 上执行
- 降级方案：如果 CH0B 不工作，程序只做 BCLM 充电限制 + TopUp + 电池信息显示

**风险 2：循环次数不准确** — Boot Camp 下 CycleCount 报 0
- 实测：powercfg XML CycleCount=0，WMI 无此字段
- 解决：UI 显示"不可用"或"需在 macOS 下读取"，不显示错误数据

**风险 3：睡眠后 CH0B 重置** — 反编译发现 `stopChargingWhenSleeping`
- 含义：系统睡眠后 CH0B 可能被固件重置为 0（充电恢复）
- 影响：如果 CH0B 可用，放电/航行在睡眠唤醒后需要重新写 CH0B=2
- 解决：轮询中持续写 CH0B（和 AlDente 5 秒轮询一样的策略）

#### ✅ 确认可行的部分

| 设计决策 | 可行性 | 依据 |
|---|---|---|
| BCLM 充电限制 | ✅ 已验证 | BCLM=77 实测生效 |
| TopUp (BCLM=0) | ✅ 理论可行 | BCLM=0 = 无限制，不需要 CH0B |
| 三层架构（请求→决策→执行） | ✅ 架构正确 | AlDente 开源版证实单一写入路径 |
| chargeInhibited 去重写入 | ✅ 架构正确 | AlDente Helper.swift L29 证实 |
| 过热保护 5 分钟迟滞 | ✅ 逻辑正确 | 反编译 _heatProtectMode + 官方文档确认 |
| 优先级链（过热>校准>TopUp>放电>航行>限制） | ✅ 逻辑正确 | 官方文档互斥关系证实 |
| WPF + NotifyIcon 互操作 | ✅ 技术可行 | 子代理调研确认，已编译通过 |
| GetSystemPowerStatus 读电量 | ✅ 已验证 | 比 BRSC 更准确 |
| powercfg 读电池健康 | ✅ 已验证 | DesignCap=66000 FullCap=51680 |

### 7.9 重写策略：分两种情况实现

由于 CH0B 可用性未知，重写时应该**同时支持两种模式**：

```csharp
// 启动时检测 CH0B 可用性
bool ch0bAvailable = TestCh0B();

if (ch0bAvailable) {
    // 完整模式：所有功能可用
    // 放电/航行/过热/校准 都通过 CH0B 控制
} else {
    // 降级模式：只有 BCLM 功能
    // 充电限制 ✅ / TopUp ✅
    // 放电 ❌ / 航行 ❌ / 过热 ⚠️(间接) / 校准 ❌
    // UI 上禁用不可用的功能按钮并显示"Boot Camp 不支持"
}
```

**CH0B 检测方法**：
```
1. BCLM = 0
2. CH0B = 2
3. 等 5 秒
4. 读 CH0B → 如果还是 2 = 写入成功（但可能不生效）
5. 读 Windows API 充电状态 → 如果从"充电中"变成"放电中" = CH0B 生效
6. CH0B = 0（恢复）
7. BCLM = 用户设定值（恢复）
```

---

## 附录：AlDente 源码文件清单

| 文件 | 行数 | 作用 |
|------|------|------|
| `AlDente/AppDelegate.swift` | 133 | 菜单栏 + NSPopover + **5s 轮询定时器** + oldKey 分支 |
| `AlDente/Helper.swift` | 294 | SMC 操作代理层（NSXPC 调用 HelperTool）+ chargeInhibited |
| `AlDente/ContentView.swift` | 259 | SwiftUI 弹窗 + Slider + Settings Toggle + SMCPresenter |
| `AlDente/PersistanceManager.swift` | 29 | UserDefaults（chargeVal, oldKey, launchOnLogin） |
| `com.davidwernhart.Helper/main.swift` | 44 | NSXPCListener + **10s 自愈检查** + 退出 reset() |
| `com.davidwernhart.Helper/HelperTool.swift` | 126 | XPC 实现 + **modifiedKeys 修改追踪** + reset() 恢复 |
| `com.davidwernhart.Helper/SMC.swift` | 795 | IOKit AppleSMC 通信 + 温度传感器定义 + 风扇控制 |
| `Common/HelperToolProtocol.swift` | 27 | NSXPC 协议定义 + helperVersion |

### 关键 SMC 键速查

| 键 | 类型 | 读写 | 用途 | Boot Camp 验证 |
|----|------|------|------|---------------|
| BCLM | ui8 | R/W | 充电上限 % | ✅ 写入 77 生效 |
| CH0B | ui8 | R/W | 充电开关 (0=充, 2=禁) | ⚠️ BCLM 非零时可能无效 |
| CH0C | ui8 | R/W | CH0B 备用键 | 未验证 |
| BRSC | ui32 | R | 电池剩余容量 (>>16=%) | ✅ 读取成功 |
| TC0P | sp78 | R | CPU 近邻温度 | ✅ 读取成功 |
| TG0D | sp78 | R | GPU 温度 | ✅ 读取成功 |
| ACIN | flag | R | 充电器连接 | ✅ 读取成功 |
| BATP | flag | R | 电池存在 | ✅ 读取成功 |
| BSIn | ui8 | R | 电池状态信息 (bit0=充电, bit1=AC) | 未验证 |
| BFCL | - | R/W | MagSafe LED | ❌ MBP 2017 不存在 |
| CHWA | ui8 | R/W | Apple Silicon 原生限制 (01=80%) | N/A (Intel only) |
