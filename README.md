# BootCampCharge

> Windows 端 MacBook Boot Camp 充电管理工具 — AlDente for Windows

通过 Apple SMC 端口协议在 Boot Camp Windows 环境下控制 MacBook 充电。

## 已验证

- ✅ MacBook Pro 2017 (MacBookPro14,3) 
- ✅ BCLM 读写（充电限制）
- ✅ CH0B 读写（充电开关）
- ✅ BRSC 读取（电池电量）
- ✅ TC0P/TG0D 读取（CPU/GPU 温度）
- ✅ InpOut 驱动 SMC 通信

## 功能

| 功能 | 状态 |
|------|------|
| 充电限制 (BCLM) | ✅ 已完成 |
| 充电开关 (CH0B) | ✅ 已完成 |
| 硬件电量读取 (BRSC) | ✅ 已完成 |
| CPU/GPU 温度 | ✅ 已完成 |
| 过热保护 | 🔧 开发中 |
| 航行模式 | 🔧 开发中 |
| 放电模式 | 🔧 开发中 |
| 校准模式 | 📋 计划中 |
| 弹出窗口 GUI | 🔧 优化中 |
| 开机自启 | ✅ 已完成 |

## 技术栈

- C# / .NET 8 / WinForms
- InpOut 驱动 (MIT) — I/O 端口访问
- Apple SMC 端口协议 (0x300/0x304)
- SMC 核心库复用 [FanCamp](https://github.com/john7rho/fancamp)

## 编译

```bash
dotnet build -c Release
```

需要 .NET 8 SDK 和 Windows x64。

## 使用

1. 以管理员身份运行 BootCampCharge.exe
2. 右下角系统托盘出现电池图标
3. 右键设置充电限制

## 许可

MIT
