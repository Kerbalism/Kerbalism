## Overview

Making a science mod work with Kerbalism needs a bit of configuration, because there are some numbers that you'll have to come up with. Like how much EC a sensor uses, how much data it generates, how long its going to take, the stuff that stock science doesn't have. It's not too hard, but it's a bit of work to do : 
- A custom `KERBALISM_EXPERIMENT` node defined inside the stock `EXPERIMENT_DEFINITION` node use used to define global informations. The KERBALISM_EXPERIMENT node is optional.
- The `Experiment` partmodule (see [documentation](part-modules/experiment.md)) replaces the stock `ModuleScienceExperiment`.
- The `HardDrive` partmodule (see [documentation](part-modules/harddrive.md)) replaces the stock `ModuleScienceContainer`.

Kerbalism uses a rather complex ModuleManager patching system that dynamically replace the stock experiments and add the `HardDrive` module to every pod and probe core. Thanks to those patches, you shouldn't have to care much about drives.

Mods that implement new custom experiments have two options : 
- Replace the stock or mod PartModule by a the Kerbalism `Experiment` module.
- Keep their module and don't use the Kerbalism one. This can be preferred if you are using a custom module that has some specific gameplay mechanisms and doesn't make much sense to be turned in a long duration experiment. Kerbalism fully support the stock science system, but a few things may need to be tweaked : 
  - Kerbalism uses limited capacity drives / sample slots. It's recommended to balance the `EXPERIMENT_DEFINITION` `dataScale` value so the data size is balanced for the drive capacities. Note that the data size of an experiment (in MB) is equal to `baseValue * dataScale`, and that a Kerbalism sample slot is equal to 1024 MB of data.
  - The stock `ModuleScienceContainer` has a `xmitDataScalar` value. Stock (or modded) experiments that use `xmitDataScalar = 0` will produce a sample, any higher value will produce a file.

## Patch example : 

