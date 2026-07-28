SolarPanelFixer
---------------
Support module designed to detect and patch the solar panel partmodule present on the part.
It patches or disable the target module at runtime, zeroing it's EC generation while keeping it's animation capacities. It implement it's own UI and support for the Planner and the Automation.
It does it's own EC output calculations based on the Kerbalism simulation for both loaded and unloaded vessels and feed the result to the Kerbalism resource simulation.

Note that if SolarPanelFixer is MM-patch added to a part, it must be added **after** the target module if that module is also added trough a MM-patch.

As this module implement support for Kopernicus-modified systems with multi-star evaluation, tracking and selection, the Kopernicus own solar panel module should be removed from the configs.

It also support the stock module "timeEfficCurve", but if used it is recommended to define it inside the SolarPanelFixer module instead, as this will extend support to all non-stock supported panels.
Note that the keys are in hours, this differ from the stock implementation where keys are in 24-hours days.

| PROPERTY       | TYPE       | KEYS               | VALUES                             |
|----------------|------------|--------------------|------------------------------------|
| timeEfficCurve | FloatCurve | Hours since launch | Efficiency factor in \[0;1\] range |

Currently supported solar panels module :

| Module                                                          | Support | Notes                                                                                                                            |
|-----------------------------------------------------------------|---------|----------------------------------------------------------------------------------------------------------------------------------|
| ModuleDeployableSolarPanel                                      | Partial | Derivatives modules may also work\. "powerCurve" and "temperatureEfficCurve" are not supported\. "timeEfficCurve" is supported\. |
| ModuleCurvedSolarPanel                                          | Full    | Near Future Solar mod                                                                                                            |
| SSTUSolarPanelStatic, SSTUSolarPanelDeployable, SSTUModularPart | Partial | No automation support, no "temperatureEfficCurve" support                                                                        |
| KopernicusSolarPanel                                            | No      | Not needed, multiple stars are simulated, tracking features are replicated                                                       |
| ModuleROSolarPanel                                              | No      | Loss of functionality is minimal \(timeEfficCurve editor tweaking\)\.                                                            |