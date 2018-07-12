.. _support:

Supported Mods
==============

Most mods work together with Kerbalism, others don't. Such is life. For a complete list of supported mods have a
look inside the `Support folder`_. Some of the interactions deserve a special mention though:

SCANsat
-------

- sensors consume EC in the background and their EC cost is evaluated by the planner
- sensors are shut down and restarted in background depending on EC availability

RemoteTech
----------

- antenna EC cost is evaluated by the planner
- failures will disable the antenna

DeepFreeze
----------

- all rules are suspended for hibernated Kerbals
- the vessel info window shows frozen Kerbals with a different color

NearFuture
----------

- curved solar panels, reactors, fission generators and RTGs produce EC in background and are evaluated by the planner

PlanetaryBaseSystem
-------------------

- the converters will work in the background and are evaluated by the planner

OrbitalScience
--------------

- experiments data size has been tweaked for background data transmission

OPM/RSS/NewHorizons
-------------------

- custom radiation definitions for these planet packs are provided

.. _Support folder: https://github.com/steamp0rt/Kerbalism/tree/master/GameData/Kerbalism/Support
