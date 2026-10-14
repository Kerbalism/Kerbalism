## HardDrive
The part has an interface to access the vessel hard drive, where the science data files are stored.
Only one HardDrive module can be defined per part.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| dataCapacity | Base storage capacity for data, in Mb (=Mib) | -1 |
|  | -1 = unlimited. |  |
| sampleCapacity | Base capacity for experiment samples, in slots (=Mib). | -1 |
|  | Note that Kerbalism will not display sample sizes in Mb, |  |
|  | but uses a virtual size unit instead (slots, bags) (TBD) |  |
|  | -1 = unlimited. |  |
| experiment_id | If set, restricts write access to the experiment with that |  |
|  | id ON THE SAME PART with the given experiment_id. |  |
| maxDataCapacityFactor | number of capacity increments available in the editor | 4 |
| dataCapacityCost | cost factor for added data capacity | 400.0 |
| dataCapacityMass | mass factor for added data capacity | 0.005 |
| maxSampleCapacityFactor | number of capacity increments available in the editor | 4 |
| sampleCapacityCost | cost factor for added sample capacity | 300.0 |
| sampleCapacityMass | mass factor for added sample capacity | 0.008 |

If the capacities are >0, you can set up to `maxDataCapacityFactor` or `maxSampleCapacityFactor` times the base capacity in the editor.

`dataCapacityCost`, `dataCapacityMass`, `sampleCapacityCost` and `sampleCapacityMass` are only effective for additional capacity only. The base `dataCapacity` / `sampleCapacity` is free, any additional capacity is *multiplied* with that value. A hard drive with 3 times the base capacity adds 3 times the cost factor to the part costs.
