# BootCampCharge — 不可用功能代码存档

> 这些功能代码经过 SmcDiag 实测验证，在 **Windows Boot Camp 下不可用**。
> 保留供未来参考——如果限制解除或找到绕过方法，可直接复用。

## 失败原因（SmcDiag 2026-08-09 实测）

| 实验 | 操作 | 结果 |
|------|------|------|
| A | BCLM=80 限制 86% 停充后，改 BCLM=0 | 充电=False ❌ |
| B | BCLM=80 限制后，改 BCLM=100 | 充电=False ❌ |
| C | BCLM=80 限制后，拔插充电器+BCLM=0 | 充电=False ❌ |

**根因**：macOS AlDente 放电/TopUp 依赖 `IOPMAssertionCreateWithName`（IOKit API）
来"模拟拔线"。Windows Boot Camp 没有等价 API。

- CH0B=2 只阻止充电器给电池充，不阻止充电器给系统供电 → 电池不掉电
- BCLM 一旦触发限制，SMC 固件锁定"已停充"状态，改 BCLM 无法恢复充电

## 文件清单

| 文件 | 功能 | 状态 |
|------|------|------|
| `BatteryManager_v0.4_legacy.cs` | 三层架构完整实现 | 代码完整，Boot Camp 下不可用 |
| `../SmcDiag.cs` | SMC 诊断工具 | ✅ 可用（诊断用） |
| `../Ch0BTest.cs` | CH0B 可用性测试 | ✅ 可用（测试用） |
| `../docs/ALDENTE_ARCHITECTURE.md` | AlDente 完整架构分析 | 参考文档 |

## 功能状态

| 功能 | Boot Camp | 原因 |
|------|-----------|------|
| 充电限制 BCLM | ✅ 可用 | 硬件级控制，固件持久 |
| 手动放电 | ❌ 不可用 | CH0B=2 只停充不放，缺 IOPMAssertion |
| 临时充满 TopUp | ❌ 不可用 | BCLM 触发限制后改值不恢复充电 |
| 航行模式 | ❌ 不可用 | 依赖 CH0B 区间控制 |
| 校准模式 | ❌ 不可用 | 放电阶段依赖 CH0B |
| 过热保护 | ⚠️ 间接 | 只能降 BCLM 间接保护 |
| 自动放电 | ❌ 不可用 | 依赖 CH0B |

## 未来恢复的技术路线

### 路线 A: Windows 内核驱动模拟 IOPMAssertion
写 WDM/WDF 内核驱动，拦截 ACPI 电池设备，向 Windows 电源管理器报告"AC 已断开"。
需要内核驱动开发 + 数字签名 + 蓝屏风险。

### 路线 B: ACPI DSDT/SSDT 补丁
修改 MacBook ACPI 表中电池设备的 `_PSR`（Power Source）方法，让 Windows 以为
在用电池供电。非常危险，刷错不能开机。

### 路线 C: 等 Apple 驱动更新
Apple 的 `AppleSMC.sys` 驱动可能未来暴露更多接口。
