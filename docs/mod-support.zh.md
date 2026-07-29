---
hide:
  - navigation
---

# 模组兼容

!!! warning "这不是保修单"
    Kerbalism 最适合**模组少一点、尺度偏原版**的玩法。本页只说明主仓库里**已经有补丁或代码集成**的地方，不保证每种组合都没 bug。大型「未来科技」向列表（KSPIE、一堆 NFT/FFT/USI-MKS、星际行星包、某些巨型部件包）风险更高。欢迎提交兼容补丁的 PR。

## 为什么要动别人的模组？

Kerbalism 得在**未加载飞船**上继续跑生命维持、ISRU、科研、天线等。原版和多数模组只在当前船跑模块。为了后台也对得上账，Kerbalism 常常**替换或接管**模块——于是和其他生命维持、后台资源、科研大修模组很容易撞车。

## 明确不兼容

代码里默认的 `ModsIncompatible` 包括其他生命维持栈：

- TacLifeSupport
- Snacks
- KolonyTools
- USILifeSupport

别和官方生命维持配置叠在一起。

科研重叠警告还包括 `[x] Science!` 和 KEI（功能大量重复）。

## 明确不支持（wontfix）

这些**不会**在启动时被 `ModsIncompatible` 硬拦，但上游已标为 **wontfix**：不要指望官方支持补丁，与 Kerbalism 同装风险自负。

