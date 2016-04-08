                             _  ________ _____  ____          _      _____  _____ __  __ 
                            | |/ /  ____|  __ \|  _ \   /\   | |    |_   _|/ ____|  \/  |
                            | ' /| |__  | |__) | |_) | /  \  | |      | | | (___ | \  / |
                            |  < |  __| |  _  /|  _ < / /\ \ | |      | |  \___ \| |\/| |
                            | . \| |____| | \ \| |_) / ____ \| |____ _| |_ ____) | |  | |
                            |_|\_\______|_|  \_\____/_/    \_\______|_____|_____/|_|  |_|
                               
                              Hundreds of Kerbals were killed in the making of this mod.
=======================================================================================================================

=======================================================================================================================
                                                ~~~ Coming soon ~~~
=======================================================================================================================

#INTRODUCTION

  Kerbalism is a mod for Kerbal Space Program that features life support, quality-of-life,
  malfunctions, signal, radiation, space weather, a decent GUI to monitor existing vessels
  and to plan new ones, and coherent background simulation.

  
#CLIMATE CONTROL

  Kerbals can't survive the extreme temperatures of space by themselves. All manned command
  pods climatize their internal environment at the expense of ElectricCharge. The EC consumption
  is proportional to external temperature, that is determined by an approximate temperature
  model that include sun visibility, solar flux, albedo radiation and atmospheric absorption.
  If there isn't enough EC to maintain the climate inside the survival range, Kerbals die.
  
  
#FOOD & OXYGEN

  All Kerbals require a constant supply of Food and Oxygen to survive. All manned command
  pods can store a small amount of both, and a few container parts are included. Kerbals
  can go without Food for a few days, but will last only a few minutes if deprived of Oxygen.
  
  
#SCRUBBERS

  All manned command pods include an embedded CO2 Scrubber that reclaim some of the Oxygen
  consumed, at the expense of Electric Charge. The scrubber efficiency depend on the
  technological level of your space agency at time of launch.
  
  
#GREENHOUSE

  Greenhouses produce Food in space and on the surface of celestial bodies, even far from the
  Sun. The growth can be speed up by using Artificial Lighting, at the expense of Electric Charge.
  
  
#EVA

  Kerbals on EVA take some Electric Charge with them and use it to maintain climate control of the
  suit, if necessary, and to power the headlight. Also they take some Oxygen, but only when outside
  a breathable atmosphere. Kerbals can die while on EVA. In this unfortunate case, the poor Kerbal's
  body will be left floating in space.
  
  
#QUALITY OF LIFE

  Do not underestimate the consequences of living in extremely close quarters for extremely
  long time. Kerbals will lose their mind if placed in cramped vessels for a long time, with
  unexpected consequences. Mitigate the problem by providing ample living space and entertainment,
  and by rotating the crew regularly. Some stock parts provide entertainment, like the cupola.
  
  
#MALFUNCTIONS

  Components can malfunction and their specs are reduced. A component never fail completely,
  but multiple malfunctions can effectively reduce its specs to the point of it being useless.
  Luckily, Engineers can fix malfunctioned components while on EVA. The manufacturing quality of
  components can be increased by researching material science technologies.
  
  
#SIGNALS

  Science data transmission and remote probe control require a link with the home planet.
  Celestial bodies block the signal, while other vessels can act as relay. Relay must be enabled
  per-antenna, as relaying has extra Electric Charge requirements, even in background.
  Antennas have range and wildly different data transmission costs, that scale with distance.
  Signal processing can be improved by researching signal processing technologies, leading to increased range.
  
  
#RADIATION

  Celestial bodies can have a magnetosphere that protect Kerbals from radiation, as well as a radiation
  belt: an extreme radiation zone. Cosmic radiation is ever present in the vast distances of space.
  Vessels can be shielded to offer some protection, at the expense of greatly increased mass.
  

#SPACE WEATHER

  Coronal mass ejections from the Sun can hit planetary systems from time to time, causing magnetic storm
  inside magnetospheres that disrupt communications, as well as intense radiation over the whole system.
  Frequency of the storms as well as time to impact depend on distance of the body from the Sun.
  
  
#COHERENT BACKGROUND SIMULATION

  All mechanics described are simulated coherently in background. Also command pods, solar panels,
  generators, fuel cells, drills, ISRU and SCANsat modules produce and consume resources in background.
  The stock behaviour for fuel cells, drills and ISRU for unloaded vessels has been completely replaced.
  
  
#VESSEL MONITOR

  A simple gui is available in the space center or in flight. It show the status for vessels and
  Kerbals, including: battery, food & oxygen levels, scrubber & greenhouse status, signal status,
  malfunctions, if the vessel is in direct sunlight, kerbal physical and mental health problems, and more.
  It also allow to enable and disable warning messages and to take notes per-vessel, just click on it.
  
  
#VESSEL PLANNER

  A simple gui is available in the editor, to help in designing missions. It show the life expectancy
  of the crew as well as how long will the vessel electric charge last in shadow or sunlight, and much more.
  The estimates are relative to a target body and situation, that can be changed. It support pods, solar
  panels, generators, fuel cells, radiators, wheels, drills, ISRU and SCANsat in addition to all mod mechanics.
  
  
#SCIENCE TWEAKS

  Early experiments have been rearranged in the tech tree. Data is either transmissibile in full or not
  at all. Experiments provide full science values. Situations have been tweaked, no more ladder dance.
  Probes can store data and perform a simple telemetry experiment.
  
  
#FAQs

  - ***I think I've founded a bug or any other undesired behaviour***
  
    Try running the game in the same conditions, but without this mod. If the problem is still present then
    there is high probability this mod wasn't responsible. If that isn't the case, however, please try to
    reproduce the bug consistently. Then, by all means, post a description of the problem in the thread or
    raise an issue on github. Copy of the savegame, log file and list of mods installed are appreciated.
    

  - ***I think this mod is too hard/easy, I wish this mod would do something different***
  
    You can suggest rebalance tweaks and any other modification by posting in the thread.
    
    
  - ***Is this mod compatible with planet packs and scale changing mods?***
  
    Yes! This mod make no assumptions about the solar system.
    
    
  - ***How can I support Kerbalism in my parts?***
  
    You can create Scrubbers, Greenhouses, Entertainment and Antenna parts by simply using the respective modules.
    An example of the module values can be found in the ModuleManager patches included in the download.
    
    
  - ***How can I support Kerbalism in my mod?***
  
    Have a look at Hooks.cs source code. Right now there is only support to disable resource consumption for specific
    Kerbals. Other hooks can be added on request, just leave a comment on the thread or raise an issue on github.
    
    
    
#REQUIREMENTS

  - ModuleManager
  
  
#LICENSE

  This mod is released under the Unlicense.

