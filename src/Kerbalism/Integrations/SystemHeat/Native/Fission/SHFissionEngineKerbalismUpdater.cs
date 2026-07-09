using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	public class SystemHeatFissionEngineKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "SHFissionEngine";
		public static string brokerTitle = Localizer.Format("#KERBALISM_Brokers_FissionEngine");

		[KSPField(isPersistant = true)]
		public bool FirstLoad = true;

		[KSPField(isPersistant = true)]
		public string engineModuleID;

		[KSPField(isPersistant = true)]
		public float MaxECGeneration = 0f;
		[KSPField(isPersistant = true)]
		public float MinThrottle = 0.25f;
		[KSPField(isPersistant = true)]
		public float MaxThrottle = 1.0f;
		[KSPField(isPersistant = true)]
		public bool GeneratesElectricity = true;

		protected static string engineModuleName = "ModuleSystemHeatFissionEngine";
		protected PartModule engineModule;

		protected bool resourcesListParsed = false;
		protected List<ResourceRatio> inputs;
		protected List<ResourceRatio> outputs;

		internal PartModule EngineModule => engineModule;
		internal List<ResourceRatio> Inputs => inputs;
		internal List<ResourceRatio> Outputs => outputs;

		internal void EnsureResourcesParsed()
		{
			if (!resourcesListParsed)
				ParseResourcesList(part);
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
						MaxECGeneration = SystemHeat.EvaluateFloatCurveField(engineModule, "ElectricalGeneration", 100f);
						MinThrottle = SystemHeat.Get(engineModule, "MinimumThrottle", 25f) / 100f;
						GeneratesElectricity = SystemHeat.Get(engineModule, "GeneratesElectricity", true);
					}
					EnsureResourcesParsed();
					FirstLoad = false;
				}
			}
		}

		public virtual void FixedUpdate()
		{
			if (engineModule != null && Lib.IsFlight())
			{
				MaxThrottle = SystemHeat.Get(engineModule, "CoreIntegrity", 100f) / 100f;
				if (MinThrottle > MaxThrottle)
					MinThrottle = MaxThrottle;

				FissionReactorResourceSim.UpdateAutoThrottle(engineModule, TimeWarp.fixedDeltaTime);
				EnsureResourcesParsed();
				FissionReactorResourceSim.ValidateLoadedReactor(
					engineModule,
					vessel,
					inputs,
					outputs,
					brokerTitle,
					part.partInfo.title);
			}
		}

		protected void ParseResourcesList(Part part)
		{
			if (resourcesListParsed)
				return;

			ConfigNode node = IntegrationUtils.GetModuleConfigNode(part, engineModuleName);
			if (node != null)
			{
				inputs = new List<ResourceRatio>();
				foreach (ConfigNode inNode in node.GetNodes("INPUT_RESOURCE"))
				{
					ResourceRatio p = new ResourceRatio();
					p.Load(inNode);
					inputs.Add(p);
				}

				outputs = new List<ResourceRatio>();
				foreach (ConfigNode outNode in node.GetNodes("OUTPUT_RESOURCE"))
				{
					ResourceRatio p = new ResourceRatio();
					p.Load(outNode);
					outputs.Add(p);
				}
			}
			resourcesListParsed = true;
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			if (engineModule != null)
			{
				float curECGeneration = SystemHeat.EvaluateFloatCurveField(
					engineModule,
					"ElectricalGeneration",
					SystemHeat.Get(engineModule, "CurrentReactorThrottle", 0f));
				if (curECGeneration > 0)
					resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", curECGeneration));

				float fuelThrottle = SystemHeat.Get(engineModule, "CurrentReactorThrottle", 0f) / 100f;
				if (fuelThrottle > 0)
				{
					EnsureResourcesParsed();
					foreach (ResourceRatio ratio in inputs)
						resourceChangeRequest.Add(new KeyValuePair<string, double>(ratio.ResourceName, -fuelThrottle * ratio.Ratio));
					foreach (ResourceRatio ratio in outputs)
						resourceChangeRequest.Add(new KeyValuePair<string, double>(ratio.ResourceName, fuelThrottle * ratio.Ratio));
				}
				return brokerTitle;
			}
			return "ERR: no engine";
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (engineModule == null)
				engineModule = FindEngineModule(part, engineModuleID);
			return FissionReactorResourceSim.AddLoadedRates(this, availableResources, resourceChangeRequest);
		}

		public static string BackgroundUpdate(Vessel v, ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot, PartModule proto_part_module, Part proto_part, Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s)
		{
			ProtoPartModuleSnapshot reactor = IntegrationUtils.FindPartModuleSnapshot(part_snapshot, engineModuleName);
			if (reactor != null)
			{
				if (Lib.Proto.GetBool(reactor, "Enabled") && Lib.Proto.GetBool(module_snapshot, "GeneratesElectricity"))
				{
					float fuelThrottle = Lib.Proto.GetFloat(reactor, "CurrentReactorThrottle") / 100f;
					float currentThrottle = Lib.Proto.GetFloat(reactor, "CurrentThrottle", Lib.Proto.GetFloat(reactor, "CurrentReactorThrottle"));
					float maxECGeneration = Lib.Proto.GetFloat(module_snapshot, "MaxECGeneration");
					bool needToStopReactor = false;
					PartModule enginePrefab = SystemHeat.FindFissionEngine(proto_part, Lib.Proto.GetString(module_snapshot, "engineModuleID"));
					float ecGeneration = enginePrefab != null
						? SystemHeat.EvaluateFloatCurveField(enginePrefab, "ElectricalGeneration", currentThrottle, maxECGeneration * fuelThrottle)
						: maxECGeneration * fuelThrottle;
					if (fuelThrottle > 0 || ecGeneration > 0)
					{
						VesselResources resources = KERBALISM.ResourceCache.Get(v);
						var updater = proto_part_module as SystemHeatFissionEngineKerbalismUpdater;
						if (!updater.resourcesListParsed)
							updater.ParseResourcesList(proto_part);

						ResourceRecipe recipe = new ResourceRecipe(KERBALISM.ResourceBroker.GetOrCreate(
							brokerName,
							KERBALISM.ResourceBroker.BrokerCategory.Converter,
							brokerTitle));
						foreach (ResourceRatio ir in updater.inputs)
						{
							recipe.AddInput(ir.ResourceName, ir.Ratio * fuelThrottle * elapsed_s);
							if (resources.GetResource(v, ir.ResourceName).Amount < double.Epsilon)
								needToStopReactor = true;
						}
						foreach (ResourceRatio or in updater.outputs)
						{
							recipe.AddOutput(or.ResourceName, or.Ratio * fuelThrottle * elapsed_s, dump: false);
							if (1 - resources.GetResource(v, or.ResourceName).Level < double.Epsilon)
							{
								needToStopReactor = true;
								Message.Post(
									Severity.warning,
									Localizer.Format(
										"#KERBALISM_ReactorOutputFull",
										or.ResourceName,
										v.GetDisplayName(),
										part_snapshot.partName)
								);
							}
						}
						if (ecGeneration > 0)
							recipe.AddOutput("ElectricCharge", ecGeneration * elapsed_s, dump: true);
						resources.AddRecipe(recipe);
					}

					if (needToStopReactor)
						Lib.Proto.Set(reactor, "Enabled", false);
				}
				Lib.Proto.Set(reactor, "LastUpdateTime", Planetarium.GetUniversalTime());
			}

			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return brokerTitle;
		}

		public PartModule FindEngineModule(Part part, string moduleName)
		{
			PartModule engine = SystemHeat.FindFissionEngine(part, moduleName);
			if (engine == null)
				IntegrationUtils.LogError($"[{part}] No ModuleSystemHeatFissionEngine was found.");
			else if (!string.IsNullOrEmpty(moduleName) && SystemHeat.GetModuleId(engine) != moduleName)
				IntegrationUtils.LogError($"[{part}] No ModuleSystemHeatFissionEngine named {moduleName} was found, using first instance");

			engineModule = engine;
			return engine;
		}
	}
}
