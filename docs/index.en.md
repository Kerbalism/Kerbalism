---
hide:
  - navigation
---

![KerbalismBanner](assets/banner.png)

Kerbalism is a mod for Kerbal Space Program that alters the game to add life support, radiation, failures and an entirely new way of doing science.

Go beyond the routine of orbital mechanics and experience the full set of engineering challenges that space has to offer. All mechanics can be configured to some degree, or even disabled if you don't like some of them. A big part of the mod is fully data-driven, so that you can create your own customized game play with only a text editor and a minimal amount of espresso. Or simply use a set of rules shared by other users.

## All vessels, all the time

Contrary to popular belief, the observable universe is not a sphere of a 3km radius centered around the active vessel. All mechanics are simulated for loaded and unloaded vessels alike, without exception. Acceptable performance was obtained by a mix of smart approximations and common sense. The performance impact on the game is by and large independent from the number of vessels.

## Resources

This isn't your classic post-facto resource simulation. Consumption and production work is coherent regardless of warp speed or storage capacity. Complex chains of transformations that you build for long-term life support or mining bases just work. 

## Environment

The environment of space is modeled in a simple yet effective way. Temperature is calculated using the direct solar flux, the indirect solar flux bouncing off from celestial bodies, and the radiative infrared cooling off their surfaces.

The simulation of the latter is especially interesting and able to reproduce good results for worlds with and without atmosphere. Radiation is implemented using an overlapping hierarchy of 3D zones, modeled and rendered using signed distance fields. These are used to simulate inner and outer belts, magnetosphere and even the heliopause. Solar weather is represented by Coronal Mass Ejection events, that happen sporadically, increase radiation and cause communication blackouts.

