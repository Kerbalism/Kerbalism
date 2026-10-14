## PlannerController
The Part has a toggle to enable/disable simulation in the *Planner*. The *Planner* simulates resource consumption and production for many types of modules, and most of the time it is useful to be able to toggle these on and off in the VAB/SPH to simulate different scenarios for the vessel.

Some modules do not offer any way to toggle them on and off in the VAB/SPH and that's where the *PlannerController* comes in, once added to a part it will add an editor-only toggle button. The *Planner* will then consider or ignore all modules in that part depending on the toggle button state.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| toggle | show the toggle button in the editor | true |
| title | name shown on the button (may be a `#loc` key) |  |
| plannerId | stable ModuleManager-safe id (no `#`); prefer `HAS[#plannerId[...]]` over matching `#title[#loc...]` |  |
| considered | default button state | false |
