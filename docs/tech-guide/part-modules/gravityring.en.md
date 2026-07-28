## GravityRing
Works alongside the [Habitat](habitat.md) module and provides support for a rotating animation that consumes EC.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| ec_rate | EC consumed per-second when deployed |  |
| deploy | a deploy animation |  |
| rotate | a rotate loop animation |  |
| animBackwards | If animation plays backwards, use this | false |
| rotateIsTransform | Rotation is a transform, not animation | false |
| SpinRate | Speed of centrifuge rotation in deg/s | 20 |
| SpinAccelerationRate | Rate of SpinRate acceleration (deg/s/s) | 1 |
| counterWeightRotate | counterweight rotate loop animation |  |
| counterWeightRotateIsTransform | Rotation is a transform, not animation | true |
| counterWeightSpinRate | Counterweight rotation speed in deg/s | 40 |
| counterWeightSpinAccelerationRate | Counterweight acceleration (deg/s/s) | 2 |
