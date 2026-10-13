# Habitat

Marks a part as a habitat volume for life support, pressure, living space, and shielding.

Source: [`Habitat.cs`](https://github.com/Kerbalism/Kerbalism/blob/master/src/Kerbalism/Modules/Habitat.cs).

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| volume | Habitable volume in m³ (else deduced from mesh) | `0` (auto) |
| surface | External surface in m² (else deduced) | `0` (auto) |
| inflate | Deploy/inflate animation name, if any | empty |
| animBackwards | Invert deploy animation direction | `false` |
| inflatableUsingRigidWalls | Allow shielding on inflatable structures | `false` |
| toggle | Show enable/disable toggle | `true` |
| nonPressurizable | Part cannot be pressurized (e.g. Mk1) | `false` |
| inflateRequiresPressure | Inflating requires pressure | `true` |
| canRetract | Allow retract after deploy | `false` |
| volumeAndSurfaceMethod | How volume/surface are computed | `Best` |
| substractAttachementNodesSurface | Subtract node surface from exterior area | `true` |
| max_pressure | **Obsolete** — never fully implemented; use `nonPressurizable` | `1.0` |

Disabled habitats are not included in radiation / living-space calculations (fixed in recent releases).