| 模组 | Issue | 原因 |
| --- | --- | --- |
| **Pathfinder**（Wild Blue Industries） | [#428](https://github.com/Kerbalism/Kerbalism/issues/428)、[#706](https://github.com/Kerbalism/Kerbalism/issues/706) | 与 Kerbalism 的栖息地、工艺、科学实验室功能重叠严重。Kerbalism 去掉原版 `ModuleScienceLab` 后，`WBIScienceConverter` 等自定义模块会坏掉（PAW 刷 NRE）。完整支持成本高、hack 多、难维护——和 USI MKS 同一档。别处有 Buffalo / MOLE / WildBlueTools 的有限补丁，**不等于**支持 Pathfinder。 |

## 半兼容

可以和 Kerbalism 同装，但部分功能不会按原作者预期生效。有缺口，别当完整支持。

| 模组 | Issue | 说明 |
| --- | --- | --- |
| **Strategia** | [#757](https://github.com/Kerbalism/Kerbalism/issues/757) | 非科研类策略及模组其余部分可用。改科学产出的策略（如 Probe Frenzy：探针加成、传输 vs 回收差异）在 Kerbalism 科学管线下**不会**正确生效。Kerbalism 有意不做传输/回收科学分差；要修好需要大幅改 Strategia 一侧。 |

## DeepFreeze / BackgroundResources / TAC-LS

若加载了 `BackgroundResources`（常随 DeepFreeze 或 TAC-LS 一起），Kerbalism 会尝试关掉它的卸载船处理，并可能弹窗。这是**冲突**，不是「完整支持」。实践里别和 DeepFreeze 或 TAC-LS 同装。

## RemoteTech

天线 / 控制路径的集成代码还在，Settings.cfg 里也有 RT 专用耗电系数。但大型 RT 安装相对原版 CommNet **维护很差**。边角问题会有，请自己测。

## 官方 Support 补丁

补丁在官方配置包的 `GameData/KerbalismConfig/Support/` 下。有补丁只表示**有人贡献过集成**——质量参差不齐。

### 近未来 / 远未来及相关

| 方向 | 补丁 / 备注 |
| --- | --- |
| Near Future Electrical | `NFElectric.cfg`（主配置里还有 SystemHeat 相关） |
| Near Future Propulsion | `NFPropulsion.cfg` |
| NF Spacecraft / Exploration / Aeronautics / LV | `NFSpacecraft*.cfg`、`NFExploration_Science.cfg` 等 |
| Far Future Technologies | `FarFutureTechnologies.cfg`（SH Extras 另说） |
| CryoTanks / CryoEngines | `CryoTanks.cfg`、`CryoEnginesExtensions.cfg` |
| Kerbal Atomics / Atomic Age | 对应 `*.cfg` |
| SpaceDust、Dynamic Radiation 等 | 对应支持文件 |

### 栖息地 / 站点 / 部件

| 方向 | 补丁 / 备注 |
| --- | --- |
| SSPX | `SSPX.cfg`、`SSPX_Science.cfg` |
| HabTech / HabTech2 | `HabTech.cfg`、`HabTech2.cfg` |
| Bluedog Design Bureau | `Bluedog.cfg`（大包，仍常脆弱） |
| KPBS、Buffalo、MOLE / ALCOR 等 | 对应 `*.cfg` |
| Restock / ReStockPlus | `Restock.cfg`、`ReStockPlus*.cfg` |
| SXT、SSTU、Tantares、VSR、mK2… | 有匹配支持文件 |
| VABOrganizer | `VABOrganizer.cfg` |

### 科研 / 探测器

| 方向 | 补丁 / 备注 |
| --- | --- |
| SCANsat | `SCANsat.cfg` + `KerbalismScansat` |
| DMagic、Sounding Rockets 等 | 各类 `*_Science.cfg` |
| Universal Storage 2 | `UniversalStorage2.cfg` + 科研 |
| 原版 / Breaking Ground | `Squad_Science.cfg`、`BreakingGrounds_Science.cfg` |

### 工具与写实向

| 方向 | 补丁 / 备注 |
| --- | --- |
| RemoteTech | `RemoteTech.cfg` |
| RealAntennas / RealFuels 等 | 有对应配置（RO/RSS 玩家多用 ROKerbalism） |
| TweakScale、B9PartSwitch、CCK、CLS | 工具向补丁 |
| Kopernicus / 行星包 | 有辐射 / 天体挂钩≠全面支持 |
| TestFlight / RO / RP-1 | 与可选包 KerbalismEngineFailures 不兼容（CKAN 冲突）；请用它们自己的引擎可靠性。核心 Kerbalism MTBF Reliability 不受影响 |

### USI 与 Sterling Systems

- **USI** — `Support/USI/`（反应堆、FTT、Kontainers）。USI **生命维持** / Kolony 栈仍然不兼容。
- **Sterling Systems** — `Support/SterlingSystems/`（含 SystemHeat 感知转换器）。**不要**再装 Jade 另发的 `SterlingSystemsKerbalism` 包。

## 可选 Extras（SystemHeat）

不在默认 `GameData` 里。从仓库 [Extras](https://github.com/Kerbalism/Kerbalism/tree/master/Extras) 拷：

| 包 | 作用 |
| --- | --- |
| `KerbalismSystemHeatCore` | 通用 SystemHeat 桥接（转换器、采集器、散热器、规划器、迁移） |
| `KerbalismSystemHeatCompat` | 依赖 Core。覆盖原版与第三方部件（Squad LV-N/Dawn、AtomicAge、Buffalo、CryoTanks、FUR、HeatControl、KerbalAtomics、NFA/NFP、KPBS、RestockPlus Cherenkov、SpaceDust、USI……） |

**互斥：** 不要和上游 SystemHeat Extras 混装（`SystemHeatFissionEngines`、`SystemHeatFissionReactors`、`SystemHeatIonEngines`、`SystemHeatConverters`、`SystemHeatHarvesters`、`SystemHeatCryoTanks` / 沸腾，或旧版 `Kerbalism-SystemHeat`）。混装等于双重打补丁。CKAN 会标冲突。

NFE、FFT、Sterling Systems 的大量 Kerbalism（+ SH）支持仍在**主** `KerbalismConfig` 里——想要通用散热器 / ISRU 的 SH 行为时再装 Core。

详情：[Extras/README.md](https://github.com/Kerbalism/Kerbalism/blob/master/Extras/README.md)。完整英文列表见本页英文版。

## 第三方配置包

用来**替代**官方 `KerbalismConfig`（不要叠装多个）：

- [ROKerbalism](https://github.com/Standecco/ROKerbalism) — RO / RP-1
- [SIMPLEX](https://spacedock.info/mod/2300)
- [SkyhawkKerbalism](https://forum.kerbalspaceprogram.com/index.php?/topic/208204-skyhawk-kerbalism-v01-alpha-release/) — 偏 BDB
- [LessRealThanReal(ism)](https://forum.kerbalspaceprogram.com/index.php?/topic/189978-112-less-real-than-realism-rp-1-with-less-r-v203/)

## 想给自己的模组加支持？

看 [技术指南](tech-guide/index.md)，尤其是 [Profile](tech-guide/profile.md)、[后台模拟](tech-guide/background-simulation.md) 和 [PartModules](tech-guide/part-modules/index.md)。优先 MM 补丁 + Kerbalism 模块，别在卸载船上硬刚原版资源 API。
