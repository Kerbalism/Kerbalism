.. _kerbals:

Kerbals
=======

Biological needs
----------------
Kerbals need a constant supply of basic resources, Food, Water and Oxygen otherwise they will eventually perish.

+-----------+---------+-----------------+-------------------+---------------+
| RULE      | CONSUME | PRODUCE         | MASS PER-DAY (Kg) | UNITS PER-DAY |
+===========+=========+=================+===================+===============+
| Eating    | Food    | Waste           | 0.07375           | 0.27          |
+-----------+---------+-----------------+-------------------+---------------+
| Drinking  | Water   | WasteWater      | 0.14              | 0.14          |
+-----------+---------+-----------------+-------------------+---------------+
| Breathing | Oxygen  | WasteAtmosphere | 0.05287           | 37.5          |
+-----------+---------+-----------------+-------------------+---------------+

Individual consumption may vary.

---------

Psychological needs
-------------------
Kerbals will suffer mental breakdown after some time, it can be increased by providing.

- More habitat volume per-capita.
- A pressurized habitat.
- Basic `comforts <habitat.html#comforts>`_.

---------

Environmental hazards
---------------------
Kerbals will die if environmental conditions get out of hand, such as.

- *CO2 poisoning* from being exposed to high *CO2* levels (above 2%) in the internal atmosphere for too long. *CO2* levels are maintained by using Scrubbers and/or Greenhouses.

- Exposed to extreme levels of *humidity* (above 95%) in the internal atmosphere for too long. *Humidity* is maintained at a constant 60% by using the Humidity controllers.

- Exposed to temperatures outside of the *survivable range*. The internal temperature in a vessel is maintained constantly within the *survivable range* if there is enough *ElectricCharge* present. The climatization system uses *ElectricCharge* in proportion to the volume of the habitat to climatize and the difference between the *external* temperature and the *survivable range*.

- Exposed to extreme levels of *radiation*. Radiation belts have extremely high levels, and *solar storms* will dramatically increase the radiation for all vessels in a region temporarily. *Shielding* can be specified per-part in the VAB to reduce the environmental radiation reaching the internal habitat.

---------

LSS
---
Each pod or the External LSS Unit (ECLSS) can be configured with Life Support System setups from among the following.

+---------------------+---------------------------------------------------------------------------------+-----------------------+
| ECLSS SETUP         | DESCRIPTION                                                                     | TECH REQUIRED         |
+=====================+=================================================================================+=======================+
| Scrubber            | Sequester CO2 from the internal atmosphere                                      |                       |
+---------------------+---------------------------------------------------------------------------------+-----------------------+
| Pressure control    | Consume Nitrogen to keep the internal pressure at an acceptable level           | Engineering 101       |
+---------------------+---------------------------------------------------------------------------------+-----------------------+
| Humidity controller | Extract potable water from the internal atmosphere and maintain humidity at 60% | Survivability         |
+---------------------+---------------------------------------------------------------------------------+-----------------------+
| Water recycler      | Extract potable water, ammonia and CO2 out of waste water                       | Space Exploration     |
+---------------------+---------------------------------------------------------------------------------+-----------------------+
| Waste processor     | Extract ammonia out of organic waste                                            | Advanced Exploration  |
+---------------------+---------------------------------------------------------------------------------+-----------------------+
| Monoprop fuel cell  | Burn monoprop and O2, producing EC with by-products of water and nitrogen       | Advanced Electrics    |
+---------------------+---------------------------------------------------------------------------------+-----------------------+

---------

Greenhouse
----------
The Greenhouse is based on design targets from the `Prototype Lunar Greenhouse <https://www.ag.arizona.edu/lunargreenhouse/Documents/2012-07-20_01_Giacomelli.pdf>`_ which is designed primarily to support the oxygen needs of one person rather than food needs. Thus one Greenhouse supports one kerbal with 100% of his/her O2 needs and half of its food needs per crop, a full crop can be harvested every 200 days, or you can use the emergency harvest function to harvest whatever amount of food has grown earlier.

The greenhouse growth is configured to.

- consume *Water* and *Ammonia*.
- consume *CO2* from waste atmosphere and/or pressurized tanks.
- consume *ElectricCharge* for the artificial lighting lamps, when their use is required.
- require an internal pressure of at least 10kPA.
- require radiation levels not in excess of 0.03 rad/h.
- produce *Oxygen* and *Food*.
