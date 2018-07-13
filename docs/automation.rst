.. _automation:

Automation
==========

Components in a vessel can be turned on and off automatically by environmental conditions. The set of component changes is stored in scripts, and a simple editor UI is provided. When a specified change in conditions is detected, the relative script is executed on a vessel. This works transparently for loaded and unloaded vessels. 

----------

Scripts
-------
A script represents a list of state changes for all vessel components. Each component can be set in one of three states: *don't care*, *on* or *off*. 

----------

Editor
------
There is a simple graphical editor for the scripts conditions. It can be opened by clicking on the **auto** icon in the Monitor UI. Click on the arrows in the panel title to select one of the scripts. Then click on the components to change their state. Components states can be manually controlled by using the *direct control* page.

----------

Direct control
--------------
The Script editor UI can also serve as a simple way to change the state of single components without clicking on the part first. This works even for unloaded vessels. The state of each component is also reported. This is not that informative usually, but can act as a sort of summary of the overall vessel status.

----------

Conditions
----------
Scripts are triggered by the following conditions.

+------------+--------------------------------------+
| CONDITION  | TRIGGER                              |
+============+======================================+
| landed     | vessel state switched to landed      |
+------------+--------------------------------------+
| atmo       | entering the atmosphere              |
+------------+--------------------------------------+
| space      | reaching space                       |
+------------+--------------------------------------+
| sunlight   | star returns to visible              |
+------------+--------------------------------------+
| shadow     | star gets occluded                   |
+------------+--------------------------------------+
| power_high | EC level goes above 80%              |
+------------+--------------------------------------+
| power_low  | EC level goes below 20%              |
+------------+--------------------------------------+
| rad_low    | radiation goes below 0.02 rad/h      |
+------------+--------------------------------------+
| rad_high   | radiation goes above 0.05 rad/h      |
+------------+--------------------------------------+
| linked     | signal is regained                   |
+------------+--------------------------------------+
| unlinked   | signal is lost                       |
+------------+--------------------------------------+
| eva_out    | going out on Eva                     |
+------------+--------------------------------------+
| eva_in     | coming back from Eva                 |
+------------+--------------------------------------+
| action[0-5]| press [0-5], on the active vessel    |
+------------+--------------------------------------+

----------

Supported modules
-----------------
Only these modules are supported by the automation system.

+------------------------------------------------+---------------------+
| MODULE                                         | ACTION              |
+================================================+=====================+
| Antenna                                        | Extend/Retract      |
+------------------------------------------------+---------------------+
| Emitter                                        | Enable/Disable      |
+------------------------------------------------+---------------------+
| Gravity Ring                                   | Enable/Disable      |
+------------------------------------------------+---------------------+
| Greenhouse                                     | Enable/Disable      |
+------------------------------------------------+---------------------+
| Harvester                                      | Start/Stop          |
+------------------------------------------------+---------------------+
| Laboratory                                     | Start/Stop          |
+------------------------------------------------+---------------------+
| Process Controller                             | Start/Stop          |
+------------------------------------------------+---------------------+
| ModuleDeployableSolarPanel                     | Extend/Retract      |
+------------------------------------------------+---------------------+
| ModuleGenerator                                | Start/Stop          |
+------------------------------------------------+---------------------+
| ModuleLight (and some derivatives)             | Turn on/off         |
+------------------------------------------------+---------------------+
| ModuleResourceConverter (and some derivatives) | Start/Stop          |
+------------------------------------------------+---------------------+
| ModuleResourceHarvester                        | Start/Stop          |
+------------------------------------------------+---------------------+
| SCANsat                                        | Start/Stop scanning |
+------------------------------------------------+---------------------+
