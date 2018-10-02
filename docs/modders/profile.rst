.. _profile:

Modding Kerbalism's Profiles
============================

Profiles
--------
A profile is a named set of rules, supplies and processes. Multiple profiles can coexist in the same install and can be defined anywhere inside the GameData folder. At any time only the one specified in the Profile parameter in `Settings <../settings.html>`_ is used.

------

Supply
------
Supply details about a resource are described by a Supply definition. These resources are shown in the Planner and Monitor UI. They are used to determine.

- amount added to all manned pods, in proportion of crew capacity
- capacity added to EVA Kerbals, that will then take the resources when going out on EVA
- amount gifted to rescue contract victims
- warning and danger levels for the resource, and messages to show

+----------------+--------------------------------------------------------------------------------------------+---------+
| PROPERTY       | DESCRIPTION                                                                                | DEFAULT |
+================+============================================================================================+=========+
| resource       | name of resource                                                                           |         |
+----------------+--------------------------------------------------------------------------------------------+---------+
| on_pod         | how much resource to add to manned parts, per-kerbal                                       | 0.0     |
+----------------+--------------------------------------------------------------------------------------------+---------+
| on_eva         | how much resource to take on Eva, if any                                                   | 0.0     |
+----------------+--------------------------------------------------------------------------------------------+---------+
| on_rescue      | how much resource to gift to rescue missions                                               | 0.0     |
+----------------+--------------------------------------------------------------------------------------------+---------+
| empty          | set initial amount to zero                                                                 | false   |
+----------------+--------------------------------------------------------------------------------------------+---------+
| low_threshold  | at what level the resource is considered low                                               | 0.15    |
+----------------+--------------------------------------------------------------------------------------------+---------+
| low_message    | messages shown when level goes below low threshold, you can use *message macros* here      |         |
+----------------+--------------------------------------------------------------------------------------------+---------+
| empty_message  | messages shown when level reach zero, you can use *message macros* here                    |         |
+----------------+--------------------------------------------------------------------------------------------+---------+
| refill_message | messages shown when level goes back above low threshold, you can use *message macros* here |         |
+----------------+--------------------------------------------------------------------------------------------+---------+

------

Rule
----
A rule describes a mechanic that increments an accumulator per-kerbal based on the environment and the availability of resources. When the accumulator reaches the fatal threshold the rule can be configured to *kill* the kerbal, or to trigger an *unplanned event* instead.

+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| PROPERTY          | DESCRIPTION                                                                                                                        | DEFAULT |
+===================+====================================================================================================================================+=========+
| name              | unique name for the rule                                                                                                           |         | 
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| input             | resource consumed, if any                                                                                                          |         |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| output            | resource produced, if any                                                                                                          |         |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| interval          | if 0 the rule is executed continuously, else it is executed every 'interval' seconds                                               | 0.0     | 
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| rate              | amount of input resource to consume at each execution                                                                              | 0.0     |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| ratio             | ratio of output resource in relation to input consumed, deduced automatically from input and output density ratio if not specified | 0.0     |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| degeneration      | amount to add to the property at each execution, when we must degenerate                                                           | 0.0     |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| variance          | variance for degeneration, unique per-kerbal and in range [1.0 +/- variance]                                                       | 0.0     |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| individuality     | variance for rate, unique per-kerbal and in range [1.0 +/- variance]                                                               | 0.0     |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| modifiers         | comma-separated list of modifiers influencing the rule                                                                             |         | 
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| breakdown         | trigger a unplanned event in the vessel, instead of killing the kerbal                                                             | false   |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| lifetime          | value will not be reset when recovering the kerbal on kerbin. used for things that cannot be cured, like radiation.                | false   |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| warning_threshold | threshold of degeneration used to show warning messages and yellow status color                                                    | 0.33    |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| danger_threshold  | threshold of degeneration used to show danger messages and red status color                                                        | 0.66    |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| fatal_threshold   | threshold of degeneration used to show fatal messages and kill/breakdown the kerbal                                                | 1.0     |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| warning_message   | messages shown when degeneration goes above warning threshold, you can use *message macros* here                                   |         |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| danger_message    | messages shown when degeneration goes above danger threshold, you can use *message macros* here                                    |         |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| fatal_message     | messages shown when degeneration goes above fatal threshold, you can use *message macros* here                                     |         |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+
| relax_message     | messages shown when degeneration goes back below warning threshold, you can use *message macros* here                              |         |
+-------------------+------------------------------------------------------------------------------------------------------------------------------------+---------+

------

Process
-------
Processes are 'vessel-wide' resource producers/consumers, with rates that can be influenced by the environment, habitat conditions, and level of resources.

