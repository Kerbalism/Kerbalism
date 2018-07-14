.. _resources:

Resources
=========

Containers
----------
The containers are configurable in the VAB. The inline supply containers can store.

- Food and Water *(Supplies)*
- Food
- Water
- Waste and WasteWater *(Sewage)*
- Waste
- WasteWater

The radial pressurized containers can store.

- Oxygen gas
- Nitrogen gas
- Hydrogen gas
- Ammonia gas
- CarbonDioxide gas
- Xenon gas

-----------

ISRU
----
Configurable ISRU's can execute a set of available chemical processes that can be configured in the VAB. Processes will stop running if they don't have the available resources or if there is no capacity for the output resources to go into. Excess resources can be dumped overboard by using the Dump function in order to enable a process to keep running if output capacity is not available.

LiquidFuel, Oxidizer and MonoPropellant chemical components regarding the processes are.

- LiquidFuel = Methane *CH4*
- Oxidizer = HydrogenPeroxide *H2O2*
- MonoPropellant = Hydrazine *N2H4*

+-------------------------------+---------------------------------+------------------------+-----------------------+
| CHEMICAL PROCESS              | INPUT RESOURCES                 | OUTPUT RESOURCES       | TECH REQUIRED         |
+===============================+=================================+========================+=======================+
| Water electrolysis            | EC, Water                       | Hydrogen, Oxygen       |                       |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Sabatier process              | EC, CO2, Hydrogen               | Water, LiquidFuel      |                       |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Haber process                 | EC, Nitrogen, Hydrogen          | Ammonia                |                       |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Waste incinerator             | Waste, Oxygen                   | CO2, Water, EC         | Precision Engineering |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Waste compressor              | EC, Waste                       | Shielding              | Precision Engineering |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Anthraquinone process         | Hydrogen, Oxygen                | Oxidizer               | Advanced Science      |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Hydrazine production          | EC, Ammonia, Oxidizer           | Water, O2, Monoprop    | Advanced Science      |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Hydrazine production N2 inj   | EC, Ammonia, Oxidizer, Nitrogen | O2, Monoprop           | Experimental Science  |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Solid oxide electrolysis      | EC, CO2                         | Oxygen, Shielding      | Experimental Science  |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Molten regolith electrolysis  | EC, Ore [Regolith]              | Oxygen, CO2, Shielding | Experimental Science  |
+-------------------------------+---------------------------------+------------------------+-----------------------+
| Selective catalytic oxidation | EC, Ammonia, Oxygen             | Nitrogen, Water        | Experimental Science  |
+-------------------------------+---------------------------------+------------------------+-----------------------+

-----------

Harvesters
----------
Crustal, Oceanic and Atmospheric harvesters can be configured to extract one among a set of resources.

+-------------+---------------+
| HARVESTER   | RESOURCE      |
+=============+===============+
| Crustal     | Water         |
+-------------+---------------+
| Crustal     | Ore           |
+-------------+---------------+
| Crustal     | Nitrogen      |
+-------------+---------------+
| Oceanic     | Water         |
+-------------+---------------+
| Oceanic     | Nitrogen      |
+-------------+---------------+
| Oceanic     | Ammonia       |
+-------------+---------------+
| Atmospheric | CarbonDioxide |
+-------------+---------------+
| Atmospheric | Oxygen        |
+-------------+---------------+
| Atmospheric | Nitrogen      |
+-------------+---------------+
| Atmospheric | Ammonia       |
+-------------+---------------+

-----------

Fuel cells
----------
Fuel cells can be configured to use a number of resources to produce *ElectricCharge*.

+-------------+---------------------------+--------------------+
| CELL TYPE   | INPUT RESOURCE            | EXTRA OUTPUT       |
+=============+===========================+====================+
| H2+O2       | Hydrogen and Oxygen       | Water              |
+-------------+---------------------------+--------------------+
| Monoprop+O2 | MonoPropellant and Oxygen | Water and Nitrogen |
+-------------+---------------------------+--------------------+
