namespace KERBALISM
{
	internal static class FarFutureTechnologies
	{
		private static readonly OptionalAssembly assembly = new OptionalAssembly("FarFutureTechnologies");

		public static bool Installed => assembly.Installed;

		public static bool IsFusionReactor(PartModule module) => assembly.IsModule(module, "FarFutureTechnologies.FusionReactor");
		public static bool IsFusionEngine(PartModule module) => assembly.IsModule(module, "FarFutureTechnologies.FusionEngine") || assembly.IsModule(module, "FarFutureTechnologies.ModuleFusionEngine");
		public static bool IsAntimatterTank(PartModule module) => assembly.IsModule(module, "FarFutureTechnologies.ModuleAntimatterTank");

		public static T Get<T>(object instance, string name, T fallback = default(T)) => assembly.Get(instance, name, fallback);
		public static void Set<T>(object instance, string name, T value) => assembly.Set(instance, name, value);
		public static object Call(object instance, string name, System.Type[] parameters = null, object[] args = null) => assembly.Call(instance, name, parameters, args);

		public static PartModule FindFusionReactor(Part part, string moduleId)
		{
			return FindModuleById(part, moduleId, IsFusionReactor, "FusionReactor", "ModuleID");
		}

		public static PartModule FindFusionEngine(Part part, string moduleId)
		{
			PartModule engine = FindModuleById(part, moduleId, IsFusionEngine, "ModuleFusionEngine", "ModuleID");
			return engine ?? FindModuleById(part, moduleId, IsFusionEngine, "FusionEngine", "ModuleID");
		}

		public static void ReactorDeactivated(PartModule reactor)
		{
			if (reactor == null)
				return;

			Call(reactor, "ReactorDeactivated");
			Set(reactor, "Enabled", false);
		}

		public static void EnableReactor(PartModule reactor) => Call(reactor, "EnableReactor");

		public static void DisableReactor(PartModule reactor) => Call(reactor, "DisableReactor");

		public static void SetChargeStateUI(PartModule reactor, int chargeState)
		{
			System.Type enumType = assembly.Type("FarFutureTechnologies.ChargeState");
			if (reactor == null || enumType == null || !enumType.IsEnum)
				return;

			object state = System.Enum.ToObject(enumType, chargeState);
			Call(reactor, "SetChargeStateUI", new[] { enumType }, new object[] { state });
		}

		public static bool HasKerbalismFusionUpdater(PartModule module)
		{
			if (module?.part == null)
				return false;

			if (module.part.FindModuleImplementing<FFTFusionReactorKerbalismUpdater>() != null)
				return true;

			return IsFusionEngine(module) && module.part.FindModuleImplementing<FFTFusionEngineKerbalismUpdater>() != null;
		}

		public static void SetPoweredState(PartModule tank, bool powered)
		{
			if (tank == null)
				return;

			Call(tank, "SetPoweredState", new[] { typeof(bool) }, new object[] { powered });
		}

		public static bool HasKerbalismAntimatterUpdater(PartModule module)
		{
			return module?.part != null && module.part.FindModuleImplementing<FFTAntimatterTankKerbalismUpdater>() != null;
		}

		private static PartModule FindModuleById(Part part, string moduleId, System.Func<PartModule, bool> typeCheck, string moduleName, string idField)
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

				string id = Get(module, idField, "");
				if (string.IsNullOrEmpty(moduleId) || id == moduleId)
					return module;
			}

			return first;
		}
	}
}
