using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Temporary diagnostics for NFE fission reactor power / running state across load ↔ background transitions.
	/// Filter KSP.log with: Kerbalism.FissionReactorDbg
	/// </summary>
	internal static class FissionReactorStateDebug
	{
		internal const string Tag = "[Kerbalism.FissionReactorDbg]";

		/// <summary>Set false to silence without rebuilding.</summary>
		internal static bool Enabled = true;
		private static bool announced = false;

		private static void AnnounceOnce()
		{
			if (announced || !Enabled)
				return;
			announced = true;
			Lib.Log(Lib.BuildString(Tag, " ENABLED - filter KSP.log with Kerbalism.FissionReactorDbg"));
		}

		internal static void Log(Part part, string phase, string detail = null)
		{
			if (!Enabled || part == null)
				return;

			AnnounceOnce();

			ProcessControllerSystemHeat process = part.FindModuleImplementing<ProcessControllerSystemHeat>();
			if (process == null || process.resource != "_Nukereactor")
				return;

			string vessel = part.vessel != null ? part.vessel.GetDisplayName() : "?";
			string partTitle = part.partInfo != null ? part.partInfo.title : part.name;
			string native = GetNativeReactorThrottleInfo(part);
			Lib.Log(Lib.BuildString(
				Tag, " ", phase,
				" | vessel=", vessel,
				" | part=", partTitle,
				" | running=", process.running.ToString(),
				" | broken=", process.broken.ToString(),
				" | power%=", process.CurrentPowerPercent.ToString("F1"),
				native.Length > 0 ? Lib.BuildString(" | ", native) : string.Empty,
				detail != null ? Lib.BuildString(" | ", detail) : string.Empty));
		}

		private static string GetNativeReactorThrottleInfo(Part part)
		{
			if (part == null)
				return string.Empty;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (module == null)
					continue;

				if (module.moduleName == "ModuleSystemHeatFissionReactor")
				{
					return Lib.BuildString(
						"nativeFR Enabled=", SystemHeat.Get(module, "Enabled", false).ToString(),
						" ReactorThrottle=", SystemHeat.Get(module, "CurrentReactorThrottle", 0f).ToString("F1"),
						" Throttle=", SystemHeat.Get(module, "CurrentThrottle", 0f).ToString("F1"));
				}
			}

			return string.Empty;
		}

		internal static void LogProto(Part part, string phase, ProtoPartModuleSnapshot protoModule = null)
		{
			if (!Enabled || part == null || part.protoPartSnapshot == null)
				return;

			if (protoModule == null)
			{
				foreach (ProtoPartModuleSnapshot module in part.protoPartSnapshot.modules)
				{
					if (module.moduleName != "ProcessControllerSystemHeat"
						|| Lib.Proto.GetString(module, "resource") != "_Nukereactor")
						continue;
					protoModule = module;
					break;
				}
			}

			if (protoModule == null)
				return;

			ProtoPartResourceSnapshot pseudo = part.protoPartSnapshot.resources.Find(k => k.resourceName == "_Nukereactor");
			string pseudoInfo = pseudo == null
				? "pseudo=missing"
				: Lib.BuildString(
					"pseudo flow=", pseudo.flowState.ToString(),
					" amt=", pseudo.amount.ToString("F3"),
					" max=", pseudo.maxAmount.ToString("F3"));

			Log(part, phase, Lib.BuildString(
				"proto running=", Lib.Proto.GetBool(protoModule, nameof(ProcessController.running)).ToString(),
				" proto power%=", Lib.Proto.GetFloat(protoModule, nameof(ProcessControllerSystemHeat.CurrentPowerPercent)).ToString("F1"),
				" ", pseudoInfo));
		}

		internal static void LogVessel(Vessel v, string phase, string detail)
		{
			if (!Enabled || v == null)
				return;

			Lib.Log(Lib.BuildString(Tag, " ", phase, " | vessel=", v.GetDisplayName(), " | loaded=", v.loaded.ToString(), " | ", detail));
		}

		internal static void LogProtoModule(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, string phase, string detail = null)
		{
			if (!Enabled || v == null || part == null || module == null)
				return;

			AnnounceOnce();

			ProtoPartResourceSnapshot pseudo = part.resources.Find(k => k.resourceName == "_Nukereactor");
			string pseudoInfo = pseudo == null
				? "pseudo=missing"
				: Lib.BuildString(
					"pseudo flow=", pseudo.flowState.ToString(),
					" amt=", pseudo.amount.ToString("F3"),
					" max=", pseudo.maxAmount.ToString("F3"));

			float coreDamage = Lib.Proto.GetFloat(module, nameof(ProcessControllerSystemHeat.CoreDamage));
			float loopK = GetProtoLoopTemperature(part, Lib.Proto.GetString(module, "systemHeatModuleID"));

			Lib.Log(Lib.BuildString(
				Tag, " ", phase,
				" | vessel=", v.GetDisplayName(),
				" | part=", part.partInfo != null ? part.partInfo.title : part.partName,
				" | proto running=", Lib.Proto.GetBool(module, nameof(ProcessController.running)).ToString(),
				" | broken=", Lib.Proto.GetBool(module, nameof(ProcessController.broken)).ToString(),
				" | proto power%=", Lib.Proto.GetFloat(module, nameof(ProcessControllerSystemHeat.CurrentPowerPercent)).ToString("F1"),
				" | CoreDamage=", coreDamage.ToString("F1"),
				" | loopK=", loopK.ToString("F1"),
				" | isEnabled=", Lib.Proto.GetBool(module, "isEnabled").ToString(),
				" | ", pseudoInfo,
				detail != null ? Lib.BuildString(" | ", detail) : string.Empty));
		}

		private static float GetProtoLoopTemperature(ProtoPartSnapshot part, string heatModuleId)
		{
			if (part == null)
				return 0f;

			ProtoPartModuleSnapshot fallback = null;
			foreach (ProtoPartModuleSnapshot protoModule in part.modules)
			{
				if (protoModule.moduleName != "ModuleSystemHeat")
					continue;

				if (fallback == null)
					fallback = protoModule;

				string protoModuleId = Lib.Proto.GetString(protoModule, "moduleID");
				if (string.IsNullOrEmpty(heatModuleId) || protoModuleId == heatModuleId)
					return Lib.Proto.GetFloat(protoModule, "currentLoopTemperature");
			}

			return fallback != null ? Lib.Proto.GetFloat(fallback, "currentLoopTemperature") : 0f;
		}
	}
}
