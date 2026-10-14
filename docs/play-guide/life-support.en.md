## Biological needs
Kerbals need a constant supply of basic resources, Food, Water and Oxygen otherwise they will eventually perish.

| RULE | CONSUME | PRODUCE | MASS PER-DAY (Kg) | UNITS PER-DAY |
| --- | --- | --- | --- | --- |
| Eating | Food | Waste | 0.07375 | 0.27 |
| Drinking | Water | WasteWater | 0.14 | 0.14 |
| Breathing | Oxygen | WasteAtmosphere | 0.05287 | 37.5 |

**In Real Solar System (23 h 56 m 4 s)** 

| RULE | CONSUME | PRODUCE | MASS PER-DAY (Kg) | UNITS PER-DAY |
| --- | --- | --- | --- | --- |
| Eating | Food | Waste | 0.29489 | 1.05 |
| Drinking | Water | WasteWater | 0.55 | 0.55 |
| Breathing | Oxygen | WasteAtmosphere | 1.26542 | 148.53 |

Individual consumption may vary.

## Psychological needs
Kerbals will suffer mental breakdown after some time, it can be increased by providing.

- More habitat volume per-capita.
- A pressurized habitat.
- Basic comforts.

## Environmental hazards
Kerbals will die if environmental conditions get out of hand, such as.

- *CO2 poisoning* from being exposed to high *CO2* levels (above 2%) in the internal atmosphere for too long. *CO2* levels are maintained by using Scrubbers and/or Greenhouses.

- Exposed to temperatures outside of the *survivable range*. The internal temperature in a vessel is maintained constantly within the *survivable range* if there is enough *ElectricCharge* present. The climatization system uses *ElectricCharge* in proportion to the volume of the habitat to climatize and the difference between the *external* temperature and the *survivable range*.

- Exposed to extreme levels of *radiation*. Radiation belts have extremely high levels, and *solar storms* will dramatically increase the radiation for all vessels in a region temporarily. *Shielding* can be specified per-part in the VAB to reduce the environmental radiation reaching the internal habitat.

## LSS
Each pod or the External LSS Unit (ECLSS) can be configured with Life Support System setups from among the following.

| ECLSS SETUP | DESCRIPTION | TECH REQUIRED |
| --- | --- | --- |
| Scrubber | Sequester CO2 from the internal atmosphere |  |
| Pressure control | Consume Nitrogen to keep the internal pressure at an acceptable level | Engineering 101 |
| Water recycler | Extract potable water, ammonia and CO2 out of waste water | Space Exploration |
| Waste processor | Extract ammonia out of organic waste | Advanced Exploration |
| Monoprop fuel cell | Burn monoprop and O2, producing EC with by-products of water and nitrogen | Advanced Electrics |

## Greenhouse
The stock greenhouse produces *Food* (and *Oxygen*) **continuously** while enabled and constraints are met — there is no harvest / emergency-harvest UI. Output pauses automatically on bad lighting, pressure, radiation, missing inputs, full Food storage, or insufficient EC for lamps, and resumes when conditions clear. No crew is required for production.

In the Default profile, one stock greenhouse supports about **9.5 Kerbals** of Food demand at full duty cycle (plus a large Oxygen by-product). Short missions are often lighter with stored Food; multi-year stations / colonies are where the part pays off. Career upgrades (`Greenhouse-Efficiency1` / `Efficiency2`) raise Food output and cut lamp EC (and, at the top tier, material inputs).

While running it is configured to:

- consume *Water* and *Ammonia*
- consume *CO2* from waste atmosphere and/or pressurized tanks
- consume *ElectricCharge* for artificial lighting when natural light is insufficient
- require an internal pressure of at least 10 kPa
- require radiation levels not in excess of 0.03 rad/h
- produce *Oxygen* and *Food*

For rates, mass break-even, and design rationale, see [Greenhouse balance](greenhouse-balance.md).