```
// This node is handled by the kerbalism patching system
// You can use it to define variables that can then be reused in individual patchs
@KERBALISM_EXPERIMENT_VALUES:NEEDS[MyMod,FeatureScience]
{
  %MyMod
  {
    MyExperimentID
    {
      size = 7250         // size in MB. If the experiment is a sample, this define how many slots it uses :  1024 MB = 1 slot.
      duration = 453600   // duration in seconds
    }
  }
}

// You need to patch the experiment definition with the data size 
// so the experiment module data_rate combined with it gives the right duration.
// Note that even if you don't use Kerbalism's Experiment module, it is recommended to set this anyway
// because data / sample size is usually not balanced against Kerbalism's drives capacities.
@EXPERIMENT_DEFINITION:HAS[#id[MyExperimentID]]:NEEDS[MyMod,FeatureScience]:FOR[zzzKerbalismDefault]
{
  @dataScale = #$@KERBALISM_EXPERIMENT_VALUES/MyMod/MyExperimentID/size$
  @dataScale /= #$baseValue$
  
  // This is optional, see full documentation further in this document.
  KERBALISM_EXPERIMENT
  {
    Situation = Surface@Biomes
    Situation = FlyingLow@Biomes 
  }
}

// Finally, patch your part
@PART[MyPart]:NEEDS[MyMod,FeatureScience]:FOR[KerbalismDefault]
{
    // Add Kerbalism's experiment module
    // See docs: tech-guide/part-modules/experiment
    // for the full documentation
    MODULE
    {
      name = Experiment
      experiment_id = MyExperimentID
      data_rate = #$@KERBALISM_EXPERIMENT_VALUES/MyMod/MyExperimentID/size$
      @data_rate /= #$@KERBALISM_EXPERIMENT_VALUES/MyMod/MyExperimentID/duration$
      // those values are optional, this is an example :
      ec_rate = 3.2
      allow_shrouded = False
      requires = SunAngleMin:5,SunAngleMax:60
    }
    
    // Delete the original module (usually it's a stock ModuleScienceExperiment)
    !MODULE[MyNonKerbalismScienceModule] {}
}
```
You can find examples using this template in the [DMagic Orbital Science patch](https://github.com/Kerbalism/Kerbalism/blob/4a7316b4645a44aff0db504588be7000becc0812/GameData/KerbalismConfig/System/ScienceRework/ModSupport/DMagicOrbitalScience.cfg)

## `KERBALISM_EXPERIMENT` documentation

Full example : 

```
KERBALISM_EXPERIMENT
{    
  // SampleMass example : the full data set will weight 50 kg
  SampleMass = 0.05

  // Body restriction example : all bodies that have an atmosphere 
  // excepted Duna and all suns (suns are atmospheric bodies)
  BodyAllowed = Atmospheric
  BodyNotAllowed = Suns
  BodyNotAllowed = Duna
 
  // VirtualBiome example : these 4 subjects will be available for 
  // every situation defined with `@VirtualBiomes`
  VirtualBiome = NoBiome
  VirtualBiome = InnerBelt
  VirtualBiome = OuterBelt
  VirtualBiome = Magnetosphere
                  

  // Situation example : normal body biomes for the landed+splashed situation and flying low, 
  // no biomes for flying high, and the virtual biomes for the space low+high situation
  Situation = Surface@Biomes
  Situation = FlyingLow@Biomes 
  Situation = FlyingHigh
  Situation = Space@VirtualBiomes
}
``` 

#### `SampleMass`
- Sample mass in tons (for a full data set). if undefined or 0, the experiment produce a file
- If you are using a stock experiment module (ex : ModuleScienceExperiment or DMModuleScienceAnimate) you can choose wherever it will produce a file or a sample by setting the "xmitDataScalar" value.
xmitDataScalar = 0 will produce a sample (with the SampleMass defined here), any higher value will produce a file.

#### `BodyAllowed` and `BodyNotAllowed`
You can use multiple lines, just don't use conflicting combinations.
You can use either a body name or the following keywords :  
Atmospheric, NonAtmospheric, Gaseous, Solid, Oceanic, HomeBody, HomeBodyAndMoons, Planets, Moons, Suns

#### `VirtualBiome`
Virtual biomes allow to create specific subjects that depend on specific conditions.
The are enabled per situation and can't be combined with normal body biomes.
When using multiple virtual biomes that may be available at the same time, the priority is hardcoded (see list)
Note that virtual biomes subjects can cause incompatibilities with the contract system (especially dynamically generated Contract Configurator contracts), you may get contracts that are not doable.

Multiple lines allowed, format is `VirtualBiome = VirtualBiomeKeyword`. Valid keywords are :
- `NoBiome` : create a "biome-agostic" situation available when no virtual biome is available.
- `NorthernHemisphere` : available when over the body north hemisphere. Lowest priority. Implemented for DMOS contracts compatibility.
- `SouthernHemisphere` : available when over the body south hemisphere. Lowest priority. Implemented for DMOS contracts compatibility.
- `Storm` : available during a solar storm. High priority.
- `InnerBelt` : available when inside the body inner radiation belt
- `OuterBelt` : available when inside the body outer radiation belt
- `Magnetosphere` : available when inside the body magnetosphere. Lower priority than the belt biomes.
- `Interstellar` : available when in a sun SOI and outside the heliopause
- `Reentry` : available when descending rapidly in atmosphere over mach 5 while apoapsis is outside the atmosphere. 

#### `Situation`
Situation values will create-or-replace the stock situationMask/biomeMask values.
Multiple lines allowed, format is `Situation = SituationKeyword`, and append `@Biomes` or `@VirtualBiomes` to allow biomes or virtual biomes
Valid situation keyword :
- `SrfLanded`, `SrfSplashed`, `FlyingLow`, `FlyingHigh`, `InSpaceLow`, `InSpaceHigh` : stock situations.
- `Surface` : valid when landed or splashed, uses the SrfLanded science value. Incompatible with SrfLanded/SrfSplashed.
- `Flying` : valid when in atmosphere, uses the FlyingHigh science value. Incompatible with FlyingLow/FlyingHigh.
- `Space` : valid when in space, uses the InSpaceLow science value. Incompatible with InSpaceLow/InSpaceHigh.
- `BodyGlobal` : always valid, uses the InSpaceLow science value. Incompatible with all other situations.

#### `IncludeExperiment`

- Add a `IncludeExperiment` config field to the `KERBALISM_EXPERIMENT` node
- Set it to another `EXPERIMENT_DEFINITION` `id`, multiple values allowed
- Any science transmitted/recovered for the experiment will also be collected for every experiment defined in `IncludeExperiment`
- This is based on science value, and processed when science is retrieved, you won't see any file / data, and the science won't appear as "in flight" points for the included experiments
- You can chain multiple experiments : if A include B and B include C, collecting science for A will collect it for B and C
- Recovering points for an experiment will trigger the completion event (contracts) every time the included experiment science value is reached. Ex : if C value is 2 points and A value is 10 points, doing A will potentially trigger the completion event for C 5 times (at 2/4/6/8/10 points), if you are transmitting. If you recover it, the event will only be triggered once.
- Situation/body restrictions can be different between chained experiments.
- Included experiments are shown in the experiment part info / experiment popup / science archive UI

Example :

```
EXPERIMENT_DEFINITION
{
  id = myExpLvl1
}
```

`myExpLvl2` will also collect `myExpLvl1`:
```
EXPERIMENT_DEFINITION
{
  id = myExpLvl2
  KERBALISM_EXPERIMENT
  {
    IncludeExperiment = myExpLvl1
  }
}
```

`myExpLvl3` will also collect `myExpLvl1` and `myExpLvl2`:
```
EXPERIMENT_DEFINITION
{
  id = myExpLvl3
  KERBALISM_EXPERIMENT
  {
    IncludeExperiment = myExpLvl2
  }
}
```

#### Special experiment : resource survey 

Use this definition to have the experiment completion and data retrieval unlock the body resource map (identical to what the stock M700 orbital scanner does) :
```
KERBALISM_EXPERIMENT
{
  UnlockResourceSurvey = true
  Situation = BodyGlobal
}
```