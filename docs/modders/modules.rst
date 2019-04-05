.. _modules:

Kerbalism's Part Modules
========================

Comfort
-------
The part provides comforts for the crew.

+----------+-----------------------------------------+
| PROPERTY | DESCRIPTION                             |
+==========+=========================================+
| bonus    | the comfort bonus provided              |
+----------+-----------------------------------------+
| desc     | short description shown in part tooltip |
+----------+-----------------------------------------+

see `Comforts <../habitat.html#comforts>`_ for a list of allowed bonus's

-------

Configure
---------
The part allows for different *setups* of modules and resources that can be selected by the user in-game.

+-------------+--------------------------------------------------------------------------------------------------------+---------+
| PROPERTY    | DESCRIPTION                                                                                            | DEFAULT |
+=============+========================================================================================================+=========+
| title       | short string to show on part UI                                                                        |         |
+-------------+--------------------------------------------------------------------------------------------------------+---------+
| slots       | number of setups that can be selected concurrently                                                     | 1       |
+-------------+--------------------------------------------------------------------------------------------------------+---------+
| reconfigure | string in the format *trait@level*, specifying that the part can be reconfigured in flight by the crew |         |
+-------------+--------------------------------------------------------------------------------------------------------+---------+
| SETUP       | one or more sub-nodes that describe a setup                                                            |         |
+-------------+--------------------------------------------------------------------------------------------------------+---------+

A **SETUP** sub-node has the following properties.

+----------+------------------------------------------------------------------+
| PROPERTY | DESCRIPTION                                                      |
+==========+==================================================================+
| name     | short string describing the setup                                |
+----------+------------------------------------------------------------------+
| desc     | longer description of the setup                                  |
+----------+------------------------------------------------------------------+
| tech     | id of technology required to unlock the setup                    |
+----------+------------------------------------------------------------------+
| cost     | extra cost, in space-bucks                                       |
+----------+------------------------------------------------------------------+
| mass     | extra mass, in tons. (1 = 1000Kg)                                |
+----------+------------------------------------------------------------------+
| MODULE   | zero or more sub-nodes associating a module with the setup       |
+----------+------------------------------------------------------------------+
| RESOURCE | zero or more sub-nodes defining a resource included in the setup |
+----------+------------------------------------------------------------------+

