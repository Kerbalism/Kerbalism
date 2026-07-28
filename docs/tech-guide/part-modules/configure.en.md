## Configure
The part allows for different *setups* of modules and resources that can be selected by the user in-game.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| title | short string to show on part UI (may be a `#loc` key) |  |
| configureId | stable MM-safe id (prefer `HAS[#configureId[...]]` over matching localized titles) |  |
| slots | number of setups that can be selected concurrently | 1 |
| reconfigure | string in the format *trait@level*, specifying that the part can be reconfigured in flight by the crew |  |
| symmetric | if true enforces same configuration on all parts in the symmetry group (useful for tanks) | false |
| SETUP | one or more sub-nodes that describe a setup |  |

A **SETUP** sub-node has the following properties.

| PROPERTY | DESCRIPTION |
| --- | --- |
| name | short string describing the setup |
| desc | longer description of the setup |
| tech | id of technology required to unlock the setup |
| cost | extra cost, in space-bucks |
| mass | extra mass, in tons. (1 = 1000Kg) |
| MODULE | zero or more sub-nodes associating a module with the setup |
| RESOURCE | zero or more sub-nodes defining a resource included in the setup |

A **MODULE** sub-node, inside a *SETUP* node, associates a specific module (that is already defined in the part) to that particular setup. The module will then be disabled (effectively acting like it wasn't there) unless the user selects the setup. A module can be associated to only a setup. Not all modules in the part need to be associated to a setup, and those that aren't will behave as usual.

| PROPERTY | DESCRIPTION |
| --- | --- |
| type | module name |
| id_field/id_value | the name of a field in the module definition and its value respectively, used to identify a module in particular if multiple ones of the same type exist in the part |
| id_index | the zero-based index, selecting a specific module of *type* among all the ones present in the part |

A **RESOURCE** sub-node, inside a *SETUP* node, adds a specific resource amount and/or capacity to the setup. The resource definition is the same as the stock one you are familiar with. The resource doesn't need to be defined in the part directly but only in the setup. When the setup is selected, the resource will be added to the part. If the part already contain the same resource, the amount and/or capacity will simply increase when the setup is selected.
