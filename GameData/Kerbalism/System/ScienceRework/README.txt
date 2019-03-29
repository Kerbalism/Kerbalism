//changes to science mechanics based on kerbalism's modules.
//patches are structured in this convoluted way for ease of patching new parts and mods.
//=================================================================================================================================================================

	HARD REQUIREMENTS:
	Kerbalism + all it's requirements
	Module Manager 
	the Kerbalism16.bin (courtesy of Sir Mortimer, he fixed a bunch of stuff) provided with this patch pack; shit will break otherwise. It should be included in later releases, but if 2.1.2 or less, you need to replace it.

	Tested on KSP 1.6.1; no idea what, how, when, if, wether, etc. works on other game versions.
	This "SHOULD NOT" break any saves, but you will get warnings for in-flight and saved crafts for missing modules, as stock experiment modules are gone.
	Try at your own risk, no warranties.

//=================================================================================================================================================================
//stock and supported mods are patched "properly" (pay attention to quotation marks)
//mods that use stock experiment module AND one of the stock experiments are affected, again "properly".
//unsupported ones retain their default behavior (instant science for the click of a button) and are unaffected by this patch bundle.

//=================================================================================================================================================================

//there's an option to add experiments to new parts, via groups. I added together a bunch of experiments that make sense to be together,
//such as atmospheric, surface, orbital, sensor, etc. (this is the reason why patch file structure is so convoluted)
//this enables a button on your RMB UI that allows you to select which experiments available from the group you want to install on your part in the editor.
//the stock groups are fairly lackluster, simply because stock has very few experiments. This was intended for mods that add a bunch of experiments and you want 
//a way of grouping them together in a somewhat sensible manner
//patching the parts yourself should be fairly straightforward, and take less than a couple of minutes per part.
//there's a Template.txt in PartPatches folder that shows exactly how to do it and explains what goes where.

//should you wish to change the group compositions, go to Groups, then mod by mod in ModSupport. it's a nightmare, would not recommend. 0/10

//=================================================================================================================================================================
//most of the data scales/data collection rates/ec/s were changed to my own taste, and I have my own version of "balance". still needs a bunch of testing.
//should you wish to fiddle around with this, go into tweakables folder. fiddle there.
//=================================================================================================================================================================
//should you simply wish to slap a bunch of specific experiments into a part (say, a probe core) without the ability to select through configure,

@PART[whatever part name]:NEEDS[FeatureScience]:BEFORE[Kerbalism]
//if part is from a mod, add the mod's name to the NEEDS[] bit, e.g. @PART[MyPart]:NEEDS[FeatureScience,MyMod]:BEFORE[Kerbalism]
{
	MODULE
		{
			name = Experiment
			experiment_id = 			//<------------- experimentID from stock ScienceDefs.cfg or each individual mod's science defs (don't forget about the :NEEDS[MyMod] at the MODULE level if experiment is from a mod.)
		}
		//add this module for each experiment. everything else should be taken care of by other patches in this bundle.
}

//IF YOU WANT CUSTOM VALUES, OTHER THAN WHAT'S PROVIDED (faster sampling rate, less ec/s, whatever else DIFFERENT than default configured values for your experiments), you'll have to add another part:

@PART[whatever part name]:NEEDS[FeatureScience]:FINAL		//Has to be final, a lot of tweaking and balancing is done very late in the patchig process
{
	@MODULE[Experiment]:HAS[#experiment_id[id from above]]
	{
		%data_rate = 
		%ec_rate = 
		%transmissible = 
		%requires = 						//these are all the possible fields.
		%crew_operate = 					//more info at https://github.com/SirMortimer/Kerbalism/blob/ebee94eef30bc033e9de0d2f0e2fc629c2b89e76/docs/modders/modules.rst
		%crew_reset = 						//Courtesy of Sir Mortimer for doing a bunch of work on the Experiment module.
		%crew_prepare = 
		%anim_deploy = 
	}
}
