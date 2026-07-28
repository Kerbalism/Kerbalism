# Patch injection

Enabled features come from user Settings and from modifiers used in the active profile. At load time Kerbalism injects Module Manager tags so you can write conditional patches with `:NEEDS[FeatureXXX]` or `:NEEDS[ProfileXXX]`.

| FEATURE | HOW IT IS DEFINED | WHAT IT ENABLES |
| --- | --- | --- |
| Reliability | Settings | Component malfunctions and critical failures |
| Deploy | Settings | Deployment / keep-alive EC costs |
| Science | Settings | Science storage, transmission, analysis |
| SpaceWeather | Settings | Coronal mass ejections |
| Automation | Settings | Script UI and automatic execution |
| Radiation | Detected from profile modifiers | Radiation simulation and rendering |
| Shielding | Detected from modifiers | Shielding resource on habitats |
| LivingSpace | Detected from modifiers | Habitat volume calculations |
| Comfort | Detected from modifiers | Comfort parts / bonuses |
| Poisoning | Detected from modifiers | Atmospheric CO₂ (WasteAtmosphere) in habitats |
| Pressure | Detected from modifiers | Atmospheric pressure in habitats |
| Habitat | Implied when habitat-related features need it | Habitat module added to crewed parts |

Profile tags follow the profile name, e.g. `:NEEDS[ProfileDefault]` for the official `default` profile.
