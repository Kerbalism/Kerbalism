## v3.6 for all versions of KSP from 1.5.0 to 1.9.x

 - 2020-02-18

### Changes since the last release

* New radial container parts (Arkolis), soft-deprecation of the old pressurized tanks and the TAC hex cans 
* Ability to show/hide individual vessels in vessel list (SirMortimer)
* Mesh/model based habitat volume/surface evaluation : improve accuracy for non explicitely supported parts (Got)
* Adjusted existing habitat volume/surface configurations (stock, SSPX...) (Got)
* Tweaked engine burn failure function to be more predictable (SirMortimer)
* German localization (Woeller) + other localization fixes (TinyGrox)
* Add CommNetAntennasInfo to incompatible mods warning (Got)
* Feature #587 : Included experiment system : allow for tiered experiments (RP1 request). See wiki for documentation : https://github.com/Kerbalism/Kerbalism/wiki/TechGuide-~-Supporting-Science-Mods#includeexperiment (Got)
* Feature #588 : API methods for Bureaucracy integration (Got)
* Fixed incorrect EC consumption of antennas / transmitters (Got)
* Fix for #546 : Account for stock requireAtmosphere/requireNoAtmosphere experiment definition restrictions (Got)
* Fix for #544 : Disable interaction in experiment popup when control unavailable (Got)
* Fix for #554 : In flight science now removed instantly when a part/vessel is destroyed (Got)
* Fix for #568 : Missing experiment name in action groups (Got)
* Fix for #596 : Universal Storage 2 goo canister animations (Got)

## v3.5 for all versions of KSP from 1.5.0 to 1.9.x

 - 2020-02-13

### Changes since the last release

* Compatibility update for KSP 1.9


## v3.4 for all versions of KSP from 1.5.0 to 1.8.9

 - 2020-01-29

### Changes since the last release

* Fixed game crashing NaN propagation bug happening when the M700 scanner was configured with the GRAVMAX experiment.
* A few config fixes

## v3.3 for all versions of KSP from 1.5.0 to 1.8.9

 - 2020-01-27

### Changes since the last release

