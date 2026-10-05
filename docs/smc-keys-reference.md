# MBP14,3 SMC 全键参考（实测 dump × 社区字典合并）

- 实测：905 键（911 枚举，905 可读，2026-10-05，`tools/smcscan/smc_dump.py` 生成 `smc-full-dump.json`）
- 可写（flags 含 W=0x40）：**384 键**
- 有社区释义：181 键（VirtualSMC SMCKeys.txt + Apple libSMC SMCSensorKeys.txt + iSMC sensors.go）
- 属性位实测标定（BCLM 可写/CHIM 只读/0x50 键读失败）：0x80=读 0x40=写 0x20=函数 0x10=常量 0x08=原子 0x01=私有写
- 标注列 R/W 语义：R=只读 W=可写（写仍可能被固件按值校验拒绝）

## 电池 (Battery) — 43 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `B0AC` | si16 | 2 | R|PRIVW | 1736 | Battery Actual Amperage (mA) | VSMC |
| `B0AP` | flt  | 4 | R | EA CE AE 41 | *未收录* |  |
| `B0AV` | ui16 | 2 | R | 12587 | Battery Actual Voltage (mV) | VSMC |
| `B0Al` | ui16 | 2 | R|W | 65535 | *未收录* |  |
| `B0Am` | ui8  | 1 | R|W | 10 | *未收录* |  |
| `B0Ar` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `B0As` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `B0At` | ui16 | 2 | R|W | 2400 | *未收录* |  |
| `B0Au` | ui16 | 2 | R|W | 2400 | *未收录* |  |
| `B0BI` | hex_ | 1 | R | 11 | Battery Info (?) | VSMC |
| `B0CI` | hex_ | 2 | R | 0B A9 | *未收录* |  |
| `B0CT` | ui16 | 2 | R | 140 | Battery Cycle Count | VSMC |
| `B0DC` | ui16 | 2 | R | 6600 | *未收录* |  |
| `B0FC` | ui16 | 2 | R|PRIVW | 4709 | Battery full charge capacity. (mAh) (B0FC) | libSMC |
| `B0FG` | hex_ | 2 | R|PRIVW | 00 80 | *未收录* |  |
| `B0LI` | ui16 | 2 | R|W | 5504 | *未收录* |  |
| `B0OS` | hex_ | 2 | R | E4 47 | Battery Extended Operation Status (kBExtendedOperationStatusCmd) | VSMC |
| `B0PS` | hex_ | 2 | R|PRIVW | 00 00 | Battery Extended PF Status (kBExtendedPFStatusCmd) | VSMC |
| `B0R1` | hex_ | 32 | R | 00 55 00 63 00 6A 00 70 00 71 00 75 00 67 00 6C 00 63 00 61 00 5B 00 56 00 65 00 7F 00 B8 01 06 | *未收录* |  |
| `B0R2` | hex_ | 32 | R | 00 55 00 4C 00 5A 00 58 00 5E 00 67 00 54 00 57 00 4C 00 4C 00 52 00 4A 00 57 00 6C 00 8A 00 CD | *未收录* |  |
| `B0R3` | hex_ | 32 | R | 00 00 00 60 00 66 00 6B 00 71 00 82 00 67 00 7B 00 6B 00 6D 00 6C 00 68 00 72 00 A1 00 D2 01 2C | *未收录* |  |
| `B0RI` | ui16 | 2 | R | 4500 | *未收录* |  |
| `B0RM` | ui16 | 2 | R|PRIVW | 3218 | Battery Remaining Capacity (mA*h) | VSMC |
| `B0RS` | ui16 | 2 | R | 167 | Battery Res Scale (kResScale) | VSMC |
| `B0RV` | ui16 | 2 | R | 13050 | *未收录* |  |
| `B0St` | hex_ | 2 | R|PRIVW | 00 80 | Battery Status | VSMC |
| `B0TF` | ui16 | 2 | R | 51 | Battery Average Time to Full | VSMC |
| `BATP` | flag | 1 | R | 0 | *未收录* |  |
| `BBAD` | flag | 1 | R | 0 | Battery Bad | VSMC |
| `BBIN` | flag | 1 | R | 1 | Battery inside | VSMC |
| `BNum` | ui8  | 1 | R | 01 | *未收录* |  |
| `BRSC` | ui16 | 2 | R|PRIVW | 68 | Battery State of Charge | VSMC |
| `BRTC` | ui8  | 1 | R | 07 | *未收录* |  |
| `BRTE` | ui8  | 1 | R | 00 | *未收录* |  |
| `BRTG` | ui8  | 1 | R | 00 | *未收录* |  |
| `BRTH` | si8  | 1 | R|CONST | 01 | *未收录* |  |
| `BRTI` | ui8  | 1 | R | 00 | *未收录* |  |
| `BRTM` | si8  | 1 | R|CONST | 05 | *未收录* |  |
| `BRTP` | ui16 | 2 | R|CONST | 0 | *未收录* |  |
| `BSAC` | hex_ | 1 | R|W | 33 | *未收录* |  |
| `BSIn` | hex_ | 1 | R | 43 | *未收录* |  |
| `MSLD` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `MSTC` | ui16 | 2 | R|W | 0 | *未收录* |  |

## 充电器/AC (Charger & AC) — 33 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `AC-A` | flag | 1 | R|W | 0 | *未收录* |  |
| `AC-D` | ui8  | 1 | W|CONST |  | *未收录* |  |
| `AC-E` | flag | 1 | W|CONST |  | *未收录* |  |
| `AC-I` | ui16 | 2 | R | 131 | *未收录* |  |
| `AC-N` | ui8  | 1 | R | 04 | AC adapter number of ports | VSMC |
| `AC-R` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `AC-U` | flag | 1 | R|W|CONST | 0 | *未收录* |  |
| `AC-V` | hex_ | 4 | R | 00 05 92 00 | *未收录* |  |
| `AC-W` | si8  | 1 | R|CONST | 01 | AC adapter winner port | VSMC |
| `AC-h` | ui8  | 1 | R|W | 04 | *未收录* |  |
| `AC-l` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `AC-p` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `AC-s` | ui16 | 2 | R|W | 3 | *未收录* |  |
| `AC-t` | ui16 | 2 | R|W | 3 | *未收录* |  |
| `ACDI` | ui16 | 2 | R | 4232 | *未收录* |  |
| `ACEN` | ui8  | 1 | R|CONST | 01 | *未收录* |  |
| `ACFP` | flag | 1 | R | 1 | *未收录* |  |
| `ACIC` | ui16 | 2 | R | 4184 | *未收录* |  |
| `ACIN` | flag | 1 | R | 0 | *未收录* |  |
| `ACLM` | ui16 | 2 | R|CONST|PRIVW | 4184 | *未收录* |  |
| `ACPW` | ui32 | 4 | R|CONST|PRIVW | 79496 | *未收录* |  |
| `ACVM` | flag | 1 | R|W | 1 | *未收录* |  |
| `CH0B` | hex_ | 1 | R|W | 00 | *未收录* |  |
| `CHBI` | ui16 | 2 | R|PRIVW | 1872 | Battery Charging Current (kChargingCurrent, mA) | VSMC |
| `CHBV` | ui16 | 2 | R | 13048 | Battery Charging Voltage (kChargingVoltage, mV) | VSMC |
| `CHGC` | ui16 | 2 | R | 55434 | *未收录* |  |
| `CHGD` | flag | 1 | PRIVW |  | *未收录* |  |
| `CHGI` | ui16 | 2 | R | 936 | *未收录* |  |
| `CHGV` | ui16 | 2 | R | 13048 | *未收录* |  |
| `CHII` | ui16 | 2 | R | 2092 | *未收录* |  |
| `CHIM` | flag | 1 | R | 1 | *未收录* |  |
| `CHIT` | ui16 | 2 | R|PRIVW | 0 | *未收录* |  |
| `CHNC` | ui8  | 1 | R | 00 | *未收录* |  |

