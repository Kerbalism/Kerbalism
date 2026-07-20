# Kerbalism SystemHeat Extras

These optional CFG packages are not part of the default `GameData` tree.

## Installation

Choose an Extra and copy its **contents** into the KSP `GameData` directory. Merge the included `KerbalismConfig` folder with `GameData/KerbalismConfig`.

`KerbalismSystemHeatCompat` and `KerbalismSystemHeatFission` require `KerbalismSystemHeat` to be installed alongside them.

## Packages

- `KerbalismSystemHeat`: generic SystemHeat bridge for native converters, harvesters, radiators, Kerbalism drills/chemical plants, planner support, and legacy module migration.
- `KerbalismSystemHeatCryoTanks`: converts CryoTanks EC cooling to `ModuleSystemHeatCryoTank` and adds its Kerbalism updater. The separate SystemHeat Boiloff/CryoTanks Extra is not required.
- `KerbalismSystemHeatFission`: fission integration for stock, ReStock+, Atomic Age, Kerbal Atomics, Near Future Aeronautics, Missing History, USI reactors, and related dynamic-radiation patches. It includes the SystemHeat fission-engine conversions needed by those parts.
- `KerbalismSystemHeatIonEngines`: makes stock and Near Future Propulsion ion engines participate in SystemHeat loops. Engine propellants remain managed by their existing `ModuleEngines` modules, so no resource updater is required.
- `KerbalismSystemHeatCompat`: SystemHeat integration for Buffalo, Feline Utility Rover, Heat Control, KPBS, SpaceDust, Sterling Systems, and related compatibility patches.

The bundled CryoTanks, fission-engine, and ion-engine conversions automatically skip themselves when the matching upstream SystemHeat Extra is detected, so existing installations do not receive duplicate modules.

Near Future Electrical and Far Future Technologies keep their core SystemHeat patches in the main KerbalismConfig package because both mods require SystemHeat. NFE/FFT users should still install `KerbalismSystemHeat` when they want Kerbalism's generic radiator background handling or SystemHeat-aware ISRU integration.

## Attribution

The bundled CryoTanks, fission-engine, and ion-engine conversion patches are adapted from SystemHeat revision `5f75a20af915ffe465949007af0de1131f745127`. They are redistributed under SystemHeat's MIT license; see `SystemHeat-LICENSE.md`.
