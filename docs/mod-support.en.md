---
hide:
  - navigation
---

# Mod support

!!! warning "Not a guarantee"
    Kerbalism is best played in a **lightly modded**, stock-scale game. This page lists where the **main repository provides patches or code**, not a promise that every combo is bug-free. Large “future-scope” packs (KSPIE, many NFT/FFT/USI-MKS setups, interstellar planet packs, some huge part packs) remain high-risk. Pull requests for support configs are welcome.

## Why Kerbalism touches other mods

Kerbalism simulates **unloaded vessels** (life support, ISRU, science, antennas, …). Stock and most mods only run their modules on the active vessel. To keep background behaviour coherent, Kerbalism often **replaces or wraps** stock/third-party modules instead of guessing their unloaded logic. That overlaps with other life-support, background-resource, or science overhaul mods.

## Hard incompatibilities (startup list)

Default `ModsIncompatible` in code includes other life-support stacks:

- TacLifeSupport
- Snacks
- KolonyTools
- USILifeSupport

Do not combine these with Kerbalism’s official life-support profile.

Default science-overlap warnings include `[x] Science!` and KEI (features largely duplicated).

## Explicitly unsupported (wontfix)

These are **not** hard `ModsIncompatible` blocks at startup, but upstream has marked them **wontfix**: do not expect official support patches, and combining them with Kerbalism is at your own risk.

