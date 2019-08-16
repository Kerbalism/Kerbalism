![KerbalismBanner]

## Welcome to Kerbalism

***Hundreds of Kerbals were killed in the making of this mod.***

# Current version : 3.0.2 
**Download** and sources : **[Github releases]** or CKAN\
**Documentation and support** : **[Github wiki]** - **[Official Discord]** - [Github issues] - [KSP forums thread]\
**License** : [Unlicense] (public domain)\
**Requires** : KSP 1.4.x to 1.7.x , [Module Manager], [CommunityResourcePack]\
**[Mod compatibility]** - [Changelog] - [Dev Builds]

## INTRODUCTION

Go beyond the routine of orbital mechanics and experience the full set of engineering challenges that space has to
offer. This mod extends KSP by simulating the crew, components, resources and environment in a more complex way.
All mechanics can be configured to some degree, or even disabled if you don't like some of them. A big part of the
mod is fully data-driven, so that you can create your own customized game play with only a text editor and a
minimal amount of espresso. Or simply use the set of rules already included, or the ones shared by other users.
What follows is a summary description of the capabilities of the mod, and for a more detailed documentation the user
is invited to read the **[Github wiki]**.

## ARCHITECTURE

Contrary to popular belief, the observable universe is not a sphere of a 3km radius centered around the active vessel.
All mechanics are simulated for loaded and unloaded vessels alike, without exception. Acceptable performance was
obtained by a mix of smart approximations and common sense. The computational complexity is by and large independent
from the number of vessels.

## RESOURCES

This isn't your classic post-facto resource simulation. Consumption and production work for all vessels, all the time,
and is coherent irregardless of warp speed or storage capacity. Complex chains of transformations just work. Enjoy
designing missions without the luxury of stopping the flow of time. No suspension of disbelief required.

## ENVIRONMENT

The environment of space is modeled in a simple yet effective way. Temperature is calculated using the direct solar
flux, the indirect solar flux bouncing off from celestial bodies, and the radiative infrared cooling of their surfaces.
The simulation of the latter is especially interesting, and contrary to popular models it is able to reproduce
satisfactory results for both atmospheric and atmospheric-less worlds. Radiation is implemented using an overlapping
hierarchy of 3D zones, modeled and rendered using signed distance fields. These are used to simulate inner and outer
belts, magnetosphere and even the heliopause. Solar weather is represented by Coronal Mass Ejection events, that
happen sporadically, increase radiation and cause communication blackouts.

## HABITAT

The habitats of vessels are modeled in terms of internal volume, external surface, and a set of dedicated pseudo
resources. These elements are then used to calculate such things as: living space per-capita, the pressure, CO2 and humidity
levels of the internal atmosphere, and radiation shielding. Individual habitats can be enabled or disabled, in the
editor and in flight, to reconfigure the internal space and everything associated with it during the mission.
Inflatable habitats are driven directly by the part pressure.

## BIOLOGICAL NEEDS

Your crew need a constant intake of Food, Water and Oxygen. Failure to provide for these needs will result in
unceremonious death. Configurable supply containers are provided.

## PSYCHOLOGICAL NEEDS

The era of tin can interplanetary travel is over. Your crew need some living space, however minimal. Failure to provide
enough living space will result in unforeseen events in the vessel, the kind that happen when operators lose
concentration. While not fatal directly, they often lead to fatal consequences later on. Some basic comforts can be
provided to delay the inevitable mental breakdown. Nothing fancy, just things like windows to look out, antennas to
call back home, or gravity rings to generate artificial gravity. Finally, recent research points out that living in a
pressurized environment is vastly superior to living in a suit. So bring some Nitrogen to compensate for leaks and keep
the internal atmosphere at an acceptable pressure.

## ENVIRONMENTAL HAZARDS

Your crew evolved in particular conditions of temperature, and at a very low level of radiation. You should reproduce
these conditions wherever your crew go, no matter the external temperature or radiation at that point. Or else death
ensues. The vessel habitat can be climatized at the expense of ElectricCharge. Environment radiation can be shielded by
applying material layers to the hull, with obvious longevity vs mass trade off.

