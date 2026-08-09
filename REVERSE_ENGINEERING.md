# AlDente DMG 反编译发现

## 方法
DMG → pydmg 解析 UDIF 结构 → lzfse Python 包解压 LZFSE 块 → 搜索字符串

## Pro 功能变量名（从二进制提取）

### 航行模式 (Sailing Mode)
- `_sailingMode` — 开关
- `_sailingLevel` — 区间大小（百分比）
- `_lastSailingCheckpoint` — 上次航行检查点

### 过热保护 (Heat Protection)
- `_heatProtectMode` — 开关
- `maxTemp` — 温度阈值
- 相关温度键: TC0P/Ts0P 等

### 校准模式 (Calibration)
- `_calibrationState` — 状态机
- `CalibrationSlowDischargeNotification` — 慢速放电通知
- `CalibrationClamshellDisplayOnNotification` — 合盖显示通知

### 临时充满 (Top Up)
- `txtTopUp` — UI 文本
- `_topUpActive` / `topUpMode`

### 放电模式 (Discharge)
- `_dischargeAssertionID` — IOPMAssertion ID
- `dischargeQueryInProgress` — 异步查询
- `dischargeWasPostponed` — 延迟放电

### 其他发现
- `setEnergyModeOnAdapterWithMode:` — 适配器能量模式
- `disableSleepUntilLimit` — 限制前禁止睡眠
- `stopChargingWhenSleeping` — 睡眠时停止充电
- `handleSetMagsafeLED:completion:` — MagSafe LED 控制
- `PowerFlow` — 功率流向图（73 次引用，大功能）
- `verifyActivationWithCompletion:` — Paddle 在线验证
- `_currentChargeVal` — 当前充电值
- `_chargeVal` — 目标充电值
- `HardwareBatteryPercentage` — 硬件电量百分比
- `preventSleepID` / `preventDisplaySleepID` — IOPMAssertion

## SMC 键确认
- BCLM (28 次) — 充电上限
- CH0B (12 次) — 充电开关
- CH0C (14 次) — 充电开关备用
- BFCL (3 次) — MagSafe LED
- BRSC (9 次) — 电池容量
