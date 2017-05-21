### CHANGELOG

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
  - fix: too generous gift package for resque missions
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

