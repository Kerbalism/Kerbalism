using KSP.Localization;
using System.Collections.Generic;

namespace KERBALISM
{
	class FFTFusionReactorKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "FFTFusionReactor";
		public static string brokerTitle = Localizer.Format("#KERBALISM_Brokers_FusionReactor");

		[KSPField(isPersistant = false)]
		public bool FirstLoad = true;

		[KSPField(isPersistant = true)]
		public string reactorModuleID = "";

		[KSPField(isPersistant = true)]
		public int lastReactorModeIndex = 0;
		[KSPField(isPersistant = true)]
		public float MaxECGeneration = 0f;
		[KSPField(isPersistant = true)]
		public float MinThrottle = 0.1f;

		protected static string reactorModuleName = "FusionReactor";
		protected PartModule reactorModule;

		internal PartModule ReactorModule => reactorModule;

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
				if (reactorModule == null)
					reactorModule = FindReactorModule(part, reactorModuleID);

				if (FirstLoad)
				{
					if (reactorModule != null)
					{
						MinThrottle = FarFutureTechnologies.Get(reactorModule, "MinimumReactorPower", 0.1f);
						ParseModesList(part);
						if (modes != null && lastReactorModeIndex >= 0 && lastReactorModeIndex < modes.Count)
							MaxECGeneration = modes[lastReactorModeIndex].powerGeneration;
						if (FusionReactorResourceSim.IsFusionUiReady(reactorModule))
						{
							FusionReactorResourceSim.SyncLoadedChargeUI(reactorModule, false);
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

			ConfigNode node = IntegrationUtils.GetModuleConfigNode(part, reactorModuleName);
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
			if (reactorModule != null)
			{
				if (FirstLoad)
				{
					MinThrottle = FarFutureTechnologies.Get(reactorModule, "MinimumReactorPower", 0.1f);
					ParseModesList(part);
					if (modes != null && lastReactorModeIndex >= 0 && lastReactorModeIndex < modes.Count)
						MaxECGeneration = modes[lastReactorModeIndex].powerGeneration;
					if (FusionReactorResourceSim.IsFusionUiReady(reactorModule))
					{
						FusionReactorResourceSim.SyncLoadedChargeUI(reactorModule, false);
						FirstLoad = false;
					}
				}

				int currentModeIndex = FarFutureTechnologies.Get(reactorModule, "currentModeIndex", 0);
				if (lastReactorModeIndex != currentModeIndex)
				{
					lastReactorModeIndex = currentModeIndex;
					if (Lib.IsEditor())
						Lib.RefreshPlanner();
					EnsureModesParsed();
					if (modes != null && lastReactorModeIndex >= 0 && lastReactorModeIndex < modes.Count)
						MaxECGeneration = modes[lastReactorModeIndex].powerGeneration;
				}

				if (Lib.IsFlight() && FarFutureTechnologies.Get(reactorModule, "Enabled", false))
				{
					FusionReactorResourceSim.UpdateLoadedThrottle(reactorModule);
					FusionReactorResourceSim.ValidateLoadedReactor(reactorModule, vessel);
				}
				else if (Lib.IsFlight())
				{
					bool hasPower = FarFutureTechnologies.Get(reactorModule, "Charging", false)
						&& !FarFutureTechnologies.Get(reactorModule, "Charged", false)
						&& FusionReactorResourceSim.HasChargeOperatingPower(reactorModule, vessel);
					FusionReactorResourceSim.SyncLoadedChargeUI(reactorModule, hasPower);
				}

				bool plannerCharging = !FarFutureTechnologies.Get(reactorModule, "Enabled", false)
					&& FarFutureTechnologies.Get(reactorModule, "Charging", false)
					&& !FarFutureTechnologies.Get(reactorModule, "Charged", false);
				if (plannerCharging != lastPlannerCharging)
				{
					lastPlannerCharging = plannerCharging;
					Lib.RefreshPlanner();
				}
			}
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (reactorModule == null)
				reactorModule = FindReactorModule(part, reactorModuleID);
			if (FusionReactorResourceSim.UpdateLoadedCharge(reactorModule, vessel, brokerName, brokerTitle))
				return brokerTitle;
			return FusionReactorResourceSim.AddLoadedRates(reactorModule, resourceChangeRequest, brokerTitle);
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			if (reactorModule == null)
				reactorModule = FindReactorModule(part, reactorModuleID);
			if (reactorModule != null)
			{
				EnsureModesParsed();
				return FusionReactorResourceSim.AddPlannerRates(
					reactorModule,
					resourceChangeRequest,
					brokerTitle,
					MaxECGeneration,
					lastReactorModeIndex,
					modes);
			}
			return "ERR: no reactor";
		}

		public static string BackgroundUpdate(Vessel v, ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot, PartModule proto_part_module, Part proto_part, Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s)
		{
			ProtoPartModuleSnapshot reactor = IntegrationUtils.FindPartModuleSnapshot(part_snapshot, reactorModuleName);
			if (reactor != null)
			{
				FusionReactorResourceSim.BackgroundCharge(v, reactor, proto_part, resourceChangeRequest, elapsed_s);
				SystemHeatBackgroundThermal.TryRun(v, elapsed_s);

				if (Lib.Proto.GetBool(reactor, "Enabled"))
				{
					float maxECGeneration = Lib.Proto.GetFloat(module_snapshot, "MaxECGeneration");
					int modeIndex = Lib.Proto.GetInt(module_snapshot, "lastReactorModeIndex");
					var updater = proto_part_module as FFTFusionReactorKerbalismUpdater;
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
			return "ERR: no reactor";
		}

		public PartModule FindReactorModule(Part part, string moduleName)
		{
			PartModule reactor = FarFutureTechnologies.FindFusionReactor(part, moduleName);
			if (reactor == null)
				IntegrationUtils.LogError($"[{part}] No FusionReactor was found.");
			else if (!string.IsNullOrEmpty(moduleName) && FarFutureTechnologies.Get(reactor, "ModuleID", "") != moduleName)
				IntegrationUtils.LogError($"[{part}] No FusionReactor named {moduleName} was found, using first instance.");

			reactorModule = reactor;
			return reactor;
		}
	}
}
