![KerbalismBanner]

### Welcome to Kerbalism

***Hundreds of Kerbals were killed in the making of this mod.***

Kerbalism is a mod for Kerbal Space Program that alters the game to add life support, radiation, ISRU chains, part and engine failures and an entirely new way of doing science.

#### Features summary :

- **Life support** : Kerbals consume food, water and oxygen and will die if they aren't provided. Various processes can be added to recycle or produce those resources in situ.
- **Stress** : Kerbals require adequate living space, atmospheric pressure and comforts. When those are lacking, they will get more and more stressed and start making mistakes.
- **Radiation** : Kerbalism simulate the space radiation environement and radiation from local sources. A vessel must be adequately shielded and mission planning must be adjusted to avoid the most deadly places like planetary radiation belts.
- **Reliability** : Components have a limited operational lifetime and will fail over time, and engines have a limited amount of ignitions and a limited burn time.
- **ISRU** : Instead of the easy "ore to everything" stock system, producing and processing resources in situ uses a semi-realistic set of extraction and conversion rules.
- **Science over time** : Experiments produce data over time. The data is also transmitted over time, making science collection an automated background mechanism instead of the stock click-spammy system. Kerbalism also removes the stock labs "infinite science" mechanism, rebalance the stock experiments and add many probe, satellite and late-game manned experiments.
- **Background processing** : All vessels are simulated continuously, not only the currently active one. Life support, resource processing, experiments and data transmission are simulated in the background, even during time warp.
- **Vessels management** : Kerbalism provide a centralized user interface to monitor and control all your vessels. Many actions can be performed without having to switch to a vessel.
- **Mission planning** : The editor user interface allow to evaluate your vessel design against the various environments and provide extended information about all aspects of the mod.

### Frequently Asked Questions: [FAQ]

## Current version: 3.15
 
**Download** : **[Github releases]** - **[CKAN]**  
**Docs & support** : **[Github wiki]** - **[Discord]** - [FAQ] - [Github issues] - [KSP forums thread]  
**License** : [Unlicense] (public domain)  
**KSP version** : 1.8.x to 1.12.x  
**Requires** : [Module Manager], [CommunityResourcePack], [HarmonyKSP], [KSPCommunityFixes]

**[Mod compatibility]** - [Changelog] - [Dev Builds]

## Download and installation

**Download on [Github releases] or use [CKAN]** 

Two packages are required :
- **Kerbalism** is the core plugin, always required.
- **KerbalismConfig** is the official configuration pack.\
  It can be be replaced by other packs distributed elsewhere.

**Requirements**

- [Module Manager]
- [HarmonyKSP]
- [KSPCommunityFixes]
- [CommunityResourcePack] (required by **KerbalismConfig** only, third-party config packs might not require it) 

**Configuration packs**

The Kerbalism official configuration pack is a feature set maintained by the Kerbalism contributors. It tries to achieve a good balance between realism, difficulty and complexity, is primarily balanced against the stock game and has a "current space tech" scope. Mixing it with other mods that significantly change the stock scale, scope or gameplay isn't well supported and not recommended for a good experience.

Several alternate configuration packs have been created by third party modders :