## ECLSS

A set of ECLSS components is available for installation in any pod. The scrubber for example, that must be used to keep
the level of CO2 in the internal atmosphere below a threshold. Or the pressure control system, that can be used to
maintain a comfortable atmospheric pressure inside the vessel. In general, if you ever heard of some kind of apparatus
used by space agencies to keep the crew alive, you will find it in this mod.

## GREENHOUSE

No life-support like mod would be complete without a greenhouse of some kind. The one included in this mod has a
relatively complete set of input resources and by-products, in addition to some more unique characteristics like a lamp
that adapts consumption to natural lighting, emergency harvesting, pressure requirements and radiation tolerance.

## ISRU

The stock ISRU converters can host a set of reality-inspired chemical processes. The emerging chains provide a flexible
and at the same time challenging system to keep your crew alive. The stock ISRU harvesters functionality has been
replaced with an equivalent one that is easier to plan against, as it is now vital for long-term manned missions. The
means to harvest from atmospheres and oceans is also present, given the importance of atmospheric resources in this regard.
A planetary resource distribution that mimics the real solar system completes the package.

## RELIABILITY

Components don't last forever in the real world. This is modeled by a simple system that can trigger failures on
arbitrary modules. Manufacturing quality can be chosen in the editor, per-component, and improve the MTBF but also
requires extra cost and mass. The crew can inspect and repair malfunctioned components. Redundancy now becomes a key aspect
of the design phase.

## SIGNAL

Transmission rates are realistic, and scale with distance to the point that it may take a long time to transmit data from
the outer solar system. Data transmission happens transparently in loaded and unloaded vessels. The resulting
communication system is simple, yet it also results in more realistic vessel and mission designs.

## SCIENCE

Experiments don't return their science output instantly, they require some time to run. Some complete in minutes, others
will take months. Not to worry, experiments can run on vessels in the background, you don't have to keep that vessel loaded.
There are two differnt kinds of experiments: sensor readings and samples. Sensor readings are just plain
data that can be transferred between vessels without extra vehicular activities, they also can be transmitted back directly.
Samples however require the delicate handling by kerbals, and cannot be transmitted but have to be recovered instead. They
also can be analyzed in a lab, which converts it to data that can be transmitted. Analyzing takes a long time, happens
transparently to loaded and unloaded vessels alike, and can't be cheated to create science out of thin air. An interesting
method is used to bridge existing stock and third-party experiments to the new science system, that works for most
experiments without requiring ad-hoc support.

## AUTOMATION

Components can be automated using a minimalist scripting system, with a graphical editor. Scripts are triggered
manually or by environmental conditions. You can create a script to turn on all the lights as soon as the Sun is not
visible anymore, or retract all solar panels as soon as you enter an atmosphere etc.

## USER INTERFACE

The UI provided by this mod took more than 5 minutes to write. A planner UI is available in the editor, to help the
user design around all the new mechanics introduced. The planner analysis include resource estimates, habitat
informations, redundancy analysis, connectivity simulation, multi-environment radiation details and more. To monitor
the status of vessels, the monitor UI is also provided. This looks like a simple list of vessels at first, but just
click on it to discover an ingenuous little organizer that allow to watch vessel telemetry, control components, create
scripts, manage your science data including transmission and analysis, and configure the alerts per-vessel.

## MODULES EMULATION

Most stock modules and some third-party ones are emulated for what concerns the mechanics introduced by the mod. The
level of support depends on the specific module, and may include: simulation of resource consumption and production in
unloaded vessels, fixing of timewarp issues in loaded vessels, the ability to disable the module after malfunctions,
and also the means to start and stop the module in an automation script.

# Downloads and installation

**Download on [Github releases].** Since version 3.0, two packages are available :
- **Kerbalism** is the core plugin, always required.
- **KerbalismConfig** is the default configuration pack.\
  It can be be replaced by other packs distributed elsewhere.

**Requirements**

- Multiple KSP versions are supported, see which ones on the releases page.
- [Module Manager] : must be installed in GameData
- [CommunityResourcePack] : must be installed in GameData

