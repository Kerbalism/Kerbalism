---
hide:
  - navigation
---

# Frequently Asked Questions

!!! tip "Not in the FAQ?"
    Ask on [Discord](https://discord.gg/3JAE2JE) or [GitHub Issues](https://github.com/Kerbalism/Kerbalism/issues).

## What do I need to install?

Always:

- **Kerbalism** (core plugin)
- **Exactly one** config pack (official `KerbalismConfig`, or ROKerbalism / SIMPLEX / etc.)

Dependencies: Module Manager, HarmonyKSP, KSPCommunityFixes. Official **KerbalismConfig** also needs Community Resource Pack. See [Downloads and links](links.md).

**KSP version:** current releases target **1.12.x** only (as of 3.40).

## Can I install multiple config packs?

No — unless a pack explicitly documents stacking. Mixing packs usually breaks balance and MM patches.

## Why did my Kerbal die?

Common causes:

1. Life-support resources exhausted (Food / Water / Oxygen)
2. Radiation or extreme temperature
3. Long-term pressure / habitat stress cascading into accidents
4. Resource conflicts with other mods so unloaded vessels “starve” in the background

Check the monitor and planner first, then [Mod support](mod-support.md).

## Why is science slower / experiments never finish?

Kerbalism runs most experiments **over time**. They need EC, drive space, and sometimes environment or crew. See [Science](play-guide/science.md) and the [science tutorial](play-guide/science-tutorial.md).

## Why does another mod break?

Kerbalism replaces or wraps many stock and third-party modules so life support and science work on **unloaded** vessels. Feature overlap or missing patches causes breakage. See [Mod support](mod-support.md). Prefer a lightly modded install.

## BackgroundResources warning (DeepFreeze / TAC-LS)

`BackgroundResources` (bundled with some JPLRepo mods such as DeepFreeze) conflicts with Kerbalism’s own background simulation. Kerbalism will warn about it.

**DeepFreeze is not a supported combo** with Kerbalism in practice: removing `GameData/REPOSoftTech/BackgroundResources` may silence the warning but DeepFreeze itself still overlaps Kerbalism’s resource/background model. Prefer not combining them.

You can mute specific startup warnings in Kerbalism difficulty settings (`ModsWarning` / related options).

## RemoteTech

Kerbalism still contains Comm / RemoteTech integration code (antennas, EC factors, control path). That does **not** mean a large RT + Kerbalism install is well maintained. Expect quirks; test carefully. Stock CommNet is the better-supported path for most players.

## How do I stop timewarp-stopping notifications?

In the Kerbalism vessel UI, open the **cfg** tab and disable alerts per vessel. Solar-storm warp stops only while at least one vessel in that planetary system still has space-weather warnings enabled.

## I can’t transfer samples between parts

Samples need a Kerbal (EVA or crew aboard). There is a difficulty option to always allow sample transfers on uncrewed vessels.

## Support for [x]Science! and similar?

Most of those features are already in Kerbalism (auto-running experiments, subject availability). The science systems differ too much from stock for meaningful dual support.

## Why is there no pressure in my Mk1 pod?

The Mk1 is not designed to be pressurizable. Keep crew alive until you unlock better habitats.

## Fuel cells produce no electricity?

Ensure there is tank space for Water (or enable dump), then toggle the cell off/on.

## Stress above 100% — can Kerbals die of stress?

At high stress a random bad event fires (dumping resources, deleting science data, destroying a part, …), then stress drops and climbs again. Stress alone is not a direct “death bar,” but the events often are mission-ending.

## Where do I report bugs?

Prefer [GitHub Issues](https://github.com/Kerbalism/Kerbalism/issues) with a [KSPBugReport](https://github.com/KSPModdingLibs/KSPBugReport) dump. Incomplete logs are often ignored.
