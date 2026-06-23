using System.Collections;
using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	internal static class CryoTankResourceSim
	{
		internal const string BrokerName = "CryoTank";
		internal static string BrokerTitle => Localizer.Format("#KERBALISM_Brokers_Cryotank");

		internal static void AddPlannerRates(PartModule cryoModule, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (cryoModule == null || !CryoTanks.GetCoolingEnabled(cryoModule))
				return;

			IList fuels = CryoTankAccess.GetFuels(cryoModule);
			float coolingCost = CryoTanks.GetCoolingCost(cryoModule);
			if (fuels == null || coolingCost <= 0f)
				return;

			double totalCost = 0.0;
			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				double amount = Lib.Amount(cryoModule.part, fuelName);
				if (amount > double.Epsilon)
					totalCost += coolingCost * amount * 0.001;
			}

			if (totalCost > 0.0)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -totalCost));
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
			double totalEcRate = 0.0;
			double totalCost = 0.0;
			bool coolingEnabled = CryoTanks.GetCoolingEnabled(cryoModule);
			float coolingCost = CryoTanks.GetCoolingCost(cryoModule);

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				PartResource resource = cryoModule.part.Resources.Get(fuelName);
				if (resource == null || resource.amount <= double.Epsilon)
					continue;

				if (coolingEnabled && coolingCost > 0f)
				{
					double fuelEcRate = coolingCost * resource.amount * 0.001;
					totalEcRate += fuelEcRate;
					totalCost += fuelEcRate * dt;
				}
				else
				{
					double boiled = CryoUtils.ApplyBoiloffAmount(resource.amount, CryoTankAccess.GetBoiloffRate(fuel), dt);
					if (boiled > double.Epsilon)
						resource.amount = (float)(resource.amount - boiled);
				}
			}

			if (coolingEnabled && totalCost > double.Epsilon)
			{
				// Fail only when EC cannot pay ~1s of cooling, not the full physics step.
				if (ec.Amount < totalEcRate)
					CryoTanks.SetCoolingEnabled(cryoModule, false);
				else
					ec.Consume(totalCost, broker);
			}

			return BrokerTitle;
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
			bool coolingAvailable = coolingEnabled && ec.Amount > double.Epsilon;
			double totalEcCost = 0.0;
			string brokerTitle = BrokerTitle;
			float coolingCost = CryoTanks.GetCoolingCost(cryoPrefab);

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;

				ProtoPartResourceSnapshot protoFuel = CryoUtils.FindPartResource(part, fuelName);
				if (protoFuel == null || protoFuel.amount <= double.Epsilon)
					continue;

				double amount = protoFuel.amount;

				if (coolingAvailable && coolingCost > 0f)
				{
					totalEcCost += coolingCost * amount * 0.001;
				}
				else
				{
					double boiled = CryoUtils.ApplyBoiloffAmount(amount, CryoTankAccess.GetBoiloffRate(fuel), elapsed_s);
					CryoUtils.ConsumePartResource(part, fuelName, boiled, v, brokerTitle);
				}
			}

			if (totalEcCost > 0.0 && ec.Amount < totalEcCost)
				Lib.Proto.Set(cryoSnapshot, "CoolingEnabled", false);

			return brokerTitle;
		}
	}
}