- [ROKerbalism](https://github.com/Standecco/ROKerbalism) : Official config pack for RO and [RP1](https://github.com/KSP-RO/RP-0), maintained by the RP1 team.
- [SIMPLEX](https://spacedock.info/mod/2300) : Stockalike simplified life support and ISRU designed to work well with the SIMPLEX tech tree and other mods by theJesuit.
- [SkyhawkKerbalism](https://forum.kerbalspaceprogram.com/index.php?/topic/208204-skyhawk-kerbalism-v01-alpha-release/) : A [BDB](https://forum.kerbalspaceprogram.com/index.php?/topic/122020-1123-bluedog-design-bureau-stockalike-saturn-apollo-and-more-v1103-%D0%BB%D1%83%D0%BD%D0%B0-17june2022/) focused profile with revamped LS, science and ISRU going alongside a custom tech tree by CessnaSkyhawk.
- [LessRealThanReal(ism)](https://forum.kerbalspaceprogram.com/index.php?/topic/189978-112-less-real-than-realism-rp-1-with-less-r-v203/) : A config pack part of a larger mod based on RP1 but made to played at stock scales without RO. 

Make sure to install exactly one configuration pack only.\
Don't combine packs unless there is explicit instructions to do so.

## Mod compatibility and support

Checking the **[mod compatibility]** page is **mandatory before installing Kerbalism on a heavily modded game**.

Kerbalism does very custom stuff. This can break other mods. For a lot of mods that breaks or need balancing, we provide support code and configuration patches. However some mods are incompatible because there is too much feature overlap or support is too complex to implement.

## Documentation, help and bug-reporting

- **Tutorials and documentation** are available at the **[Github wiki]**

- Need **help** ?

  Ask on **[Discord]** or in the **[KSP forums thread]**\
  Also see [this short YouTube video](https://www.youtube.com/watch?v=eW9pW_839sw) about useful UI tips.

- You **found a bug** ?
  - Maybe it's related to another mod ? Check the [Mod Compatibility] page.
  - Maybe it's a known issue ? Check the [GitHub issues] and ask on [Discord].

- You want to **report a bug** ?
  - Install the [KSPBugReport] plugin and generate a bug report with it. Support requests that don't provide full logs and KSP database dumps are often ignored.
  - Report it on [Github issues] (preferred) or in the [KSP forums thread] (we don't go there often).

- You want to **contribute** or add support for your mod ?
  - Check the technical guide on the wiki
  - Pull requests are welcome, especially for mod support configs. For code contributions, it is recommended to talk to us on [Discord] before engaging anything.
  - Read the [contributing] documentation
  - To build the plugin from the source code, read the [BuildSystem] documentation

## Disclaimer and license

This mod is released under the [Unlicense], which mean it's in the public domain.


[Github releases]: https://github.com/Kerbalism/Kerbalism/releases
[Github wiki]: https://github.com/Kerbalism/Kerbalism/wiki
[GitHub issues]: https://github.com/Kerbalism/Kerbalism/issues
[Dev Builds]: https://github.com/Kerbalism/DevBuilds/releases
[Mod Compatibility]: https://github.com/Kerbalism/Kerbalism/wiki/Home-~-Mod-Support
[Changelog]: https://github.com/Kerbalism/Kerbalism/blob/master/CHANGELOG.md
[Contributing]: https://github.com/Kerbalism/Kerbalism/blob/master/CONTRIBUTING.md
[BuildSystem]: https://github.com/Kerbalism/Kerbalism/blob/master/BuildSystem/README.MD
[System/API.cs]: https://github.com/Kerbalism/Kerbalism/blob/master/src/System/API.cs
[KSP forums thread]: https://forum.kerbalspaceprogram.com/index.php?/topic/201171-kerbalism
[Discord]: https://discord.gg/3JAE2JE

[KSPBugReport]: https://github.com/KSPModdingLibs/KSPBugReport
[Module Manager]: https://ksp.sarbian.com/jenkins/job/ModuleManager/lastStableBuild/
[CommunityResourcePack]: https://github.com/BobPalmer/CommunityResourcePack/releases
[HarmonyKSP]: https://github.com/KSPModdingLibs/HarmonyKSP/releases
[KSPCommunityFixes]: https://github.com/KSPModdingLibs/KSPCommunityFixes/releases
[CKAN]: https://forum.kerbalspaceprogram.com/index.php?/topic/197082-ckan
[Unlicense]: https://github.com/Kerbalism/Kerbalism/blob/master/LICENSE

[KerbalismBanner]: https://github.com/Kerbalism/Kerbalism/raw/master/misc/img/banner.png

[New and Noteworthy]: https://github.com/Kerbalism/Kerbalism/wiki/New-And-Noteworthy
[FAQ]: https://github.com/Kerbalism/Kerbalism/wiki/FAQ
