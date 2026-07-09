using System;
using System.Collections;
using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	internal static class SystemHeatCryoResourceSim
	{
		internal const string BrokerName = "SystemHeatCryoTank";
		internal static string BrokerTitle => Localizer.Format("#KERBALISM_Brokers_Cryotank");

		internal static PartModule FindCryoModule(Part part, string moduleId)
		{
			if (part == null)
				return null;

			PartModule first = null;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (module == null || module.moduleName != "ModuleSystemHeatCryoTank" && !SystemHeat.IsCryoTank(module))
					continue;

				if (first == null)
					first = module;

				if (string.IsNullOrEmpty(moduleId) || SystemHeat.GetModuleId(module) == moduleId)
					return module;
			}

			return first;
		}

		internal static void AddPlannerHeatRates(PartModule cryo, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (cryo == null || !SystemHeat.Get(cryo, "CoolingEnabled", false) || !SystemHeat.Get(cryo, "CoolingAllowed", false))
				return;

			double heatKw = EstimateCoolingHeatKw(cryo);
			if (heatKw > 0.0)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("SystemHeat", heatKw));
		}

		internal static double EstimateCoolingHeatKw(PartModule cryo)
		{
			if (cryo == null)
				return 0.0;

			IEnumerable fuels = SystemHeatCryoTankAccess.GetFuels(cryo);
			if (fuels == null)
				return 0.0;

			double fuelAmount = 0.0;
			double heatCost = 0.0;
			foreach (object fuel in fuels)
			{
				string fuelName = SystemHeatCryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				PartResource resource = cryo.part.Resources.Get(fuelName);
				if (resource == null || resource.amount <= double.Epsilon)
					continue;

				fuelAmount += resource.amount;
				float entryCost = SystemHeatCryoTankAccess.GetCoolingHeatCost(fuel);
				if (entryCost > 0f)
					heatCost = Math.Max(heatCost, entryCost);
			}

			if (heatCost <= 0f)
				heatCost = SystemHeat.Get(cryo, "CoolingHeatCost", 0f);

			return heatCost * fuelAmount * 0.001;
		}

		internal static string UpdateLoaded(PartModule cryo)
		{
			if (cryo == null)
				return BrokerTitle;

			return BrokerTitle;
		}

		internal static string BackgroundUpdate(
			Vessel v,
			ProtoPartSnapshot part,
			ProtoPartModuleSnapshot cryoSnapshot,
			PartModule cryoPrefab,
			double elapsed_s)
		{
			if (cryoPrefab == null || part == null || elapsed_s <= 0.0)
				return BrokerTitle;

			bool coolingEnabled = Lib.Proto.GetBool(cryoSnapshot, "CoolingEnabled");
			bool coolingAllowed = Lib.Proto.GetBool(cryoSnapshot, "CoolingAllowed");
			IEnumerable fuels = SystemHeatCryoTankAccess.GetFuels(cryoPrefab);
			if (fuels == null)
				return BrokerTitle;

			double fluxScale = 1.0;
			double fuelAmount = 0.0;

			foreach (object fuel in fuels)
			{
				string fuelName = SystemHeatCryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				ProtoPartResourceSnapshot protoFuel = CryoUtils.FindPartResource(part, fuelName);
				if (protoFuel == null || protoFuel.amount <= double.Epsilon)
					continue;

				fuelAmount += protoFuel.amount;
			}

			if (fuelAmount <= double.Epsilon)
				return BrokerTitle;

			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			float loopTemp = GetLoopTemperature(part, SystemHeat.Get(cryoPrefab, "systemHeatModuleID", ""), v);

			bool allFuelsBoiloff = !coolingAllowed || !coolingEnabled;
			bool boiloffOccuring = false;

			foreach (object fuel in fuels)
			{
				string fuelName = SystemHeatCryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				ProtoPartResourceSnapshot protoFuel = CryoUtils.FindPartResource(part, fuelName);
				if (protoFuel == null || protoFuel.amount <= double.Epsilon)
					continue;

				float cryoTemp = SystemHeatCryoTankAccess.GetCryoTemperature(fuel);
				bool fuelShouldBoiloff = allFuelsBoiloff || (cryoTemp > 0f && loopTemp > cryoTemp);
				if (!fuelShouldBoiloff)
					continue;

				float boiloffRate = SystemHeatCryoTankAccess.GetBoiloffRate(fuel);
				double boiled = CryoUtils.ApplyBoiloffAmountSystemHeat(protoFuel.amount, boiloffRate, elapsed_s, fluxScale);
				CryoUtils.ConsumePartResource(part, fuelName, boiled, v, BrokerTitle);
				boiloffOccuring = true;
			}

			Lib.Proto.Set(cryoSnapshot, "BoiloffOccuring", boiloffOccuring);
			return BrokerTitle;
		}

		static float GetLoopTemperature(ProtoPartSnapshot part, string systemHeatModuleId, Vessel v)
		{
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "ModuleSystemHeat")
					continue;

				string moduleId = Lib.Proto.GetString(module, "moduleID");
				if (!string.IsNullOrEmpty(systemHeatModuleId) && moduleId != systemHeatModuleId)
					continue;

				float temp = Lib.Proto.GetFloat(module, "currentLoopTemperature");
				if (temp > 0f)
					return temp;
			}

			return 300f;
		}
	}
}
