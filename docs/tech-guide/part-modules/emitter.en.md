## Emitter
The part emits radiation. Use a negative radiation value for absorption.

| PROPERTY | DESCRIPTION | DEFAULT |
| --- | --- | --- |
| radiation | radiation in rad/s, can be negative |  |
| ec_rate | EC consumption rate per-second (optional) |  |
| toggle | true if the effect can be toggled on/off | false |
| active | name of animation to play when enabling/disabling |  |
| emitterId | stable MM-safe id (no `#`); use with Configure `id_field = emitterId` when a part has multiple Emitters |  |

When several Emitters share a part (for example one per Configure setup / reactor recipe), give each a unique `emitterId` and reference it from the setup:

```
MODULE
{
	type = Emitter
	id_field = emitterId
	id_value = my-reactor-uranium
}
```

Prefer `emitterId` over `id_index`: module order is fragile when other mods also patch the part.
