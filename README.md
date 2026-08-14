![KerbalismBanner]

### Welcome to Kerbalism

***Hundreds of Kerbals were killed in the making of this mod.***

Kerbalism is a mod for Kerbal Space Program that alters the game to add life support, radiation, ISRU chains, part and engine failures and an entirely new way of doing science.

#### Features summary :

- **Life support** : Kerbals consume food, water and oxygen and will die if they aren't provided. Various processes can be added to recycle or produce those resources in situ.
- **Stress** : Without adequate living space, atmospheric pressure and comforts, Kerbals will get stressed and start making mission-threatening mistakes.
- **Radiation** : Kerbalism simulate the space radiation environment and radiation from local sources. A vessel must be adequately shielded and mission planning must be adjusted to avoid the most deadly places like planetary radiation belts.
- **Reliability** : Components have a limited operational lifetime and will fail over time. Optional **KerbalismEngineFailures** adds engine ignitions, burn-time limits and turn-on failures.
- **ISRU** : Instead of the easy "ore to everything" stock system, producing and processing resources in situ uses a semi-realistic set of extraction and conversion rules.
- **Science over time** : Experiments produce data over time, up to several years. This data is also transmitted over time, making science collection a relaxing background mechanism instead of the stock click-spammy system. Kerbalism also replace the stock labs "infinite science", rebalance existing experiments and add many probe, satellite and late-game manned experiments.
- **Background processing** : All vessels are simulated continuously, not only the currently active one. Life support, resource processing, experiments and data transmission are simulated in the background while keeping a low performance overhead.
- **Vessels management** : All vessels can be monitored and controlled to some extent from a centralized user interface available in all scenes.
- **Mission planning** : The editor user interface allow to evaluate your vessel design against various environments and provide extended information about all aspects of the mod.

For more detailed information, go to the **[Documentation]** and the **[FAQ]**.

## Current version: 3.41
 
**Download** : **[Github releases]** - **[CKAN]**  
**Docs & support** : **[Documentation]** - **[Discord]** - [FAQ] - [Github issues] - [KSP forums thread]  
**License** : [Unlicense] (public domain)
**KSP version** : 1.12.x  
**Requires** : [Module Manager], [CommunityResourcePack], [HarmonyKSP], [KSPCommunityFixes]

**[Mod compatibility]** - [Changelog]

## Download and installation

**Download on [Github releases] or use [CKAN]** 

Two packages are required :
- **Kerbalism** is the core plugin, always required.
- **KerbalismConfig** is the official configuration pack.\
  It can be be replaced by other packs distributed elsewhere.

Optional :
- **KerbalismEngineFailures** adds engine ignition counts, rated burn time and turn-on failures. Recommended by CKAN with official KerbalismConfig. Conflicts with TestFlight / RO / RP-1.

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

## Important notice

There may be compatibility issues with some mods.  R-T-B is maintaining it now and doing his best.  PRs are most welcome.

Mods introducing mechanisms or features that greatly differ from the stock ones are more likely to cause issues.
This notably include most "future scope" mods like KSPIE, USI MKS, BlueShift, as well as "interstellar" scoped planet packs.

## Mod compatibility and support

The **[mod compatibility]** page may contain outdated information, but can still help to avoid some issues.

Kerbalism does very custom stuff. This can break other mods. For a lot of mods that breaks or need balancing, we provide support code and configuration patches. However some mods are incompatible because there is too much feature overlap or support is too complex to implement.

**SystemHeat users**: Kerbalism's SystemHeat extras (`KerbalismSystemHeatCore` / `KerbalismSystemHeatCompat`) are exclusive. Do **not** also install the upstream SystemHeat extras (`SystemHeatFissionEngines`, `SystemHeatFissionReactors`, `SystemHeatIonEngines`, `SystemHeatConverters`, `SystemHeatHarvesters`, `SystemHeatCryoTanks` / `SystemHeatBoiloff`, or the legacy `Kerbalism-SystemHeat`). Mixing both will double-patch parts. See [Extras/README.md](Extras/README.md) for details.

## Documentation, help and bug-reporting

- **Tutorials and documentation** are available in the **[Documentation]** (MkDocs; English / 中文)

- Need **help** ?

  Ask on **[Discord]**\
  Also see [this short YouTube video](https://www.youtube.com/watch?v=eW9pW_839sw) about useful UI tips.

- You **found a bug** ?
  - Maybe it's related to another mod ? Check the [Mod Compatibility] page.
  - Maybe it's a known issue ? Check the [GitHub issues] and ask on [Discord].

- You want to **report a bug** ?
  - Install the [KSPBugReport] plugin and generate a bug report with it. Support requests that don't provide full logs and KSP database dumps are often ignored.
  - Report it on [Discord] (preferred) or on [Github issues].

- You want to **contribute** or add support for your mod ?
  - Check the technical guide in the [Documentation]
  - Pull requests are welcome, especially for mod support configs. For code contributions, it is recommended to talk to us on [Discord] before engaging anything.
  - Read the [contributing] documentation
  - To build the plugin from the source code, see [CONTRIBUTING.md](CONTRIBUTING.md#setup-guide)
  - To preview docs locally: `pip install -r requirements.txt` then `mkdocs serve`

## Disclaimer and license

This mod is released under the [Unlicense], which mean it's in the public domain.


[Github releases]: https://github.com/Kerbalism/Kerbalism/releases
[Documentation]: https://kerbalism.github.io/Kerbalism/
[GitHub issues]: https://github.com/Kerbalism/Kerbalism/issues
[Dev Builds]: https://github.com/Kerbalism/DevBuilds/releases
[Mod Compatibility]: https://kerbalism.github.io/Kerbalism/mod-support/
[Changelog]: https://github.com/Kerbalism/Kerbalism/blob/master/CHANGELOG.md
[Contributing]: https://github.com/Kerbalism/Kerbalism/blob/master/CONTRIBUTING.md
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

[FAQ]: https://kerbalism.github.io/Kerbalism/faq/
