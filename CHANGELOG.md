#CHANGELOG

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


