# Reliability

Disables other modules when a failure happens. Also models engine ignitions / burn-time wear.

Source: [`Reliability.cs`](https://github.com/Kerbalism/Kerbalism/blob/master/src/Kerbalism/Modules/Reliability.cs).

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| type | name of the component part module (e.g. `ModuleParachute`) |  |
| mtbf | mean time between failures, in seconds; `0` disables MTBF failures | `21600000` (1000×6h) |
| repair | trait and experience for repair, `trait@experience` |  |
| title | short description of component |  |
| redundancy | redundancy group |  |
| extra_cost | extra cost for high-quality, proportion of part cost | `0` |
| extra_mass | extra mass for high-quality, proportion of part mass | `0` |
| rated_radiation | rad/h this part can endure without extra MTBF damage; `0` = no radiation damage | `0` |
| radiation_decay_rate | how fast time-to-next-failure shrinks while over rated radiation | `1` |

## Engine failures

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| turnon_failure_probability | failure chance on ignition (0..1); `-1` disables | `-1` |
| rated_operation_duration | expected burn duration (s) before failure rate rises; `-1` disables | `-1` |
| rated_ignitions | expected ignitions before failure rate rises; `-1` disables | `-1` |

Applies to `ModuleEngines` / `ModuleEnginesFX`. Ignition means providing thrust after not providing thrust (not merely enabling the PAW toggle).

Engines get a fraction of `rated_operation_duration` / `rated_ignitions` with low failure odds; beyond that, failure rate rises sharply. Failures can destroy the engine.

## MTBF

Implementation guarantees roughly 50% of MTBF without failure, then failure rate rises (near-certain by ~150% MTBF).

## Radiation damage

While vessel radiation exceeds `rated_radiation`, MTBF countdown accelerates using `radiation_decay_rate`.

## High quality

In the editor, parts can be set to high quality:

- Adds `extra_cost` / `extra_mass`
- Multiplies MTBF by Settings `QualityScale` (default `4`)
- Improves ignition / burn / radiation ratings accordingly

Difficulty options can require **repair kits** for repairs (non-default).