**Third-party configuration packs**

Make sure to install exactly one configuration pack only.\
Don't combine packs unless there is explicit instructions to do so.
- [ROKerbalism](https://github.com/Standecco/ROKerbalism) for Realism Overhaul / RP-1 by standecco
- [SIMPLEX Living](https://spacedock.info/mod/2067) by theJesuit

**Installation checklist** for the "GameData" folder required content : 

- `CommunityResourcePack` (folder)
- `Kerbalism` (folder)
- `KerbalismConfig` (folder, can be replaced by a third-party config pack)
- `ModuleManager.X.X.X.dll` (file)

## Documentation, help and bug-reporting

- **Tutorials and documentation** are available at the **[Github wiki]**

- Need **help** ?

  Ask on the **[official Discord]** or in the **[KSP forums thread]**

- You **found a bug** ?
  - Maybe it's related to another mod ? Check the [Mod Compatibility] page.
  - Maybe it's a known issue ? Check the [GitHub issues] and ask on the [official Discord].
  - Try to reproduce it consistently, take screenshots, save your KSP.log file.

- You want to **report a bug** ?

  Report it on [Github issues] (preferred) or in the [KSP forums thread] (we don't go there often).\
  A good bug report contain the expected behavior, what is happening, steps to reproduce, download link to your KSP.log and screenshots.

## [Mod compatibility] and support

Kerbalism does custom stuff. In some cases this break compatibility with other mods. For a lot of them, we provide support code and configuration files. However sometimes there is too much feature overlap or support is too complex to implement.

See the **[mod compatibility]** page for the full support list.

#### Creating support configs for my part mod

If your part have crew capacity, resource converters, experiments or antennas, you will probably need to tweak some values and replace some PartModules with the Kerbalism ones.

Check the technical guide on the [Github wiki], especially the pages on the [profile](https://github.com/Kerbalism/Kerbalism/wiki/TechGuide-~-Profile) and on the [background simulation](https://github.com/Kerbalism/Kerbalism/wiki/TechGuide-~-Background-Simulation).

#### Using the Kerbalism API in my plugin

Have a look at the [System/API.cs] source code on GitHub. Raise an issue to request more functions.

## Disclaimer and license

This mod is released under the [Unlicense], which mean it's in the public domain.

It includes [MiniAVC]. If you opt-in, it will use the Internet to check whether there is a new version available. Data is only read from the Internet and no personal information is sent. For more control, download the full [KSP-AVC Plugin].

[Github releases]: https://github.com/Kerbalism/Kerbalism/releases
[Github wiki]: https://github.com/Kerbalism/Kerbalism/wiki
[GitHub issues]: https://github.com/Kerbalism/Kerbalism/issues
[Dev Builds]: https://github.com/Kerbalism/DevBuilds/releases
[Mod Compatibility]: https://github.com/Kerbalism/Kerbalism/wiki/Home-~-Mod-Support
[Changelog]: https://github.com/Kerbalism/Kerbalism/blob/master/CHANGELOG.md
[System/API.cs]: https://github.com/Kerbalism/Kerbalism/blob/master/src/System/API.cs
[KSP forums thread]: https://forum.kerbalspaceprogram.com/index.php?/topic/172400-131144-kerbalism-v171/
[official Discord]: https://discord.gg/3JAE2JE

[Module Manager]: https://github.com/sarbian/ModuleManager/releases
[CommunityResourcePack]: https://github.com/BobPalmer/CommunityResourcePack/releases
[MiniAVC]: https://ksp.cybutek.net/miniavc/Documents/README.htm
[KSP-AVC Plugin]: https://forum.kerbalspaceprogram.com/index.php?/topic/72169-13-12-ksp-avc-add-on-version-checker-plugin-1162-miniavc-ksp-avc-online-2016-10-13/
[Unlicense]: https://github.com/Kerbalism/Kerbalism/blob/master/LICENSE

[KerbalismBanner]: https://github.com/Kerbalism/Kerbalism/blob/master/misc/img/banner.png
[build]: https://travis-ci.com/steamp0rt/Kerbalism.svg?style=flat-square&branch=master
