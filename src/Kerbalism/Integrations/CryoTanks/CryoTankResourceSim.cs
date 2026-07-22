using System.Collections;
using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	internal static class CryoTankResourceSim
	{
		internal const string BrokerName = "CryoTank";
		internal static string BrokerTitle => Localizer.Format("#KERBALISM_Brokers_Cryotank");

		/// <summary>
		/// CryoTanks cooling is CoolingCost (module) + present fuels' BOILOFFCONFIG CoolingCost,
		/// in EC/s per 1000 units. Matches SimpleBoiloff.ModuleCryoTank.GetTotalCoolingCost().
		/// </summary>
		internal static double EstimateCoolingEcRate(PartModule cryoModule, Part part)
		{
			if (cryoModule == null || part == null)
				return 0.0;

			IList fuels = CryoTankAccess.GetFuels(cryoModule);
			if (fuels == null)
				return 0.0;

			float moduleCost = CryoTanks.GetCoolingCost(cryoModule);
			float presentFuelCost = 0f;
			double totalAmount = 0.0;

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				double amount = Lib.Amount(part, fuelName);
				if (amount <= double.Epsilon)
					continue;

				presentFuelCost += CryoTankAccess.GetFuelCoolingCost(fuel);
				totalAmount += amount;
			}

			float coolingCostPer1000 = moduleCost + presentFuelCost;
			if (coolingCostPer1000 <= 0f || totalAmount <= double.Epsilon)
				return 0.0;

			return coolingCostPer1000 * totalAmount * 0.001;
		}

		internal static double EstimateCoolingEcRate(PartModule cryoPrefab, ProtoPartSnapshot part)
		{
			if (cryoPrefab == null || part == null)
				return 0.0;

			IList fuels = CryoTankAccess.GetFuels(cryoPrefab);
			if (fuels == null)
				return 0.0;

			float moduleCost = CryoTanks.GetCoolingCost(cryoPrefab);
			float presentFuelCost = 0f;
			double totalAmount = 0.0;

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				ProtoPartResourceSnapshot protoFuel = CryoUtils.FindPartResource(part, fuelName);
				if (protoFuel == null || protoFuel.amount <= double.Epsilon)
					continue;

				presentFuelCost += CryoTankAccess.GetFuelCoolingCost(fuel);
				totalAmount += protoFuel.amount;
			}

			float coolingCostPer1000 = moduleCost + presentFuelCost;
			if (coolingCostPer1000 <= 0f || totalAmount <= double.Epsilon)
				return 0.0;

			return coolingCostPer1000 * totalAmount * 0.001;
		}

		internal static void AddPlannerRates(PartModule cryoModule, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (cryoModule == null || cryoModule.part == null)
				return;

			IList fuels = CryoTankAccess.GetFuels(cryoModule);
			if (fuels == null)
				return;

			bool coolingEnabled = CryoTanks.GetCoolingEnabled(cryoModule);
			double ecRate = EstimateCoolingEcRate(cryoModule, cryoModule.part);

			if (coolingEnabled && ecRate > 0.0)
			{
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -ecRate));
				return;
			}

			// Cooling off, or tank has no cooling cost: report boiloff in planner.
			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				double amount = Lib.Amount(cryoModule.part, fuelName);
				if (amount <= double.Epsilon)
					continue;

				double boiloffRate = CryoTankAccess.GetBoiloffRate(fuel) / 360000.0;
				if (boiloffRate <= 0.0)
					continue;

				resourceChangeRequest.Add(new KeyValuePair<string, double>(fuelName, -amount * boiloffRate));
			}
		}

		internal static string UpdateLoaded(PartModule cryoModule, Vessel v)
		{
			if (cryoModule == null || v == null)
				return BrokerTitle;

			IList fuels = CryoTankAccess.GetFuels(cryoModule);
			if (fuels == null)
				return BrokerTitle;

			KERBALISM.ResourceBroker broker = KERBALISM.ResourceBroker.GetOrCreate(BrokerName, KERBALISM.ResourceBroker.BrokerCategory.VesselSystem, BrokerTitle);
			ResourceInfo ec = KERBALISM.ResourceCache.GetResource(v, "ElectricCharge");
			double dt = TimeWarp.fixedDeltaTime;
			bool coolingEnabled = CryoTanks.GetCoolingEnabled(cryoModule);
			double ecRate = EstimateCoolingEcRate(cryoModule, cryoModule.part);

			if (coolingEnabled && ecRate > 0.0)
			{
				// Fail only when EC cannot pay ~1s of cooling, not the full physics step.
				if (ec.Amount < ecRate)
				{
					CryoTanks.SetCoolingEnabled(cryoModule, false);
					SyncCryoTankPaw(cryoModule, false, true, ecRate);
				}
				else
				{
					ec.Consume(ecRate * dt, broker);
					SyncCryoTankPaw(cryoModule, true, false, ecRate);
					return BrokerTitle;
				}
			}

			double boiloffPerSecond = 0.0;
			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				PartResource resource = cryoModule.part.Resources.Get(fuelName);
				if (resource == null || resource.amount <= double.Epsilon)
					continue;

				double boiled = CryoUtils.ApplyBoiloffAmount(resource.amount, CryoTankAccess.GetBoiloffRate(fuel), dt);
				if (boiled > double.Epsilon)
				{
					KERBALISM.ResourceCache.GetResource(v, fuelName).Consume(boiled, broker);
					if (dt > double.Epsilon)
						boiloffPerSecond += boiled / dt;
				}
			}

			SyncCryoTankPaw(cryoModule, false, boiloffPerSecond > 0.0, ecRate, boiloffPerSecond);
			return BrokerTitle;
		}

		static void SyncCryoTankPaw(PartModule cryoModule, bool cooling, bool boiling, double ecRate, double boiloffPerSecond = 0.0)
		{
			// Native ModuleCryoTank.FixedUpdate is skipped while the updater is present.
			if (cooling)
			{
				CryoTanks.Set(cryoModule, "BoiloffOccuring", false);
				CryoTanks.Set(cryoModule, "BoiloffStatus", Localizer.Format("#LOC_CryoTanks_ModuleCryoTank_Field_BoiloffStatus_Insulated"));
				CryoTanks.Set(cryoModule, "CoolingStatus", Localizer.Format("#LOC_CryoTanks_ModuleCryoTank_Field_CoolingStatus_Cooling", ecRate.ToString("F2")));
				CryoTanks.Set(cryoModule, "currentCoolingCost", ecRate);
				return;
			}

			CryoTanks.Set(cryoModule, "BoiloffOccuring", boiling);
			CryoTanks.Set(cryoModule, "currentCoolingCost", 0.0);
			CryoTanks.Set(cryoModule, "CoolingStatus", Localizer.Format("#LOC_CryoTanks_ModuleCryoTank_Field_CoolingStatus_Disabled"));
			if (boiling && boiloffPerSecond > 0.0)
				CryoTanks.Set(cryoModule, "BoiloffStatus", string.Format("-{0:F3}/s", boiloffPerSecond));
			else
				CryoTanks.Set(cryoModule, "BoiloffStatus", Localizer.Format("#LOC_CryoTanks_ModuleCryoTank_Field_CoolingStatus_Disabled"));
		}

		internal static string BackgroundUpdate(
			Vessel v,
			ProtoPartSnapshot part,
			ProtoPartModuleSnapshot cryoSnapshot,
			PartModule cryoPrefab,
			double elapsed_s)
		{
			if (cryoPrefab == null || part == null)
				return BrokerTitle;

			bool coolingEnabled = Lib.Proto.GetBool(cryoSnapshot, "CoolingEnabled");
			IList fuels = CryoTankAccess.GetFuels(cryoPrefab);
			if (fuels == null)
				return BrokerTitle;

			ResourceInfo ec = KERBALISM.ResourceCache.Get(v).GetResource(v, "ElectricCharge");
			double ecRate = EstimateCoolingEcRate(cryoPrefab, part);
			string brokerTitle = BrokerTitle;

			// When cooling can run, EC is reported via resourceChangeRequest in CryoTankKerbalismUpdater.
			if (coolingEnabled && ecRate > 0.0 && ec.Amount >= ecRate)
				return brokerTitle;

			if (coolingEnabled && ecRate > 0.0)
				Lib.Proto.Set(cryoSnapshot, "CoolingEnabled", false);

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				ProtoPartResourceSnapshot protoFuel = CryoUtils.FindPartResource(part, fuelName);
				if (protoFuel == null || protoFuel.amount <= double.Epsilon)
					continue;

				double boiled = CryoUtils.ApplyBoiloffAmount(protoFuel.amount, CryoTankAccess.GetBoiloffRate(fuel), elapsed_s);
				CryoUtils.ConsumePartResource(part, fuelName, boiled, v, brokerTitle);
			}

			return brokerTitle;
		}
	}
}