## 风扇 (Fans) — 12 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `F0Ac` | fpe2 | 2 | R|CONST | 144.52 | Fan0 Actual RPM(F0Ac) | libSMC |
| `F0ID` | {fds | 16 | R | 00 00 0C 00 4C 65 66 74 20 73 69 64 65 20 20 00 | *未收录* |  |
| `F0Mn` | fpe2 | 2 | R|W | 86.4 | *未收录* |  |
| `F0Mt` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `F0Mx` | fpe2 | 2 | R|W | 237.08 | *未收录* |  |
| `F0Tg` | fpe2 | 2 | R|W|CONST | 145.48 | *未收录* |  |
| `F1Ac` | fpe2 | 2 | R|CONST | 134.28 | Fan1 Actual RPM(F1Ac) | libSMC |
| `F1ID` | {fds | 16 | R | 00 00 0E 00 52 69 67 68 74 20 73 69 64 65 20 00 | *未收录* |  |
| `F1Mn` | fpe2 | 2 | R|W | 80 | *未收录* |  |
| `F1Mt` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `F1Mx` | fpe2 | 2 | R|W | 219.56 | *未收录* |  |
| `F1Tg` | fpe2 | 2 | R|W|CONST | 134.72 | *未收录* |  |

## 温度 (Temperature) — 47 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `TA0V` | sp78 | 2 | R|W | 28.426 | Ambient virtual temp (DegC) (TA0V) | libSMC |
| `TB0T` | sp78 | 2 | R|W | 25.297 | Battery Temp (TB0T) / Battery Temp(TB0T) | libSMC |
| `TB1T` | sp78 | 2 | R|W | 30.797 | Battery TS1 Temp (DegC) (TB1T) / Battery Thermistor 0(TB1T) | libSMC |
| `TB2T` | sp78 | 2 | R|W | 31.398 | Battery TS2 Temp (DegC) (TB2T) / Battery Thermistor 1(TB2T) | libSMC |
| `TBXT` | sp78 | 2 | R|W | 25.297 | Battery temp (Same as TB0T) (DegC) (TBXT) | libSMC |
| `TC0E` | sp78 | 2 | R|W | 90.723 | CPU PECI Die filtered temp (DegC) (TC0E) / CPU PECI Die filtered temp for fan control (DegC) (TC0E) | libSMC |
| `TC0F` | sp78 | 2 | R|W | 92.766 | CPU PECI Die filtered and adjusted temp for fan/power control (DegC) (TC0F) / CPU PECI Die filtered and adjust | libSMC |
| `TC0P` | sp78 | 2 | R|W | 68.562 | CPU Prox Temp(TC0P) / CPU Proximity cooked temp (DegC) (TC0P) | libSMC |
| `TC1C` | sp78 | 2 | R|W | 87 | CPU Core 0 temp (PECI) (DegC) (TC1C) / CPU Core 1 absolute cooked temp (PECI) (DegC) (TC1C) | libSMC |
| `TC2C` | sp78 | 2 | R|W | 89 | CPU Core 1 temp (PECI) (DegC) (TC2C) / CPU Core 2 absolute cooked temp (PECI) (DegC) (TC2C) | libSMC |
| `TC3C` | sp78 | 2 | R|W | 83 | CPU Core Temperature from PECI in C°, 1 per physical core | VSMC |
| `TC4C` | sp78 | 2 | R|W | 82 | CPU Core 3 temp (PECI) (DegC) (TC4C) | libSMC |
| `TCFC` | ui16 | 2 | R|W | 4 | CPU PECI die temp filter coeff | VSMC |
| `TCGC` | sp78 | 2 | R|W | 78 | CPU Gfx Core absolute cooked temp, in C°; GPU Intel Graphics [Temp] | VSMC+iSMC |
| `TCMc` | sp78 | 2 | R|W | -128 | CPU eDRAM Hot Indicator(PECI) (DegC) (TCMc) | libSMC |
| `TCSA` | sp78 | 2 | R|W | 82 | CPU System Agent Core temp (PECI) (DegC) (TCSA); System Agent [Temp] | libSMC+iSMC |
| `TCTD` | sp78 | 2 | R|W | 0.621 | CPU PECI die temp Trend (DegC) (TCTD) / CPU PECI die temp Trend (SIS! force bit 5) (DegC) (TCTD) | libSMC |
| `TCXC` | sp78 | 2 | R|W | 92.328 | CPU Core PECI temp (DegC) (SIS! force bit 7) (TCXC) / CPU Max Package Core absolute cooked temp (PECI) (DegC)  | libSMC |
| `TG0D` | sp78 | 2 | R|W | 73.062 | GPU 0 Die cooked temp (DegC) (TG0D) / GPU A Die cooked temp (DegC) (TG0D) | libSMC |
| `TG0F` | sp78 | 2 | R|W | 70.789 | GPU 0 Die filtered and adjusted temp for fan/power control (DegC) (TG0F) / GPU 0 Die filtered and adjusted tem | libSMC |
| `TG0P` | sp78 | 2 | R|W | 66.938 | GPU 0 Proximity cooked temp (DegC) (TG0P) / GPU A Proximity cooked temp (DegC) (TG0P) | libSMC |
| `TGDD` | sp78 | 2 | R|W | 67 | GPU AMD Radeon [Temp] | iSMC |
| `TGVP` | sp78 | 2 | R|W | 60.812 | *未收录* |  |
| `TH0A` | sp78 | 2 | R|W | 40.125 | Drive 0 OOBv3 absolute cooked temp A (DegC) (TH0A) / Drive 0 OOBv3 cooked temp A (DegC) (TH0A) | libSMC |
| `TH0B` | sp78 | 2 | R|W | 39 | Drive 0 OOBv3 absolute cooked temp B (DegC) (TH0B) / Drive 0 OOBv3 cooked temp B (DegC) (TH0B) | libSMC |
| `TH0C` | sp78 | 2 | R|W | 40.75 | Drive 0 OOBv3 absolute cooked temp C (DegC) (TH0C) / Drive 0 OOBv3 cooked temp C (DegC) (TH0C) | libSMC |
| `TH0F` | sp78 | 2 | R|W | -36.254 | Cooked and filtered SSD OOB temperature (SIT! force bit 7) (TH0F) / Drive 0 OOBv3 relative cooked temp Max Fil | libSMC |
| `TH0R` | sp78 | 2 | R|W | -36.25 | Drive 0 OOBv3 relative cooked temp Max (DegC) (TH0R) / SSD 0 (2.5) OOB v3 relative cooked temp Max (DegC) (TH0 | libSMC |
| `TH0a` | sp78 | 2 | R|W | 40.125 | Drive 0 OOBv3 absolute raw temp A (DegC) (TH0a) / Drive 0 OOBv3 raw temp A (DegC) (TH0a) | libSMC |
| `TH0b` | sp78 | 2 | R|W | 39 | Drive 0 OOBv3 absolute raw temp B (DegC) (TH0b) / Drive 0 OOBv3 raw temp B (DegC) (TH0b) | libSMC |
| `TH0c` | sp78 | 2 | R|W | 40.75 | Drive 0 OOBv3 absolute raw temp C (DegC) (TH0c) / Drive 0 OOBv3 raw temp C (DegC) (TH0c) | libSMC |
| `TH0x` | sp78 | 2 | R|W | -127 | Drive 0 OOBv3 temp Max  (DegC) (TH0x) / Drive0 (SSD Gumstick) OOB v3 raw temp Max (DegC) (TH0x); NAND [Temp] | libSMC+iSMC |
| `TM0P` | sp78 | 2 | R|W | 62.812 | DIMM 0 Proximity cooked temp (DegC) (TM0P) / DIMM 0/1 Top Proximity cooked temp (DegC) (TM0P) | libSMC |
| `TPCD` | sp78 | 2 | R|W | 50 | PCH Die Temp Digital (DegC) (TPCD) / PCH Die Temp(TPCD) | libSMC |
| `TTLD` | sp78 | 2 | R|W | 39.438 | Thunderbolt Left [Temp] | iSMC |
| `TTRD` | sp78 | 2 | R|W | 49.562 | Thunderbolt Right [Temp] | iSMC |
| `TW0P` | sp78 | 2 | R|W | 62.062 | Airport Card temp (DegC) (TW0P) / Airport Temp (DegC) (TW0P) | libSMC |
| `TaLC` | sp78 | 2 | R|W | 36.625 | *未收录* |  |
| `TaRC` | sp78 | 2 | R|W | 46.062 | *未收录* |  |
| `Th1H` | sp78 | 2 | R|W | 62.062 | Fin Stack Proximity temp (DegC) (Th1H) / Fin Stack Temp(Th1H) | libSMC |
| `Th2H` | sp78 | 2 | R|W | 65.5 | Fin Stack Temp(Th2H) / Heat Pipe Temp(Th2H); SoC Heatsink Die 2 5 [Temp] | libSMC+iSMC |
| `Ts0P` | sp78 | 2 | R|W | 33.062 | Palm Rest Temp (DegC) (Ts0P) / Palm Rest Temp(Ts0P); SSD Controller [Temp] | libSMC+iSMC |
| `Ts0S` | sp78 | 2 | R|W | 44.543 | Bottom Case Synthetic Temp(Ts0S) / Heat Pipe Synthetic Temp(Ts0S); SSD 8 [Temp] | libSMC+iSMC |
| `Ts1P` | sp78 | 2 | R|W | 30 | Actuator Temp (DegC) (Ts1P) / Palm Rest Temp 2 (DegC) (Ts1P); SSD Controller [Temp] | libSMC+iSMC |
| `Ts1S` | sp78 | 2 | R|W | 44.488 | Synthetic Top skin (DegC) (Ts1S) / Synthetic top skin (DegC) (Ts1S) | libSMC |
| `Ts2S` | sp78 | 2 | R|W | 43.844 | *未收录* |  |
| `pHDC` | sp78 | 2 | R|W | 0 | *未收录* |  |

## 功率 (Power) — 53 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `PAPC` | flt  | 4 | R|W | 00 00 00 00 | Airport  Power (DEBUG) (Watts) (PAPC) / Airport Power   (Watts) (PAPC); Airport [Power] | libSMC+iSMC |
| `PC0R` | flt  | 4 | R|W | CA 41 DF 41 | Average CPU High side power (IC0R * VD0R) in watts | VSMC |
| `PCAC` | flt  | 4 | R|W | 00 00 00 00 | CPU core in watts | VSMC |
| `PCAM` | flt  | 4 | R|W | 00 00 00 00 | CPU core (IMON) in watts; CPU Core (IMON) [Power] | VSMC+iSMC |
| `PCMC` | flt  | 4 | R|W | 00 00 00 00 | S2 Camera Power (Watts) (PCMC) | libSMC |
| `PCPC` | sp87 | 2 | R|W | 39.828 | CPU package core power (PECI) in watts; CPU Package [Power] | VSMC+iSMC |
| `PCPG` | sp87 | 2 | R|W | 0 | CPU package Gfx power (PECI) in watts; GPU Intel Graphics [Power] | VSMC+iSMC |
| `PCPT` | sp87 | 2 | R|W | 47.703 | CPU package total power (PECI) in watts; CPU Package Total [Power] | VSMC+iSMC |
| `PCSC` | flt  | 4 | R|W | 00 00 00 00 | CPU VCCSA Power in watts | VSMC |
| `PCTC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PCTM` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PDTR` | flt  | 4 | R|W | DD D3 8F 42 | DC-In total power in Watts; DC In [Power] | VSMC+iSMC |
| `PF3C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PF5C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PG0C` | flt  | 4 | R|W | 00 00 00 00 | Average GPU Vcore power (IG0C * VG0C) (SPS! force bit 12) (Watts) (PG0C) / Ext GPU Power (Watts) (PG0C); GPU [ | libSMC+iSMC |
| `PG0R` | flt  | 4 | R|W | 29 3A 21 41 | Average GPU High side power (IG0R * VD0R) (SPS! force bit 10) (Watts) (PG0R) / Ext GPU Ext GPU High Side Power | libSMC |
| `PG1C` | flt  | 4 | R|W | 00 00 00 00 | Ext GPU 1.8V Power  (DEBUG)  (Watts) (PG1C) / Ext GPU 1.8V Power  (DEBUG) (Watts) (PG1C); Ext GPU 1.8V [Power] | libSMC+iSMC |
| `PG2C` | flt  | 4 | R|W | 00 00 00 00 | Ext GPU 1.05V Power (Watts) (PG2C) / Ext GPU 1.0V Power  (DEBUG) (Watts) (PG2C); Ext GPU 1.05V [Power] | libSMC+iSMC |
| `PG3C` | flt  | 4 | R|W | 00 00 00 00 | Ext GPU 1.35V Power (Watts) (PG3C) / Ext GPU 1.8V Power  (DEBUG) (Watts) (PG3C); Ext GPU 1.35V [Power] | libSMC+iSMC |
| `PG4C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PGAC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PHDC` | flt  | 4 | R|W | 00 00 00 00 | HDD Power  (DEBUG) (Watts) (PHDC) / HDD Power  (Watts) (PHDC); SSD [Power] | libSMC+iSMC |
| `PHPC` | flt  | 4 | R|W | 6B EF 17 42 | Average Thermal Control Power (SPS! force bit 13) (Watts) (PHPC) / HP Power (PHPC); Heatpipe [Power] | libSMC+iSMC |
| `PIDC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PKBC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PLDC` | flt  | 4 | R|W | 00 00 00 00 | LCD Driver Power (Watts) (PLDC) / LCD Panel Power (Watts) (PLDC); LCD Panel [Power] | libSMC+iSMC |
| `PM0C` | flt  | 4 | R|W | 00 00 00 00 | 1.2V to CPU/MEM lowside power (IM0C * VM0C) (Watts) (PM0C) / Average Memory power (IM0C * 1.5V) (SPS! force bi | libSMC+iSMC |
| `PM1C` | flt  | 4 | R|W | 00 00 00 00 | 1.8V S3 lowside power (IM1C * VM1C) (Watts) (PM1C) / DDR 1.5V Power (DEBUG) (Watts) (PM1C); DDR [Power] | libSMC+iSMC |
| `PMCC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PO3R` | flt  | 4 | R|W | 00 00 00 00 | Other 3.3V High Side Power   (Watts) (PO3R); Other 3.3V High Side [Power] | libSMC+iSMC |
| `PO5R` | flt  | 4 | R|W | 00 00 00 00 | Other 5V High Side Power   (Watts) (PO5R); Other 5V High Side [Power] | libSMC+iSMC |
| `PPBR` | flt  | 4 | R|W | B2 44 AD 3F | PBus (BMON) Batt Dischg Power (Watts) (PPBR) / PBus on battery power (Watts) (PPBR); Battery [Power] | libSMC+iSMC |
| `PSTR` | sp87 | 2 | R|W | 99.031 | System Total Power Consumed (Delayed 1 Second) in watts; System Total [Power] | VSMC+iSMC |
| `PT3C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PTAR` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PULC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PURC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PZ0E` | flt  | 4 | R|W | 00 00 8C 42 | Zone 0 Target Power (PZ0E); Zone 0 Target [Power] | libSMC+iSMC |
| `PZ0F` | flt  | 4 | R|W | 35 2F 18 42 | Zone 0 Filtered Power (PZ0F); Zone 0 Filtered [Power] | libSMC+iSMC |
| `PZ0G` | flt  | 4 | R|W | 6B EF 17 42 | Zone 0 Power (PZ0G) / Zone 0 average power (Watts) (PZ0G); Zone 0 Average [Power] | libSMC+iSMC |
| `PZ0T` | flt  | 4 | R | 00 00 00 00 | Zone 0 Abstract Throttle (PZ0T); Zone 0 Abstract Throttle [Power] | libSMC+iSMC |
| `PZ1E` | flt  | 4 | R|W | 00 00 34 42 | Zone 1 Target Power (PZ1E); Zone 1 Target [Power] | libSMC+iSMC |
| `PZ1F` | flt  | 4 | R|W | 3E 84 25 41 | Zone 1 Filtered Power (PZ1F); Zone 1 Filtered [Power] | libSMC+iSMC |
| `PZ1G` | flt  | 4 | R|W | 29 3A 21 41 | Zone 1 average power (Watts) (PZ1G); Zone 1 Average [Power] | libSMC+iSMC |
| `PZ1T` | flt  | 4 | R | 00 00 00 00 | Zone 1 Abstract Throttle (PZ1T); Zone 1 Abstract Throttle [Power] | libSMC+iSMC |
| `PZ2E` | flt  | 4 | R|W | 00 00 50 41 | Zone 1 Target Power (PZ2E) / Zone 2 Target Power (PZ2E); Zone 2 Target [Power] | libSMC+iSMC |
| `PZ2F` | flt  | 4 | R|W | 01 00 E1 3D | Zone 1 Filtered Power (PZ2F) / Zone 2 Filtered Power (PZ2F); Zone 2 Filtered [Power] | libSMC+iSMC |
| `PZ2G` | flt  | 4 | R|W | 81 58 DB 3D | Zone 2 average power (Watts) (PZ2G); Zone 2 Average [Power] | libSMC+iSMC |
| `PZ2T` | flt  | 4 | R | 00 00 00 00 | Zone 1 Abstract Throttle (PZ2T) / Zone 2 Abstract Throttle (PZ2T); Zone 2 Abstract Throttle [Power] | libSMC+iSMC |
| `PZ3E` | flt  | 4 | R|W | 00 04 13 43 | Zone 1 Target Power (PZ3E) / Zone 3 Target Power (PZ3E); Zone 3 Target [Power] | libSMC+iSMC |
| `PZ3F` | flt  | 4 | R|W | 55 E3 3C 3C | Zone 1 Filtered Power (PZ3F) / Zone 3 Filtered Power (PZ3F); Zone 3 Filtered [Power] | libSMC+iSMC |
| `PZ3G` | flt  | 4 | R|W | D1 25 3C 3C | Zone 3 average power (Watts) (PZ3G); Zone 3 Average [Power] | libSMC+iSMC |
| `PZ3T` | flt  | 4 | R | 00 00 00 00 | Zone 1 Abstract Throttle (PZ3T) / Zone 3 Abstract Throttle (PZ3T); Zone 3 Abstract Throttle [Power] | libSMC+iSMC |

## 电流 (Current) — 44 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `IAPC` | flt  | 4 | R|W | 00 00 00 00 | Airport  Current (DEBUG) (Amps) (IAPC) / Airport Current (Amps) (IAPC); WLAN Low-Side [Current] | libSMC+iSMC |
| `IBAC` | sp78 | 2 | R|W | -1.676 | Battery Current (Amps) (IBAC) / Battery Current (IBAC); Battery [Current] | libSMC+iSMC |
| `IBLR` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IBSC` | flt  | 4 | R|W | 78 7D 3D 3C | *未收录* |  |
| `IBTC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IC0R` | flt  | 4 | R|W | 5F 68 13 40 | CPU Computing High Side current. (Amps) (IC0R) / CPU High Compute Current (IC0R); CPU High Side [Current] | libSMC+iSMC |
| `ICAC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `ICAM` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `ICMC` | flt  | 4 | R|W | 00 00 00 00 | Camera S2 Current (Amps) (ICMC) / S2 Camera Current  (Amps) (ICMC); Camera S2 [Current] | libSMC+iSMC |
| `ICSC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `ICTC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `ICTM` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `ID0R` | flt  | 4 | R|W | 83 43 6C 40 | DC In Average current. (SPS! force bit 3) (Amps) (ID0R) / DC In current (Amps) (ID0R); Mainboard S0 Rail (DC I | libSMC+iSMC |
| `ID0r` | ui16 | 2 | R | 848 | *未收录* |  |
| `IF3C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IF5C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IG0C` | flt  | 4 | R|W | 00 00 00 00 | Ext GPU Core Current (Amps) (IG0C) / Ext GPU Core current. (Amps) (IG0C); GPU Rail [Current] | libSMC+iSMC |
| `IG0R` | flt  | 4 | R|W | 25 08 44 3F | Ext GPU High Side Current. (Amps) (IG0R) / Ext GPU High Side current. (Amps) (IG0R); GPU High Side [Current] | libSMC+iSMC |
| `IG0c` | ui16 | 2 | R|W | 10 | *未收录* |  |
| `IG0r` | ui16 | 2 | R | 439 | *未收录* |  |
| `IG1C` | flt  | 4 | R|W | 00 00 00 00 | Ext GPU  1.8 V current. (DEBUG) (Amps) (IG1C) / Ext GPU 1.8V Current (DEBUG) (Amps) (IG1C); GPU Core Input-Sid | libSMC+iSMC |
| `IG1c` | ui16 | 2 | R|W | 15 | *未收录* |  |
| `IG2C` | flt  | 4 | R|W | 00 00 00 00 | Ext GPU 1.05V current. (Amps) (IG2C) / Ext GPU 1.0V Current (DEBUG) (Amps) (IG2C); Ext GPU 1.05V [Current] | libSMC+iSMC |
| `IG2c` | ui16 | 2 | R|W | 10 | *未收录* |  |
| `IG3C` | flt  | 4 | R|W | 00 00 00 00 | Ext GPU  1.35 V current. (Amps) (IG3C) / Ext GPU VRAM & I/O current Current (DEBUG) (Amps) (IG3C); Ext GPU VRA | libSMC+iSMC |
| `IG3c` | ui16 | 2 | R|W | 145 | *未收录* |  |
| `IG4C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IG4c` | ui16 | 2 | R|W | 10 | *未收录* |  |
| `IHDC` | flt  | 4 | R|W | 00 00 00 00 | HDD  Current (DEBUG) (Amps) (IHDC) / HDD Current  (Amps) (IHDC); SSD [Current] | libSMC+iSMC |
| `IHDc` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `IIDC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IKBC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `ILDC` | flt  | 4 | R|W | 00 00 00 00 | LCD Driver Current (Debug)  (Amps) (ILDC) / LCD Panel Current  (Amps) (ILDC); LCD Panel [Current] | libSMC+iSMC |
| `IM0C` | flt  | 4 | R|W | 00 00 00 00 | 1.2V to CPU/MEM lowside current (Amps) (IM0C) / CPU DDR current. (Amps) (IM0C); Memory Controller [Current] | libSMC+iSMC |
| `IM0c` | ui16 | 2 | R|W | 571 | *未收录* |  |
| `IMCC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IO3R` | flt  | 4 | R|W | 00 00 00 00 | Current other 3.3V High. (Amps) (IO3R) / Other 3.3V High Side current.  (Amps) (IO3R); Current Other 3.3V High | libSMC+iSMC |
| `IO5R` | flt  | 4 | R|W | 00 00 00 00 | Battery current BMON. (Amps) (IO5R) / Other 5V High Side current.  (Amps) (IO5R); Battery BMON [Current] | libSMC+iSMC |
| `IPBR` | flt  | 4 | R|W | 01 23 DC 3D | Battery current BMON. (Amps) (IPBR) / PBus on battery current. (Amps) (IPBR); Charger BMON [Current] | libSMC+iSMC |
| `IPBr` | ui16 | 2 | R|W | 14 | *未收录* |  |
| `IT3C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `ITAR` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IULC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `IURC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |

## 电压 (Voltage) — 14 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `VCAC` | flt  | 4 | R|W | 23 E1 8C 3F | CPU IA [Voltage] | iSMC |
| `VCAc` | ui16 | 2 | R|W | 1511 | *未收录* |  |
| `VCSC` | flt  | 4 | R|W | 09 61 64 3F | CPU System Agent [Voltage] | iSMC |
| `VCSc` | ui16 | 2 | R|W | 1218 | *未收录* |  |
| `VCTC` | flt  | 4 | R|W | 01 98 DC 3A | GPU Intel Graphics [Voltage] | iSMC |
| `VCTc` | ui16 | 2 | R|W | 3 | *未收录* |  |
| `VD0R` | flt  | 4 | R|W | 24 56 9D 41 | Adapter Voltage (Volts) (VD0R) / DC In Voltage (SPS! force bit 16) (Volts) (VD0R); DC In [Voltage] | libSMC+iSMC |
| `VD0r` | ui16 | 2 | R|W | 3505 | *未收录* |  |
| `VG0C` | flt  | 4 | R|W | C1 36 49 3F | Ext GPU Core voltage. (Volts) (VG0C) / GPU A Core low side voltage (Volts) (VG0C) | libSMC |
| `VG0c` | ui16 | 2 | R|W | 1073 | *未收录* |  |
| `VG2C` | flt  | 4 | R|W | 1F C6 4D 3F | *未收录* |  |
| `VG2c` | ui16 | 2 | R|W | 1097 | *未收录* |  |
| `VP0R` | flt  | 4 | R|W | E9 1D 4A 41 | PBus Voltage  (Volts) (VP0R) / PBus Voltage (SPS! force bit 4) (Volts) (VP0R); 12V Rail [Voltage] | libSMC+iSMC |
| `VP0r` | ui16 | 2 | R|W | 2872 | *未收录* |  |

## 其他 (Other) — 533 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `#KEY` | ui32 | 4 | R | 911 | *未收录* |  |
| `$Adr` | hex_ | 4 | R | 00 00 03 00 | *未收录* |  |
| `$Num` | ui8  | 1 | R|W|CONST | 01 | smc-count | VSMC |
| `+LKS` | flag | 1 | R|CONST | 1 | *未收录* |  |
| `ADC0` | ui16 | 2 | R | 452 | *未收录* |  |
| `ADC1` | ui16 | 2 | R | 2870 | *未收录* |  |
| `ADC2` | ui16 | 2 | R | 15 | *未收录* |  |
| `ADC3` | ui16 | 2 | R | 1144 | *未收录* |  |
| `ADC4` | ui16 | 2 | R | 3494 | *未收录* |  |
| `ADC5` | ui16 | 2 | R | 1651 | *未收录* |  |
| `ADC6` | ui16 | 2 | R | 11 | *未收录* |  |
| `ADC7` | ui16 | 2 | R | 11 | *未收录* |  |
| `ADC8` | ui16 | 2 | R | 1072 | *未收录* |  |
| `ADC9` | ui16 | 2 | R | 571 | *未收录* |  |
| `ADCa` | ui16 | 2 | R | 9 | *未收录* |  |
| `ADCb` | ui16 | 2 | R | 1098 | *未收录* |  |
| `ADCc` | ui16 | 2 | R | 234 | *未收录* |  |
| `ADCd` | ui16 | 2 | R | 14 | *未收录* |  |
| `ADCe` | ui16 | 2 | R | 0 | *未收录* |  |
| `ADCf` | ui16 | 2 | R | 9 | *未收录* |  |
| `ADCg` | ui16 | 2 | R | 253 | *未收录* |  |
| `ADCh` | ui16 | 2 | R | 126 | *未收录* |  |
| `ADCi` | ui16 | 2 | R | 8 | *未收录* |  |
| `ADCj` | ui16 | 2 | R | 1217 | *未收录* |  |
| `ADCk` | ui16 | 2 | R | 1537 | *未收录* |  |
| `ADCl` | ui16 | 2 | R | 3 | *未收录* |  |
| `ADCm` | ui16 | 2 | R | 7 | *未收录* |  |
| `ADCn` | ui16 | 2 | R | 9 | *未收录* |  |
| `ADSC` | hex_ | 1 | R | 33 | *未收录* |  |
| `ATCT` | ui32 | 4 | R | 0 | *未收录* |  |
| `ATPS` | ui8  | 1 | R|CONST|PRIVW | 03 | *未收录* |  |
| `ATVB` | ui8  | 1 | R|CONST|PRIVW | 03 | *未收录* |  |
| `AUPO` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `AUWT` | ui16 | 2 | R|W | 550 | *未收录* |  |
| `BALG` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `BBIF` | ui8  | 1 | R|CONST | 0E | *未收录* |  |
| `BC1V` | ui16 | 2 | R | 4255 | Battery Cell 1 Voltage | VSMC |
| `BC2V` | ui16 | 2 | R | 4139 | Battery Cell 2 Voltage | VSMC |
| `BC3V` | ui16 | 2 | R | 4193 | Battery Cell 3 Voltage | VSMC |
| `BCLM` | ui8  | 1 | R|W|CONST | 64 | *未收录* |  |
| `BCMV` | ui16 | 2 | R|PRIVW | 4255 | *未收录* |  |
| `BDD1` | ui8  | 1 | R | 29 | *未收录* |  |
| `BDD2` | ui8  | 1 | R | 2A | *未收录* |  |
| `BDD3` | ui8  | 1 | R | 29 | *未收录* |  |
| `BDVT` | hex_ | 1 | R|W | 00 | *未收录* |  |
| `BEMB` | flag | 1 | R | 1 | Be mobile | VSMC |
| `BFCT` | ui16 | 2 | R | 0 | *未收录* |  |
| `BFLO` | ui8  | 1 | R | 00 | *未收录* |  |
| `BFWC` | ui16 | 2 | R | 0 | *未收录* |  |
| `BIMX` | ui16 | 2 | R|PRIVW | 9767 | *未收录* |  |
| `BIPD` | ui16 | 2 | R|CONST|PRIVW | 0 | *未收录* |  |
| `BITV` | ui16 | 2 | R|CONST|PRIVW | 0 | *未收录* |  |
| `BL0A` | hex_ | 32 | R | 00 00 00 00 00 48 3F F4 00 01 08 F1 00 00 00 00 06 0A E9 C4 00 80 E4 67 B2 40 00 00 00 00 00 00 | *未收录* |  |
| `BL0B` | hex_ | 32 | R | 01 D9 00 4E 11 06 08 4B 33 09 1F C6 15 5B E5 27 1A 8C E0 74 EA DC E8 AC 01 11 00 06 E0 07 00 31 | *未收录* |  |
| `BLPT` | hex_ | 20 | R|CONST | 01 01 04 1A 13 56 31 38 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `BLTA` | si16 | 2 | R | 4353 | *未收录* |  |
| `BLTO` | ui32 | 4 | R|PRIVW | 7208960 | *未收录* |  |
| `BLTP` | hex_ | 16 | R|PRIVW | 00 00 00 00 23 05 00 00 12 00 00 00 00 00 00 00 | *未收录* |  |
| `BNCM` | ui8  | 1 | PRIVW |  | *未收录* |  |
| `BNCR` | ui8  | 1 | R | 00 | Battery Not Charging Reason | VSMC |
| `BPII` | sp78 | 2 | R | -1.801 | *未收录* |  |
| `BPIT` | sp78 | 2 | R | 12.125 | *未收录* |  |
| `BQCC` | si16 | 2 | R | -1078 | Battery Passed Charge (kPassedCharge) | VSMC |
| `BQD1` | ui16 | 2 | R | 9280 | Battery Cell 1 Depth of Discharge (kDOD0) | VSMC |
| `BQD2` | ui16 | 2 | R | 9344 | Battery Cell 2 Depth of Discharge (kDOD1) | VSMC |
| `BQD3` | ui16 | 2 | R | 9248 | Battery Cell 3 Depth of Discharge (kDOD2) | VSMC |
| `BQX1` | ui16 | 2 | R | 6888 | Battery Cell 1 Absolute Capacity (QmaxCell0) | VSMC |
| `BQX2` | ui16 | 2 | R | 6921 | Battery Cell 2 Absolute Capacity (QmaxCell1) | VSMC |
| `BQX3` | ui16 | 2 | R | 7150 | Battery Cell 3 Absolute Capacity (QmaxCell2) | VSMC |
| `BROS` | ui16 | 2 | R|CONST | 0 | *未收录* |  |
| `BTFU` | flag | 1 | R|W | 0 | *未收录* |  |
| `BTIL` | ui16 | 2 | R | 1872 | *未收录* |  |
| `BTTI` | ui8  | 1 | R | 02 | *未收录* |  |
| `BTVI` | ui8  | 1 | R | 03 | *未收录* |  |
| `BTVR` | ui8  | 1 | R|PRIVW | 01 | *未收录* |  |
| `BTVS` | fp1f | 2 | R | 5D B2 | *未收录* |  |
| `BTVT` | ui8  | 1 | R|PRIVW | 01 | *未收录* |  |
| `BVVC` | ui32 | 4 | R | 0 | *未收录* |  |
| `BVVL` | ui16 | 2 | R | 65040 | *未收录* |  |
| `BVVM` | ui16 | 2 | R | 65040 | *未收录* |  |
| `BWLM` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `Bvt0` | Flag | 1 | R|PRIVW | 01 | *未收录* |  |
| `Bvt1` | ui32 | 4 | R|PRIVW | 7208960 | *未收录* |  |
| `Bvt2` | flt  | 4 | R|PRIVW | 18 0E 74 3F | *未收录* |  |
| `Bvt3` | flt  | 4 | R|PRIVW | 18 0E 74 3F | *未收录* |  |
| `Bvt4` | flt  | 4 | R|PRIVW | DF B6 F4 BE | *未收录* |  |
| `Bvt5` | flt  | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `Bvt6` | flt  | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `Bvt7` | flt  | 4 | R|PRIVW | 33 33 8B 40 | *未收录* |  |
| `Bvt8` | flt  | 4 | R|PRIVW | 33 33 8B 40 | *未收录* |  |
| `Bvt9` | Flag | 1 | R|PRIVW | 00 | *未收录* |  |
| `Bvta` | hex_ | 16 | R|PRIVW | 00 00 00 00 23 05 00 00 12 00 00 00 00 00 00 00 | *未收录* |  |
| `Bvtb` | ui32 | 4 | R|PRIVW | 0 | *未收录* |  |
| `Bvtc` | Flag | 1 | ? |  | *未收录* |  |
| `Bvtd` | ui32 | 4 | ? |  | *未收录* |  |
| `Bvte` | ui8* | 16 | ? |  | *未收录* |  |
| `Bvtf` | flt  | 4 | ? |  | *未收录* |  |
| `Bvtg` | flt  | 4 | ? |  | *未收录* |  |
| `Bvth` | flt  | 4 | ? |  | *未收录* |  |
| `Bvti` | flt  | 4 | ? |  | *未收录* |  |
| `Bvtj` | flt  | 4 | ? |  | *未收录* |  |
| `Bvtk` | flt  | 4 | ? |  | *未收录* |  |
| `Bvtl` | flt  | 4 | ? |  | *未收录* |  |
| `Bvtm` | flt  | 4 | ? |  | *未收录* |  |
| `Bvtn` | flt  | 4 | ? |  | *未收录* |  |
| `Bvto` | ui8* | 20 | ? |  | *未收录* |  |
| `Bvtp` | ui8* | 80 | ? |  | *未收录* |  |
| `C2HD` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `C2HP` | hex_ | 2 | R|PRIVW | 00 00 | *未收录* |  |
| `C2TD` | ch8* | 32 | R|PRIVW | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `C2TM` | ch8* | 32 | CONST|PRIVW |  | *未收录* |  |
| `C2TP` | hex_ | 1 | R|PRIVW | 00 | *未收录* |  |
| `CLKT` | ui32 | 4 | R|W|CONST | 23150 | *未收录* |  |
| `CLSD` | ui16 | 2 | R|W|CONST | 0 | *未收录* |  |
| `CLWK` | ui16 | 2 | R|W|CONST | 0 | Clock since Wake | VSMC |
| `CPL1` | sp87 | 2 | CONST|PRIVW |  | *未收录* |  |
| `CPL2` | sp87 | 2 | CONST|PRIVW |  | *未收录* |  |
| `CPL3` | ui32 | 4 | CONST|PRIVW |  | *未收录* |  |
| `CPL4` | sp87 | 2 | CONST|PRIVW |  | *未收录* |  |
| `CPPL` | sp87 | 2 | R | 90 | *未收录* |  |
| `CPTX` | sp78 | 2 | R | 100 | *未收录* |  |
| `CRCA` | hex_ | 4 | R | 6C 16 41 AA | CRC Application Expected | VSMC |
| `CRCB` | hex_ | 4 | R | A0 17 8B 87 | CRC Base Flasher Expected | VSMC |
| `CRCC` | hex_ | 4 | R | BD 69 D0 8D | CRC CV Expected | VSMC |
| `CRCF` | hex_ | 4 | R|CONST |  | CRC Full Actual | VSMC |
| `CRCc` | hex_ | 4 | R|CONST | BD 69 D0 8D | CRC CV Actual | VSMC |
| `CRCr` | hex_ | 4 | R|CONST | 39 CF AA 08 | CRC Reset Vectors Actual | VSMC |
| `CRCs` | hex_ | 4 | R|CONST | 66 88 1F 9E | *未收录* |  |
| `CRCu` | hex_ | 4 | R|CONST | 6A D0 9F 3C | CRC Update Flasher | VSMC |
| `D1CA` | ui16 | 2 | R|W | 11 | *未收录* |  |
| `D1CD` | ui16 | 2 | R|W | 10 | *未收录* |  |
| `D1CR` | ui16 | 2 | R|W | 4 | *未收录* |  |
| `D1Cd` | hex_ | 14 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D1IC` | ui16 | 2 | R|W|CONST | 65535 | *未收录* |  |
| `D1IR` | ui16 | 2 | R | 4350 | *未收录* |  |
| `D1JA` | ui32 | 4 | R|CONST | 7500 | *未收录* |  |
| `D1JB` | ui32 | 4 | R|CONST | 7500 | *未收录* |  |
| `D1JD` | {jst | 17 | R|CONST | 00 00 1D 4C 00 00 1D 4C 1D 4C 1D 4C 1D 4C 00 00 00 | *未收录* |  |
| `D1JR` | ui32 | 4 | W|CONST |  | *未收录* |  |
| `D1JS` | ui32 | 4 | W|CONST |  | *未收录* |  |
| `D1LR` | ui8  | 1 | R | 00 | *未收录* |  |
| `D1VC` | ui16 | 2 | R|W|CONST | 0 | *未收录* |  |
| `D1VM` | ui16 | 2 | R | 19000 | *未收录* |  |
| `D1VR` | ui16 | 2 | R | 20000 | *未收录* |  |
| `D1VX` | ui16 | 2 | R | 21000 | *未收录* |  |
| `D1if` | ch8* | 12 | R | 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D1ih` | ch8* | 12 | R | 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D1ii` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D1im` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D1in` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D1is` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D2CA` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `D2CD` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `D2CR` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `D2Cd` | hex_ | 14 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D2IC` | ui16 | 2 | R|W|CONST | 65535 | *未收录* |  |
| `D2IR` | ui16 | 2 | R | 0 | *未收录* |  |
| `D2JA` | ui32 | 4 | R|CONST | 7500 | *未收录* |  |
| `D2JB` | ui32 | 4 | R|CONST | 7500 | *未收录* |  |
| `D2JD` | {jst | 17 | R|CONST | 00 00 00 00 00 00 00 00 1D 4C 1D 4C 1D 4C 00 00 00 | *未收录* |  |
| `D2JR` | ui32 | 4 | W|CONST |  | *未收录* |  |
| `D2JS` | ui32 | 4 | W|CONST |  | *未收录* |  |
| `D2LR` | ui8  | 1 | R | 01 | *未收录* |  |
| `D2VC` | ui16 | 2 | R|W|CONST | 0 | *未收录* |  |
| `D2VM` | ui16 | 2 | R | 0 | *未收录* |  |
| `D2VR` | ui16 | 2 | R | 0 | *未收录* |  |
| `D2VX` | ui16 | 2 | R | 0 | *未收录* |  |
| `D2if` | ch8* | 12 | R | 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D2ih` | ch8* | 12 | R | 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D2ii` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D2im` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D2in` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D2is` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D3CA` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `D3CD` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `D3CR` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `D3Cd` | hex_ | 14 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D3IC` | ui16 | 2 | R|W|CONST | 65535 | *未收录* |  |
| `D3IR` | ui16 | 2 | R | 0 | *未收录* |  |
| `D3JA` | ui32 | 4 | R|CONST | 7500 | *未收录* |  |
| `D3JB` | ui32 | 4 | R|CONST | 7500 | *未收录* |  |
| `D3JD` | {jst | 17 | R|CONST | 00 00 00 00 00 00 00 00 1D 4C 1D 4C 1D 4C 00 00 00 | *未收录* |  |
| `D3JR` | ui32 | 4 | W|CONST |  | *未收录* |  |
| `D3JS` | ui32 | 4 | W|CONST |  | *未收录* |  |
| `D3LR` | ui8  | 1 | R | 01 | *未收录* |  |
| `D3VC` | ui16 | 2 | R|W|CONST | 0 | *未收录* |  |
| `D3VM` | ui16 | 2 | R | 0 | *未收录* |  |
| `D3VR` | ui16 | 2 | R | 0 | *未收录* |  |
| `D3VX` | ui16 | 2 | R | 0 | *未收录* |  |
| `D3if` | ch8* | 12 | R | 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D3ih` | ch8* | 12 | R | 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D3ii` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D3im` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D3in` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D3is` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D4CA` | ui16 | 2 | R|W | 6 | *未收录* |  |
| `D4CD` | ui16 | 2 | R|W | 5 | *未收录* |  |
| `D4CR` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `D4Cd` | hex_ | 14 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D4IC` | ui16 | 2 | R|W|CONST | 65535 | *未收录* |  |
| `D4IR` | ui16 | 2 | R | 0 | AC port current (in mA) | VSMC |
| `D4JA` | ui32 | 4 | R|CONST | 15000 | *未收录* |  |
| `D4JB` | ui32 | 4 | R|CONST | 15000 | *未收录* |  |
| `D4JD` | {jst | 17 | R|CONST | 00 00 1D 4C 00 00 1D 4C 3A 98 3A 98 3A 98 00 00 01 | *未收录* |  |
| `D4JR` | ui32 | 4 | W|CONST |  | *未收录* |  |
| `D4JS` | ui32 | 4 | W|CONST |  | *未收录* |  |
| `D4LR` | ui8  | 1 | R | 01 | *未收录* |  |
| `D4VC` | ui16 | 2 | R|W|CONST | 0 | *未收录* |  |
| `D4VM` | ui16 | 2 | R | 0 | AC port minimal voltage (in mV) | VSMC |
| `D4VR` | ui16 | 2 | R | 0 | AC port voltage (in mV) | VSMC |
| `D4VX` | ui16 | 2 | R | 0 | AC port maximum voltage (in mV) | VSMC |
| `D4if` | ch8* | 12 | R | 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D4ih` | ch8* | 12 | R | 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D4ii` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D4im` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D4in` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `D4is` | ch8* | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `DBGE` | ui8  | 1 | R | 00 | *未收录* |  |
| `DBGP` | ui8  | 1 | R | 04 | *未收录* |  |
| `DBGT` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `DICT` | flag | 1 | R|W|CONST | 0 | *未收录* |  |
| `DID0` | hex_ | 4 | R | 18 05 01 01 | *未收录* |  |
| `DID1` | hex_ | 4 | R | 10 DC C8 34 | *未收录* |  |
| `DM0C` | hex_ | 2 | R|CONST | 03 03 | *未收录* |  |
| `DM0E` | flag | 1 | R|CONST|PRIVW | 1 | *未收录* |  |
| `DM0F` | ui8  | 1 | R | 00 | *未收录* |  |
| `DM0I` | {hdi | 14 | R|CONST | 02 06 01 03 03 03 00 0B 02 00 00 0B 00 00 | *未收录* |  |
| `DM0M` | ui8  | 1 | R | 00 | *未收录* |  |
| `DM0P` | ui8  | 1 | R|PRIVW | 02 | *未收录* |  |
| `DM0R` | ui8  | 1 | R | 00 | *未收录* |  |
| `DM0S` | ui8  | 1 | R|PRIVW | 02 | *未收录* |  |
| `DM0W` | ui8  | 1 | R | 00 | *未收录* |  |
| `DM0h` | ui8  | 1 | R|CONST | 49 | *未收录* |  |
| `DM0r` | ui8  | 1 | R | 00 | *未收录* |  |
| `DM0w` | ui8  | 1 | R | 00 | *未收录* |  |
| `DME!` | hex_ | 1 | R|PRIVW | 00 | *未收录* |  |
| `DMFR` | hex_ | 2 | R | 00 02 | *未收录* |  |
| `DMFV` | flag | 1 | R | 0 | *未收录* |  |
| `DMP!` | hex_ | 1 | R|PRIVW | 00 | *未收录* |  |
| `DMS!` | hex_ | 1 | R|PRIVW | 00 | *未收录* |  |
| `DPB0` | ui8  | 1 | R|W|CONST | 03 | *未收录* |  |
| `DPB1` | ui8  | 1 | R|W | 04 | *未收录* |  |
| `DPBR` | ui16 | 2 | R|W | 65535 | *未收录* |  |
| `DPLM` | {lim | 5 | CONST|PRIVW |  | *未收录* |  |
| `DSAS` | flag | 1 | R|W | 0 | *未收录* |  |
| `DT0A` | sp78 | 2 | R | 95 | *未收录* |  |
| `DT0B` | sp78 | 2 | R | 77 | *未收录* |  |
| `DT0C` | sp78 | 2 | R | 77 | *未收录* |  |
| `DT0a` | sp78 | 2 | R | 25 | *未收录* |  |
| `DT0b` | sp78 | 2 | R | 3 | *未收录* |  |
| `DT0c` | sp78 | 2 | R | 3 | *未收录* |  |
| `DULI` | ui8  | 1 | W |  | *未收录* |  |
| `DULT` | ui8  | 1 | R | 46 | *未收录* |  |
| `DURR` | ui8  | 1 | R|W | 04 | *未收录* |  |
| `DUST` | ui8  | 1 | R|CONST | 00 | *未收录* |  |
| `DUTC` | flag | 1 | R | 1 | *未收录* |  |
| `DUTI` | ui8  | 1 | W |  | *未收录* |  |
| `DUTT` | ui8  | 1 | R | 2D | *未收录* |  |
| `ECIP` | hex_ | 19 | PRIVW |  | *未收录* |  |
| `ECIT` | ui8  | 1 | CONST|PRIVW |  | *未收录* |  |
| `EECC` | hex_ | 4 | W|CONST |  | *未收录* |  |
| `EECT` | ui32 | 4 | R | 4294967295 | *未收录* |  |
| `ENV0` | ui8  | 1 | R | 03 | *未收录* |  |
| `EPCA` | hex_ | 4 | R | 00 00 B0 00 | *未收录* |  |
| `EPCF` | flag | 1 | R|CONST | 1 | *未收录* |  |
| `EPCI` | hex_ | 4 | R | 09 A0 F0 00 | *未收录* |  |
| `EPCV` | ui16 | 2 | R | 1 | *未收录* |  |
| `EPMA` | ch8* | 4 | R|CONST | 00 00 A8 10 | *未收录* |  |
| `EPMI` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `EPUA` | hex_ | 4 | R | 00 00 A8 00 | *未收录* |  |
| `EPUF` | flag | 1 | R|CONST | 1 | *未收录* |  |
| `EPUI` | hex_ | 4 | R | 09 A0 00 01 | *未收录* |  |
| `EPUV` | ui16 | 2 | R | 1 | *未收录* |  |
| `ERCE` | flag | 1 | R | 0 | *未收录* |  |
| `ERCR` | ui16 | 2 | R|W | 10 | *未收录* |  |
| `ERCS` | flag | 1 | R|W | 0 | *未收录* |  |
| `EVCT` | hex_ | 2 | R|W|CONST | 0D 0D | *未收录* |  |
| `EVHF` | ch8* | 28 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `EVMD` | hex_ | 4 | W|CONST |  | *未收录* |  |
| `EVRD` | ch8* | 32 | R|CONST | F6 06 02 00 00 40 49 C8 71 05 16 00 25 40 49 C8 64 10 7E 00 2E 40 45 E3 64 10 70 00 70 40 49 C8 | *未收录* |  |
| `EVSL` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `EVVR` | ui8  | 1 | R | 01 | *未收录* |  |
| `FNum` | ui8  | 1 | R | 02 | *未收录* |  |
| `FOff` | ui8  | 1 | R | 00 | *未收录* |  |
| `FPDc` | fp79 | 2 | R|W|CONST | 43 F8 | *未收录* |  |
| `FRmn` | ui16 | 2 | R|PRIVW | 0 | *未收录* |  |
| `FRmp` | ui16 | 2 | R | 0 | *未收录* |  |
| `FS! ` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `FSDc` | ui16 | 2 | R|W|CONST | 29952 | *未收录* |  |
| `G3AO` | ui32 | 4 | R|PRIVW | 0 | *未收录* |  |
| `G3NO` | ui8  | 1 | PRIVW |  | *未收录* |  |
| `G3WD` | flag | 1 | R|W | 0 | *未收录* |  |
| `GT0C` | ui16 | 2 | R | 0 | *未收录* |  |
| `GT0R` | hex_ | 1 | R|W | 00 | *未收录* |  |
| `GTH!` | hex_ | 1 | R|CONST|PRIVW | 00 | *未收录* |  |
| `GTHR` | hex_ | 1 | R|CONST|PRIVW | 00 | *未收录* |  |
| `HBKP` | ch8* | 32 | R|W | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `HBKT` | ui32 | 4 | R|W | 345600 | *未收录* |  |
| `HC0N` | ui8  | 1 | R|W | 10 | *未收录* |  |
| `HC0T` | ui8  | 1 | R|W | 01 | *未收录* |  |
| `HDBS` | ui8  | 1 | R | 01 | *未收录* |  |
| `HDST` | hex_ | 4 | R | 00 00 00 00 | *未收录* |  |
| `HDSW` | hex_ | 4 | R | 00 03 00 03 | *未收录* |  |
| `HE0N` | ui8  | 1 | R|W|CONST | 08 | *未收录* |  |
| `HE0T` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `HE1N` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `HE1T` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `HI0N` | ui8  | 1 | R|W|CONST | 10 | *未收录* |  |
| `HI0T` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `HI1N` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `HI1T` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `HNVD` | ui16 | 2 | R | 0 | *未收录* |  |
| `I18C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `KDD0` | hex_ | 32 | R | C7 07 7B 6D AB E7 07 F1 5D 9D E5 F4 AE 93 FC 42 FE E4 EB D7 35 04 4B 77 47 5E 24 14 00 00 67 85 | *未收录* |  |
| `KDD1` | hex_ | 32 | R | 00 00 00 00 00 00 8F AC 00 00 00 00 A1 3B 10 0D 74 73 00 00 00 00 0D 78 00 00 00 00 E4 81 00 00 | *未收录* |  |
| `KDFT` | ui8  | 1 | R | 04 | *未收录* |  |
| `KDTP` | ui8  | 1 | R | 01 | *未收录* |  |
| `KINC` | ui16 | 2 | R | 11 | *未收录* |  |
| `KINV` | ch8* | 4 | R|W|CONST | 4C 4B 53 42 | *未收录* |  |
| `KINX` | hex_ | 4 | R | 4C 4B 53 42 | *未收录* |  |
| `LAcN` | ui8  | 1 | CONST|PRIVW |  | *未收录* |  |
| `LAtN` | ui16 | 2 | CONST|PRIVW |  | *未收录* |  |
| `LC2D` | ui16 | 2 | R | 19670 | *未收录* |  |
| `LC2E` | ui16 | 2 | R | 19670 | *未收录* |  |
| `LCCC` | ui8  | 1 | R | 00 | *未收录* |  |
| `LCCN` | ui8  | 1 | R | D5 | *未收录* |  |
| `LCCQ` | ui8  | 1 | R | 5F | *未收录* |  |
| `LCKA` | ui8  | 1 | R | B4 | *未收录* |  |
| `LCKN` | ui8  | 1 | R | D3 | *未收录* |  |
| `LCLD` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `LCLG` | ui8  | 1 | R | 00 | *未收录* |  |
| `LCSA` | ui8  | 1 | R | EA | *未收录* |  |
| `LCTN` | ui8  | 1 | R | EB | *未收录* |  |
| `LCTQ` | ui8  | 1 | R | 54 | *未收录* |  |
| `LDEN` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `LDI2` | ui8  | 1 | R|W|CONST | 01 | *未收录* |  |
| `LDKN` | ui8  | 1 | R | 02 | Returns device generation, which is 2 for new SMC. The key is missing on old SMC but is supposed to be 1. | VSMC |
| `LDLG` | ui8  | 1 | W|CONST |  | Log Dialogue | VSMC |
| `LDLT` | ui8  | 1 | R | 01 | *未收录* |  |
| `LDS4` | ui8  | 1 | R | 00 | *未收录* |  |
| `LDSB` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `LDSD` | ui8  | 1 | R|CONST | 03 | *未收录* |  |
| `LDSE` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `LDSP` | flag | 1 | W |  | *未收录* |  |
| `LDSS` | sp78 | 2 | R|W | 0 | *未收录* |  |
| `LDT1` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `LDT2` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `LDT4` | ui32 | 4 | R|W | 0 | *未收录* |  |
| `LDTF` | ui16 | 2 | R|W|CONST | 0 | *未收录* |  |
| `LDWE` | hex_ | 4 | R | 00 00 24 01 | *未收录* |  |
| `LIDB` | flag | 1 | R | 0 | *未收录* |  |
| `LPMC` | ui32 | 4 | R|W | 42 | *未收录* |  |
| `LVME` | flag | 1 | CONST|PRIVW |  | *未收录* |  |
| `LVMS` | ui8  | 1 | PRIVW |  | *未收录* |  |
| `MACA` | hex_ | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MACM` | flag | 1 | R|W | 1 | *未收录* |  |
| `MACR` | ch8* | 32 | R|CONST |  | *未收录* |  |
| `MD0R` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `MD0W` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `MDr!` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `MDw!` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `MFIX` | ui8  | 1 | R | 00 | *未收录* |  |
| `MT0c` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT0g` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT0t` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT1c` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT1g` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT1t` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT2c` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT2g` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT2t` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT3c` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT3g` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MT3t` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MTBc` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MTBg` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MTBt` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `NACK` | ui8  | 1 | PRIVW |  | *未收录* |  |
| `NATJ` | ui8  | 1 | R|W | 00 | Ninja Action Timer Job | VSMC |
| `NATi` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `NOPB` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `NTOK` | ui8  | 1 | W|CONST |  | Interrupt OK | VSMC |
| `ONMI` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `OOBC` | ui16 | 2 | R|CONST|PRIVW | 0 | *未收录* |  |
| `OOBR` | hex_ | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `OOBS` | hex_ | 32 | R | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `OOBT` | hex_ | 2 | R|CONST|PRIVW | 00 00 | *未收录* |  |
| `OOBW` | hex_ | 21 | R|CONST|PRIVW | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `OSSS` | hex_ | 32 | R|W | 00 53 33 3A 52 65 73 75 6D 65 45 6E 64 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `OSWD` | ui16 | 2 | R|W | 0 | Operating System Watchdog Reboot timer | VSMC |
| `P18C` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PB0L` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `PBLC` | flt  | 4 | R|W | 00 00 00 00 | Backlight Input Power   (Watts) (PBLC) / Backlight Input Power  (DEBUG) (Watts) (PBLC); Battery Rail [Power] | libSMC+iSMC |
| `PBTC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `QCEC` | si8  | 1 | R | 00 | *未收录* |  |
| `QCFE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QCHA` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QCHE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QCHa` | si8  | 1 | R | FF | *未收录* |  |
| `QCLA` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QCLE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QCLV` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `QCLa` | si8  | 1 | R | FF | *未收录* |  |
| `QCLv` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QCPE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QECI` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `QECS` | ui8  | 1 | R | 00 | *未收录* |  |
| `QENA` | flag | 1 | R|W|CONST | 0 | *未收录* |  |
| `QENN` | flag | 1 | R|PRIVW | 1 | *未收录* |  |
| `QERI` | hex_ | 4 | R|CONST|PRIVW | 00 00 00 00 | *未收录* |  |
| `QERR` | hex_ | 4 | R | 00 00 00 00 | *未收录* |  |
| `QERS` | hex_ | 4 | R|W|CONST | 00 00 00 00 | *未收录* |  |
| `QFL!` | hex_ | 1 | R|PRIVW | 00 | *未收录* |  |
| `QGEC` | si8  | 1 | R | 00 | *未收录* |  |
| `QGFE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QGHA` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QGHE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QGHa` | si8  | 1 | R | FF | *未收录* |  |
| `QGLA` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QGLE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QGLV` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `QGLa` | si8  | 1 | R | FF | *未收录* |  |
| `QGLv` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QGPE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QIEC` | si8  | 1 | R | 00 | *未收录* |  |
| `QIFE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QIHA` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QIHE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QIHa` | si8  | 1 | R | FF | *未收录* |  |
| `QILA` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QILE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QILV` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `QILa` | si8  | 1 | R | FF | *未收录* |  |
| `QILv` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QIPE` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `QVER` | ui8  | 1 | R | 01 | *未收录* |  |
| `RBr ` | ch8* | 8 | R | 32 30 31 37 6D 62 70 00 | *未收录* |  |
| `REV ` | {rev | 6 | R|CONST | 02 45 0F 00 00 05 | *未收录* |  |
| `RMde` | char | 1 | R | 41 | *未收录* |  |
| `RPlt` | ch8* | 8 | R | 6A 38 30 67 61 00 00 00 | *未收录* |  |
| `RSvn` | ui32 | 4 | R | 0 | *未收录* |  |
| `RVBF` | {rev | 6 | R|CONST | 02 45 0F 00 00 05 | *未收录* |  |
| `RVCR` | {rev | 6 | R|CONST | FF FF FF FF FF FF | *未收录* |  |
| `RVUF` | {rev | 6 | R|CONST | 02 45 0F 00 00 05 | *未收录* |  |
| `SAS!` | hex_ | 4 | R|CONST|PRIVW | 00 FF FF FF | *未收录* |  |
| `SBF ` | hex_ | 4 | R | 00 00 00 00 | *未收录* |  |
| `SBFC` | flag | 1 | PRIVW |  | *未收录* |  |
| `SBFD` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `SBFE` | flag | 1 | R|W | 1 | *未收录* |  |
| `SBFF` | hex_ | 4 | R | 00 00 00 00 | *未收录* |  |
| `SBFL` | hex_ | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `SBFN` | hex_ | 4 | R | 00 00 00 00 | *未收录* |  |
| `SBFU` | hex_ | 4 | R|PRIVW | 00 00 07 00 | *未收录* |  |
| `SBFV` | hex_ | 4 | R | 00 00 3F FF | *未收录* |  |
| `SBS!` | ui16 | 2 | PRIVW |  | *未收录* |  |
| `SBSF` | ui32 | 4 | PRIVW |  | *未收录* |  |
| `SCIA` | ui16 | 2 | R|W|CONST | 1016 | *未收录* |  |
| `SCII` | ui8  | 1 | R|W|CONST | 03 | *未收录* |  |
| `SCIL` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `SCXC` | sp78 | 2 | R | 100 | *未收录* |  |
| `SDAF` | ui16 | 2 | R|PRIVW | 0 | *未收录* |  |
| `SDAS` | ui16 | 2 | R|PRIVW | 0 | *未收录* |  |
| `SECD` | flag | 1 | R|CONST | 0 | *未收录* |  |
| `SECN` | ch8* | 32 | R|CONST | 15 D2 CB AD 93 4A 15 08 D9 12 9B FA E9 01 6D 89 F4 A9 B8 4B 3A 34 DC E0 C7 0E 3F 8B 2D 35 E1 E8 | *未收录* |  |
| `SECS` | ch8* | 32 | W|CONST |  | *未收录* |  |
| `SECl` | flag | 1 | R | 0 | *未收录* |  |
| `SECs` | flag | 1 | R | 0 | *未收录* |  |
| `SFBR` | ui8  | 1 | R|W | 04 | Fan cooling preference bit mask | VSMC |
| `SIP ` | ui8  | 1 | R|W|CONST | 01 | *未收录* |  |
| `SIPB` | flag | 1 | R | 0 | *未收录* |  |
| `SIS!` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `SIT!` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `SLP0` | ui8  | 1 | R | 00 | *未收录* |  |
| `SLPE` | ui8  | 1 | R|CONST|PRIVW | 00 | *未收录* |  |
| `SLPS` | flag | 1 | R|W | 0 | *未收录* |  |
| `SMBC` | hex_ | 6 | PRIVW |  | *未收录* |  |
| `SMBG` | ui8  | 1 | CONST|PRIVW |  | *未收录* |  |
| `SMBR` | hex_ | 32 | ? |  | *未收录* |  |
| `SMBS` | hex_ | 2 | PRIVW |  | *未收录* |  |
| `SMBW` | hex_ | 32 | PRIVW |  | *未收录* |  |
| `SPDO` | ui8  | 1 | R|W|CONST | 01 | *未收录* |  |
| `SPF!` | hex_ | 1 | PRIVW |  | *未收录* |  |
| `SPH0` | ui16 | 2 | R | 0 | *未收录* |  |
| `SPHE` | ui8  | 1 | R|CONST|PRIVW | 00 | *未收录* |  |
| `SPHR` | hex_ | 4 | R | 00 00 00 00 | *未收录* |  |
| `SPHS` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `SPHT` | ui16 | 2 | R | 0 | *未收录* |  |
| `SPHZ` | ui8  | 1 | W|CONST |  | *未收录* |  |
| `SPP!` | hex_ | 1 | PRIVW |  | *未收录* |  |
| `SPT!` | hex_ | 1 | PRIVW |  | *未收录* |  |
| `SPU!` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `SPV!` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `SPW!` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `SRS!` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `SRT!` | hex_ | 4 | R|PRIVW | 00 00 00 00 | *未收录* |  |
| `STDK` | ui16 | 2 | R | 0 | *未收录* |  |
| `STFD` | flag | 1 | R | 0 | *未收录* |  |
| `SWER` | hex_ | 1 | R | 00 | *未收录* |  |
| `UFIS` | hex_ | 4 | R|CONST | 00 00 00 00 | *未收录* |  |
| `UREV` | ui8  | 1 | R | B1 | *未收录* |  |
| `URPP` | hex_ | 4 | R | 00 00 00 33 | *未收录* |  |
| `URWD` | hex_ | 4 | R | 00 00 00 00 | *未收录* |  |
| `USR0` | hex_ | 4 | R | FF FF FF FF | *未收录* |  |
| `USR1` | hex_ | 4 | R | FF FF FF FF | *未收录* |  |
| `USR2` | hex_ | 4 | R | FF FF FF FF | *未收录* |  |
| `USR3` | hex_ | 4 | R | 06 34 10 90 | *未收录* |  |
| `WCPD` | ui8  | 1 | R | 00 | *未收录* |  |
| `WCPW` | ui8  | 1 | R | 01 | *未收录* |  |
| `WKEN` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `WKTP` | ui8  | 1 | R|W|CONST | 00 | Wake Power Type | VSMC |
| `WOr0` | si8  | 1 | R | 02 | *未收录* |  |
| `WOw0` | si8  | 1 | R | FF | *未收录* |  |
| `WPo0` | si8  | 1 | R | FF | *未收录* |  |
| `ZPEN` | hex_ | 1 | R|CONST|PRIVW | 0F | *未收录* |  |
| `hSci` | ui8  | 1 | CONST|PRIVW |  | *未收录* |  |
| `hSdn` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `hSei` | ui16 | 2 | CONST|PRIVW |  | *未收录* |  |
| `hSia` | ui16 | 2 | CONST|PRIVW |  | *未收录* |  |
| `hSii` | ui8  | 1 | CONST|PRIVW |  | *未收录* |  |
| `hSij` | ui8  | 1 | CONST|PRIVW |  | *未收录* |  |
| `hSta` | hex_ | 3 | R|CONST | 01 01 02 | *未收录* |  |
| `hSup` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `tt11` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `tt12` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `tt13` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `tt14` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `tt15` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `tt16` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `tt17` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `tt18` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `tt1j` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `ttcm` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `ttcn` | ui8  | 1 | R|W | 00 | *未收录* |  |

## 杂项状态 (Misc Status) — 126 键

| 键 | 类型 | 长 | R/W | 当前值 | 含义 | 来源 |
|---|---|---|---|---|---|---|
| `MSAL` | hex_ | 1 | R|CONST|PRIVW | 4B | *未收录* |  |
| `MSAc` | fp88 | 2 | R | 0 | *未收录* |  |
| `MSAf` | fp6a | 2 | R | 00 00 | *未收录* |  |
| `MSAg` | fp88 | 2 | R | 0 | *未收录* |  |
| `MSAi` | fp88 | 2 | R | 0 | *未收录* |  |
| `MSBC` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `MSBP` | ui16 | 2 | R|W | 488 | *未收录* |  |
| `MSBc` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `MSBp` | ui16 | 2 | R|W | 488 | *未收录* |  |
| `MSC0` | ui32 | 4 | R|W | 0 | *未收录* |  |
| `MSDW` | flag | 1 | W|CONST |  | *未收录* |  |
| `MSEP` | flag | 1 | R|W | 0 | *未收录* |  |
| `MSF0` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `MSF1` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSF2` | ui8  | 1 | R|CONST|PRIVW | 00 | *未收录* |  |
| `MSF8` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSF9` | ui8  | 1 | R | 00 | *未收录* |  |
| `MSFA` | ch8* | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MSFB` | ch8* | 32 | CONST|PRIVW |  | *未收录* |  |
| `MSFC` | ui8  | 1 | R|CONST|PRIVW | 00 | *未收录* |  |
| `MSFD` | ch8* | 32 | R|PRIVW | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `MSFE` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSFL` | ch8* | 4 | R|W|CONST | 00 00 00 00 | *未收录* |  |
| `MSFM` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `MSFN` | ui8  | 1 | R|CONST|PRIVW | 00 | *未收录* |  |
| `MSFV` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `MSFW` | flag | 1 | R|W | 0 | Set to 1 when the previously reported [MSPP] value was below 7. | VSMC |
| `MSG3` | flag | 1 | R|W|CONST | 0 | *未收录* |  |
| `MSGA` | fp6a | 2 | R | 00 00 | *未收录* |  |
| `MSLB` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `MSLC` | ui8  | 1 | R | 00 | *未收录* |  |
| `MSLE` | ui8  | 1 | R|W | 01 | *未收录* |  |
| `MSLF` | ui8  | 1 | R | 00 | *未收录* |  |
| `MSLG` | ui8  | 1 | R | 00 | *未收录* |  |
| `MSLP` | ui8  | 1 | R | 00 | *未收录* |  |
| `MSLS` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSN0` | ui16 | 2 | R|W | 2 | *未收录* |  |
| `MSPA` | fp6a | 2 | R | 00 00 | *未收录* |  |
| `MSPB` | flag | 1 | R | 0 | *未收录* |  |
| `MSPC` | ui8  | 1 | R|W|CONST | 10 | Reads and writes the number of available "Power states" for the CPU. | VSMC |
| `MSPE` | ui8  | 1 | R|W|CONST | 08 | *未收录* |  |
| `MSPI` | ui8  | 1 | R|W|CONST | 10 | *未收录* |  |
| `MSPP` | ui8  | 1 | R | 00 | System Power State (from BridgeOS _PE_smc_stashed_x86_power_state) | VSMC |
| `MSPR` | ui16 | 2 | R | 1 | Abstract System State (from BridgeOS _PE_smc_stashed_x86_system_state) | VSMC |
| `MSPS` | hex_ | 2 | R|W | 00 02 | Consists of current and previous [MSPP] condition. E.g. 00 01 or 00 04. Could also be ui16. | VSMC |
| `MSPT` | hex_ | 5 | R|CONST | 11 10 00 09 08 | *未收录* |  |
| `MSQC` | hex_ | 2 | R | 01 30 | Full power nap / silent run state, defaults to zero. See [WKTP]. | VSMC |
| `MSQL` | ui8  | 1 | R | 00 | *未收录* |  |
| `MSQM` | ui8  | 1 | R | 04 | *未收录* |  |
| `MSQN` | flag | 1 | R|W | 0 | *未收录* |  |
| `MSQO` | ui8  | 1 | R|W|CONST | 78 | *未收录* |  |
| `MSQS` | flag | 1 | R|W | 0 | *未收录* |  |
| `MSQW` | ui16 | 2 | R | 0 | *未收录* |  |
| `MSRC` | hex_ | 4 | R | 00 00 00 02 | *未收录* |  |
| `MSSD` | si8  | 1 | R|W|CONST | 9A | Stores the last known Shut-down cause. | VSMC |
| `MSSF` | hex_ | 4 | R|W|CONST | 00 00 00 00 | *未收录* |  |
| `MSSG` | hex_ | 4 | R | 00 00 00 00 | *未收录* |  |
| `MSSP` | si8  | 1 | R|W | 05 | *未收录* |  |
| `MSSR` | flag | 1 | R|PRIVW | 1 | *未收录* |  |
| `MSSS` | {mss | 1 | R | 00 | *未收录* |  |
| `MSSW` | flag | 1 | R|W | 0 | Works as a flag influencing [MSSD] interpretation in AppleSMC::smcPublishShutdownCause. | VSMC |
| `MSTD` | ui16 | 2 | R|W | 0 | *未收录* |  |
| `MSTS` | ui8  | 1 | R|W|CONST | 01 | *未收录* |  |
| `MSTc` | ui8  | 1 | R | 00 | CPU Plimit (MSTc) | libSMC |
| `MSTe` | ui8  | 1 | R | 00 | *未收录* |  |
| `MSTf` | ui8  | 1 | R | 00 | Forced Idle Limit (MSTf) | libSMC |
| `MSTi` | ui8  | 1 | R | 00 | 2nd GPU Plimit (MSTi) | libSMC |
| `MSTj` | ui8  | 1 | R | 00 | *未收录* |  |
| `MSWA` | fp6a | 2 | R|CONST|PRIVW | 00 00 | *未收录* |  |
| `MSWE` | ui8  | 1 | R|CONST|PRIVW | 00 | *未收录* |  |
| `MSWF` | ui16 | 2 | R|PRIVW | 1000 | *未收录* |  |
| `MSWO` | ui16 | 2 | R|PRIVW | 1000 | *未收录* |  |
| `MSWP` | ui8  | 1 | R|PRIVW | 00 | *未收录* |  |
| `MSWd` | flag | 1 | R|W | 1 | *未收录* |  |
| `MSWo` | flag | 1 | R|W|CONST | 1 | *未收录* |  |
| `MSWr` | ui8  | 1 | R | 72 | Machine State Wake Reason (?) | VSMC |
| `MSX0` | ui16 | 2 | R|W | 12336 | *未收录* |  |
| `MSX2` | ui8  | 1 | R|W|CONST | 0C | *未收录* |  |
| `MSX9` | ui8  | 1 | R | 30 | *未收录* |  |
| `MSXA` | ch8* | 4 | R|W | 50 53 54 52 | *未收录* |  |
| `MSXC` | ch8* | 4 | R|CONST | 00 00 00 00 | *未收录* |  |
| `MSXD` | ch8* | 16 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `MSXH` | ch8* | 16 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `MSXK` | ch8* | 32 | R|CONST | 21 22 23 24 27 28 29 2A 2B 2D 2E 2F 30 31 32 E8 EF 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `MSXL` | ch8* | 16 | R|CONST | 00 00 FD E8 00 00 FD E8 00 00 FD E8 00 00 FD E8 | *未收录* |  |
| `MSXN` | ui8  | 1 | R | 31 | *未收录* |  |
| `MSXP` | ui32 | 4 | R|W | 0 | *未收录* |  |
| `MSXS` | ch8* | 4 | R|W|CONST | 01 02 03 04 | *未收录* |  |
| `MSXT` | ui32 | 4 | R|W | 4294967295 | *未收录* |  |
| `MSXb` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `MSXc` | ch8* | 4 | R|CONST | 00 00 00 00 | *未收录* |  |
| `MSXd` | ch8* | 16 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `MSXh` | ch8* | 16 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `MSXk` | ch8* | 32 | R|CONST | 21 22 23 24 25 26 27 28 29 2A 2B 2C 2D 2E 2F 30 31 32 33 34 35 36 38 39 3A 3B 3C 3D 3E 3F 40 41 | *未收录* |  |
| `MSXl` | ch8* | 16 | R|CONST | 28 6B 6E 4E 28 6B 6E 4E 28 6B 6E 4E 28 6B 6E 4E | *未收录* |  |
| `MSXm` | ui16 | 2 | R|W|CONST | 8 | *未收录* |  |
| `MSXn` | ui8  | 1 | R | 5C | *未收录* |  |
| `MSXs` | hex_ | 4 | R|W|CONST | 01 02 03 04 | *未收录* |  |
| `MSXt` | ch8* | 16 | R|CONST | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 | *未收录* |  |
| `MSa!` | hex_ | 1 | R|W | 00 | *未收录* |  |
| `MSac` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSaf` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSag` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSah` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSai` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSap` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSaw` | ui8  | 1 | R|W | 00 | *未收录* |  |
| `MSbo` | flag | 1 | R|W|CONST | 1 | *未收录* |  |
| `MSt0` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `MSt1` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `MSt2` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `MSt3` | flt  | 4 | R | 00 00 00 00 | *未收录* |  |
| `mSTC` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `mSTD` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `mSTK` | flt  | 4 | R|W | 00 00 00 00 | *未收录* |  |
| `zBDC` | flag | 1 | R|CONST | 0 | *未收录* |  |
| `zDBG` | ui8  | 1 | R|W|CONST | 03 | *未收录* |  |
| `zDBI` | flag | 1 | W |  | *未收录* |  |
| `zDSF` | flag | 1 | R|PRIVW | 0 | *未收录* |  |
| `zKLG` | hex_ | 5 | R|CONST|PRIVW | FF FF FF FF FF | *未收录* |  |
| `zKLI` | hex_ | 2 | R|CONST|PRIVW | FF FF | *未收录* |  |
| `zLDG` | hex_ | 5 | R|CONST|PRIVW | FF FF FF FF FF | *未收录* |  |
| `zLDI` | hex_ | 2 | CONST|PRIVW |  | *未收录* |  |
| `zMOJ` | flag | 1 | R|W | 1 | *未收录* |  |
| `zRS0` | ui8  | 1 | R|W|CONST | 00 | *未收录* |  |
| `zSEN` | flag | 1 | R|CONST|PRIVW | 1 | *未收录* |  |
