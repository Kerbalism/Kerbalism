using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// Kerbalism resource rates for native SystemHeat converter/harvester modules (resource IO blocked via Harmony).
	/// </summary>
	internal static class SHNativeConverterResourceSim
	{
		internal static string AddLoadedConverterRates(
			PartModule converter,
			string brokerTitle,
			List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			ModuleResourceConverter resourceConverter = converter as ModuleResourceConverter;
			if (resourceConverter == null || !SystemHeat.IsActivated(converter) || !SystemHeat.ModuleIsActive(converter))
				return brokerTitle;

			double scale = SystemHeat.GetLastTimeFactor(converter) * SystemHeat.GetHeatThrottle(converter);
			if (scale <= double.Epsilon)
				return brokerTitle;

			foreach (ResourceRatio input in resourceConverter.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio * scale));

			foreach (ResourceRatio output in resourceConverter.outputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(output.ResourceName, GetConverterEfficiency(converter) * output.Ratio * scale));

			return brokerTitle;
		}

		internal static string AddLoadedHarvesterRates(
			PartModule harvester,
			string brokerTitle,
			List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			ModuleResourceHarvester resourceHarvester = harvester as ModuleResourceHarvester;
			if (resourceHarvester == null || !SystemHeat.IsActivated(harvester) || !SystemHeat.ModuleIsActive(harvester))
				return brokerTitle;

			double scale = SystemHeat.GetLastTimeFactor(harvester) * SystemHeat.GetHeatThrottle(harvester);
			if (scale <= double.Epsilon)
				return brokerTitle;

			foreach (ResourceRatio input in resourceHarvester.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio * scale));

			double abundance = IntegrationUtils.SampleResourceAbundance(resourceHarvester.vessel, resourceHarvester);
			float harvestThreshold = IntegrationReflection.GetFloat(harvester, "HarvestThreshold");
			if (abundance > harvestThreshold)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(resourceHarvester.ResourceName, abundance * resourceHarvester.Efficiency * scale));

			return brokerTitle;
		}

		internal static void BackgroundUpdateConverter(
			Vessel v,
			ProtoPartModuleSnapshot converterSnapshot,
			PartModule converterPrefab,
			string brokerName,
			string brokerTitle,
			double elapsed_s)
		{
			ModuleResourceConverter resourceConverter = converterPrefab as ModuleResourceConverter;
			if (converterSnapshot == null || resourceConverter == null || !Lib.Proto.GetBool(converterSnapshot, "IsActivated"))
				return;

			VesselResources resources = KERBALISM.ResourceCache.Get(v);
			ResourceRecipe recipe = new ResourceRecipe(KERBALISM.ResourceBroker.GetOrCreate(
				brokerName,
				KERBALISM.ResourceBroker.BrokerCategory.Converter,
				brokerTitle));

			foreach (ResourceRatio input in resourceConverter.inputList)
				recipe.AddInput(input.ResourceName, input.Ratio * elapsed_s);

			foreach (ResourceRatio output in resourceConverter.outputList)
				recipe.AddOutput(output.ResourceName, GetConverterEfficiency(converterPrefab) * output.Ratio * elapsed_s, output.DumpExcess);

			resources.AddRecipe(recipe);
			Lib.Proto.Set(converterSnapshot, "lastUpdateTime", Planetarium.GetUniversalTime());
		}

		internal static void BackgroundUpdateHarvester(
			Vessel v,
			ProtoPartModuleSnapshot harvesterSnapshot,
			PartModule harvesterPrefab,
			string brokerName,
			string brokerTitle,
			double elapsed_s)
		{
			ModuleResourceHarvester resourceHarvester = harvesterPrefab as ModuleResourceHarvester;
			if (harvesterSnapshot == null || resourceHarvester == null || !Lib.Proto.GetBool(harvesterSnapshot, "IsActivated"))
				return;

			double abundance = IntegrationUtils.SampleResourceAbundance(v, resourceHarvester);
			float harvestThreshold = IntegrationReflection.GetFloat(harvesterPrefab, "HarvestThreshold");
			if (abundance <= harvestThreshold)
				return;

			VesselResources resources = KERBALISM.ResourceCache.Get(v);
			ResourceRecipe recipe = new ResourceRecipe(KERBALISM.ResourceBroker.GetOrCreate(
				brokerName,
				KERBALISM.ResourceBroker.BrokerCategory.Harvester,
				brokerTitle));

			foreach (ResourceRatio input in resourceHarvester.inputList)
				recipe.AddInput(input.ResourceName, input.Ratio * elapsed_s);

			recipe.AddOutput(resourceHarvester.ResourceName, abundance * resourceHarvester.Efficiency * elapsed_s, true);
			resources.AddRecipe(recipe);
			Lib.Proto.Set(harvesterSnapshot, "lastUpdateTime", Planetarium.GetUniversalTime());
		}

		internal static string AddPlannerConverterRates(
			PartModule converter,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			string brokerTitle)
		{
			ModuleResourceConverter resourceConverter = converter as ModuleResourceConverter;
			if (resourceConverter == null)
				return brokerTitle;

			foreach (ResourceRatio input in resourceConverter.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio));

			foreach (ResourceRatio output in resourceConverter.outputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(output.ResourceName, GetConverterEfficiency(converter) * output.Ratio));

			return brokerTitle;
		}

		internal static string AddPlannerHarvesterRates(
			PartModule harvester,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			string brokerTitle)
		{
			ModuleResourceHarvester resourceHarvester = harvester as ModuleResourceHarvester;
			if (resourceHarvester == null)
				return brokerTitle;

			foreach (ResourceRatio input in resourceHarvester.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio));

			resourceChangeRequest.Add(new KeyValuePair<string, double>(resourceHarvester.ResourceName, resourceHarvester.Efficiency * 0.1));
			return brokerTitle;
		}

		private static float GetConverterEfficiency(PartModule converter)
		{
			return IntegrationReflection.GetFloat(converter, "Efficiency", 1f);
		}
	}
}
