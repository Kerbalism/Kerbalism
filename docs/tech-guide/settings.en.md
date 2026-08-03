# Settings

Global options live in `GameData/KerbalismConfig/Settings.cfg` under a `Kerbalism { }` node. Parsed by [`Settings.cs`](https://github.com/Kerbalism/Kerbalism/blob/master/src/Kerbalism/System/Settings.cs).

Values below are the **shipped official config** defaults (3.40). Code fallbacks differ for some feature flags if a key is missing (`false` in code vs `true` in shipped cfg).

## Profile and features

| KEY | DESCRIPTION | SHIPPED DEFAULT |
| --- | --- | --- |
| Profile | Profile name: `default`, `none`, or a custom profile under `Profiles/` | `default` |
| Reliability | Component malfunctions / critical failures | `true` |
| Deploy | EC cost to keep modules working / extend-retract | `true` |
| Science | Science storage, transmission, analysis | `true` |
| SpaceWeather | Coronal mass ejections | `true` |
| Automation | Script UI and automatic execution | `true` |

## Pressure and poisoning

| KEY | DESCRIPTION | SHIPPED DEFAULT |
| --- | --- | --- |
| PressureFactor | Pressurized modifier when below threshold | `10.0` |
| PressureThreshold | Atmosphere resource level for “pressurized” | `0.9` |
| PoisoningFactor | Poisoning modifier when below threshold | `0.0` |
| PoisoningThreshold | WasteAtmosphere level for CO₂ poisoning | `0.02` |
| EqualizationRateFactor | Manual equalization rate for inflatable/deployable habs (%/s at max ΔP) | `0.01` (code default; may be omitted from cfg) |

## Comms

| KEY | DESCRIPTION | SHIPPED DEFAULT |
| --- | --- | --- |
| TransmitterActiveEcFactor | Fraction of antenna EC rate while transmitting (CommNet) | `1.5` |
| TransmitterPassiveEcFactor | Fraction while idle/receiving (CommNet) | `0.04` |
| TransmitterActiveEcFactorRT | Same for RemoteTech while transmitting | `1.0` |
| TransmitterPassiveEcFactorRT | Same for RemoteTech while idle | `1.0` |
| DataRateMinimumBitsPerSecond | Floor on science data rate when a control link exists | `1.0` |
| DampingExponentOverride | Optional override for data-rate damping; auto-calibrates from home-star AU when unset (see KSP.log for `DataRateDampingExponent` / `Home system`) | commented out |
| UnlinkedControl | Control when unlinked: `none` / `limited` / `full` | `none` (code; often omitted from cfg) |

## Science and reliability

| KEY | DESCRIPTION | SHIPPED DEFAULT |
| --- | --- | --- |
| ScienceDialog | Keep stock science result dialog | `true` |
| QualityScale | MTBF multiplier for high-quality components | `4.0` |
| LaboratoryCrewLevelBonus | Lab speed gain per scientist level | `0.2` |
| MaxLaborartoryBonus | Cap on lab bonus | `2.0` |
| HarvesterCrewLevelBonus | Harvester gain per engineer level | `0.1` |
| MaxHarvesterBonus | Cap on harvester bonus | `1.5` in cfg (`2.0` code fallback) |

## Radiation and storms

| KEY | DESCRIPTION | SHIPPED DEFAULT |
| --- | --- | --- |
| ExternRadiation | Cosmic background rad/h | `0.04` |
| StormRadiation | Default storm rad/h (also difficulty-adjustable) | `5.0` |
| RadiationInSievert | Use Sv instead of rad | commented / `false` |
| UseSIUnits | Prefer SI pretty-printing when resources define units | commented / `false` |

Additional storm / comfort / shielding presets exist as code defaults (`StormFrequency`, `ComfortLivingSpace`, `ComfortFirmGround`, `ShieldingEfficiency*`, …) and feed in-game difficulty preferences even when not listed in Settings.cfg.

`ComfortFirmGround` is the comfort **factor weight** when firm ground is active — not a spin threshold. Whole-vessel spin firm ground is controlled separately (also as Comfort difficulty preferences):

| KEY | DESCRIPTION | CODE DEFAULT |
| --- | --- | --- |
| ComfortSpinFirmGround | Allow firm ground from whole-vessel spin | `true` |
| ComfortSpinMinArtificialG | Minimum artificial gravity (g) for a seat to count | `0.25` |
| ComfortSpinMaxRpm | Maximum whole-vessel spin rate (rpm) | `3.0` |
| ComfortSpinCrewCoverage | Fraction of aboard crew that must have a qualifying high-g seat | `1.0` |

See [Habitat → Comforts](../play-guide/habitat.md) for how qualifying seats are counted.

## Misc

| KEY | DESCRIPTION | SHIPPED DEFAULT |
| --- | --- | --- |
| EnforceCoherency | Mitigate high-warp issues in external modules | `true` |
| TrackingPivot | Present in Settings.cfg — **not referenced in C#** (stale key) | `true` |
| HeadLampsCost | EC/s for EVA headlamps | `0.002` |
| LowQualityRendering | Fewer magnetic-field particles | `false` |
| UIScale / UIPanelWidthScale | UI scaling vs KSP DPI settings | `1.0` |
| CheckForCRP | Warn if Community Resource Pack missing | `true` |
| ModsWarning | Comma list of mods that trigger a warning (`none` disables) | default includes `CommNetAntennasInfo`; science-overlap mods added when Science feature on |
| UseSamplingSunFactor | Experimental sunlight factor at fast warp | `false` |
| UseResourcePriority | Respect part flow priority for resource deltas | `false` |
| VolumeAndSurfaceLogging | Verbose habitat volume/surface calc to KSP.log | `false` |

## Difficulty preferences

Many comfort, storm, shielding, reliability (including **require repair kits**), and science options are also exposed as **in-game difficulty / Kerbalism preferences**, not only Settings.cfg. Changing Settings.cfg affects new games / presets; check the Kerbalism difficulty pane for save-specific toggles.
