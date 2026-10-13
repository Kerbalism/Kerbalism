## Laboratory
The part transforms non-transmissible science samples into transmissible science data over time. It does not create bonus science — it only converts sample mass/data into a form that can be transmitted.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| ec_rate | EC consumed per-second while analyzing |  |
| analysis_rate | analysis speed in Mb/s |  |
| researcher | required crew for analysis, in the format *trait@level*; empty = no crew required |  |
| cleaner | if true, the lab can clean/reset experiments (PAW action when a researcher is present, or when none is required) | true |

`running` is persistent. While enabled, analysis needs EC, free science storage, an analyzable sample on the vessel, and (if configured) the researcher. The lab stops itself when there is nothing left to analyze; it will not keep running empty.
