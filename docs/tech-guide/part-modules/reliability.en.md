# Reliability

Disables other modules when a failure happens.

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

Engine ignition counts, rated burn time and turn-on failure chance are **not** part of core `Reliability`. They live in the optional companion mod **KerbalismEngineFailures** (`GameData/KerbalismEngineFailures`, module `EngineFailures`).

See that package's README and `EngineReliability.cfg` for propulsion-family auto-ratings. CKAN recommends the companion with official KerbalismConfig and marks conflicts with TestFlight / RO / RP-1.

## MTBF

Implementation guarantees roughly 50% of MTBF without failure, then failure rate rises (near-certain by ~150% MTBF).

## Radiation damage

While vessel radiation exceeds `rated_radiation`, MTBF countdown accelerates using `radiation_decay_rate`.

## High quality

In the editor, parts can be set to high quality:

- Adds `extra_cost` / `extra_mass`
- Multiplies MTBF by Settings `QualityScale` (default `4`)
- Improves radiation ratings accordingly

Difficulty options can require **repair kits** for repairs (non-default).
