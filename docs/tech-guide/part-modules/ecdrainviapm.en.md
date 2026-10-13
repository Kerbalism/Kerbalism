# ECDrainViaPM

Adds an ElectricCharge rate that follows another PartModule’s enabled state on the same part. Used for mods that need background-aware EC drain without rewriting their module.

Source: [`ECDrainViaPM.cs`](https://github.com/Kerbalism/Kerbalism/blob/master/src/Kerbalism/Modules/ECDrainViaPM.cs).

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| targetModule | `moduleName` of the PartModule to watch on this part | *(required)* |
| ec_rate | EC consumed per second while running (must be **&gt; 0** to apply a drain) | `0.0` |
| moduleTitle | Label used in planner / PAW if `title` is empty | falls back to target module name |
| title | Explicit PAW / planner label | empty |
| running | Persisted run state; kept in sync with the target’s `moduleIsEnabled` | `false` |

## Behaviour

- Each frame (loaded), `running` is set from `targetPM.moduleIsEnabled` (not Unity `Behaviour.enabled`).
- While `running` and `ec_rate > 0`, Kerbalism requests `ElectricCharge` at `-ec_rate` via `IKerbalismModule` (`ResourceUpdate`, `PlannerUpdate`, and `BackgroundUpdate`).
- Current code only applies a drain when `ec_rate > 0`; it does **not** treat negative rates as generators.
- Works loaded and unloaded; shows up in the planner under the configured title.

## Example

```text
MODULE
{
  name = ECDrainViaPM
  targetModule = SomeModModule
  ec_rate = 0.05
  moduleTitle = Some Mod Device
}
```