| Mod | Issues | Why |
| --- | --- | --- |
| **Pathfinder** (Wild Blue Industries) | [#428](https://github.com/Kerbalism/Kerbalism/issues/428), [#706](https://github.com/Kerbalism/Kerbalism/issues/706) | Heavy feature overlap with Kerbalism habitats, processes, and science labs. Custom modules such as `WBIScienceConverter` break when Kerbalism removes stock `ModuleScienceLab` (PAW NRE spam). Full support would be large, hacky, and hard to maintain — same class of problem as USI MKS. Limited Buffalo / MOLE / WildBlueTools patches elsewhere do **not** mean Pathfinder is supported. |

## Partial compatibility

These can run alongside Kerbalism, but some features will not work as their authors intended. Expect gaps; do not treat this as full support.

| Mod | Issues | Notes |
| --- | --- | --- |
| **Strategia** | [#757](https://github.com/Kerbalism/Kerbalism/issues/757) | Non-science strategies and the rest of the mod can be used. Science-modifying strategies (e.g. Probe Frenzy: probe bonuses and transmit-vs-recover differences) do **not** apply correctly under Kerbalism’s science pipeline. Kerbalism deliberately avoids transmit/recover science splits; fixing that properly would need non-trivial changes on the Strategia side. |

## BackgroundResources / DeepFreeze / TAC-LS

If `BackgroundResources` is loaded (often with DeepFreeze or TAC-LS), Kerbalism tries to disable its unloaded-vessel processing and may show a popup. That is a **conflict**, not “full support.” Prefer not running DeepFreeze or TAC-LS alongside Kerbalism.

## RemoteTech

Code integration for RemoteTech antennas / control path still exists, and Settings.cfg has RT-specific EC factors. A large RT install is still **poorly maintained** compared to stock CommNet. Expect warnings and edge cases.

## Bundled Support patches

Patches live under `GameData/KerbalismConfig/Support/` in the official config pack. Presence of a patch means **someone contributed integration** — quality varies.

### Near Future / Far Future / related

| Area | Patch / notes |
| --- | --- |
| Near Future Electrical | `NFElectric.cfg` (+ SystemHeat-aware bits in main config) |
| Near Future Propulsion | `NFPropulsion.cfg` |
| Near Future Spacecraft / Exploration / Aeronautics / Launch Vehicles | `NFSpacecraft*.cfg`, `NFExploration_Science.cfg`, `NFAeronautics.cfg`, `NFLaunchVehicle.cfg` |
| Far Future Technologies | `FarFutureTechnologies.cfg` (main config; SH extras elsewhere) |
| CryoTanks / CryoEngines | `CryoTanks.cfg`, `CryoEnginesExtensions.cfg` |
| Kerbal Atomics | `KerbalAtomics.cfg` |
| Atomic Age | `AtomicAge.cfg` |
| Dynamic Radiation | `DynamicRadiation.cfg` |
| SpaceDust harvesters | `SpaceDustHarvesters.cfg` |
| Supplementary electric engines | `SupplementaryElectricEngines.cfg` |
| RDK | `RDK.cfg` |

### Habitats / stations / parts

| Area | Patch / notes |
| --- | --- |
| Station Parts Expansion Redux (SSPX) | `SSPX.cfg`, `SSPX_Science.cfg` |
| HabTech / HabTech2 | `HabTech.cfg`, `HabTech2.cfg` |
| Bluedog Design Bureau | `Bluedog.cfg` (large pack — still often fragile) |
| Planetary Base Inc (KPBS) | `PlanetaryBaseInc.cfg` |
| Buffalo | `Buffalo.cfg` |
| MOLE / ALCOR / CxAerospace / Kerbalow | matching `*.cfg` |
| Restock / ReStockPlus | `Restock.cfg`, `ReStockPlus*.cfg` |
| SXT, SSTU, Tantares, VSR, mK2, … | matching support files |
| VABOrganizer | `VABOrganizer.cfg` |

### Science / probes

| Area | Patch / notes |
| --- | --- |
| SCANsat | `SCANsat.cfg` + `KerbalismScansat` module |
| DMagic Orbital Science | `DMagicOrbitalScience_Science.cfg` |
| Sounding Rockets, Solar Science, Field Research, … | `*_Science.cfg` files |
| Universal Storage 2 | `UniversalStorage2.cfg` + science |
| Stock / Breaking Ground science | `Squad_Science.cfg`, `BreakingGrounds_Science.cfg` |

### Utilities / realism-adjacent

| Area | Patch / notes |
| --- | --- |
| RemoteTech | `RemoteTech.cfg` |
| RealAntennas, RealBattery, RealChute, RealFuels | matching configs (RO/RSS users often use ROKerbalism instead) |
| TweakScale, B9PartSwitch, CCK, CLS | utility patches |
| Contract Configurator, Engineer, … | limited hooks |
| Kopernicus / planet packs (OPM, GPP, RSS, …) | radiation / body hooks where present — not full “support” |
| EngineIgnitor, TestFlight, PayToPlay | reliability/ignition related |

### USI and Sterling Systems

- **USI** — folder `Support/USI/` (reactors, FTT, Kontainers). USI **life support** / Kolony stacks remain incompatible.
- **Sterling Systems** — folder `Support/SterlingSystems/` (including SystemHeat-aware converters). Do **not** also install Jade’s separate `SterlingSystemsKerbalism` pack.

## Optional Extras (SystemHeat)

Not in default `GameData`. Copy from the repo [Extras](https://github.com/Kerbalism/Kerbalism/tree/master/Extras):

| Package | Role |
| --- | --- |
| `KerbalismSystemHeatCore` | Generic SystemHeat bridge (converters, harvesters, radiators, planner, migration) |
| `KerbalismSystemHeatCompat` | Requires Core. Patches stock + third-party parts (Squad LV-N/Dawn, AtomicAge, Buffalo, CryoTanks, FUR, HeatControl, KerbalAtomics, MissingHistory, NFA/NFP, KPBS, RestockPlus Cherenkov, SpaceDust, USI, …) |

**Exclusive:** do **not** install upstream SystemHeat Extras alongside these (`SystemHeatFissionEngines`, `SystemHeatFissionReactors`, `SystemHeatIonEngines`, `SystemHeatConverters`, `SystemHeatHarvesters`, `SystemHeatCryoTanks` / boiloff, or legacy `Kerbalism-SystemHeat`). Mixing double-patches parts. CKAN marks conflicts.

NFE, FFT, and Sterling Systems keep much of their Kerbalism (+ SH) support in the **main** `KerbalismConfig` package — install Core when you want Kerbalism’s generic radiator/ISRU SH behaviour.

Details: [Extras/README.md](https://github.com/Kerbalism/Kerbalism/blob/master/Extras/README.md).

## Alternate config packs

Use **instead of** official KerbalismConfig when documented:

- [ROKerbalism](https://github.com/Standecco/ROKerbalism) — RO / RP-1
- [SIMPLEX](https://spacedock.info/mod/2300)
- [SkyhawkKerbalism](https://forum.kerbalspaceprogram.com/index.php?/topic/208204-skyhawk-kerbalism-v01-alpha-release/) — BDB-focused
- [LessRealThanReal(ism)](https://forum.kerbalspaceprogram.com/index.php?/topic/189978-112-less-real-than-realism-rp-1-with-less-r-v203/)

## Adding support for your mod

See the [technical guide](tech-guide/index.md), especially [Profile](tech-guide/profile.md), [background simulation](tech-guide/background-simulation.md), and [PartModules](tech-guide/part-modules/index.md). Prefer MM patches + Kerbalism modules over fighting stock resource APIs on unloaded vessels.
