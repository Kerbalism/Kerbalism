using System.Collections;
using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	internal static class SystemHeat
	{
		private static readonly OptionalAssembly assembly = new OptionalAssembly("SystemHeat");
		private static readonly System.Type[] addFluxSignature = { typeof(string), typeof(float), typeof(float), typeof(bool) };
		private static readonly System.Type[] updateFluxSignature = { typeof(float) };

		public static bool Installed => assembly.Installed;

		public static bool IsModuleSystemHeat(PartModule module) => assembly.IsModule(module, "SystemHeat.ModuleSystemHeat");
		public static bool IsConverter(PartModule module) => assembly.IsModule(module, "SystemHeat.ModuleSystemHeatConverter");
		public static bool IsHarvester(PartModule module) => assembly.IsModule(module, "SystemHeat.ModuleSystemHeatHarvester");
		public static bool IsFissionReactor(PartModule module) => assembly.IsModule(module, "SystemHeat.ModuleSystemHeatFissionReactor");
		public static bool IsFissionEngine(PartModule module) => assembly.IsModule(module, "SystemHeat.ModuleSystemHeatFissionEngine");
		public static bool IsRadiator(PartModule module) => assembly.IsModule(module, "SystemHeat.ModuleSystemHeatRadiator");
		public static bool IsCryoTank(PartModule module) => assembly.IsModule(module, "SystemHeat.ModuleSystemHeatCryoTank");

		public static T Get<T>(object instance, string name, T fallback = default(T)) => assembly.Get(instance, name, fallback);
		public static void Set<T>(object instance, string name, T value) => assembly.Set(instance, name, value);
		public static object Call(object instance, string name, System.Type[] parameters = null, object[] args = null) => assembly.Call(instance, name, parameters, args);

		public static string GetModuleId(PartModule module) => Get(module, "moduleID", "");

		public static float GetHeatThrottle(PartModule module, float fallback = 1f)
		{
			object result = Call(module, "GetHeatThrottle");
			return result is float value ? value : fallback;
		}

		public static float GetLastTimeFactor(PartModule module, float fallback = 1f)
		{
			return Get(module, "lastTimeFactor", fallback);
		}

		public static bool IsActivated(PartModule module)
		{
			if (module == null)
				return false;

			if (Get<bool>(module, "IsActivated"))
				return true;

			return IntegrationReflection.GetBool(module, "IsActivated");
		}

		public static bool ModuleIsActive(PartModule module)
		{
			object result = Call(module, "ModuleIsActive");
			return result is bool active && active;
		}

		public static PartModule FindHeatModule(Part part, string moduleId)
		{
			if (part == null)
				return null;

			PartModule fallback = null;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (module == null || !IsHeatLoopModule(module))
					continue;

				if (fallback == null)
					fallback = module;

				string id = GetModuleId(module);
				if (string.IsNullOrEmpty(moduleId) || id == moduleId)
					return module;
			}

			return fallback;
		}

		private static bool IsHeatLoopModule(PartModule module)
		{
			return IsModuleSystemHeat(module) || module.moduleName == "ModuleSystemHeat";
		}

		public static PartModule FindConverter(Part part, string moduleId)
		{
			return FindModuleById(part, moduleId, IsConverter, "ModuleSystemHeatConverter");
		}

		public static PartModule FindHarvester(Part part, string moduleId)
		{
			return FindModuleById(part, moduleId, IsHarvester, "ModuleSystemHeatHarvester");
		}

		public static PartModule FindFissionReactor(Part part, string moduleId)
		{
			return FindModuleById(part, moduleId, IsFissionReactor, "ModuleSystemHeatFissionReactor");
		}

		public static PartModule FindFissionEngine(Part part, string moduleId)
		{
			return FindModuleById(part, moduleId, IsFissionEngine, "ModuleSystemHeatFissionEngine");
		}

		public static float EvaluateCurve(object curve, float input, float fallback = 0f)
		{
			return IntegrationReflection.EvaluateFloatCurve(curve, input, fallback);
		}

		public static object GetFloatCurve(PartModule module, string name)
		{
			return IntegrationReflection.GetField<object>(module, name);
		}

		public static float EvaluateFloatCurveField(PartModule module, string fieldName, float input, float fallback = 0f)
		{
			return EvaluateCurve(GetFloatCurve(module, fieldName), input, fallback);
		}

		public static void ReactorDeactivated(PartModule reactor)
		{
			if (reactor == null)
				return;

			Call(reactor, "ReactorDeactivated");
			Set(reactor, "Enabled", false);
		}

		public static float CurrentLoopTemperature(PartModule heatModule, float fallback = 4f)
		{
			return Get(heatModule, "currentLoopTemperature", fallback);
		}

		public static void AddFlux(PartModule heatModule, string id, float outletTemperature, float systemPower, bool active)
		{
			if (heatModule == null)
				return;

			if (Call(heatModule, "AddFlux", addFluxSignature, new object[] { id, outletTemperature, systemPower, active }) != null)
				return;

			IntegrationReflection.Call(heatModule, "AddFlux", new object[] { id, outletTemperature, systemPower, active }, addFluxSignature);
		}

		public static void UpdateFlux(PartModule heatModule, float timeFactor)
		{
			if (heatModule == null)
				return;

			Call(heatModule, "UpdateFlux", updateFluxSignature, new object[] { timeFactor });
		}

		public static IList GetResHandlerInputResources(PartModule module)
		{
			object resHandler = IntegrationReflection.GetField<object>(module, "resHandler");
			if (resHandler == null)
				return null;

			return IntegrationReflection.GetField<IList>(resHandler, "inputResources");
		}

		public static void InvokeRadiatorFixedUpdate(PartModule radiator)
		{
			if (radiator == null || !IsRadiator(radiator))
				return;

			IntegrationReflection.Call(radiator, "FixedUpdate");
		}

		private static PartModule FindModuleById(Part part, string moduleId, System.Func<PartModule, bool> typeCheck, string moduleName)
		{
			if (part == null)
				return null;

			PartModule first = null;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (module == null)
					continue;

				if (!typeCheck(module) && module.moduleName != moduleName)
					continue;

				if (first == null)
					first = module;

				if (string.IsNullOrEmpty(moduleId) || GetModuleId(module) == moduleId)
					return module;
			}

			return first;
		}

		public static string FormatPartInfoAdd(float systemPower, float outletTemperature, float shutdownTemperature)
		{
			return Localizer.Format("#KERBALISM_SystemHeat_partinfo",
				systemPower.ToString("F0"),
				outletTemperature.ToString("F0"),
				shutdownTemperature.ToString("F0"));
		}
	}
}
