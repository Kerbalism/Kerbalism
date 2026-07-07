using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	/// <summary>
	/// Shared fission reactor resource logic for Kerbalism background, loaded ResourceUpdate, and validation.
	/// </summary>
	internal static class FissionReactorResourceSim
	{
		internal static void UpdateAutoThrottle(PartModule reactor, float timeStep)
		{
			if (reactor == null || !SystemHeat.Get(reactor, "Enabled", false) || SystemHeat.Get(reactor, "ManualControl", false))
				return;

			object result = SystemHeat.Call(reactor, "CalculateGoalThrottle", new[] { typeof(float) }, new object[] { timeStep });
			if (result is float goal)
				SystemHeat.Set(reactor, "CurrentReactorThrottle", goal);
		}

		internal static string AddLoadedRates(
			SystemHeatFissionReactorKerbalismUpdater updater,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			return AddLoadedRates(
				updater.ReactorModule,
				updater.Inputs,
				updater.Outputs,
				SystemHeatFissionReactorKerbalismUpdater.brokerTitle,
				resourceChangeRequest);
		}

		internal static string AddLoadedRates(
			SystemHeatFissionEngineKerbalismUpdater updater,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			return AddLoadedRates(
				updater.EngineModule,
				updater.Inputs,
				updater.Outputs,
				SystemHeatFissionEngineKerbalismUpdater.brokerTitle,
				resourceChangeRequest,
				updater.GeneratesElectricity);
		}

		private static string AddLoadedRates(
			PartModule reactor,
			List<ResourceRatio> inputs,
			List<ResourceRatio> outputs,
			string brokerTitle,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			bool generatesElectricity = true)
		{
			if (reactor == null || !SystemHeat.Get(reactor, "Enabled", false) || !generatesElectricity || !SystemHeat.Get(reactor, "GeneratesElectricity", true))
			{
				SyncLoadedReactorStatus(reactor, false, 0f, 0f, inputs);
				return brokerTitle;
			}

			float fuelThrottle = SystemHeat.Get(reactor, "CurrentReactorThrottle", 0f) / 100f;
			if (fuelThrottle <= 0f)
			{
				SyncLoadedReactorStatus(reactor, false, 0f, 0f, inputs);
				return brokerTitle;
			}

			float currentThrottle = SystemHeat.Get(reactor, "CurrentThrottle", 0f);
			float ecRate = SystemHeat.EvaluateFloatCurveField(reactor, "ElectricalGeneration", currentThrottle);
			if (ecRate > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", ecRate));
			SyncLoadedReactorStatus(reactor, true, fuelThrottle, ecRate, inputs);

			if (inputs != null)
			{
				foreach (ResourceRatio input in inputs)
					resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -fuelThrottle * input.Ratio));
			}

			if (outputs != null)
			{
				foreach (ResourceRatio output in outputs)
					resourceChangeRequest.Add(new KeyValuePair<string, double>(output.ResourceName, fuelThrottle * output.Ratio));
			}

			return brokerTitle;
		}

		internal static void ValidateLoadedReactor(PartModule reactor, Vessel v, List<ResourceRatio> inputs, List<ResourceRatio> outputs, string brokerTitle, string partTitle)
		{
			if (reactor == null || !SystemHeat.Get(reactor, "Enabled", false) || v == null)
				return;

			float fuelThrottle = SystemHeat.Get(reactor, "CurrentReactorThrottle", 0f) / 100f;
			if (fuelThrottle <= 0f)
				return;

			VesselResources resources = KERBALISM.ResourceCache.Get(v);
			bool needToStop = false;

			if (inputs != null)
			{
				foreach (ResourceRatio input in inputs)
				{
					if (resources.GetResource(v, input.ResourceName).Amount < double.Epsilon)
						needToStop = true;
				}
			}

			if (outputs != null)
			{
				foreach (ResourceRatio output in outputs)
				{
					if (1 - resources.GetResource(v, output.ResourceName).Level < double.Epsilon)
					{
						needToStop = true;
						Message.Post(
							Severity.warning,
							Localizer.Format(
								"#KERBALISM_ReactorOutputFull",
								output.ResourceName,
								v.GetDisplayName(),
								partTitle)
						);
					}
				}
			}

			if (needToStop)
				SystemHeat.ReactorDeactivated(reactor);
		}

		internal static float GetWasteHeatKw(PartModule reactor)
		{
			if (reactor == null || !SystemHeat.Get(reactor, "Enabled", false))
				return 0f;

			float currentThrottle = SystemHeat.Get(reactor, "CurrentThrottle", 0f);
			float heatGen = SystemHeat.EvaluateFloatCurveField(reactor, "HeatGeneration", currentThrottle);
			float elecGen = SystemHeat.EvaluateFloatCurveField(reactor, "ElectricalGeneration", currentThrottle);
			return System.Math.Max(0f, heatGen - elecGen);
		}

		private static void SyncLoadedReactorStatus(PartModule reactor, bool fuelCheckPassed, float fuelThrottle, float currentElectricalGeneration, List<ResourceRatio> inputs)
		{
			if (reactor == null)
				return;

			SystemHeat.Set(reactor, "CurrentElectricalGeneration", currentElectricalGeneration);
			float currentThrottle = SystemHeat.Get(reactor, "CurrentThrottle", 0f);
			float coreIntegrity = SystemHeat.Get(reactor, "CoreIntegrity", 100f);
			float maxElectrical = SystemHeat.Get(reactor, "ManualControl", false)
				? currentElectricalGeneration
				: SystemHeat.EvaluateFloatCurveField(reactor, "ElectricalGeneration", 100f) * coreIntegrity / 100f;
			SystemHeat.Set(reactor, "MaxElectricalGeneration", maxElectrical);
			SyncProtoReactorStatus(reactor, currentThrottle, currentElectricalGeneration, maxElectrical);

			IntegrationReflection.SetField(reactor, "fuelCheckPassed", fuelCheckPassed);
			IntegrationReflection.SetField(reactor, "burnRate", fuelCheckPassed ? GetFuelBurnRate(reactor, fuelThrottle, inputs) : 0d);
		}

		private static void SyncProtoReactorStatus(PartModule reactor, float currentThrottle, float currentElectricalGeneration, float maxElectricalGeneration)
		{
			ProtoPartSnapshot protoPart = reactor.part?.protoPartSnapshot;
			if (protoPart == null)
				return;

			string moduleId = SystemHeat.GetModuleId(reactor);
			ProtoPartModuleSnapshot fallback = null;
			foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
			{
				if (protoModule.moduleName != reactor.moduleName)
					continue;

				if (fallback == null)
					fallback = protoModule;

				string protoModuleId = Lib.Proto.GetString(protoModule, "moduleID");
				if (!string.IsNullOrEmpty(moduleId) && protoModuleId != moduleId)
					continue;

				SyncProtoReactorStatus(protoModule, reactor, currentThrottle, currentElectricalGeneration, maxElectricalGeneration);
				return;
			}

			if (fallback != null)
				SyncProtoReactorStatus(fallback, reactor, currentThrottle, currentElectricalGeneration, maxElectricalGeneration);
		}

		private static void SyncProtoReactorStatus(ProtoPartModuleSnapshot protoModule, PartModule reactor, float currentThrottle, float currentElectricalGeneration, float maxElectricalGeneration)
		{
			Lib.Proto.Set(protoModule, "Enabled", SystemHeat.Get(reactor, "Enabled", false));
			Lib.Proto.Set(protoModule, "CurrentReactorThrottle", SystemHeat.Get(reactor, "CurrentReactorThrottle", 0f));
			Lib.Proto.Set(protoModule, "CurrentThrottle", currentThrottle);
			Lib.Proto.Set(protoModule, "CurrentElectricalGeneration", currentElectricalGeneration);
			Lib.Proto.Set(protoModule, "MaxElectricalGeneration", maxElectricalGeneration);
			Lib.Proto.Set(protoModule, "CoreIntegrity", SystemHeat.Get(reactor, "CoreIntegrity", 100f));
		}

		private static double GetFuelBurnRate(PartModule reactor, float fuelThrottle, List<ResourceRatio> inputs)
		{
			if (inputs == null)
				return 0d;

			string fuelName = SystemHeat.Get(reactor, "FuelName", "");
			foreach (ResourceRatio input in inputs)
			{
				if (input.ResourceName == fuelName)
					return fuelThrottle * input.Ratio;
			}

			return 0d;
		}
	}
}
