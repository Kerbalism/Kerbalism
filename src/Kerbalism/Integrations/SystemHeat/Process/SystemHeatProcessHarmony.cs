using System.Collections.Generic;
using HarmonyLib;
using KERBALISM.Planner;

namespace KERBALISM
{
	internal static class SystemHeatProcessHarmony
	{
		[HarmonyPatch(typeof(ProcessController), "SetRunning")]
		private static class PatchProcessControllerSetRunning
		{
			private static void Prefix(ProcessController __instance, ref bool value)
			{
				if (!value || Lib.IsEditor())
					return;

				ProcessControllerSystemHeat heatController = __instance as ProcessControllerSystemHeat;
				if (heatController != null && heatController.RequiresDeployGate() && !heatController.IsDeployedForUse())
					value = false;

				ProcessControllerDeployable deployableController = __instance as ProcessControllerDeployable;
				if (deployableController != null && deployableController.RequiresDeployGate() && !deployableController.IsDeployedForUse())
					value = false;
			}

			private static void Postfix(ProcessController __instance)
			{
				ProcessControllerSystemHeat heatController = __instance as ProcessControllerSystemHeat;
				if (heatController != null)
				{
					heatController.OnRunningChanged();
					return;
				}

				ProcessControllerDeployable deployableController = __instance as ProcessControllerDeployable;
				if (deployableController != null)
					deployableController.OnRunningChanged();
			}
		}

		[HarmonyPatch(typeof(ModuleAnimationGroup), "DeployModule")]
		private static class PatchDeployModule
		{
			private static void Postfix(ModuleAnimationGroup __instance)
			{
				ProcessDeploySync.FromAnimator(__instance == null ? null : __instance.part, deployStarted: true);
			}
		}

		[HarmonyPatch(typeof(ModuleAnimationGroup), "RetractModule")]
		private static class PatchRetractModule
		{
			private static void Postfix(ModuleAnimationGroup __instance)
			{
				ProcessDeploySync.FromAnimator(__instance == null ? null : __instance.part);
			}
		}

		[HarmonyPatch(typeof(Harvester), "Toggle")]
		private static class PatchHarvesterToggle
		{
			private static void Postfix(Harvester __instance)
			{
				if (__instance is HarvesterSystemHeat && Lib.IsEditor())
					Lib.RefreshPlanner();
			}
		}

		[HarmonyPatch(typeof(ResourceSimulator), "RunSimulator")]
		private static class PatchResourceSimulatorRunSimulator
		{
			private static void Prefix(List<Part> parts)
			{
				if (!Lib.IsEditor() || parts == null)
					return;

				foreach (Part part in parts)
				{
					foreach (PartModule module in part.Modules)
					{
						if (module is ProcessControllerSystemHeat heat && heat.resource == "_Nukereactor")
							heat.SyncPlannerPseudoResource();
					}
				}
			}
		}
	}

	internal static class ProcessDeploySync
	{
		internal static void FromAnimator(Part part, bool deployStarted = false)
		{
			if (part == null)
				return;

			ModuleAnimationGroup animator = part.FindModuleImplementing<ModuleAnimationGroup>();
			if (animator == null)
				return;

			foreach (ProcessControllerSystemHeat module in part.FindModulesImplementing<ProcessControllerSystemHeat>())
			{
				if (!module.RequiresDeployGate())
					continue;

				if (animator.isDeployed)
				{
					if (deployStarted)
						module.MarkDeployStarted();
					else
						module.EnableModule();
				}
				else
					module.DisableModule();
			}

			foreach (ProcessControllerDeployable module in part.FindModulesImplementing<ProcessControllerDeployable>())
			{
				if (!module.RequiresDeployGate())
					continue;

				if (animator.isDeployed)
				{
					if (deployStarted)
						module.MarkDeployStarted();
					else
						module.EnableModule();
				}
				else
					module.DisableModule();
			}
		}
	}
}
