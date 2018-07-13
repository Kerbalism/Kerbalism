.. _background_sim:

Background Simulation
=====================

Resources
---------
Modules that consume and produce resources are simulated for unloaded vessels.

+---------------------------------------------+
| MODULE                                      |
+=============================================+
| Greenhouse                                  |
+---------------------------------------------+
| GravityRing                                 |
+---------------------------------------------+
| Emitter                                     |
+---------------------------------------------+
| Harvester                                   |
+---------------------------------------------+
| Laboratory                                  |
+---------------------------------------------+
| ModuleCommand                               |
+---------------------------------------------+
| ModuleDeployableSolarPanel                  |
+---------------------------------------------+
| ModuleGenerator                             |
+---------------------------------------------+
| ModuleResourceConverter (and some variants) |
+---------------------------------------------+
| ModuleResourceHarvester                     |
+---------------------------------------------+
| ModuleAsteroidDrill                         |
+---------------------------------------------+
| ModuleScienceConverter                      |
+---------------------------------------------+
| ModuleLight (and some variants)             |
+---------------------------------------------+
| SCANsat                                     |
+---------------------------------------------+
| ModuleSCANresourceScanner                   |
+---------------------------------------------+
| ModuleCurvedSolarPanel                      |
+---------------------------------------------+
| FissionGenerator                            |
+---------------------------------------------+
| ModuleRadioisotopeGenerator                 |
+---------------------------------------------+

----------

Solar panels
------------
Solar panel output is simulated. Fixed panel orientation is taken into account, and tracking panels are simulated around the pivot. A portion of the flux is blocked by the atmosphere depending on density and path length.

----------

Algorithm details
-----------------
Dependency information is preserved in *recipes*, all the recipes are executed using an iterative algorithm that is order-less, works at arbitrary time steps and is not limited by storage capacity.