Experiment
-------
Hooks experiments into the Kerbalism science system.

| PROPERTY              | DESCRIPTION                                                                                                                                                                                                                                        | DEFAULT       |
|-----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------|
| experiment_id         | The ID of the experiment (which must be defined elsewhere)                                                                                                                                                                                         |               |
| experiment_desc       | A nice description of the experiment.                                                                                                                                                                                                              |               |
| data_rate             | sampling rate in MBytes/s (Mbits/s ÷ 8)                                                                                                                                                                                                                              | 0.001        |
| ec_rate               | EC consumption rate per second while recording                                                                                                                                                                                                     | 0.0          |
| allow_shrouded        | Allow the experiment to run while it's part is shrouded                                                                                                                                                                                            | true          |
| sample_amount      | Amount of samples shipped with the module                                                                                                                                                                                                   | 0.0 |
| sample_collecting     | If set to true, the experiment will produce a sample out of nothing (sample_amount must be 0)                                                                                                                                                                                               | false         |
| requires              | Additional requirements that must be met for recording. See below.                                                                                                                                                                                 |               |
| resources             | Resources consumed while the experiment is running. Will stop if one of the resources is depleted. Rate is per sec. Malformed definitions or unknown resources will be ignored. Example: resources = Water@0.01,Food@0.02                          |               |
| crew_operate          | Requirements for crew on vessel for recording. If this is not set, the experiment can run on unmanned probes.                                                                                                                                      |               |
| crew_reset            | Requirements for crew to reset the experiment. If this is set, the experiment will only record data from within the situation where recording was started, until it is reset (either by a kerbal that has to match the requirement, or by a lab.   |               |
| crew_prepare          | If set, a kerbal has to prepare the experiment before it can record data. Once prepared, the experiment will only record data while it remains in the situation it was prepared for. The kerbal doing the preparation has to match the requiremens |               |
| hide_when_unavailable | Don't show the UI when the experiment is unavailable.                                                                                                                                                                                              |               |
| pointing_axis | Part-local axis used by `SunPointingMax`: `Forward`, `NegForward`, `Right`, `NegRight`, `Up`, `NegUp` | Forward |
| anim_deploy | Name of the part animation to trigger when recording starts.|               |
| anim_deploy_reverse | Play `anim_deploy` in reverse | false |
| anim_loop | Name of the part animation to play on loop while the experiment is active.|               |
| anim_loop_reverse | Play `anim_loop` in reverse | false |
| retractedDragCube| Name of the config-defined drag cube to be used while retracted | Retracted |
| deployedDragCube| Name of the config-defined drag cube to be used while extended | Deployed |
|use_animation_group| If set to true, the module will search for a ModuleAnimationGroup and use it for the deploy/retract animation. Use it in case you have several experiment modules on the part that must share the same animation|false|

[See here for how crew specs work](crew-specifications.md).

### Requirements
They work as additional filters on top of the `EXPERIMENT_DEFINITION` situation restrictions. For example, the `seismicScan` definition doesn't allow getting results in orbit. That definition level restriction is checked first, then the per experiment partmodule requirements are checked. Requirements are case sensitive and comma-separated, and must ALL be met for recording.

Examples : 
- `requires = Shadow,Space,Body:Kerbin` will only record data while in space near Kerbin AND in shadow. 
- `requires = AltitudeMin:250000,Surface` will never record anything for plainly obvious reasons.

Here is a list of currently supported requirements:

| Requirement | Description |
|-------------|-------------|
| OrbitMinInclination, OrbitMaxInclination | min./max. inclination of the orbit (f.i. `OrbitMinInclination:30`) |
| OrbitMinEccentricity, OrbitMaxEccentricity | min./max. eccentricity of the orbit (f.i. `OrbitMaxEccentricity:0.1`) |
| OrbitMinArgOfPeriapsis, OrbitMaxArgOfPeriapsis | min./max. argument of periapsis |
| TemperatureMin, TemperatureMax | min./max. Temperature in Kelvin |
| AltitudeMin, AltitudeMax | min./max. Altitude in Meters |
| RadiationMin, RadiationMax | min./max. radiation in rad/h |
| CommSpeedMin, CommSpeedMax | min./max. achieved vessel to ground station transmission rate in MB/s |
| Shadow | vessel must not be exposed to sunlight |
| Sunlight | vessel must be in the presence of a supreme being that radiates warmth and light upon it |
| AtmosphereAltMin, AtmosphereAltMax | Altitude of vessel as a multiplier of atmosphere thickness. On Kerbin, AtmosphereAltMin:1 equals 70km. |
| AbsoluteZero | temperature < 30 K |
| InnerBelt | vessel must be in a inner Van Allen Belt |
| OuterBelt | vessel must be in a outer Van Allen Belt |
| MagneticBelt | vessel must be in any Van Allen Belt |
| Magnetosphere | vessel must be inside a magnetosphere |
| InterStellar | vessel must be outside the sun magnetopause |
| Greenhouse | there must be one greenhouse on the vessel. |
| CrewMin, CrewMax | min./max. amount of crew on vessel |
| CrewCapacityMin, CrewCapacityMax | min./max. crew capacity |
| VolumePerCrewMin, VolumePerCrewMax | min./max. habitat volume per crew member |
| MissionControlLevelMin, MissionControlLevelMax, AdministrationLevelMin,  AdministrationLevelMax, TrackingStationLevelMin,  TrackingStationLevelMax, AstronautComplexLevelMin,  AstronautComplexLevelMax | Facility building levels |
| MaxAsteroidDistance | max. distance to the nearest asteroid. For unloaded vessels this only works if the asteroid is set as the target. |
| Part | name of a part that has to be on the vessel |
| Module | name of a partmodule that has to be on any part on the vessel |
| SunAngleMin, SunAngleMax | min./max. angle of sunlight on the surface of the body |
| SunPointingMax | max. angle (degrees) between the part `pointing_axis` and the sun direction |
| **The following might or might not work for unloaded vessels...** | **...please update this list when you find out** |
| SurfaceSpeedMin, SurfaceSpeedMax | Surface speed (corrected for body rotation) |
| VerticalSpeedMin, VerticalSpeedMax | Vertical speed |
| SpeedMin, SpeedMax | Orbital speed |
| DynamicPressureMin, DynamicPressureMax | current dynamic pressure |
| StaticPressureMin, StaticPressureMax | current static pressure |
| AtmDensityMin, AtmDensityMax | current atmospheric density |
| AltAboveGroundMin, AltAboveGroundMax | Altitude above ground. Note that this value can change rapidly as KSP loads/unloads the terrain of a body |