using CommNet;
using Harmony;
using static ModuleCommand;

namespace KERBALISM
{
	/*
	Issues with ModuleCommand :

	- It make you loose control as soon as the EC amount is zero, independently of
	  if there is enough production for it when total ec consumption > total ec production.

	- When control is lost, it set the ModuleCommand.commCapable boolean to false, causing CommNet
	  to ignore the vessel. If the vessel goes from loaded to unloaded while in this state,
	  CommNet ignore it forever, even if the EC comes back thanks to our unloaded resource sim.

	Solution :

	- At prefab compilation (OnLoad + GameScenes.LOADING):
	  - We save the ec rate present in ModuleCommand.ResHandler into ModuleCommand.hibernationMultiplier
	  - We remove all inputs from ModuleCommand.ResHandler to have the availability check in
	    ModuleCommand.UpdateControlSourceState() always return true, this prevent ModuleCommand.commCapable
		from being set to false in that case while respecting the other conditions.

	- After ModuleCommand.FixedUpdate() :
	  - We consume the EC, with the "critical" flag.
	  - If that critical request isn't satified, we set the part control state (Part.isControlSource)
	    and the module control state (ModuleCommand.localVesselControlState), replicating the result
		of ModuleCommand.UpdateControlSourceState() we bypassed (and the resulting effect done in
		ModuleCommand.FixedUpdate)

	- In OnStart : we disable the "hibernateOnWarp" feature.

	This **seems** to work well, causing the vessel to still be considered by CommNet while triggering the correct
	PAW / UI restrictions when there is no EC at all.
	*/

	[HarmonyPatch(typeof(ModuleCommand))]
	[HarmonyPatch("OnLoad")]
	class ModuleCommand_Start
	{
		static void Prefix(ModuleCommand __instance)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				if (__instance.resHandler.inputResources.Count > 0 && __instance.resHandler.inputResources[0].name == "ElectricCharge")
				{
					__instance.hibernationMultiplier = __instance.resHandler.inputResources[0].rate;
				}
				else
				{
					__instance.hibernationMultiplier = 0.0;
				}

				__instance.resHandler.inputResources.Clear();
			}
		}
	}


	[HarmonyPatch(typeof(ModuleCommand))]
	[HarmonyPatch("FixedUpdate")]
	class ModuleCommand_FixedUpdate
	{
		static void Postfix(ModuleCommand __instance, ref VesselControlState ___localVesselControlState, ref ModuleControlState ___moduleState)
		{
			if (Lib.IsEditor())
				return;

			VesselData vd = __instance.vessel.KerbalismData();
			if (!__instance.hibernation)
				vd.hasNonHibernatingCommandModules = true;

			// don't change anything if the command module doesn't require EC
			if (__instance.hibernationMultiplier == 0.0)
				return;

			VesselResource ec = vd.ResHandler.ElectricCharge;

			// do not consume if this is a non-probe MC with no crew
			// this make some sense: you left a vessel with some battery and nobody on board, you expect it to not consume EC
			if (__instance.minimumCrew == 0 || __instance.part.protoModuleCrew.Count > 0)
			{
				double ecRate = __instance.hibernationMultiplier;
				if (__instance.hibernation)
					ecRate *= Settings.HibernatingEcFactor;

				ec.Consume(ecRate * Kerbalism.elapsed_s, ResourceBroker.Command, true);
			}

			// replicate the resource checking code of ModuleCommand.UpdateControlSourceState()
			// and the resulting states set from ModuleCommand.FixedUpdate()
			if (!ec.CriticalConsumptionSatisfied)
			{
				__instance.part.isControlSource = Vessel.ControlLevel.NONE;
				___moduleState = ModuleControlState.NotEnoughResources;

				if (__instance.minimumCrew > 0)
					___localVesselControlState = VesselControlState.Kerbal;
				else
					___localVesselControlState = VesselControlState.Probe;
			}
		}
	}

	[HarmonyPatch(typeof(ModuleCommand))]
	[HarmonyPatch("OnStart")]
	class ModuleCommand_OnStart
	{
		static void Postfix(ModuleCommand __instance)
		{
			__instance.Fields["hibernateOnWarp"].guiActive = false;
			__instance.Fields["hibernateOnWarp"].guiActiveEditor = false;
			__instance.hibernateOnWarp = false;
		}
	}




}
