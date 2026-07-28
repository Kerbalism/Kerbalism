## ProcessController
The part has resource processing capabilities. This module allows the implementation of a scheme to provide converter-like modules on a vessel, while keeping the computation independent of the number of individual converters.

The trick is by using a process_ which uses a hidden pseudo-resource created ad-hoc e.g. \_WaterRecycler\_.

This module then adds that resource to its part automatically, and provides a way to *start/stop* the process by a part UI button. Under the hood, starting and stopping the process is implemented by merely setting the resource flow to true and false respectively.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| resource | pseudo-resource to control |  |
| title | name to show on UI |  |
| desc | description to show on tooltip |  |
| capacity | amount of pseudo-resource to add | 1.0 |
| toggle | show the enable/disable toggle | true |
| running | start the process by default | false |
