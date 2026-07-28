# Crew specifications 
Used f.i. in Experiments (used in crew_operate, crew_reset or crew_prepare as well as in some
other Kerbalism modules) have to be given according to `true|trait|[trait]@level`

Examples:

- "true": any kerbal will do.
- "Scientist": you need a Scientist, doesn't matter how experienced. Other traits are "Pilot" and "Engineer". We're not assuming that you'll want to use "Tourist"...
- If the value is "@3" any Kerbal with 3 or more stars will do
- If the value is "Scientist@2" you need a Scientist with 2 or more stars.
- Empty values usually turn the feature off.