[![Kerbalism Radiation Belts](https://github.com/Kerbalism/Kerbalism/raw/master/misc/img/showcase/radiation.png)](https://www.youtube.com/watch?v=CXmeSMBzf1c)

## Habitats

The habitats of vessels are modelled in terms of internal volume, external surface, and a set of dedicated pseudo
resources. These elements are then used to calculate such things as: living space per-capita, the pressure and CO₂ levels of the internal atmosphere, and radiation shielding. Individual habitats can be enabled or disabled, in the editor and in flight, to reconfigure the internal space and everything associated with it during the mission.
Inflatable habitats are driven directly by the part pressure.

## Life support

Your crew need a constant intake of Food, Water and Oxygen. Failure to provide for these needs will result in
unceremonious death. Configurable supply containers are provided.

Kerbals evolved in particular conditions of temperature, and at a very low level of radiation. You should reproduce these conditions wherever your crew go, no matter the external temperature or radiation at that point. Or else death ensues. The vessel habitat can be climatized at the expense of ElectricCharge. Environment radiation can be shielded by applying material layers to the hull, with obvious longevity vs mass trade off.

## Psychological needs

The era of tin can interplanetary travel is over. Your crew need some living space, however minimal. Failure to provide enough living space will result in unforeseen events in the vessel, the kind that happen when operators lose concentration. While not fatal directly, they often lead to fatal consequences later on. Some basic comforts can be provided to delay the inevitable mental breakdown. Nothing fancy, just things like windows to look out, antennas to call back home, or gravity rings to generate artificial gravity. Finally, recent research points out that living in a pressurized environment is vastly superior to living in a suit. So bring some Nitrogen to compensate for leaks and keep the internal atmosphere at an acceptable pressure.

## ECLSS

A set of <abbr title="Environmental Control and Life Support System">ECLSS</abbr> components is available for installation in any pod. The scrubber for example, that must be used to keep the level of CO2 in the internal atmosphere below a threshold. Or the pressure control system, that can be used to maintain a comfortable atmospheric pressure inside the vessel. In general, if you ever heard of some kind of apparatus used by space agencies to keep the crew alive, you will find it in this mod.

## ISRU

The stock <abbr title="In Situ Resource Utilization">ISRU</abbr> converters can host a set of reality-inspired chemical processes. The emerging chains provide a flexible and at the same time challenging system to keep your crew alive. The stock ISRU harvesters functionality has been replaced with an equivalent one that is easier to plan against, as it is now vital for long-term manned missions. The means to harvest from atmospheres and oceans is also present, given the importance of atmospheric resources in this regard.

No life-support like mod would be complete without a greenhouse of some kind. The one included in this mod has a
relatively complete set of input resources and by-products, continuous Food production while constraints are met, a lamp that adapts consumption to natural lighting, and pressure / radiation tolerances.

A planetary resource distribution that mimics the real solar system completes the package.

## Reliability

Components don't last forever in the real world. This is modeled by a simple system that can trigger failures on
arbitrary modules. Manufacturing quality can be chosen in the editor, per-component, and improve the MTBF but also
requires extra cost and mass. The crew can inspect and repair malfunctioned components. Redundancy now becomes a key aspect of the design phase.

## Science

<img src="https://github.com/Kerbalism/Kerbalism/raw/master/misc/img/showcase/experiments.png" align="left">

Experiments don't return their science output instantly, they require some time to run. Some complete in minutes, others will take months. Not to worry, experiments can run on vessels in the background, you don't have to keep that vessel loaded.

There are two different kinds of experiments: sensor readings and samples. Sensor readings are just plain
data that can be transferred between vessels without extra vehicular activities, they also can be transmitted back directly.
Samples however require the delicate handling by kerbals, and cannot be transmitted but have to be recovered instead. They also can be analyzed in a lab, which converts it to data that can be transmitted. Analyzing takes a long time, happens transparently to loaded and unloaded vessels alike, and can't be cheated to create science out of thin air. An interesting method is used to bridge existing stock and third-party experiments to the new science system, that works for most experiments without requiring ad-hoc support.

Transmission rates are realistic, and scale with distance to the point that it may take a long time to transmit data from the outer solar system. Data transmission happens transparently in loaded and unloaded vessels. The resulting communication system is simple, yet it also results in more realistic vessel and mission designs.

<img src="https://github.com/Kerbalism/Kerbalism/raw/master/misc/img/showcase/telemetry-demo.gif" align="right">

## Automation

Components can be automated using a minimalist scripting system, with a graphical editor. Scripts are triggered
manually or by environmental conditions. You can create a script to turn on all the lights as soon as the Sun is not visible anymore, or retract all solar panels as soon as you enter an atmosphere etc.

## User Interface

Kerbalism has a nice user interface. A planner UI is available in the editor, to help the user design around all the new mechanics introduced. The planner analysis include resource estimates, habitat informations, redundancy analysis, connectivity simulation, multi-environment radiation details and more. To monitor the status of vessels, the monitor UI is also provided. This looks like a simple list of vessels at first, but just click on it to discover an ingenuous little organizer that allow to watch vessel telemetry, control components, create scripts, manage your science data including transmission and analysis, and configure the alerts per-vessel.

<img src="https://github.com/Kerbalism/Kerbalism/raw/master/misc/img/showcase/planner-demo.gif"  align="left">

## Modules Emulation

Most stock modules and some third-party ones are emulated for what concerns the mechanics introduced by the mod. The level of support depends on the specific module, and may include: simulation of resource consumption and production in unloaded vessels, fixing of timewarp issues in loaded vessels, the ability to disable the module after malfunctions, and also the means to start and stop the module in an automation script.

[KerbalismBanner]: https://github.com/Kerbalism/Kerbalism/raw/master/misc/img/banner.png
[CKAN]: https://forum.kerbalspaceprogram.com/index.php?/topic/154922-ckan-the-comprehensive-kerbal-archive-network-v1264-orion/
[Github releases]: https://github.com/Kerbalism/Kerbalism/releases
[Module Manager]: https://github.com/sarbian/ModuleManager/releases
[CommunityResourcePack]: https://github.com/BobPalmer/CommunityResourcePack/releases
[Readme]: https://github.com/Kerbalism/Kerbalism/edit/master/README.md