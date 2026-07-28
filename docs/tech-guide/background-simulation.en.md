## Resources simulation

Kerbalism use it's own resource processing system to overcome the limitations of the stock system.
This allow consistent simulation of complex resource chains no matter if the vessel is loaded or not and no matter what the timewarp speed is.

The resource simulation is based on **recipes**. They are executed using an iterative algorithm that is **order-less**, **works at arbitrary time steps** and is **not limited by storage capacity**, three major limitations that are present in stock and in most others mods.

For it to work correctly, explicit code support is required for all stock and other mods modules.
Using unsupported or partially supported resource producers/consumers in your game can have the following consequences :

- For unsupported modules, producers and consumers of resources will not be simulated in the background.
- **Producers** of resources will prevent loaded vessels from timewarping faster than 1000x if the resource is also consumed or produced by the Kerbalism resource simulation.

This last limitation can be disabled ("EnforceCoherency" in settings.cfg), not recommended unless you are sure that the unsupported producer is using a non-critical resource for the life support resource chain.

## Supported PartModules

| PartModule | Stock / Mod | Support | 1000x warp limit | Kerbalism replacement module / remarks |
| --- | --- | --- | --- | --- |
| ModuleCommand | Stock | Full | No |  |
| ModuleGenerator | Stock | Partial | Yes | Replacement : Process / ProcessController |
| ModuleResourceConverter | Stock | Partial | Yes | Replacement : Process / ProcessController |
| ModuleResourceHarvester | Stock | Partial | Yes | Replacement : Harvester |
| ModuleAsteroidDrill | Stock | Partial | Yes | No replacement |
| ModuleScienceConverter | Stock | Full | No | Replacement : Laboratory |
| ModuleLight | Stock | Full | No |  |
| ModuleColoredLensLight, ModuleMultiPointSurfaceLight | Surface Mounted Lights | Full | No |  |
| SCANsat, ModuleSCANresourceScanner | ScanSat | Full | No |  |
| FissionGenerator, ModuleRadioisotopeGenerator | NF Electrical / Atomics | Partial | Yes | Background sim ignores the reactor temperature |
| ModuleCryoTank | Cryo Engines / Cryo Tanks | Full | No | Resource boiloff in the background; SystemHeat boiloff via optional [Extras](https://github.com/Kerbalism/Kerbalism/tree/master/Extras) |
| SystemHeat converters / harvesters / radiators | SystemHeat | Full (with Extras) | No | Prefer KerbalismSystemHeatCore/Compat; see [Mod support](../mod-support.md) |
| ModuleKPBSConverter | Planetary Base Systems | Partial | Yes | Replacement : Process / ProcessController |
| FNGenerator | KSPIE | Partial | Yes | Basic background processing support |
| ModuleDeployableSolarPanel | Stock | Full | No | Require adding SolarPanelFixer to the part |
| ModuleCurvedSolarPanel | Near Future Solar | Full | No | Require adding SolarPanelFixer to the part |
| SSTUSolarPanelStatic, SSTUSolarPanelDeployable, SSTUModularPart | SSTU | Full | No | Require adding SolarPanelFixer to the part |
| KopernicusSolarPanel | Kopernicus | No | N/A | Incompatible, Kopernicus support provided by SolarPanelFixer |
| ModuleROSolarPanel | Realism Overhaul | No | N/A | Incompatible, timeEfficCurve support provided by SolarPanelFixer |

## Solar panels
We have a "patcher module", *SolarPanelFixer* that override the EC generation for all supported solar panels (stock, NFS, SSTU...). This module uses our own raytracing, solar flux and atmospheric simulations in order to provide realistic and consistent output at high timewarp speeds for loaded and unloaded vessels. See the partmodules documentation for details.