+----------------+----------------------------------------------------------------------------------------+---------+
| PROPERTY       | DESCRIPTION                                                                            | DEFAULT |
+================+========================================================================================+=========+
| name           | unique name for the process                                                            |         |
+----------------+----------------------------------------------------------------------------------------+---------+
| modifiers      | comma-separated list of modifiers                                                      |         |
+----------------+----------------------------------------------------------------------------------------+---------+
| input          | zero or more input resource names and rates, in the format **input = resource@rate**   |         |
+----------------+----------------------------------------------------------------------------------------+---------+
| output         | zero or more output resource names and rates, in the format **output = resource@rate** |         |
+----------------+----------------------------------------------------------------------------------------+---------+
| dump           | comma-separated list of resources to dump excess output of overboard                   | false   |
+----------------+----------------------------------------------------------------------------------------+---------+

------

Modifiers
---------
Rule and Process rates can be influenced by the environment, habitat conditions and resource levels. This is accomplished by multiplying the rates with a set of modifiers, that can be specified as a comma-separated list.

+-----------------+--------------------------------------------------------------------------------------------------------------+
| MODIFIER        | RATES ARE MULTIPLIED BY                                                                                      |
+=================+==============================================================================================================+
| breathable      | zero inside an atmosphere containing oxygen and above 25kPA of atmospheric pressure, 1.0 otherwise           |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| temperature     | absolute difference between the external temperature, and the survival range that is specifiable in settings |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| radiation       | incoming radiation at the vessel position, in rad/s                                                          |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| shielding       | *shielding factor*, computed from the level of Shielding resource                                            |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| volume          | volume in m³ of all enabled habitats in the vessel                                                           |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| surface         | surface in m² of all enabled habitats in the vessel                                                          |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| living_space    | the *volume* per-capita, normalized against an ideal living space                                            |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| comfort         | the *comfort factor*, computed from the Comfort providers in the vessel                                      |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| pressure        | the *pressure factor*, or 1.0 if *Atmosphere* level is above threshold (both specified in settings)          |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| poisoning       | the *poisoning_factor*, or 1.0 if *WasteAtmosphere* level is above threshold (both specified in settings)    |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| humidity        | the *humidity_factor*, or 1.0 if *MoistAtmosphere* level is above threshold (both specified in settings)     |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| per_capita      | the inverse of number of crew members, the effect on rates is a division by number of crew members           |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| zerog           | 1 if the ship is above the atmosphere of a body, 0 if in atmospheric flight or landed                        |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| landed          | 1 if the ship is on the ground or in water, 0 otherwise                                                      |
+-----------------+--------------------------------------------------------------------------------------------------------------+
| *resource name* | the level of resource specified                                                                              |
+-----------------+--------------------------------------------------------------------------------------------------------------+

------

Message macros
--------------
The messages specified in a *Rule* or a *Supply* can contain macros.

+---------------+----------------------------------------------------------------------------------------------+
| MACRO         | REPLACED BY                                                                                  |
+===============+==============================================================================================+
| $NEWLINE      | The new line character                                                                       |
+---------------+----------------------------------------------------------------------------------------------+
| $VESSEL       | The vessel name                                                                              |
+---------------+----------------------------------------------------------------------------------------------+
| $KERBAL       | The Kerbal name. Empty for the resource level messages                                       |
+---------------+----------------------------------------------------------------------------------------------+
| $ON_VESSEL    | Replaced by 'On $VESSEL, ' if the vessel is not the active one, or an empty string otherwise |
+---------------+----------------------------------------------------------------------------------------------+
| $HIS_HER      | Replaced by 'his' or 'her' based on Kerbal gender                                            |
+---------------+----------------------------------------------------------------------------------------------+

------

Unplanned events
----------------
If breakdown is set to true in a Rule then one of these events will trigger at random when it reaches its fatal threshold.

+------------------+--------------------------------------------------------+--------------------------+
|TYPE              | DESCRIPTION                                            | EFFECT                   |
+==================+========================================================+==========================+
|Mumbling          | A Kerbal has been in space for too long                | *none*                   |
+------------------+--------------------------------------------------------+--------------------------+
|Fat Finger        | The wrong button was pressed on the control panel      | Loss of science data     |
+------------------+--------------------------------------------------------+--------------------------+
|Wrong Valve       | The wrong valve was opened for lack of concentration   | Loss of supply resources |
+------------------+--------------------------------------------------------+--------------------------+
|Rage              | A component was the victim of your Kerbal's rage       | A component fail         |
+------------------+--------------------------------------------------------+--------------------------+
