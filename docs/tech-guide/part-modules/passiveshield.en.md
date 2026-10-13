## PassiveShield
The part absorbs radiation. Intended for parts that are deployed in situ
and require some kind of action to activate (like filling sandbags).

Use a positive radiation value for absorption.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| title | GUI name of status string | Sandbags |
| engageActionTitle | Name of engage action in PAW | fill |
| disengageActionTitle | Name of engage action in PAW | empty |
| disabledTitle | What to display as status while disengaged | stowed |
| ec_rate | EC consumption rate per-second (optional) | 0 |
| toggle | true if the effect can be toggled on/off | true |
| animation | name of animation to play when enabling/disabling |  |
| added_mass | how much mass to add when deployed, in tons | 1.5 |
| require_eva | if true, part can only be activated on EVA | true |
| crew_operate | Crew requirements needed for activation | true |
