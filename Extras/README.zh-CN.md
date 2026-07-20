# Kerbalism Extras

这些可选 CFG 包不属于默认的 `GameData` 树。每个 Extra 都是独立的 GameData 文件夹，任意 Kerbalism 配置包均可使用。

English: [README.md](README.md).

## 安装

将所选 Extra 文件夹复制到 KSP 的 `GameData` 目录。例如：`Extras/KerbalismSystemHeatCore` → `GameData/KerbalismSystemHeatCore`。

安装 `KerbalismSystemHeatCompat` 时，需要同时安装 `KerbalismSystemHeatCore`。

Compat 内补丁按目标 mod 常见的 GameData 文件夹名分组。`Squad` 对应原版零件。

## 兼容性

Kerbalism 的 SystemHeat 包为**独占**方案。请**勿**与上游 SystemHeat Extras 混装：

- `SystemHeatFissionEngines`
- `SystemHeatFissionReactors`
- `SystemHeatIonEngines`
- `SystemHeatConverters`
- `SystemHeatHarvesters`
- `SystemHeatCryoTanks` / `SystemHeatBoiloff`
- 旧版 `Kerbalism-SystemHeat`

CKAN 已将这些包标为冲突。手动混装会导致零件被重复打补丁。

## 包说明

- `KerbalismSystemHeatCore`：通用 SystemHeat 桥接（原版转换器 / 采集器 / 散热器、Kerbalism 钻机与化工厂、规划器支持、旧模块迁移等）。
- `KerbalismSystemHeatCompat`：第三方与原版零件支持，内容如下：

  | 路径 | 用途 |
  |------|------|
  | `DynamicRadiation.cfg` | SH 裂变反应堆 / 引擎的功率缩放辐射 |
  | `Squad/LV-N.cfg` | 原版 LV-N → SH 裂变引擎 |
  | `Squad/Dawn.cfg` | 原版 Dawn → SH 离子引擎热回路 |
  | `AtomicAge` | SH 散热器 + 裂变 NTR 桥接 |
  | `Buffalo` | SAFER 反应堆的 SH / Kerbalism 桥接 |
  | `CryoTanks` | EC 冷却 → `ModuleSystemHeatCryoTank` + Kerbalism updater |
  | `FelineUtilityRover` | FUR 转换器 / 采集器 SH 桥接 |
  | `HeatControl` | Heat Control 散热器 SH 桥接 |
  | `KerbalAtomics` | KA NTR 的 SH 转换 |
  | `MissingHistory` | MissingHistory NTR SH / Kerbalism 桥接 |
  | `NearFutureAeronautics` | NFA 原子喷气 SH / Kerbalism 桥接 |
  | `NearFuturePropulsion` | NFP 离子引擎的 SH 转换 |
  | `PlanetaryBaseInc` | KPBS 核反应堆 SH 桥接 |
  | `RestockPlus` | Cherenkov 的 SH 裂变引擎 |
  | `SpaceDust` | SpaceDust 采集器 SH 桥接 |
  | `USI` | USI 反应堆 / FTT / Kontainer SH 桥接 |

Near Future Electrical、Far Future Technologies 与 Sterling Systems 的 Kerbalism（含 SystemHeat）支持仍在主包 `KerbalismConfig` 中——勿再安装 Jade 自带的 `SterlingSystemsKerbalism`。若需要 Kerbalism 对通用散热器的后台处理，或其它零件的 SystemHeat 感知 ISRU 集成，请安装 `KerbalismSystemHeatCore`。

## 归属

随附的 CryoTanks、裂变引擎与离子引擎转换补丁改编自 SystemHeat revision `5f75a20af915ffe465949007af0de1131f745127`，按 SystemHeat 的 MIT 许可再分发；详见 `SystemHeat-LICENSE.md`。
