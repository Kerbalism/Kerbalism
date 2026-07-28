The internal habitat of a vessel is modeled as a set of individual parts flagged as habitats. Each part has an internal volume and an external surface, deduced automatically from their bounding box or specified by the part author.

From these basic properties, more complex ones are deduced and made available as modifiers to the rule framework.

## Pseudo-resources
Some pseudo-resources are added to each habitat. Each one is used to simulate the individual properties of a vessels internal habitat volume and surface area. Their flow state is synchronized automatically from the habitat enabled/disabled state.

| RESOURCE | CAPACITY | USE | DENSITY (per-unit) |
| --- | --- | --- | --- |
| Atmosphere | Set by volume | Pressure | 1 m³ of Nitrogen at STP |
| WasteAtmosphere | Set by volume | CO2 level | 1 m³ of CarbonDioxide at STP |
| Shielding | Set by surface | Radiation shielding | 0.075 (alloy composite shielding) |

## Atmospheric control
Atmospheric conditions inside a vessel are regulated by Life Support Systems fitted into manned parts or by the External Life Support Unit.
Each vessel has a number of configurable LSS slots that can be configured into an assortment of different LSS processes. The number of slots is upgradeable by purchasing the Slot Upgrade in the Electronics section of the Tech Tree. Also as you progress through the Tech Tree more options become available for the LSS slots.

The internal atmospheric pressure is regulated by the Pressure Controller, this unit is used to overcome the losses from leaks and for pressurizing inflatable habitats.

The CO2 level is regulated by the Scrubber, this unit is used to scrub from the atmosphere the CO2 that the Kerbal's exhale. The greenhouse can also be used to remove CO2 from the atmosphere.

## Radiation shielding
The user can choose the level of Shielding for each individual habitat part in the editor. The overall Shielding level on all enabled habitat parts is then used to reduce the environment radiation. It is possible to influence the level after launch by producing the Shielding resource.

## Enable/disable habitats
The user can enable and disable habitat parts individually, both in flight and in the editor. This is used to configure and reconfigure the vessels internal volume, to influence its properties as the need arise.

## Equalization and venting
When a habitat transitions from the enabled to the disabled state or vise versa, special care is used to avoid abrupt changes to the overall pressure of the whole vessels internal habitat. This is accomplished by two temporary states, in addition to *enabled* and *disabled*. These are *equalizing* that first matches the part pressure with the rest of the vessel and then switches to *enabled* and the other being *venting* that depressurizes the part completely by dumping the removed atmosphere either into the rest of the vessel, if there is room, or outside.

## Inflatable habitats

If a habitat is inflatable, its inflate/deflate animation will be driven by the actual pressure of the part. Note that pressurizing a large habitat with a small pressure controller can take a long time. For example the Mk1 pod's pressure control will take approx 12 days (3 Earth days) to inflate the Gravity Ring. So remember to add enough pressure controllers for the job.

And by the way, you also should bring enough nitrogen. To estimate how much, look at `Volume` in the `Habitat` section for the part. It takes 1,000 units of nitrogen to fill 1 cubic meter. So for a habitat volume of 19 cubic meters you will need 19,000 units of nitrogen just to inflate. You wouldn't want to blow up a bouncy castle with your mouth :/

To inflate an inflatable habitat on a celestial body surface with breathable atmosphere the "Air pump" should be used. Every crewable habitat got an air pump, including the non-inflatable ones. The more air pumps are enabled, the faster the inflation proceeds. Inflating the Gravity Ring with the air pump of the mk1 pod will take approx 3.5 hours. (Of course you will not bring the gravity ring to a planet's surface, or will you? This was just a comparision...)

## Comforts
Comforts are provided by some vessel conditions, and parts implementing the [Comfort](../tech-guide/part-modules/comfort.md) module.

| COMFORT | CONDITION | PART |
| --- | --- | --- |
| firm-ground | vessel is landed/splashed, or a Gravity Ring is deployed on a powered vessel | Gravity Ring |
| not-alone | more than 1 crew member in the vessel |  |
| call-home | vessel can communicate with DSN via an antennas science rate |  |
| exercise | Kerbal's can ride a bike or use a treadmill etc | Hitchhiker |
| panorama | Kerbal's can look out of a big window | Cupola |

Whole-vessel spin does **not** grant firm ground. Use a dedicated Gravity Ring (deployed, vessel powered) or land/splash the vessel.
