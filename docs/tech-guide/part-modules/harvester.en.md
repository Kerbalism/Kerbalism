## Harvester
The part harvests resources, similar to the stock resource harvester (`ModuleResourceHarvester`). Extraction runs only when abundance is at or above `min_abundance`. Above that threshold, output scales linearly with abundance:

```text
amount/s = rate × (abundance / abundance_rate) × engineer_bonus
```

`rate` is the extraction rate at the abundance level given by `abundance_rate` (default 10%), not a flat rate for any abundance above the threshold. EC consumption (`ec_rate`) does not scale with abundance.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| title | name to show on UI |  |
| type | harvest type: `0`–`3` match stock HarvestTypes; `4` = asteroid/comet space object | 0 |
| resource | resource to extract |  |
| min_abundance | minimal abundance required, in the range [0.0, 1.0] |  |
| min_pressure | minimal pressure required, in kPA |  |
| rate | amount of resource extracted per-second at `abundance_rate` abundance |  |
| abundance_rate | abundance level at which `rate` is specified | 0.1 |
| ec_rate | amount of EC consumed per-second, regardless of abundance |  |
| drill | the drill transform (ImpactTransform in ModuleResourceHarvester) |  |
| length | length of the extendible part (ImpactRange in ModuleResourceHarvester) | 5 |
