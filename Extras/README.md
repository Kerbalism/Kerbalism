# Kerbalism Extras

These optional CFG packages are not part of the default `GameData` tree. Each Extra is a standalone GameData folder so any Kerbalism configuration pack can use them.

中文版见 [README.zh-CN.md](README.zh-CN.md)。

## Installation

Copy a chosen Extra folder into the KSP `GameData` directory. Example: `Extras/KerbalismSystemHeatCore` → `GameData/KerbalismSystemHeatCore`.

`KerbalismSystemHeatCompat` requires `KerbalismSystemHeatCore` to be installed alongside it.

Inside Compat, patches are grouped by the target mod's usual GameData folder name. `Squad` covers stock parts.

## Compatibility

Kerbalism's SystemHeat packages are **exclusive**. Do **not** install upstream SystemHeat Extras alongside them:

- `SystemHeatFissionEngines`
- `SystemHeatFissionReactors`
- `SystemHeatIonEngines`
- `SystemHeatConverters`
- `SystemHeatHarvesters`
- `SystemHeatCryoTanks` / `SystemHeatBoiloff`
- Legacy `Kerbalism-SystemHeat`

CKAN marks these as conflicts. Manual installs that mix both will double-patch parts.

## Packages

- `KerbalismSystemHeatCore`: generic SystemHeat bridge (native converters/harvesters/radiators, Kerbalism drills/chemical plants, planner support, legacy module migration).
- `KerbalismSystemHeatCompat`: third-party and stock support. Contents:

  | Path | Purpose |
  |------|---------|
  | `DynamicRadiation.cfg` | power-scaled radiation for SH fission reactors/engines |
  | `Squad/LV-N.cfg` | stock LV-N → SH fission engine |
  | `Squad/Dawn.cfg` | stock Dawn → SH ion loop |
  | `AtomicAge` | SH radiators + fission NTR bridge |
  | `Buffalo` | SAFER reactor SH / Kerbalism bridge |
  | `CryoTanks` | EC cooling → `ModuleSystemHeatCryoTank` + Kerbalism updater |
  | `FelineUtilityRover` | FUR converter/harvester SH bridge |
  | `HeatControl` | Heat Control radiators SH bridge |
  | `KerbalAtomics` | KA NTR SH conversion |
  | `MissingHistory` | MissingHistory NTR SH / Kerbalism bridge |
  | `NearFutureAeronautics` | NFA atomic jet SH / Kerbalism bridge |
  | `NearFuturePropulsion` | NFP ion SH conversion |
  | `PlanetaryBaseInc` | KPBS nuclear reactor SH bridge |
  | `RestockPlus` | Cherenkov SH fission engine |
  | `SpaceDust` | SpaceDust harvester SH bridge |
  | `USI` | USI reactor / FTT / Kontainer SH bridge |

Near Future Electrical, Far Future Technologies, and Sterling Systems keep their Kerbalism (including SystemHeat) support in the main `KerbalismConfig` package — do not also install Jade's `SterlingSystemsKerbalism`. Install `KerbalismSystemHeatCore` when you want Kerbalism's generic radiator background handling or SystemHeat-aware ISRU integration for other parts.

## Attribution

The bundled CryoTanks, fission-engine, and ion-engine conversion patches are adapted from SystemHeat revision `5f75a20af915ffe465949007af0de1131f745127`. They are redistributed under SystemHeat's MIT license; see `SystemHeat-LICENSE.md`.
