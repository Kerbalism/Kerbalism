                             _  ________ _____  ____          _      _____  _____ __  __
                            | |/ /  ____|  __ \|  _ \   /\   | |    |_   _|/ ____|  \/  |
                            | ' /| |__  | |__) | |_) | /  \  | |      | | | (___ | \  / |
                            |  < |  __| |  _  /|  _ < / /\ \ | |      | |  \___ \| |\/| |
                            | . \| |____| | \ \| |_) / ____ \| |____ _| |_ ____) | |  | |
                            |_|\_\______|_|  \_\____/_/    \_\______|_____|_____/|_|  |_|

                              Hundreds of Kerbals were killed in the making of this mod.
=======================================================================================================================


#INTRODUCTION

  Kerbalism extend Kerbal Space Program by simulating additional aspects of space missions.
  Anything will happen coherently to loaded and unloaded vessels alike, without exceptions.
  All mechanics can be enabled, disabled and utterly configured by the user.


#SIMULATION

  Many aspects are simulated:
  - the environment: temperature, radiation, space weather
  - the habitat: living space, comforts, pressure, co2 levels
  - the kerbals: life support, quality of life
  - the components: reliability, automation
  - the data: communications, analysis and transmission
  - the resources: consumption and production in background


#PLANNER

  A planner GUI is available in the VAB, to help the user design around all the new aspects introduced.
  Resource estimates, habitat informations, redundancy analysis: no matter what, the planner got you covered.
  Recommended by Wernher von Kerman.


#ENVIRONMENT

  From the mission designer point of view, the most important aspects of the space environment are temperature
  and ionizing radiation. Both are simulated by Kerbalism. External vessel temperature is determined by the
  solar flux, as well as the albedo radiation and radiative cooling of the nearest body. Radiation is simulated
  using an overlapping hierarchy of radiation zones, modelled and rendered using signed distance fields.
  Marvel at the complex geometry of Kerbin magnetic fields, and plan your path around the powerful Jool inner belt.
  Coronal Mass Ejection events are also simulated, triggering solar storms over planetary systems.


#HABITAT

  The habitat of vessels is modelled in terms of internal volume and external surface. From these properties,
  a plethora of others are deduced such as: living space per-capita, pressure, co2 level and shielding required.
  Habitats can be enabled or disabled, in the editor and in flight: This allow to reconfigure the internal space
  and everything associated with it. Inflatable habitats are driven directly by pressure.


#KERBALS

  Kerbals simulation is data-driven: the actual needs are determined by an arbitrary set of rules. These rules can consume
  and produce resources, and are influenced by the environment and habitat simulation. The system is flexible enough to
  implement such things as: climatization, eating, drinking, breathing, co2 poisoning, stress and the effects of radiation.


#COMPONENTS

  Components don't last forever in the real world. This is modelled in Kerbalism by a simple system that can trigger failures
  on arbitrary modules. Manufacturing quality can be choosen in the VAB, per-component: higher quality mean longer MTBF but
  also extra cost and mass for the component. Components can also be automated using an intuitive scripting system, where
  scripts are triggered manually or by environmental conditions. You can create a script to turn on all the lights as soon
  as the Sun is not visible anymore, or retract all solar panels as soon as you enter an atmosphere. Simple but interesting stuff.


#DATA

  Data is collected and stored in the vessel solid state drives, then transmitted back home for that sweet science reward.
  Some of this data can't be transmitted directly, and need to be analyzed in a laboratory to produce transmissible data.
  Transmission require a connection with DSN, and has a specific data transmission rate that degrade with distance.
  The signal is obstructed by celestial bodies, and can be relayed by other vessels. Low-Gain and High-Gain antennas are
  different: the former can be used for short-range inter-vessel communciations, the latter always point at DSN.
  Your voyager-style probe will now require a voyager-style antenna, and it will end up having voyager-style transmission rates.


#RESOURCES

  Resource consumers and producers are simulated at all time, even on unloaded vessels. This not only include all Kerbalism
  mechanics that involve resources, but also all stock components as well as the components of some selected third-party mods.


#DEFAULT PROFILE

  At its core, Kerbalism is a framework that execute data-driven rules. A standard set of rules is provided, called the
  Default profile. It serve both as an example of what the framework can do, as well as providing a standard experience.

  It implement the following:
  - kerbals need resources to survive: food, water, oxygen
  - kerbals lose their mind if confined in cramped space, in unpressurized habitats or without comforts for too long
  - kerbals will freeze or burn if exposed to temperatures outside the survival range
  - kerbals will die if exposed to high levels of carbon dioxide
  - kerbals will die if exposed to extreme radiation
  - pods can be configured with ECLSS setups: Scrubbers, Pressure Control Systems, Water Recyclers and Waste Processors
  - a set of configurable supply containers
  - a greenhouse with interesting mechanics
  - a configurable ISRU system focused on life support, and that include an atmospheric harvester
  - more realistic fuel cells


#SUPPORTED MODS

  Kerbalism provide ad-hoc support for a multitude of third-party mods. Some of the interactions deserve a special mention:

    SCANsat:
      . sensors consume EC in background and their cost is evalued by the planner EC
      . sensors are shut down and restarted in background depending on EC availability

    DeepFreeze:
      . all rules are suspended for hibernated Kerbals
      . the vessel info window show frozen Kerbals with a different color

    NearFuture:
      . curved solar panels, reactors, fission and radioisotope generators
        produce EC in background and are considered by the planner

    PlanetaryBaseSystem:
      . the coverters will work in background and are considered by the planner

    OrbitalScience:
      . experiments data size has been tweaked for background data transmission

    OPM/RSS/NewHorizons:
      . custom radiation definitions for these planet packs are provided


#CONTRIBUTIONS

  Over the time, many have contributed: may it be an asset, a patch, testing or even just brainstorming. Thank you all.

  And special thanks to our Art Department:

    @mehka: contributed that awesome piece of work that is the Gravity Ring
    @Nazari1382: ramped the art to 11 with the Geiger Counter, and gave us all a valid alternative to starving
    @tygoo7: single-handedly provided all the supply containers



#FAQs

  - ***I found a bug***

    First demonstrate that the bug doesn't exist without Kerbalism installed. Then reproduce the bug consistently.
    Finally, by all means, inform me about it by raising an issue on github or post on the thread on KSP forums.
    Include all necessary information in the report.


  - ***I am a part author. How can I add support for Kerbalism?***

    You simply add the appropriate modules to your parts. Check the wiki for the module specifications.


  - ***I am a mod author. How can I interact with Kerbalism?***

    Have a look at System/API.cs source code on the github. Raise an issue to request more functions there.


#REQUIREMENTS

  - ModuleManager


#LICENSE

  This mod is released under the Unlicense.

