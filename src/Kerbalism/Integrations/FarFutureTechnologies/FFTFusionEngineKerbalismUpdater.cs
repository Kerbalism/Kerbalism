using KSP.Localization;
using System.Collections.Generic;

namespace KERBALISM
{
	class FFTFusionEngineKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "FFTFusionEngine";
		public static string brokerTitle = Localizer.Format("#KERBALISM_Brokers_FusionEngine");

		[KSPField(isPersistant = false)]
		public bool FirstLoad = true;

		[KSPField(isPersistant = true)]
		public string engineModuleID = "";

		[KSPField(isPersistant = true)]
		public int lastReactorModeIndex = 0;
		[KSPField(isPersistant = true)]
		public float MaxECGeneration = 0f;
		[KSPField(isPersistant = true)]
		public float MinThrottle = 0.1f;

		protected static string engineModuleName = "ModuleFusionEngine";
		protected PartModule engineModule;

		internal PartModule EngineModule => engineModule;

		protected List<FusionModeData> modes;
		protected bool modesListParsed = false;
		private bool lastPlannerCharging;

		internal void EnsureModesParsed()
		{
			if (!modesListParsed)
				ParseModesList(part);
		}

		public virtual void Start()
		{
			if (Lib.IsFlight() || Lib.IsEditor())
			{
				if (engineModule == null)
					engineModule = FindEngineModule(part, engineModuleID);

				if (FirstLoad)
				{
					if (engineModule != null)
					{
						MinThrottle = FarFutureTechnologies.Get(engineModule, "MinimumReactorPower", 0.1f);
						ParseModesList(part);
						if (modes != null && lastReactorModeIndex >= 0 && lastReactorModeIndex < modes.Count)
							MaxECGeneration = modes[lastReactorModeIndex].powerGeneration;
						if (FusionReactorResourceSim.IsFusionUiReady(engineModule))
						{
							FusionReactorResourceSim.SyncLoadedChargeUI(engineModule, false);
							FirstLoad = false;
						}
					}
				}
			}
		}

		protected void ParseModesList(Part part)
		{
			if (modesListParsed)
				return;

			ConfigNode node = IntegrationUtils.GetModuleConfigNode(part, engineModuleName);
			if (node != null)
			{
				ConfigNode[] varNodes = node.GetNodes("FUSIONMODE");
				modes = new List<FusionModeData>();
				for (int i = 0; i < varNodes.Length; i++)
					modes.Add(new FusionModeData(varNodes[i]));
			}
			modesListParsed = true;
		}

		public virtual void FixedUpdate()
		{
			if (engineModule != null)
			{
				if (FirstLoad)
				{
					MinThrottle = FarFutureTechnologies.Get(engineModule, "MinimumReactorPower", 0.1f);
					ParseModesList(part);
					if (modes != null && lastReactorModeIndex >= 0 && lastReactorModeIndex < modes.Count)
						MaxECGeneration = modes[lastReactorModeIndex].powerGeneration;
					if (FusionReactorResourceSim.IsFusionUiReady(engineModule))
					{
						FusionReactorResourceSim.SyncLoadedChargeUI(engineModule, false);
						FirstLoad = false;
					}
				}

				int currentModeIndex = FarFutureTechnologies.Get(engineModule, "currentModeIndex", 0);
				if (lastReactorModeIndex != currentModeIndex)
				{
					lastReactorModeIndex = currentModeIndex;
					if (Lib.IsEditor())
						Lib.RefreshPlanner();
					EnsureModesParsed();
					if (modes != null && lastReactorModeIndex >= 0 && lastReactorModeIndex < modes.Count)
						MaxECGeneration = modes[lastReactorModeIndex].powerGeneration;
				}

				if (Lib.IsFlight() && FarFutureTechnologies.Get(engineModule, "Enabled", false))
				{
					FusionReactorResourceSim.UpdateLoadedThrottle(engineModule);
					FusionReactorResourceSim.ValidateLoadedReactor(engineModule, vessel);
				}
				else if (Lib.IsFlight())
				{
					bool hasPower = FarFutureTechnologies.Get(engineModule, "Charging", false)
						&& !FarFutureTechnologies.Get(engineModule, "Charged", false)
						&& FusionReactorResourceSim.HasChargeOperatingPower(engineModule, vessel);
					FusionReactorResourceSim.SyncLoadedChargeUI(engineModule, hasPower);
				}

				bool plannerCharging = !FarFutureTechnologies.Get(engineModule, "Enabled", false)
					&& FarFutureTechnologies.Get(engineModule, "Charging", false)
					&& !FarFutureTechnologies.Get(engineModule, "Charged", false);
				if (plannerCharging != lastPlannerCharging)
				{
					lastPlannerCharging = plannerCharging;
					Lib.RefreshPlanner();
				}
			}
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (engineModule == null)
				engineModule = FindEngineModule(part, engineModuleID);
			if (FusionReactorResourceSim.UpdateLoadedCharge(engineModule, vessel, brokerName, brokerTitle))
				return brokerTitle;
			return FusionReactorResourceSim.AddLoadedRates(engineModule, resourceChangeRequest, brokerTitle);
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			if (engineModule == null)
				engineModule = FindEngineModule(part, engineModuleID);
			if (engineModule != null)
			{
				EnsureModesParsed();
				return FusionReactorResourceSim.AddPlannerRates(
					engineModule,
					resourceChangeRequest,
					brokerTitle,
					MaxECGeneration,
					lastReactorModeIndex,
					modes);
			}
			return "ERR: no engine";
		}

		public static string BackgroundUpdate(Vessel v, ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot, PartModule proto_part_module, Part proto_part, Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s)
		{
			ProtoPartModuleSnapshot reactor = IntegrationUtils.FindPartModuleSnapshot(part_snapshot, engineModuleName);
			if (reactor != null)
			{
				FusionReactorResourceSim.BackgroundCharge(v, reactor, proto_part, resourceChangeRequest, elapsed_s);
				SystemHeatBackgroundThermal.TryRun(v, elapsed_s);

				if (Lib.Proto.GetBool(reactor, "Enabled"))
				{
					float maxECGeneration = Lib.Proto.GetFloat(module_snapshot, "MaxECGeneration");
					int modeIndex = Lib.Proto.GetInt(module_snapshot, "lastReactorModeIndex");
					var updater = proto_part_module as FFTFusionEngineKerbalismUpdater;
					if (!updater.modesListParsed)
						updater.ParseModesList(proto_part);

					FusionReactorResourceSim.AddBackgroundRates(
						v,
						part_snapshot,
						reactor,
						proto_part,
						updater.modes,
						modeIndex,
						maxECGeneration,
						brokerName,
						brokerTitle,
						elapsed_s);
				}
				return brokerTitle;
			}
			return "ERR: no engine";
		}

		public PartModule FindEngineModule(Part part, string moduleName)
		{
			PartModule engine = FarFutureTechnologies.FindFusionEngine(part, moduleName);
			if (engine == null)
				IntegrationUtils.LogError($"[{part}] No ModuleFusionEngine was found.");
			else if (!string.IsNullOrEmpty(moduleName) && FarFutureTechnologies.Get(engine, "ModuleID", "") != moduleName)
				IntegrationUtils.LogError($"[{part}] No ModuleFusionEngine named {moduleName} was found, using first instance.");

			engineModule = engine;
			return engine;
		}
	}
}