A **MODULE** sub-node, inside a *SETUP* node, associates a specific module (that is already defined in the part) to that particular setup. The module will then be disabled (effectively acting like it wasn't there) unless the user selects the setup. A module can be associated to only a setup. Not all modules in the part need to be associated to a setup, and those that aren't will behave as usual.

+-------------------+----------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| PROPERTY          | DESCRIPTION                                                                                                                                                          |
+===================+======================================================================================================================================================================+
| type              | module name                                                                                                                                                          |
+-------------------+----------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| id_field/id_value | the name of a field in the module definition and its value respectively, used to identify a module in particular if multiple ones of the same type exist in the part |
+-------------------+----------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| id_index          | the zero-based index, selecting a specific module of *type* among all the ones present in the part                                                                   |
+-------------------+----------------------------------------------------------------------------------------------------------------------------------------------------------------------+

A **RESOURCE** sub-node, inside a *SETUP* node, adds a specific resource amount and/or capacity to the setup. The resource definition is the same as the stock one you are familiar with. The resource doesn't need to be defined in the part directly but only in the setup. When the setup is selected, the resource will be added to the part. If the part already contain the same resource, the amount and/or capacity will simply increase when the setup is selected.

-------

Emitter
-------
The part emits radiation. Use a negative radiation value for absorption.

+-----------+---------------------------------------------------+---------+
| PROPERTY  | DESCRIPTION                                       | DEFAULT |
+===========+===================================================+=========+
| radiation | radiation in rad/s, can be negative               |         |
+-----------+---------------------------------------------------+---------+
| ec_rate   | EC consumption rate per-second (optional)         |         |
+-----------+---------------------------------------------------+---------+
| toggle    | true if the effect can be toggled on/off          | false   |
+-----------+---------------------------------------------------+---------+
| active    | name of animation to play when enabling/disabling |         |
+-----------+---------------------------------------------------+---------+

-------

Experiment
-------
Hooks experiments into the Kerbalism science system.

+-------------------+-------------------------------------------------------------+---------------+
| PROPERTY          | DESCRIPTION                                                 | DEFAULT       |
+===================+=============================================================+===============+
| experiment_id     | The ID of the experiment (which must be defined elsewhere)  |               |
+-------------------+-------------------------------------------------------------+---------------+
| experiment_desc   | A nice description of the experiment.                       |               |
+-------------------+-------------------------------------------------------------+---------------+
| data_rate         | sampling rate in Mb/s                                       | 0.01          |
+-------------------+-------------------------------------------------------------+---------------+
| ec_rate           | EC consumption rate per second while recording              | 0.01          |
+-------------------+-------------------------------------------------------------+---------------+
| allow_shrouded    | Allow the experiment to run while it's part is shrouded     | true          |
+-------------------+-------------------------------------------------------------+---------------+
| sample_mass       | If not 0, this is a sample and cannot be transmitted        | 0.0           |
+-------------------+-------------------------------------------------------------+---------------+
| sample_reservoir  | Amount of sampling material stored on the part              | = sample_mass |
+-------------------+-------------------------------------------------------------+---------------+
| sample_collecting | If set to true, the experiment will produce mass.           | false         |
+-------------------+-------------------------------------------------------------+---------------+
| requires          | Additional requirements that must be met for recording.     |               |
|                   | See below.                                                  |               |
+-------------------+-------------------------------------------------------------+---------------+
| crew_operate      | Requirements for crew on vessel for recording. If this is   |               |
|                   | not set, the experiment can run on unmanned probes.         |               |
+-------------------+-------------------------------------------------------------+---------------+
| crew_reset        | Requirements for crew to reset the experiment. If this is   |               |
|                   | set, the experiment will only record data from within the   |               |
|                   | situation where recording was started, until it is reset    |               |
|                   | (either by a kerbal that has to match the requirement, or   |               |
|                   | by a lab.                                                   |               |
+-------------------+-------------------------------------------------------------+---------------+
| crew_prepare      | If set, a kerbal has to prepare the experiment before it    |               |
|                   | can record data. Once prepared, the experiment will only    |               |
|                   | record data while it remains in the situation it was        |               |
|                   | prepared for. The kerbal doing the preparation has to match |               |
|                   | the requiremens                                             |               |
+-------------------+-------------------------------------------------------------+---------------+
| anim_deploy       | Name of the part animation to trigger when recording starts.|               |
+-------------------+-------------------------------------------------------------+---------------+

**Crew** specifications (used in crew_operate, crew_reset or crew_prepare as well as in some
other Kerbalism mods) have to be given according to `true|trait|[trait]@level`

Examples:

- "true": any kerbal will do.
- "Scientist": you need a Scientist, doesn't matter how experienced. Other traits are "Pilot" and "Engineer". We're not assuming that you'll want to use "Tourist"...
- If the value is "@3" any Kerbal with 3 or more stars will do
- If the value is "Scientist@2" you need a Scientist with 2 or more stars.
- Empty values usually turn the feature off.

**Requirements** of the experiments work as additional filters, and work ON TOP OF what the underlying experiment uses. If you create a Kerbalism Experiment for `seismicScan`it won't work in orbit. The underlying experiment restrictions are checked first, then the additional requirements are checked.

The restrictions are case sensitive and comma-separated, and must ALL be met for recording. `restriction = Shadow,Space,Body:Kerbin` will only record data while in space near Kerbin AND in shadow. `restriction = AltitudeMin:250000,Surface` will never record anything for plainly obvious reasons.

Here is a list of currently supported requirements:

* OrbitMinInclination, OrbitMaxInclination: min./max. inclination of the orbit (f.i. `OrbitMinInclination:30`)
* OrbitMinEccentricity, OrbitMaxEccentricity: min./max. eccentricity of the orbit (f.i. `OrbitMaxEccentricity:0.1`)
* TemperatureMin, TemperatureMax: min./max. Temperature in Kelvin
* AltitudeMin, AltitudeMax: min./max. Altitude in Meters
* RadiationMin, RadiationMax: min./max. radiation in rad/h
* Microgravity: not on a surface, not in atmosphere. Thrust provided by Engines is OK tho.
* Body: body on which the experiment can run. Only one body is possible (f.i. `Body:Eve`)
* Shadow: vessel must not be exposed to sunlight
* Surface: vessel must be on a surface
* Atmosphere: vessel must be within an atmosphere
* Ocean: vessel must be submerged
* Space: in planetary space, i.e. not around the sun
* AbsoluteZero: temperature < 30 K
* InnerBelt: vessel must be in a inner Van Allen Belt
* OuterBelt: vessel must be in a outer Van Allen Belt
* MagneticBelt: vessel must be in any Van Allen Belt
* Magnetosphere: vessel must be inside a magnetosphere
* Thermosphere: vessel must be inside a thermosphere
* Exosphere: vessel must be inside an exosphere
* InterPlanetary: vessel must be in interplanetary space, i.e. in the SOI of the Sun
* InterStellar: vessel must be outside the sun magnetopause
* Greenhouse: there must be one greenhouse on the vessel.
* CrewMin, CrewMax: min./max. amount of crew on vessel
* CrewCapacityMin, CrewCapacityMax: min./max. crew capacity
* VolumePerCrewMin, VolumePerCrewMax: min./max. habitat volume per crew member
* Facility building levels: MissionControlLevelMin, MissionControlLevelMax, AdministrationLevelMin, AdministrationLevelMax, TrackingStationLevelMin, TrackingStationLevelMax, AstronautComplexLevelMin, AstronautComplexLevelMax
* MaxAsteroidDistance: max. distance to the nearest asteroid. For unloaded vessels this only works if the asteroid is set as the target.

-------

GravityRing
-----------
Used by the *Gravity Ring* part.

+----------+------------------------------------------+
| PROPERTY | DESCRIPTION                              |
+==========+==========================================+
| ec_rate  | EC consumed per-second when deployed     |
+----------+------------------------------------------+
| deploy   | a deploy animation can be specified      |
+----------+------------------------------------------+
| rotate   | a rotate loop animation can be specified |
+----------+------------------------------------------+

-------

Greenhouse
----------
The part simulates a greenhouse. The crop grows over time, then it is harvested as a resource. Growth has lighting requirements that can be satisfied from the environment and/or the integrated lamps. Additional requirements can be specified, such as input resources, minimal pressure and maximal radiation. By-product resources can be produced.

+---------------------+-------------------------------------------------------------------------------------------------+
| PROPERTY            | DESCRIPTION                                                                                     |
+=====================+=================================================================================================+
| crop_resource       | name of resource produced by harvests                                                           |
+---------------------+-------------------------------------------------------------------------------------------------+
| crop_size           | amount of resource produced by harvests                                                         |
+---------------------+-------------------------------------------------------------------------------------------------+
| crop_rate           | growth per-second when all conditions apply                                                     |
+---------------------+-------------------------------------------------------------------------------------------------+
| ec_rate             | EC/s consumed by the lamp at max capacity, set to 0 to disable the lamp                         |
+---------------------+-------------------------------------------------------------------------------------------------+
| light_tolerance     | minimum lighting flux required for growth, in W/m^2                                             |
+---------------------+-------------------------------------------------------------------------------------------------+
| pressure_tolerance  | minimum pressure required for growth, in sea level atmospheres (optional)                       |
+---------------------+-------------------------------------------------------------------------------------------------+
| radiation_tolerance | maximum radiation allowed for growth in rad/s, considered after shielding is applied (optional) |
+---------------------+-------------------------------------------------------------------------------------------------+
| lamps               | object with emissive texture used to represent intensity graphically                            |
+---------------------+-------------------------------------------------------------------------------------------------+
| shutters            | animation to manipulate shutters                                                                |
+---------------------+-------------------------------------------------------------------------------------------------+
| plants              | animation to represent plant growth graphically                                                 |
+---------------------+-------------------------------------------------------------------------------------------------+

Resource requirements and by-products (other than EC for the lamps) are specified using the stock *resHandler* specification

.. code-block:: C#

	INPUT_RESOURCE
	{
	  name = Water
	  rate = 0.00023148
	}

	OUTPUT_RESOURCE
	{
	  name = Oxygen
	  rate = 0.00463
	}

-------

Habitat
-------
The part has an internal habitat.

+----------+--------------------------------------------------------------------+---------+
| PROPERTY | DESCRIPTION                                                        | DEFAULT |
+==========+====================================================================+=========+
| volume   | habitable volume in m³, deduced from bounding box if not specified |         |
+----------+--------------------------------------------------------------------+---------+
| surface  | external surface in m², deduced from bounding box if not specified |         |
+----------+--------------------------------------------------------------------+---------+
| inflate  | inflate animation, if any                                          |         |
+----------+--------------------------------------------------------------------+---------+
| toggle   | show the enable/disable toggle                                     | true    |
+----------+--------------------------------------------------------------------+---------+

-------

HardDrive
---------
The part has an interface to access the vessel hard drive, where the science data files are stored.

+----------------+------------------------------------------------------------+---------+
| PROPERTY       | DESCRIPTION                                                | DEFAULT |
+================+============================================================+=========+
| dataCapacity   | Storage capacity for transmissible data, in Mb (=Mib)      | 102400  |
+----------------+------------------------------------------------------------+---------+
| sampleCapacity | Capacity for experiment samples, in slots (=Mib).          | 100     |
|                | Note that Kerbalism will not display sample sizes in Mb,   |         |
|                | but uses a virtual size unit instead (slots, bags) (TBD)   |         |
+----------------+------------------------------------------------------------+---------+
| title          | Name displayed in file manager                             |         |
+----------------+------------------------------------------------------------+---------+
| experiment_id  | If set, restricts write access to the experiment with that |         |
|                | id ON THE SAME PART with the given experiment_id.          |         |
+----------------+------------------------------------------------------------+---------+


-------

Harvester
---------
The part harvests resources, similar to the stock resource harvester. The differences are that the output doesn't scale with concentration, instead it has the specified rate when above a threshold and zero below it.

+---------------+-----------------------------------------------------------------------------+---------+
| PROPERTY      | DESCRIPTION                                                                 | DEFAULT |
+===============+=============================================================================+=========+
| title         | name to show on UI                                                          |         |
+---------------+-----------------------------------------------------------------------------+---------+
| type          | type of resource, same values accepted by stock harvester                   | 0       |
+---------------+-----------------------------------------------------------------------------+---------+
| resource      | resource to extract                                                         |         |
+---------------+-----------------------------------------------------------------------------+---------+
| min_abundance | minimal abundance required, in the range [0.0, 1.0]                         |         |
+---------------+-----------------------------------------------------------------------------+---------+
| min_pressure  | minimal pressure required, in kPA                                           |         |
+---------------+-----------------------------------------------------------------------------+---------+
| rate          | amount of resource to extract per-second, when abundance is above threshold |         |
+---------------+-----------------------------------------------------------------------------+---------+
| ec_rate       | amount of EC consumed per-second, irregardless of abundance                 |         |
+---------------+-----------------------------------------------------------------------------+---------+
| drill         | the drill transform                                                         |         |
+---------------+-----------------------------------------------------------------------------+---------+

-------

Laboratory
----------
The part transforms non-transmissible science samples into transmissible science data over time.

+---------------+---------------------------------------------------------+
| PROPERTY      | DESCRIPTION                                             |
+===============+=========================================================+
| ec_rate       | EC consumed per-second                                  |
+---------------+---------------------------------------------------------+
| analysis_rate | analysis speed in Mb/s                                  |
+---------------+---------------------------------------------------------+
| researcher    | required crew for analysis, in the format *trait@level* |
+---------------+---------------------------------------------------------+

-------

PlannerController
-----------------
The Part has a toggle to enable/disable simulation in the *Planner*. The *Planner* simulates resource consumption and production for many types of modules, and most of the time it is useful to be able to toggle these on and off in the VAB/SPH to simulate different scenarios for the vessel.

Some modules do not offer any way to toggle them on and off in the VAB/SPH and that's where the *PlannerController* comes in, once added to a part it will add an editor-only toggle button. The *Planner* will then consider or ignore all modules in that part depending on the toggle button state.

+------------+--------------------------------------+---------+
| PROPERTY   | DESCRIPTION                          | DEFAULT |
+============+======================================+=========+
| toggle     | show the toggle button in the editor | true    |
+------------+--------------------------------------+---------+
| considered | default button state                 | false   |
+------------+--------------------------------------+---------+

-------

ProcessController
-----------------
The part has resource processing capabilities. This module allows the implementation of a scheme to provide converter-like modules on a vessel, while keeping the computation independent of the number of individual converters.

The trick is by using a `Process <profile.html#process>`_ which uses a hidden pseudo-resource created ad-hoc e.g. \_WaterRecycler\_.

This module then adds that resource to its part automatically, and provides a way to *start/stop* the process by a part UI button. Under the hood, starting and stopping the process is implemented by merely setting the resource flow to true and false respectively.

+----------+----------------------------------+---------+
| PROPERTY | DESCRIPTION                      | DEFAULT |
+==========+==================================+=========+
| resource | pseudo-resource to control       |         |
+----------+----------------------------------+---------+
| title    | name to show on UI               |         |
+----------+----------------------------------+---------+
| desc     | description to show on tooltip   |         |
+----------+----------------------------------+---------+
| capacity | amount of pseudo-resource to add | 1.0     |
+----------+----------------------------------+---------+
| toggle   | show the enable/disable toggle   | true    |
+----------+----------------------------------+---------+
| running  | start the process by default     | false   |
+----------+----------------------------------+---------+

-------

Reliability
-----------
The part has the capability of module failure. This module disables other modules when a *failure* happens.

+------------+--------------------------------------------------------------------------+------------+
| PROPERTY   | DESCRIPTION                                                              | DEFAULT    |
+============+==========================================================================+============+
| string     | component module name                                                    |            |
+------------+--------------------------------------------------------------------------+------------+
| mtbf       | mean time between failures, in seconds                                   | 21600000.0 |
+------------+--------------------------------------------------------------------------+------------+
| repair     | trait and experience required for repair, in the form *trait@experience* |            |
+------------+--------------------------------------------------------------------------+------------+
| title      | short description of component                                           |            |
+------------+--------------------------------------------------------------------------+------------+
| redundancy | redundancy group                                                         |            |
+------------+--------------------------------------------------------------------------+------------+
| extra_cost | extra cost for high-quality, in proportion of part cost                  | 0.0        |
+------------+--------------------------------------------------------------------------+------------+
| extra_mass | extra mass for high-quality, in proportion of part mass                  | 0.0        |
+------------+--------------------------------------------------------------------------+------------+

-------

Sensor
------
The part has sensor capabilities that adds environmental readings to a parts UI and to the *telemetry* panel on the *Monitor* UI.

+----------+-----------------------------------------+
| PROPERTY | DESCRIPTION                             |
+==========+=========================================+
| type     | type of sensor                          |
+----------+-----------------------------------------+
| pin      | pin animation driven by telemetry value |
+----------+-----------------------------------------+

The types of sensors available are.

+-------------+----------------------------------------------------------------------------------+
| TYPE        | READINGS                                                                         |
+=============+==================================================================================+
| temperature | external vessel temperature in K                                                 |
+-------------+----------------------------------------------------------------------------------+
| radiation   | environment radiation at vessel position, in rad/s (before shielding is applied) |
+-------------+----------------------------------------------------------------------------------+
| pressure    | environment pressure in kPA                                                      |
+-------------+----------------------------------------------------------------------------------+
| gravioli    | number of negative gravioli particles detected                                   |
+-------------+----------------------------------------------------------------------------------+

-------

Patch injection
---------------
Enabled features are specified by the user in the `Settings <../settings.html>`_ file and are detected automatically from the modifiers used in the current profile. They are then used to inject MM patches on-the-fly at loading time, so that it is possible to do conditional MM patching depending on the features enabled by using **:NEEDS[FeatureXXX]**. Likewise it is possible to use **:NEEDS[ProfileXXX]** to do conditional MM patching depending on the current profile.

+--------------+---------------------------------+-----------------------------------------------+
| FEATURE      | HOW IT IS DEFINED               | WHAT DOES IT ENABLE                           |
+==============+=================================+=================================+=============+
| Reliability  | user-specified in Settings file | component malfunctions and critical failures  |
+--------------+---------------------------------+-----------------------------------------------+
| Deploy       | user-specified in Settings file | the deployment system                         |
+--------------+---------------------------------+-----------------------------------------------+
| Science      | user-specified in Settings file | the science system                            |
+--------------+---------------------------------+-----------------------------------------------+
| SpaceWeather | user-specified in Settings file | coronal mass ejections                        |
+--------------+---------------------------------+-----------------------------------------------+
| Automation   | user-specified in Settings file | script UI and automatic execution             |
+--------------+---------------------------------+-----------------------------------------------+
| Radiation    | detected from modifiers used    | simulation and rendering of radiation         |
+--------------+---------------------------------+-----------------------------------------------+
| Shielding    | detected from modifiers used    | shielding resource added to habitats          |
+--------------+---------------------------------+-----------------------------------------------+
| LivingSpace  | detected from modifiers used    | volume is calculated for habitats             |
+--------------+---------------------------------+-----------------------------------------------+
| Comfort      | detected from modifiers used    | comfort parts are added                       |
+--------------+---------------------------------+-----------------------------------------------+
| Poisoning    | detected from modifiers used    | atmospheric CO2 is simulated in habitats      |
+--------------+---------------------------------+-----------------------------------------------+
| Pressure     | detected from modifiers used    | atmospheric pressure is simulated in habitats |
+--------------+---------------------------------+-----------------------------------------------+
| Humidity     | detected from modifiers used    | atmospheric humidity is simulated in habitats |
+--------------+---------------------------------+-----------------------------------------------+
| Habitat      | one or more features require it | the habitat module is added to parts          |
+--------------+---------------------------------+-----------------------------------------------+
