## Greenhouse
The part simulates a greenhouse that produces a resource **continuously** while the module is active and environmental / resource constraints are satisfied. There is no harvest step or harvest UI — Food (or whatever `crop_resource` is set to) is added every simulation tick. Growth has lighting requirements that can be satisfied from the environment and/or the integrated lamps. Additional requirements can be specified, such as input resources, minimal pressure and maximal radiation. By-product resources can be produced.

| PROPERTY | DESCRIPTION |
| --- | --- |
| crop_resource | name of resource produced continuously |
| crop_size | legacy sizing field; Food rate = `crop_size × crop_rate × food_rate_mult` |
| crop_rate | legacy growth-per-second field used with `crop_size` to set continuous output |
| food_rate_mult | multiplier on Food (crop) output rate; default `1.0` (PartUpgrade) |
| input_rate_mult | multiplier on configured `INPUT_RESOURCE` rates; default `1.0` |
| ec_rate | EC/s consumed by the lamp at max capacity; set to `0` to disable the lamp |
| ec_rate_mult | multiplier on lamp EC use; default `1.0` |
| light_tolerance | minimum lighting flux required for production, in W/m² |
| pressure_tolerance | minimum pressure required for production, in sea level atmospheres (optional) |
| radiation_tolerance | maximum radiation allowed for production in rad/s, after shielding (optional) |
| lamps | object with emissive texture used to represent intensity graphically |
| shutters | animation to manipulate shutters |
| plants | animation to represent plants graphically |
| animBackwards | invert shutter animation direction if the clip plays backward |

Production pauses when lighting, pressure, or radiation fail, when inputs are missing, when Food storage has no free capacity, or when artificial lighting is needed but EC is insufficient. It resumes automatically when blocking conditions clear. Unmanned vessels are supported.

Resource requirements and by-products (other than EC for the lamps) are specified using the stock *resHandler* specification:

```C#
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
```