* Science experiments in sandbox mode
* Fixed a savegame loading crash
* Better handling of fatal errors: show a message to the user instead of hard crashing without a hint
* Some UI tweaks (compacted module infos, more tooltips in automation, habitat volume in part info and others)
* Tinygrox took on the herculanean task of localizing all of Kerbalism, which means that now a lot of strings can be localized that previously were not. Chinese localization included. 
* Harvester automation fixed by asazernik
* Updated support for: Beyond Home (radiation models), BDB (science configuration), RealAntennas, ROSolar, stock antenna configurations, Near Future Exploration (reflectors), US2 (rtg), SSPX (rotating gravity ring counterweights), ReStockPlus (the new nuclear engine now emits radiation)
* [API updates](https://github.com/Kerbalism/Kerbalism/wiki/TechGuide-~-C%23-API) and extensions (in preparation for RPR compatibility, and part modules that need to produce/consume vessel resources)
* Improved data transmitter module info, now includes effective data rates and EC consumption
* New formula to calculate the shadow period for elliptical orbits. And there is a new (experimental because potentially CPU hungry) method of estimating sun exposure during very fast time warping. Needs to be turned on in settings.
* The stock M700 survey scanner is now integrated in the science system: on data retrieval, unlocks the resource map for this body

## v3.2 for all versions of KSP from 1.5.0 to 1.8.9

 - 2019-12-14

### Changes since the last release

* Spanish localization (elbuglione)
* Some adjustments to engine failure probabilities
* DMagic Orbital Science experiments tweaked to make them a bit easier to finish
* Fixed remaining duration display for very long durations (lampeh)
* Fixed invisible data being saved during time warp (Got)
* Fixed facility level requirement evaluation in science mode for surface samples
* Fix restock+ sample capsule patch
* Engine ignition failure probability tweaks
* Repairing an engine resets it to new state, and now can be done with engines that only have 1-2 ignitions
* Experiments attached using KIS will now work as expected
* Removed mod warning for DynamicBatteryStorage, the latest version disables itself when Kerbalism is present
* API changes for background simulation and planner support of 3rd party part modules
* Added some fixes and checks to avoid corrupting the science data bases when loading older saves
* Fixed ground contact check for harvesters
* New inline and surface attached container parts (TAC-LS with changed textures), the old parts are being phased out
* MoltenRegolithExtraction is now available sooner in the tech tree
* Storm warning preset changed to false, except for crewed vessels
* Added ResearchBodies to the list of mods we warn about, see FAQ on kerbalism.github.io
* New part module for passive shielding
* Add crew experiments to the MLP science lab only, not to all parts that have the lab module
* Don't allow emitter toggling in automation when vessel is unloaded and emitter cannot be toggled, like a NERVA
* Better presets for dump valve settings for water recycler and sabatier processes
* Some config fixes and improvements by GordonDry

## v3.1 for all versions of KSP from 1.5.0 to 1.8.9

 - 2019-10-28

### Changes since the last release

* Support tilted magnetic fields and radiation belts with offsets (Sir Mortimer)
* Updated the RSS radiation model according to http://evildrganymede.net/work/magfield.htm (Sir Mortimer)
* Fixed an error in new science support for DMOS (Sir Mortimer)
* Breaking Grounds DLC: Fixed the science value indication when the transmission is complete (Sir Mortimer)
* Added mod support to query the radiation model, as well as toggle visibility of belts and magnetopause (Sir Mortimer)
* Added mod support for CME prediction accuracy that influences the probability that you get an advanced warning, and its accuracy (Sir Mortimer)
* Configs for SoundingRockets (Arthur, Breach Candy)
* New "SolarPanelFixer" partmodule replacing the "WarpFixer" module. **Unloaded vessels will have incorrect solar panel EC rates until reloaded** (Got)
* Support for (almost all) SSTU solar panels, require SSTU version 0.11.49.161 min (Got)
* Added an editor PAW option to consider / not consider solar panels in the planner (Got)
* Added a third sunlight option in the planner : when slected, solar panel output is estimated using the VAB/SPH sun direction, with tracking/occlusion handling (Got)
* Kopernicus stars support in the simulation and Solar Panels, including multi-star systems support (Got)
* Improvements to the sunlight flux evaluation and occlusion checks (Got)
* Improved sunlight raytracing code, now checking occlusion against all bodies whose apparent diameter is greater than 10 arcmin from the vessel POV (Got)
* Added an additional vertical position offset to tooltips so they don't hide what is under the cursor (Got)
* Auto-assign reference bodies for radiation calculations (Shiolle)
* Fixed DMOS experiment restriction to plantary space. This breaks some DMOS contracts that require experiments in solar orbit (Sir Mortimer)
* Improved shielding efficiency calculation (Free Thinker)
* Apply artificial radiation sources after Gamma Transparency is applied (Free Thinker)
* Fixed the configuration for EVA kerbals, a problem introduced by Serenity (Sir Mortimer)
* Added support for Universal Storage 2 fuel cells (lordcirth)
* Added config for Unkerballed start tech tree (Sir Mortimer)
* Added config for a new ReStock+ probe core RC-XL001 (Sir Mortimer)
* Radiation values in settings now take a preset from Settings.cfg (Sir Mortimer)
* Added radiation source to kerbal atomics engines (Shiolle)
* When transmitting data, prioritize the file with the highest relative value (Sir Mortimer)
* Changed the way science data is handled on recovered vessels. This should provide compatibility with stock KSP (Breaking Grounds) and other mods (Got, Sir Mortimer)
* Added basic science information to telemetry (connection status, total science gain, last data transmitted) (Sir Mortimer)
* Fix for taking an asteroid not working (issue #458) (Got)
* Fix for incoherent resource producers not being detected and failing to trigger the 1000x warp limit (Got)
* Fixed `[PartSet]: Failed to add Resource XXXXX...` log spam (Got)
* Implemented resource rate per consumer/producer information tooltip in the Supply section of the vessel telemetry panel (Got)
* Support configs for SSTU, require SSTU version 0.11.49.161 min (Got, Steamp0rt, Arthur)
* Better handling of multiple hard drives in one part (#479) (Sir Mortimer)
* Fixed gravity ring disappearing with Community Category Kit (CoriW)
* Broken antennas / data transmitters will no longer transmit data (Sir Mortimer)
* Fixes for Breaking Grounds: the return contracts now are doable, and the rover arm scans are drastically reduced in data size (Sir Mortimer)
* UI windows made a bit wider, and they now are all the same widths (Sir Mortimer)
* Some configuration fixes for DMagic Orbital Science (Sir Mortimer)
* Reliability changes: engine failures depending on ignitions and burn duration, radiation damage to solar panels, transmitters and reaction wheels (Sir Mortimer, Valerian)
* Account for distance between radiation emitters (NERVs, RTGs etc.) and habitats when calculating the effect of radiation (Sir Mortimer)
* EVAs will receive radiation from nearby emitters (Sir Mortimer)
* Lead was replaced by a lighter alloy composite as a shielding material. For our type of radiation, it provides the same shielding effect with less mass. (Sir Mortimer)
* Warn users about incompatible mods or missing CommunityResourcePack (Sir Mortimer)
* Dropped support for KSP 1.4 (Sir Mortimer)
* Kidney-shaped radiation belts (EagleEyeLogic)
* Added surface gamma radiation to celestial bodies (Sir Mortimer)
* Detect habitat shielding against CMEs using raytracing from habitats to the sun (Sir Mortimer)
* Added a warning when CRP is missing, or known mods with incompatibilities are installed (Sir Mortimer)
* Hard drives with customizable capacity (Sir Mortimer)
* Intensity, duration and frequency of CME events depend on solar cycle (Sir Mortimer)
* Many fixes in science gathering and transmission (Got)
* Added a science archive window, available in flight, KSC and editors (Got)
* All configs moved to KerbalismConfig folder, even the ones that remained in Kerbalism core previously (Got)
* Overall cleanup and improvement of UI windows (Got)
* Radiation unit can be changed from rad to Sievert in the cfg file (Sir Mortimer)
* Reorganized in-game preferences, added some new options, removed some old and obscure ones. Now with easy/normal/moderate/hard presets. (Sir Mortimer)
* Atmosphere humidity removed. Humidity controller removed. (Got)
* Show career radiation dosis in Kerbal info window (Sir Mortimer)
* Added + fixed radiation configs for Atomic Age and mK2Expansion (Shaddow Phönix)
* Fixed life support slot upgrades in SSPX (Hauke Lampe)
* Unpressurized habitats include a fair warning in the part description and title (zer0Kerbal)

## v3.0.2 for all versions of KSP from 1.4.0 to 1.7.x

 - 2019-06-08

### Changes since the last release

* ACTUALLY fixed the unintended need to repeat the same experiments multiple times for full science value. (SirMortimer, Arthur)

## v3.0.1 for all versions of KSP from 1.4.0 to 1.7.x

 - 2019-06-08

### Changes since the last release

* Fixed the unintended need to repeat the same experiments multiple times for full science value. (Arthur)

## v3.0 for all versions of KSP from 1.4.0 to 1.7.x

 - 2019-06-07

### Changes since the last release

* New Science system. See https://github.com/Kerbalism/Kerbalism/wiki/Science-System (Sir Mortimer, Arthur, and a lot of people who helped)
* Add Reliability to USI Nuclear Reactors (PiezPiedPy)
* Fix ISRU capacities for CryoTanks and USI (PiezPiedPy)
* Correct LH2 storage capacities of the Radial container for AirlineKuisine, CryoTanks and USI (PiezPiedPy)
* Near Future Electrical tweaks: Uranite drilling, storage and ISRU processing added (PiezPiedPy)
* Textures Unlimited support (HaullyGames)
* Update habitats for the new V2 and Making History DLC pods (PiezPiedPy)
* Recalculated habitat atmosphere leakage, was originally calculated for a Human day which is 4x longer (PiezPiedPy)
* Added an 'EVA's available' indicator to the Planner and Monitor (PiezPiedPy)
* Optimized Planner: Part 1 - Chill the stuttering in VAB/SPH (PiezPiedPy)
* Reduced Mass of ECLSS and Chemical Processors from 450kg to 40kg (Sir Mortimer)
* CO2 poisoning warning message will pop up sooner to give you some time to fix the issue (Sir Mortimer)
* Preemptive maintenance: if a component is found not to be in very good condition during inspection, it can be serviced to avoid a failure (Sir Mortimer)
* Fixed emitters (shields) can be used in automation tab again (Sir Mortimer)
* Processes ECLSS, Fuel Cells, Chemical Plant etc. can be controlled from automation tab again (Sir Mortimer)
* Added Kerbalism flags (Mzxs)
* Adjusted N2 leakage (Sir Mortimer)
* When analyzing science in a lab, don't drive people crazy with the "Transmission finished" message for every bit of researched data (Sir Mortimer)
* Going to EVA will now loose a nominal amount of nitrogen to the airlock. The amount can be changed in the settings (Sir Mortimer)
* Fixed the bug where monoprop appeared out of nowhere when leaving a vessel that had none in it (#288) (Sir Mortimer)
* Monoprop+O2 fuel cell is now available sooner in the tech tree. Basic Science unlocks this process along with the fuel cell (Sir Mortimer)
* Added LSS system diagrams, and a small guide on how to set up O2 + water recycling (Sir Mortimer)
* Hide Sickbay from automation if it is unavailable (Sir Mortimer)
* Reverted the part-specific process handling introduced with PR #280 as it caused other issues (Sir Mortimer)
* Fixed the issue with placing parts in a symmetry group > 2 in the editor (Sir Mortimer)
* Added API: mods now can register callbacks for failure events triggered by Kerbalism Reliability (Sir Mortimer)
* Added API: mods now can provide their own communication characteristics (antenna info). Currently we just support RemoteTech, this allows other mods to support Kerbalism (Sir Mortimer)
* Fixed #249: NEOS incompatibility (Sir Mortimer)
* Added option for automated sample analysis, works like automated data transmission (Sir Mortimer)
* SCANsat support for new science system (Sir Mortimer)
* Some changes to the communication system: data rates, EC consumption and range calculation changed (Sir Mortimer)
* Support for solar panel efficiency curve. Panels can degrade over time. (Sir Mortimer)
* KER Parts will act as hard drives (Sir Mortimer)
* A lot of new experiments (Arthur, theJesuit)
* Mod Support for new science: DMagic Orbital Science, Station Science (Arthur)
* Some performance improvements with caches (Sir Mortimer)
* Added support for Breaking Ground surface experiments (steamp0rt, Gotmachine, SirMortimer)
* Split Kerbalism into core and config packages for better usability with other configurations (looking at you, RO)


------------------------------------------------------------------------------------------------------

## v2.1.2 for all versions of KSP from 1.3.1 to 1.6.x

 - 2019-02-04

### Changes since the last release

* Fix Kerbalism parts search filters and missing tab in the VAB/SPH (PiezPiedPy)
* Fix processes not calculating capacities correctly (PiezPiedPy)
* Made the PartUpgrade for module slots require ProfileDefault (theJesuit)
* Took away some of the Partupgrade as I upgraded my MM fu. (theJesuit)
* Fixed compatability with Module Manager 4.x (steamp0rt, with lots of help from blowfish)

------------------------------------------------------------------------------------------------------

## v2.1.1 for KSP 1.6.x, 1.5.x , 1.4.x and 1.3.1
 - 2018-12-22

### Changes since the last release

* Updated for KSP 1.6.x

------------------------------------------------------------------------------------------------------

## v2.1.0 for KSP 1.5.x , 1.4.x and 1.3.1
 - 2018-12-18

### Changes since the last release

* xmitDataScalar = 1 for Bluedog DB early probes. (Gordon Dry)
* Add support patch for the USI-NF compatibility patch. (Gordon Dry)
* Make CCK play nice with FilterExtensions. (Gordon Dry)
* Added sickbay RDU to four additional parts (with 1 slot each):
* Bluedog_DB - MOL Orbital Habitation SegmentS
* StationPartsExpansionRedux - PTD-5 'Sunrise' Habitation Module
* NearFutureSpacecraft - PPD-24 Itinerant Service Container
* Duna Direct's Kerbin Return Vehicle (KRV) (Gordon Dry)
* Sickbay TV is only available in crewable parts (crew >3) without a laboratory now and it uses 0.25 EC/s instead of 12 EC/s. It's 64x less effective now - and same as effective as 100% comfort. (Gordon Dry)
* Sickbay RDU now uses 3.5 EC/s instead of 35 EC/s but also works 5x slower (cures 0.02 rad/h now). (Gordon Dry)
* Added support for Kerbalow KA-330 Inflatable Space Hotel. (Gordon Dry)
* Added missing xmitDataScalar to Support/OrbitalScience.cfg and also added UniversalStorage2 compatibility. (Gordon Dry)
* Converted all remaining png and mbm textures to dds (Gordon Dry)
* The connection signal icon alternates between yellow and red when signal strength is below 5%. (HaullyGames)
* Connection: connection rate is minimum rate in ControlPath. (HaullyGames)
* Kerbals on EVA got a radio to transmit science data. Antenna range limited to 35km. (Gordon Dry)
* File Manager shows remaining transmission time when hovering over file. (HaullyGames)
* RemoteTech - Added signal strength based on Dist/maxDist. (HaullyGames)
* Added "Set as Target" in Monitor. (HaullyGames)
* Panel - Define the hover area to be the same size of font size. (HaullyGames)
* Increased SCANSat experiment sizes. A planetary map shouldn't be smaller than a temperature reading. (Sir Mortimer)
* Connection Manager Panel, click in signal Icon to open the Connection Panel. (HaullyGames)
* CommNet: disable stock transmission buttons and transmission action group. (HaullyGames)
* Add support for per-part resource simulation which is ONLY used for resources that are NO_FLOW, such as EnrichedUranium. (madman2003)
* Fix support for dump options that don't use valves. (madman2003)
* Make the planner outputs less quirky by running the simulator several cycles to reach steady state. (madman2003)
* NearFutureSolar: Add reliability for curved solar panels. (madman2003)
* NearFutureElectrical: Support Reactors and Reprocessor. (madman2003)
* Don't show resources in telemetry that have practically zero value, avoid flickering telemetry. (madman2003)
* Don't fill habitat with CO2 rich atmosphere when enabling habitat in VAB. (madman2003)
* Air pump should only work on planets with naturally breathable atmosphere. (madman2003)

------------------------------------------------------------------------------------------------------

## v2.0.0 for KSP 1.5.x , 1.4.x and 1.3.1
 - 2018-19-10

### Changes since the last release

* Support for KSP 1.5.x

* Also "normal" launch clamps' generators are not simulated in planner by default (Gordon Dry)
* Quick fix to crewable launch towers like the FASA Launch Tower: disable the habitat by default and disable simulating the generator in planner by default (Gordon Dry)
* Dump valve not saving its state on vessel change, bug fixed (PiezPiedPy)
* Fixed SSPX IVA rotation. (HaullyGames)
* Kerbals consume slightly different amounts of food, water and oxygen, and react differently to stress and radiation. When under stress they can make mistakes, some do better than others. (Sir Mortimer)
* A laboratory with high level crew members in it will work faster (Sir Mortimer)
* Harvesters will work better with an engineer on board. (Sir Mortimer)
* Fixed another icons sometimes not displaying bug (PiezPiedPy)
* SSPX 2.5m Greenhouse now producing food at the expected rate. (theJesuit)
* ResourceBalance run the pressurizing\depressurizing (old "equalize\venting"), it gives priority to habitats with crew. (HaullyGames)
* Habitats equalize/venting function changed to pressurizing/depressurizing, crewed habitats have priority while multiple habitats are pressurizing. (HaullyGames)
* New AirPump process added to control pressure in breathable environment. (HaullyGames)
* Fixed missing N2 when mod is added in an existing game. (HaullyGames)
* Fixed issue with CommNet not updating for unloaded vessels (leomike, HaullyGames)
* Added bandwidth preferences in Game Settings. (HaullyGames)
* Added 'GoTo' button in Penal, the vesselType button allowed to change to other vessel. (HaullyGames)
* Fixed Orbital Science experiments size (Sir Mortimer)
* Fixed antenna consumption and Automation controller to antennas with GenericAnimation. (HaullyGames)
* Automation has devices sorted by name. (HaullyGames)
* Updated docs for the recent 1/16 buff (Sir Mortimer)
* Don't show fixed antennas in device manager (Sir Mortimer)

------------------------------------------------------------------------------------------------------

## v1.9.0 for KSP 1.4.x and 1.3.1
 - 2018-15-9

### Changes since the last release

* Kerbal LS rates have been recalculated based on 1/16 of a Humans consumption due to their size and day length (PiezPiedPy)
* A dump valve has been added to the Fuel Cells (PiezPiedPy)
* Reliability added to the active shield and slightly increased its effectiveness (PiezPiedPy)
* Vessels with RemoteTech antennas fitted that where missing on the monitor due to old SaveGames, bug fixed (PiezPiedPy)
* Stock antennas can now be controlled by automation (Yaar Podshipnik)
* Devices shown in the device manager are now sorted (Sir Mortimer)
* Fixed the EC issue when accelerating to extremely fast time warp while a vessel is in shadow (Sir Mortimer)
* Improved vessel search in monitor: you can search for the name of the central body and the vessel name (Sir Mortimer)
* Added vessel type icons and filter buttons to include/exclude vessels in the monitor list (Sir Mortimer, PiezPiedPy)
* SSPX PDT-6 'Star' Utility Module balanced: shield strength, costs, tech level requirement and reliability (Sir Mortimer)
* SSPX greenhouses have been rebalanced and missing exercise equipment for some SSPX parts added (Dr.Jet)
* If Community Category Kit is installed then Kerbalism will place its parts into CCK respective categories (Sir Mortimer)
* Game preferences now includes Kerbalism settings that previously were in Settings.cfg (Sir Mortimer)

### Known Issues

* KerboKatz FrameLimiter mod is known to make the Icons disappear

------------------------------------------------------------------------------------------------------

## v1.8.0 for KSP 1.4.x and 1.3.1
 - 2018-08-21

### Changes since the last release

 * Kerbalism documents are now available here: https://kerbalism.readthedocs.io Note they are still a Work in Progress

 * Fixed the icons sometimes not displaying bug and icon scaling bug (PiezPiedPy)
 * RemoteTech support now integrates correctly with the planner and signal system (PiezPiedPy)
 * Improved RemoteTech support (simulate in planner buttons, reliability, antenna EC consumption) (Gordon Dry & PiezPiedPy)
 * RemoteTech antennas will need power even if vessel is unloaded (Sir Mortimer)
 * RemoteTech antennas fitted to Vessels without power will no longer relay signals to other vessels (Sir Mortimer)
 * RemoteTech antennas can now be enabled/disabled in Automation (Sir Mortimer)
 * RemoteTech antennas can now break down due to reliability failures (Sir Mortimer)
 * Chemical Plant and ECLSS parts are now surface attachable, ECLSS part capacity increased to support 3 crew (PiezPiedPy)
 * Added support for ConfigurableContainers, they now have 6 additional tank configs as defined below:
 * KerbalismSupplies (Food, Water) - KerbalismBreathing (Oxygen, Nitrogen)
 * KerbalismWaste (Waste, WasteWater) - KerbalismGreenhouse (CarbonDioxide, Ammonia, Water)
 * KerbalismFuelcellH2 (Oxygen, Hydrogen) - KerbalismFuelcellMP (Oxygen, MonoPropellant)  (Gordon Dry)
 * Containers have had their volume and mass calculated with a calculator (PiezPiedPy)
 * Science labs can now reset experiments (PiezPiedPy)
 * CryoTanks are now simulated in the background, also fuel boiloff is simulated in the planner (PiezPiedPy)
 * Reverted the Quick'n'dirty fix for GPOSpeedFuelPump because it has been fixed with v1.8.14 (Gordon Dry)
 * Added a fix to make sure there is a module Reliability for parachutes, also for RealChute/RealChuteFAR (Gordon Dry)
 * Scaled the ISRU's capacity to be more representative of their size (PiezPiedPy)
 * All priority type processes have been removed and replaced with a Dump button that configures the dumped resource type(s) to dump overboard, the Dump button is also usable InFlight allowing for changes of strategies on the go (PiezPiedPy)
 * Overhaul of all Chemical Plant and ISRU processes using CRP densities and molar masses (PiezPiedPy)
 * SOE process now converts wasted Carbon into Shielding, Haber process now needs EC (PiezPiedPy)
 * Hydrazine process now outputs Oxygen and requires EC and A New Nitrogen injected Hydrazine process added (PiezPiedPy)
 * Vessel group filter can now search for vessel names that contain multiple words (Sir Mortimer)
 * Changed the Small Supply Container to be 0.625m in diameter instead of 0.5m (Gordon Dry)
 * Added radiation belts to ExtraSolar planets and moons - science definition texts still missing (Gordon Dry)
 * Fixed a commented out bracket in another patch that hindered the Bluedog_DB Geiger counter from being a sensor (Gordon Dry)
 * Configurable parts can now contain the same process in multiple slots (PiezPiedPy)
 * Fixed MRE not running when shielding is full or does not exist on a vessel (PiezPiedPy)
 * MRE process now outputs a small amount of CO2 (PiezPiedPy)
 * GeigerCounter science experiment fixes for OPM and NewHorizons. Also SEP support fixes (Gordon Dry)
 * Rebalanced Sabatier and Anthraquinone processes to output LiquidFuel and Oxidizer at Stock ratio of 9:11 (PiezPiedPy)
 * Rebalanced H2+O2 and LH2+O2 fuel cells to output more realistic EC levels (PiezPiedPy)
 * Some tooltip colors changed from a nasty hard to see red to a nice gold (PiezPiedPy)
 * Fixed antennas bug having no science data rate in languages other than English (PiezPiedPy)
 * Support for AirlineKuisine (PiezPiedPy)

### For Developers

 * Support for development on mac now included, also with help in the CONTRIBUTING.md file (Sir Mortimer)

------------------------------------------------------------------------------------------------------

## v1.7.1.1 for KSP 1.4.4 and 1.3.1
 - 2018-07-03

### Changes since the last release

 * Localization translations added (Sir Mortimer [GRUMP])
 * MM errors fixed (Gordon Dry)

### For Developers

 * Added a title parameter to the PlannerController that shows in the buttons text (PiezPiedPy)

------------------------------------------------------------------------------------------------------

## v1.7.1 for KSP 1.4.4 and 1.3.1
 - 2018-07-02

### Changes since the last release

 * The Chemical Plant and External Life Support have had a repaint (PiezPiedPy)
 * Added Xenon Gas to the Radial Pressurized Tanks (PiezPiedPy)
 * Added Engines and RCS that use EC and/or LH2 when supported by a mod such as USI to the Planner (PiezPiedPy)
 * Added part specific tags (Gordon Dry)
 * A simple support patch for RealBattery - no final solution (Gordon Dry)
 * Recalculated pressure control EC consumption to be more realistic (PiezPiedPy)
 * Increase all crewable parts' EC because ECLSS uses EC constantly, you wanna survive in the Apollo LM, right? (Gordon Dry, PiezPiedPy)
 * Quick'n'dirty fix for GPOSpeedFuelPump to avoid shielding to be pumpable by default (Gordon Dry)
 * Reliability: mtbf depends mass; lighter parts last longer - max. ~16 years (~64 years in high quality), heavier parts last shorter - min. ~4 years (~16 years in high quality). Built in reliability modules don't take the whole part's mass into account, but their respective extra_mass (Gordon Dry)
 * Greenhouses now act like a scrubber and also will not use CO2 or produce O2 when in a breathable atmosphere (PiezPiedPy)
 * Reliability: mass and cost difference between standard and high quality is now relative to the part type (Gordon Dry)
 * Allow vessel config when there is no vessel signal (PiezPiedPy)
 * Fixed EVA Scrubber, ooops was broken by changes to Habitation (PiezPiedPy)
 * Fuel Cells are now configurable with H2+O2 and Monoprop+O2 processes (PiezPiedPy)
 * Added LH2+O2 processes to the Fuel Cells for USI and CryoTanks Support (PiezPiedPy)
 * Added Hydrogen Liquefaction and Liquid Hydrogen Evaporator processes to USI and CryoTanks Support (PiezPiedPy)
 * Fixed CryoTanks NRE in Planner and added LH2 to radial tanks for CryoTanks Support (PiezPiedPy)

### For Developers

------------------------------------------------------------------------------------------------------

## v1.7.0 for KSP 1.4.3
 - 2018-06-18

### Changes since the last release

 * Moved Humidity into Habitation (PiezPiedPy)
 * Pressure and CO2 Poisoning rates are back to normal (PiezPiedPy)
 * GravityRing NRE's in VAB/SPH bug fixed (PiezPiedPy)
 * Click through prevention added (some things can still be clicked through the windows, due to using KSP's old style Gui) (PiezPiedPy)
 * Overhaul to transmitter use, planning and monitoring, data rates, signal strength, EC cost and targets now work. Internal transmitters are separate from external transmitters and will only transmit telemetry and command control, they are also shown separately in the planner and are constantly powered unless you run out of EC, with the added benefit of loosing contact with DSN and subsequent control. External transmitters will lose contact when retracted, break, or if you run out of EC rendering long distance comms, call home and your ability to transmit science to zero. External transmitters will also stop using EC when retracted. EVA suits now contain a small internal transmitter for transmitting telemetry and controlling remote probes and rovers etc. All transmitters have had their EC usage changed to more realistic values and are also combinable. There is a minor drawback though, when changing scenes with the [ESC]Pause menu you may notice the target readout pointing to the wrong vessel and some signals that where previously dis-connected wrongly connecting back online, simply changing scene from for example the Space Center to Tracking Station will solve all errors in the network. *Thanks to (PiezPiedPy) for the transmitter overhaul.*

 * Nitrogen added to pods on rescue, Humidity controller now detects breathable atmospheres (PiezPiedPy)
 * Kerbalism Communotron 8 transmitter is back (PiezPiedPy)
 * Transmitters have reliability added and tweaked EC costs, also removed simulate button for internal transmitters (PiezPiedPy)
 * Signal lost/found during GameScene changes and SaveGame load, annoying bug fixed (PiezPiedPy)
 * Support for Connected Living Spaces fixed (PiezPiedPy) also thanks go to Gordon-Dry ;)
 * Module Manager cache bug fixed (PiezPiedPy)
 * Added an upgrade part to the TechTree that adds a slot to the Manned pods, ECLSS module and Chemical plants (PiezPiedPy)
 * Chemical plant capacity fixed, it was nurfed by accident in a previous release (PiezPiedPy)
 * TechTree locations for Humidity controller and External ECLSS moved for a better Career balance (PiezPiedPy)
 * Fix ContractConfigurator bug (PiezPiedPy)

### For Developers

------------------------------------------------------------------------------------------------------

## v1.6.0 for KSP 1.4.3
 - 2018-05-26

### Changes since the last release

 * Harvesters can now extract Nitrogen from the surface (JadeOfMom)
 * Filters can now extract Ammonia from the atmosphere (JadeOfMom)
 * New parts MiniPump and RadialPump to extract Water, Nitrogen and Ammonia from oceans (thanks to JadeOfMom for the Harvesters and PiezPiedPy for the parts)
 * Harvesters are now spec'd at 10% abundance by default (madman2003)
 ** The percentage is specified in the UI when selecting the process the harvester will run
 ** The percentage can be overruled for individual parts
 * Harvesting rate scales linearly with abundance (madman2003)
 * Water harvesting has been buffed by a factor 6 (at the reference 10% abundance) compared to release 1.5.1 (madman2003)
 * Restored antenna simulation button (PiezPiedPy)
 * Fix greenhouse animation for SSPX (madman2003)
 * Allow gravity rings that use solid walls to be shielded (madman2003)
 * Fix harvesters background simulation, as well as scaling the produced resources by abundance (madman2003)
 * Fix SSPX inflatable habitats and centrifuges to have crew capacity and somewhat realistic habitat volumes/areas (madman2003)
 * Add nitrogen storage to SSPX modules containing a pressurization module (madman2003)
 * Add habitat to Kerbalism gravity ring (madman2003)
 * A first attempt at scaling Kerbalism UI to follow KSP UI scaling
 ** By default using the scaling configured for KSP, with an additional scale factor in Settings.cfg if needed to overrule the default
 * Clicking the middle mouse button on the popout menu will now close the popout window if it is already open.
 * Humidity Control and a new Life Support Unit part (courtesy of PiezPiedPy)
 * Scale down food production to realistic levels, and make it dependent on CO2 (madman2003)
 ** Every kerbal now requires 2x Kerbalism greenhouse, or 3x 2.5m SSPX greenhouses, or 6x 3.5m SSPX greenhouses for permanent food production
 ** SSPX greenhouses include a habitat area, leaving very little space for food production
 * Water recycling is more realistic, recovering only 85% of Water (this is better than what ISS achieves in real life)
 ** Ammonia and CO2 are recovered from Water, rather than producing Waste. This is to avoid (future) conflicts between the mineral content of urine and feces
 * Kerbalism CO2 tanks are now full by default in order to supply Greenhouses with CO2 (madman2003)
 * Fix display of Habitat volume and space in tooltip, they used to be swapped (madman2003)
 * Minor fixes to SSPX config file (madman2003)
 * Add Electrolysis with H2 priority and Sabatier with H2O priority (madman2003)
 * Give Greenhouses the basic resources needed to run (madman2003)
 * There is now a help file on GitHub for those wishing to report bugs or contribute to Kerbalism.
   see [CONTRIBUTING.md](https://github.com/MoreRobustThanYou/Kerbalism/blob/master/CONTRIBUTING.md)

### For Developers

 * Profile importing is now available to modders who wish to import their own processes, rules, supplies etc (thanks to PiezPiedPy)
   see Issue #2 on GitHub for more information [Here](https://github.com/MoreRobustThanYou/Kerbalism/issues/2)
 * Updated Profiler GUI to use the Canvas system. Added Reset averages & Show zero calls buttons,
   a Framerate limiter, avg calls and frame counter (thanks go to PiezPiedPy)

------------------------------------------------------------------------------------------------------

1.5.1
  - SSPX support
  - Adds a version of the Water Electrolysis process that doesn't dump excess Oxygen
  - Added huge 3.5m containers
  - German/Russian Localization (thank you Riggers and player101!)
  - Probably some other stuff I forgot

1.5.0
  - Removed Signal completely
  - CME blackouts are now supported by RemoteTech, but only in 1.8.10.3 or above
  - Added @HaullyGames Deploy system. Some things, such as landing legs, will take EC to extend/retract
  - Fixed some Science nullrefs

1.4.4
  - Fixed DSN stations showing with RemoteTech/CommNet
  - Fixed Kerbalism overriding RemoteTech locks

1.4.3
  - RemoteTech antennas now show in the Ship Monitor

1.4.2
  - Updated B9Switch config

1.4.1.4
  - Fixed SSPX config
  - Signal is now OFF by default

1.4.1.3
  - Improve SSPX support

1.4.1.2
  - Implement player101's science fix.

1.4.1.1
  - Actually fix relay strength this time

1.2.9
  - ported to KSP 1.3.0.1804
  - improved SSTU support patch (@Maxzhao1999)

1.2.8
  - rebalanced atmosphere leak rate from ISS data
  - new process in chemical plant: SCO (selective catalytic oxidation of NH3 to N2)
  - radiation fields can now be oriented using a specific reference body
  - lowered abundance thresholds of ore and co2 harvesters
  - scale part icon of pressurized radial containers
  - custom order of part icons in our category
  - Coatl Aerospace support patch (@dieDoktor)
  - fix: properly detect if drill head intersect ground
  - fix: no more signal warnings on prelaunch
  - fix: false detection of incoherent oxygen production
  - fix: try to not break AmpYear/BonVoyage solar panel output detection

1.2.7
  - remember window position (@PiezPiedPie)
  - depressurizing habitats also vent WasteAtmosphere
  - improved ec and supply icon tooltips in monitor
  - fix: too generous gift package for rescue missions
  - fix: comfort from parts not overriding environment factors
  - fix: disable modules on tutorial scenarios
  - fix: wrong pressurized state inside breathable atmosphere
  - fix: excessive cryotank background consumption

1.2.6
  - improved Bluedog support patch (@ModZero)
  - SSTU support patch (@Maxzhao1999)
  - reduced monoprop fuel cell rates, adapt to required EC
  - fix: planner not considering solar panels after last update
  - fix: exception in Configure module for crafts that never entered VAB
  - fix: duplicate extend/retract in some supported antennas

1.2.5
  - detect and avoid issues at high timewarp in external modules
  - hack stock solar panels and RTGs to use resource cache
  - RTGs decay over time, with an half-life of 28.8 kerbin-years
  - corrected all chemical reactions, some were very wrong
  - fix: solar panel sun visibility sampling error at max timewarp for loaded vessels (#95)
  - fix: impossible to guarantee coherency in resource simulation of loaded vessels (#96)

1.2.4
  - SMURFF compatibility patch (@eberkain)
  - Laboratory module satisfy stock contracts
  - fix: resource amount not clamped to capacity
  - fix: script editor window UB after scene changes

1.2.3
  - resource cache production/consumption simulate ALL_VESSEL_BALANCED
  - added Waste Incinerator process to ISRU chemical plants
  - moved Waste Compressor process from manned pods to ISRU chemical plants
  - EVA kerbals now have a non-regenerative scrubber, with fixed duration
  - increased amount of EC and Oxygen on EVA kerbals
  - improved description of configure setups
  - scale low-gain and high-gain antenna distances differently in supported planet packs
  - lowered xmit scalar threshold used to deduce if data is file or sample
  - safemode malfunctions don't stop timewarp anymore
  - support patch for RLA Stockalike Continued (@YaarPodshipnik)
  - NearFuture PB-AS-NUK emit radiation (@YaarPodshipnik)

1.2.2
  - fix: remove stock antenna from probe cores

1.2.1
  - new ECLSS component: Waste Compressor, compress Waste into Shielding
  - new ECLSS component: Monoprop Fuel Cell, burn Monoprop and has Water+Nitrogen by-products
  - atmosphere is breathable on all bodies containing oxygen, when pressure is above 25 kPA
  - proper experience bonus calculation in stock converters and harvesters (@Gotmachine)
  - MOLE solar panels support in planner and background simulation  (@Gotmachine)
  - support patches for SXT & HGR, improved patches for VSR & HabTech, and more (@Eberkain)
  - support patch for OrbitalTug (@PiezPiedPie)
  - fix: cache stop updating after planting flag (#50, #52, #75)
  - fix: exception in main loop when space weather is disabled (#78)
  - fix: exception in planner analysis when comfort modifier is used in a process (#79)
  - fix: greenhouse harvest ready message spam
  - fix: missing configure setup descriptions in some cases

1.2.0
  - process: it is possible to specify the set of output resources that can be dumped overboard
  - background/planner simulation of stock converter module support DumpExcess flag
  - fuel cell, water recycler and waste processor adapt consumption to required production
  - reduced atmosphere leak rate by 80%
  - allow scripts to be executed even without electric charge
  - added HardDrive to labs
  - removed ControlRate setting
  - fat-finger breakdown event support new Science system (#53)
  - fix: do not allow stock science lab modules around when Science is enabled
  - fix: toggling processes on unloaded vessels using automation doesn't really toggle it (#71)

1.1.9
  - SoundingRockets support patch by ValynEritai
  - BluedogDesignBureau support patch by ValynEritai & Maxzhao1999
  - OrbitalScience sigint antenna support patch by alaxandir
  - increased amount of resources on_rescue
  - waste processor can be toggled on/off
  - rebalanced ECLSS modules, capacity now proportional to crew capacity
  - rebalanced climatization: less EC cost, longer degeneration time
  - greenhouse has integrated pressure control
  - monitor panels require a connection on unmanned vessels
  - only remove stock conversions from ISRU, don't touch third-party ones
  - there is now an Extra/ folder containing patches not enabled by default
  - ad-hoc habitat volume/surfaces for all stock parts and a ton of other ones, by schrema
  - fix: amount of configurable resources is reset after vessel load in editor
  - fix: wrong capacity in ISRU setups
  - fix: wrong savegame version saved
  - fix: missing setup details in Configure window when module is defined after (#57)
  - fix: missing name of non-stock tech in Configure module part tooltip (#58)
  - fix: monitor don't forget selected vessel when flagged as debris after selection (#60)
  - fix: automation panel throw exception on duplicate id (#65)
  - fix: asset bundle loading cause problems in HullCam/KronalVesselViewer (#66)
  - fix: ContractConfigurator packs not working with Laboratory/Antenna module (#68)

1.1.8
  - the science dialog is back (but can be hidden with settings)
  - stop disabling Science if ScienceRelay is detected
  - new API functions to deal with science data
  - better support for SSPX by Yaar Podshipnik
  - telemetry env readings require sensor parts
  - kerbin magnetotail now extend just beyond mun orbit
  - SCANsat support in automation
  - show BodyInfo window automatically the first time user enter map view or tracking station
  - tweak antenna distances on supported planet packs
  - fix: popup message about data when entering from EVA
  - fix: configure window is closed when related part is deleted in editor
  - fix: minor fixes in Science.cfg
  - fix: heliopause crossing contract
  - fix: pass credits instead of data size when firing OnScienceReceived event
  - fix: do not throw exception during data hijacking if science container is not present
  - new Experiment module with custom situation support (WIP, currently disabled)

1.1.7
  - HardDrive module implement IScienceDataContainer
  - monitor auto-switch vessel
  - window get scroll bars if necessary
  - fix: prevent camera scroll is working again
  - fix: show monitor panels depending on features enabled

1.1.6
  - improved the ui
  - JX2Antenna support patch by YaarPodshipnik
  - ContractConfigurator support patch by Gotmachine

1.1.5
  - Rule framework
    - multiple profiles can cohexist in the same installation, only one is enabled from user settings
    - Process: vessel-wide resource consumer/producers driven by modifiers
    - split Supply out of Rule, for additional flexibility
    - can use resource amount as a modifier
    - many new modifiers, to leverage the information provided by habitat
  - Features framework
    - user specified features are set by a flag in settings
    - other features are detected automatically from the modifiers used in the active profile
    - inject MM patches during loading screen, before MM is executed
    - third parties can check for specific features or profiles by using NEEDS[]
    - parts are enabled/disabled automatically depending on features used
  - Resource cache
    - new 'exact, order-agnostic' algorithm for consumption/production chains at arbitrary timesteps
    - consider interval-based outputs in depletion estimates
  - Configure
    - new module Configure: can select between setups in the VAB or in flight
    - setups can specify resources and/or modules
    - setups can include extra cost and mass
    - setups can be unlocked with technologies
    - configuration UI, that show info on modules and resources for a setup
  - Habitat
    - new module Habitat: replace CLS internal spaces
    - used to calculate internal volume in m^3, and surface in m^2
    - can be disabled/enabled even in flight to configure the internal space as required
    - support inflatable habitats
    - can be pressurized/depressurized
    - can keep track of level of CO2 in the internal atmosphere
    - can be added to parts with no crew capacity
  - Greenhouse
    - improved module: Greenhouse
    - lamps intensity is determined automatically, and is expressed in W/m^2
    - can have radiation and pressure thresholds for growth
    - can require an arbitrary set of input resources
    - can produce an arbitrary set of by-product resources
    - growth will degenerate if lighting/radiation/pressure conditions aren't met
  - ISRU
    - planetary resource definitions based on real data
    - new module Harvester: for crustal/atmospheric resource extraction, use abundance/pressure thresholds
  - Wet workshops
    - some stock tanks can now be configured as either fuel tanks or habitats, even in flight
  - QualityOfLife
    - new module Comfort: replace Entertainment and provide a specific bonus, added to some stock parts
    - modified module GravityRing: now provide firm-ground bonus
    - living space is calculated from volume per-capita
  - Radiation
    - shielding required is now determined by habitat surface, and map to millimeters of Pb
    - rtg emit a small amount of radiation
  - Planner
    - single page layout, with panel selection
    - show consumers/producers of a resource in tooltip
    - improved/redesigned most panels
    - redundancy analysis for Reliability panel
  - Reliability
    - improved subsystem: Reliability
    - support arbitrary third party modules
    - components are now disabled when they fail
    - two types of failures: malfunctions (can be repaired) and critical failures (can't be repaired)
    - safemode: there is a chance of remote repairs for unmanned vessels
    - components can be assigned to redundancy groups
    - an optional redundancy incentive is provided: when a component fail, all others in the same redundancy group delay their next failure
    - removed 'manufacturing quality'
    - can select quality per-component in the vab, high quality means higher cost and/or mass but longer MTBF
  - Signal
    - improved: focus on data transmission rates and differences between low-gain and high-gain antennas
    - high-gain antennas: can communicate only with DSN
    - low-gain antennas: can communicate with DSN and with other vessels
    - low-gain antennas: can be flagged as 'relay' to receive data from other vessels
    - can choose what level of control to lose without a connection:
      . 'none' (lose all control),
      . 'limited' (same as CommNet limited control) and
      . 'full' (only disable data transmission)
    - easy parameters for antenna definitions
    - simple data rate attenuation model
    - render data transmission particles during data transmission
    - disable CommNet automatically when enabled
    - connection status is obtained by CommNet or RemoteTech when signal is disabled
    - new signal panel in vessel info window, show data rates, destination and file being transmitted
  - Science
    - new subsystem: Science, improve on data storage, transmission and analysis
    - transmit data over time, even in background
    - analyze data over time, even in background
    - the background data transmission work with Signal, CommNet or RemoteTech.
    - new module: HardDrive, replace stock data container, can flag files for transmission and lab analysis
    - new module: Laboratory, can analyze samples and produce transmissible data
    - work with all science experiment modules, both stock and third-party, by hijacking the science result dialog
    - data storage: can store multiple results of same experiment type, can transfer to other parts without requiring EVA
    - data storage: can still be stored on EVA kerbals, and EVA kerbals can take/store data from/to pods
    - data UI: show files and samples per-vessel, can flag for transmission or analysis, can delete files or samples
    - properly credit the science over time
    - do not break science collection contracts
  - Automation
    - removed the Console and command interpreter
    - new scripting system: not text-based anymore
    - new component control and script editing UI
    - script editor UI highlight parts for ease of use
  - Misc
    - ported to KSP 1.2.1
    - consistent part naming scheme
    - rebalanced mass/cost of all parts
    - improved part descriptions
    - do not change stock EC producers/consumers anymore
    - adapted all support patches, removed the ones not necessary anymore
    - shaders are loaded from asset bundle
    - removed workarounds for old SCANsat versions
    - some Settings added, others removed
    - action group support for all modules
    - properly support multiple modules of the same type in the same part
    - optimized how animations in modules are managed
    - can optionally use the stock message system instead of our own
    - can optionally simulate the effect of tracking pivots on solar panels orientability
    - removed helmet handling for EVA kerbals
    - doesn't require CRP anymore, but it will still work along it
    - improved how crew requirements are specified in modules
    - show limited body info window when Sun is selected, instead of nothing
    - new contract: analyze sample in space
    - new contract: cross the heliopause
    - rebalanced ec consumers/producers
    - show tooltips in vessel info
    - use common style for all part info tooltips
    - AtomicAge engines emit radiation (ThePsion5)
    - more love for VenStockRevamp patch (YaarPodshipnik)
  - Profile: 'Default'
    - rewritten from scratch
    - balanced consumption rates from real data
    - balanced container capacity from real data
    - water
    - co2 poisoning
    - pressurization: influence quality of life
    - configurable ECLSS in pods: scrubber, water recycler, pressure control, waste processing
    - configurable supply containers: can store Food, Water, Waste
    - configurable pressurized tanks: can store Oxygen, Nitrogen, Hydrogen, Ammonia
    - greenhouse: require Ammonia and Water, produce Oxygen and WasteWater as by-product, need to be pressurized, has radiation threshold
    - stock ISRU plants can be configured with one among a set of reality-inspired chemical processes
    - stock drills can be configured with a specific resource harvester
    - stock atmo experiment is also used as configurable atmospheric harvester
    - stock fuel cells act like real fuel cells
    - new part: Chemical Plant, can execute reality-inspired chemical processes, unlocked early in the tech tree
  - Profile: 'Classic'
    - this profile mimick the old default profile, without the new stuff
  - Profile: 'None'
    - choose this if you want to play with third-party life support mods
  - Bugs fixed
    - fix: nasty problem with interaction between cache and analytical sunlight estimation
    - fix: radiation body definitions were not loaded in some cases
    - fix: planner, stock laboratory EC consumption wasn't considered
    - fix: planner, solar panel flux estimation was considering atmo factor even in space
    - fix: planner, correctly skip disabled modules
    - fix: spurious signal loss message when undocking
    - fix: maintain notes and scripts even after docking/undocking
    - fix: highlighting of malfunction components in pods
    - fix: in monitor UI signal icon, show all relays in the chain
    - fix: bug with killing eva kerbals while iterating the list of crew
    - fix: exception when loading dead eva kerbals
    - fix: module index mismatch when loading dead eva kerbals

1.1.4
  - replaced Malfunction with Reliability module
    - support multiple reliability modules per-part and per-component
    - RCS, Greenhouse, GravityRing and ActiveShield can malfunction
    - Antennas can't malfunction anymore
    - can specify trait and experience level required for repair
    - disabled automatically if TestFlight is detected
  - new module PlannerController: permit to include or exclude part modules
    from the planner calculations using a toggle in right-click UI in the VAB
  - entertainment modules can be configured to ignore internal space
  - add some Entertainment to Ven Stock Revamp small inflatable hab (YaarPodshipnik)
  - SurfaceExperimentPackage science tweaks patch (YaarPodshipnik)
  - telemetry experiment is added coherently to all probes (YaarPodshipnik)
  - geiger counter science definitions for NewHorizon (BashGordon33)
  - entertainment added to Space Station Part Expansion cupolas and habitats
  - some KIS items provide a small amount of entertainment
  - fix: solar panel malfunctions were not applied in loaded vessels
  - fix: malfunction highlights throwing exception in some circumstances
  - fix: relativistic time dilation when orbit is not properly set
  - fix: better approximation for atmospheric gamma absorption

1.1.3
  - do not use an EVA module anymore, to avoid triggering Kerbal duplication bug
  - rescaled geiger counter part to half size
  - clarified scrubber tooltip description
  - fix: exception in computer system when two networked vessels have the same name
  - fix: correct lifetime estimates during simulation steps when meals are consumed

1.1.2
  - replaced tutorial notifications with KSPedia entries
  - added a very small radial oxygen container unlocked at survivability
  - added RadiationOnly and StressOnly profiles
  - updated CLS interface dll
  - balancl: food capacity reduced by 50% in 0.625m food container
  - balance: oxygen capacity reduced by 75% in big radial oxygen container
  - balance: rearrange US goo/matlab in the tech tree for consistency
  - balance: tweak US supply containers capacity
  - balance: active shield moved to experimental science, made more powerful
  - fix: body info panel will not break when sun is selected

1.1.1
  - new automation system: vessel computer, console, scripts
  - improved body info window
  - vessel info show crew specialization
  - lights consume ec in background and are considered in planner
  - support: KerbalAtomics engines radiation patch by TheSaint
  - support: NewHorizons radiation definitions patch by BashGordon33
  - support: SampleReturnCapsule antenna patch
  - support: SurfaceLights in planner and background simulation
  - balance: reduced Vall/Io surface radiation
  - balance: (realism profile) less co2 capacity in pods
  - balance: (realism profile) kerbals eat twice per-day
  - fix: crossing belt contract condition & warnings
  - fix: hiding the GUI will not show any window
  - fix: EVA headlight not working without a profile

1.1.0
  - scrolling inside a window will not zoom the camera anymore
  - optimization: compute vessel position only once
  - balance: radiation levels fade off between zones
  - balance: increased NERV radiation
  - fix: fields rendering on some Nvidia GPUs
  - fix: planner exception with unmanned vessels
  - fix: Vall use 'surface' radiation model
  - fix: workaround for SCANsat issue #234 (versions before 16.6)
  - fix: resource cache will not break BackgroundProcessing
  - fix: cross belts contract can be completed with unmanned vessels
  - fix: active shield not working in background

1.0.9
  - new magnetosphere & radiation models
  - support for stock bodies, OPM & RSS
  - press ALT+N on map view or tracking station to show/hide the magnetic fields
  - you can also press Keypad 0/1/2/3 to quickly toggle between them
  - new module: Emitter, can add/remove radiation from a vessel
  - the stock nuclear engine emit some radiation
  - the old high-tech food container has been repurposed as an active shield
  - tech progression patch for ETT by autumnalequinox
  - OPM science definitions for geiger counter by BashGordon33
  - pressing ALT in the VAB make the planner consider the crew at full capacity
  - optimized line rendering and improved antialiasing
  - fix: issue with greenhouse when waste_rate is zero
  - fix: greenhouse growth becoming NaN when attached using KIS
  - fix: signal disconnections in some circumstances
  - fix: detect when converters are full in background
  - fix: cryotank background cooling and boiloff rates proportional to capacity
  - fix: vessel info window height when unmanned vessel is selected
  - fix: support SCANsat version 16.4+

1.0.8
  - stable signal link rendering
  - science experiment definitions for the geiger counter, thanks BashGordon33!
  - dropped support for savegames from version 0.9.9.4 or older
  - only allow 1 malfunction module per-part
  - fix: monitor reporting poor manufacturing quality for all vessels
  - fix: depletion estimates reporting perpetual with very small rates
  - fix: possible division by zero in resource simulation
  - fix: wrong amount of ec consumed by cryotank background simulation

1.0.7
  - improved planner and vessel info ui
  - removed the old parts that were disabled ages ago, can still be downloaded from here:
    https://github.com/ShotgunNinja/Kerbalism/raw/master/misc/OldParts.zip

1.0.6
  - a better temperature model
  - vessel info window can show solar, albedo and body flux
  - planner consider all resources from all supported modules
  - improved planner calculations for scrubbers & recyclers
  - relativistic time dilation on resource consumption and production (disabled by default)
  - optimized raytracing
  - atmospheric decay of unloaded vessels can be disabled in settings
  - RTG output decay over time can be disabled in settings
  - scrubber module: waste to resource ratio can be configured
  - sensor module: more environment readings available
  - fix: greenhouse waste bonus calculation
  - fix: antenna throwing exceptions on active debris
  - fix: greenhouse natural lighting now consider atmospheric absorption
  - fix: exception when re-entering a debris from eva with vessel info opened

1.0.5
  - optimized vessel monitor
  - optimized debris decay in atmosphere
  - recycler module efficiency can depend on technology progression

1.0.4
  - refactored overall architecture
  - new resource system: faster, stronger
  - optimized signal system
  - optimized background resource simulation
  - vessel cache: smart FIFO eviction strategy
  - optimized malfunction module
  - more stable depletion estimates
  - improved signal link rendering
  - vessel info show consumption/production rates
  - vessel monitor and planner ui remain visible on mouse over
  - balance: decreased malfunction rate
  - balance: reduced engine malfunction penalty
  - fix: background resource simulation inconsistencies at extreme timewarp
  - fix: vessels not getting included in relay network calculations
  - fix: scrubber and recycler inconsistencies during timewarp blending
  - fix: greenhouse assuming the part has shutters
  - fix: atmosphere description in vessel info window

1.0.3
  - recompiled for KSP 1.1.3
  - interplanetary coronal mass ejections
  - fix: scrubber efficiency reverting to 50%
  - fix: 30 days manned orbit contract not generated again after completion
  - patch to allow Shielding production on Extra Planetary Launchpad, by Enceos

1.0.2
  - planner: warn the user if resource capacity isn't enough to avoid inconsistencies at extreme timewarp
  - tweaked MOLE solar panel output
  - realism and tac emulation profiles presence can be queried using NEEDS[]
  - fix: typo in EC amount checking for relay antennas
  - fix: resque missions getting hydrazine instead of monoprop when RealFuel isn't installed

1.0.1
  - atmosphere is not considered breathable under the ocean surface
  - is possible to force kerbals to have helmet and oxygen by holding SHIFT when going to EVA
  - simulate CryoTanks boiloff in background, CryoTanks EC consumption supported in planner
  - use Hydrazine instread of MonoPropellant for the EVA suit, if RealFuels is installed
  - made RealFuels aware of default profile waste resources
  - some modules are not simulated in background if BackgroundProcessing is detected
  - fix: shortAntenna will not break existing vessels when signal mechanic is disabled
  - fix: telemetry experiment data size reverted to previous behaviour
  - fix: 'put a kerbal in orbit for 30 days' contract will not consider resque missions

1.0.0.0
  - optimized everything
  - improved planner calculations, thanks Barrin!
  - contracts: put a kerbal in orbit for 30 days, cross the radiation belt, harvest food in space
  - temperature simulation and storm mechanic will work for arbitrarily deep body hierarchies
  - made the telemetry experiment more interesting
  - only show belt warnings if a radiation rule is present
  - MM patch for Tundra Exploration
  - tweaked entertainment factor
  - realism profile has been tweaked
  - barebone profile now also include radiation mechanic
  - science tweaks support for Dmagic and Universal Storage experiments
  - coverters and drills background simulation consider trait bonus
  - fix: correct sunlight evaluation at extreme timewarp
  - fix: problem with interval-based rules at extreme timewarp
  - fix: resource-related breakdown events
  - fix: muting messages will also prevent stopwarp
  - fix: resources given to resque mission when claw is used
  - fix: geiger counter is considered for satellite contracts

0.9.9.9
  - science lab EC consumption is simulated in background
  - science labs EC cost tweaked
  - fix: ui offsets respect scaling
  - fix: removed signal lock exploit for unmanned debris
  - fix: missing data in part prefabs will no longer break background simulation
  - fix: problem with vessel info and resource names being used instead of rule names

0.9.9.8
  - depend on CommunityResourcePack, for real this time
  - the Realism profile just got better: water filters and more, check it out
  - can show signal link line per-vessel, in map and trackingview
  - it is now possible to define custom antenna range scopes
    and to redefine the ranges of the default scopes
  - solar panels output rebalanced
  - right click in the planner menu to go back in the lists
  - rule: can be influenced by a comma-separed list of modifiers
  - rule: waste buffer size configurable
  - rule: can force waste resources to be hidden in pods
  - new module: Recycler, full support in planner and monitor
  - Greenhouse module can consume an optional input resource
  - more hooks, check them out
  - disabled the 'redundancy incentive' function, that was too slow
  - fix: planner calculate phone-home bonus when signal is disabled
  - fix: do not show unknown objects in monitor anymore
  - fix: potential problem with docking in the tutorials
  - fix: can now take data from a geiger counter

0.9.9.7
  - re-added Food/Oxygen definitions temporarely to the default profile
  - changed key combination to mute/unmute messages to CTRL+N

0.9.9.6
  - the default profile now require CommunityResourcePack
  - NEW PARTS! inline food containers and radial oxygen tank by Tygoo7
  - NEW PART! geiger counter by Naazari1382
  - phased out old food and oxygen containers
  - messages can be muted and unmuted by using CTRL+M
  - moved gravity ring higher in the tech tree
  - experimental Realism profile
  - experimental TAC-LS emulation profile
  - fix: depletion estimates with meal-based rules
  - fix: probes and other parts getting supply resources in some occasions
  - fix: vessel info window doesn't show supplies depletion estimates for unmanned vessels
  - fix: corrected automatic waste capacity in pods
  - fix: correct depletion estimates at extreme timewarps
  - fix: greenhouse doesn't consume waste when there is no lighting

0.9.9.5
  BIG REFACTOR
  - can run arbitrary rules that consume a resource and accumulate a value per-kerbal
  - rules can be influenced by environment
  - existing mechanics reimplemented as a set of rules and enabled by default
  - with no rules it degenerate into a background simulation of the resources with ui
  - you can write your own rules, go check in profiles/ directory
  CRP COMPATIBILITY
  - food/oxygen properties have been changed to match CRP ones
  - food/oxygen consumption changed to more realistic rates
  - previous savegames will keep working (yay!)
  CONFIGURATION
  - settings.cfg to customize the simulation
  - choose a file in the profiles/ directory, create your own, or don't use one at all
  - signal mechanic is disabled automatically if you are using RemoteTech or AntennaRange
  - malfunction mechanic is disabled automatically if you are using DangIt
  OTHER MODS SUPPORT
  - SCANsat modules re-enable automatically when EC is back
  - support for NearFuture reactors, fission generators and radioisotope generators
  - support Planetary Base System converters
  - support for Origami antennas
  - NearFutureSpacecraft, CryoTanks and KerbalAtomics MM patches by Fraz86
  - greenhouse & scrubber modules work on arbitrary resources
  - more hooks added, go check out
  MALFUNCTIONS
  - malfunctioned components are highlighted, can be toggled on/off from the monitor ui
  - engineers can inspect parts and get an estimate of lifetime
  - use new curve for part aging
  - use new method to incentive redundancy
  - seriously lowered the malfunction rate
  - antennas will last longer
  - limit of 2 malfunctions per-component at max
  - reduced range penalty for antenna malfunctions
  - radiation don't influence malfunctions anymore
  - planner show correct malfunctions/year estimates
  MISC
  - recompiled against KSP 1.1.2
  - tech descriptions are updated automatically, no need to do that in MM patches anymore
  - improved tech description visibility
  - phased out the high-tech 1.25m food container
  - more robust depletion estimates
  - new Sensor module to add environment readings to a part
  - storm messages can be disabled per-vessel
  - storms can be turn off in settings
  - added a partlist icon in the vab and moved parts there
  BALANCE
  - lowered mass of shielding
  - reduced mass of parts in general
  - breakdown events also incur a reputation penalty
  - increased time before breakdown a bit
  - reduced frequency of storms
  - increased science value of experiments a bit
  - moved small fixed panel to basic science
  - rebalanced solar panels outputs to visually match number of panels
  BUGFIXES
  - fix: bug with multiple ModuleResourceConverters in background and active flag
  - fix: helmet state no forced on top of KIS anymore
  - fix: problems with flowState
  - fix: problems with resque kerbals on eva
  - fix: setup resources for resque missions
  - fix: kerbal climate property recover slowly to avoid exploit
  - fix: thermometer readings


0.9.9.4
  - new part! an artificial gravity hab by mehka
  - new part! small food container by Nazari1382
  - new part! a better 1.25m food container by tygoo7
  - added support for ConnectedLivingSpace
  - vessel info window
  - new 'medium' scope for antennas
  - doubled amount of EC on eva suits
  - minor changes in the EnergyTweaks
  - reduced radiation influence over malfunctions
  - MM patches for NearFutureElectrical and NearFuturePropulsion by Fraz86
  - default antenna patch by speedwaystar
  - greenhouse module can specify an emissive object for the lamps
  - fix: greenhouse module do not assume there is a shutter anymore
  - fix: monitor tooltip, this time for real
  - fix: antennas should work with contracts now
  - fix: issue with EVA prop getting created out of thin air
  - fix: curved solar panels weren't working
  - fix: kerbals don't stop eating anymore


0.9.9.3
  - technologies can be customized
  - radiation influence malfunctions
  - support for NearFutureSolar
  - moved all parts to utility menu
  - no more oxygen warnings at prelaunch
  - tweaked some EnergyTweaks values
  - MM patches in directory tweaks can now be deleted
  - fix: problem with EVA monoprop
  - fix: planner doesn't cover staging icons anymore
  - fix: monitor was visible in main menu
  - fix: monitor tooltip problems with scrubber
  - fix: problem with negative part
  - fix: bug in malfunction penality


0.9.9.2
  - added tags to parts
  - reverted to stock monoprop behavior from/to EVA temporarely


0.9.9.1
  - ported to KSP 1.1.0.1230
  - removed assumptions on technologies order
  - versioning of serialized data
  - doubled radiation life expectancy at max shielding
  - added CommunityTechTree patch courtesy of DarkonZ
  - monitor: vessels can be assigned to groups
  - monitor: can filter vessels by group
  - monitor: added time to depletion of Food & Oxygen to supply icon tooltip
  - planner: EC cost of active radiators respect enabled/disabled toggle
  - planner: EC cost of new wheels, respect motor toggle
  - hooks: scan for assembly only once
  - hooks: new function InjectKerbal()
  - bugfix: proper text clamping for vessel name in monitor
  - bugfix: release input locks even when not in flight
  - bugfix: human-readable durations weren't using earth time settings
  - bugfix: SCANsat resource scanners weren't consuming EC in background


0.9.9.0
  First public release
