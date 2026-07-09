namespace KERBALISM
{
	internal static class CryoUtils
	{
		internal static void Log(string message)
		{
			Lib.Log("[zKerbalismCryo] " + message);
		}

		internal static void LogError(string message)
		{
			Lib.Log("[zKerbalismCryo] ERROR: " + message);
		}

		internal static bool PartHasCryoUpdater(ProtoPartSnapshot part)
		{
			if (part == null)
				return false;

			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName == "CryoTankKerbalismUpdater"
					|| module.moduleName == "SystemHeatCryoTankKerbalismUpdater")
					return true;
			}

			return false;
		}

		internal static PartModule FindCryoTankModule(Part part)
		{
			return CryoTanks.FindCryoTankModule(part);
		}

		internal static double ApplyBoiloffAmount(double amount, float boiloffRatePercentPerHour, double elapsed_s)
		{
			double boiloffRate = boiloffRatePercentPerHour / 360000.0;
			return amount * (1.0 - System.Math.Pow(1.0 - boiloffRate, elapsed_s));
		}

		internal static double ApplyBoiloffAmountSystemHeat(double amount, float boiloffRatePercentPerHour, double elapsed_s, double scale)
		{
			double boiloffRateSeconds = boiloffRatePercentPerHour / 100.0 / 3600.0;
			return amount * (1.0 - System.Math.Pow(1.0 - boiloffRateSeconds, elapsed_s)) * scale;
		}

		internal static ProtoPartResourceSnapshot FindPartResource(ProtoPartSnapshot part, string resourceName)
		{
			return part.resources.Find(r => r.resourceName == resourceName);
		}

		internal static void ConsumePartResource(ProtoPartSnapshot part, string resourceName, double amount, Vessel v, string brokerTitle)
		{
			if (amount <= 0.0)
				return;

			ProtoPartResourceSnapshot proto = FindPartResource(part, resourceName);
			if (proto == null)
				return;

			double removed = System.Math.Min(proto.amount, amount);
			proto.amount -= removed;

			ResourceInfo vesselResource = KERBALISM.ResourceCache.GetResource(v, resourceName);
			if (vesselResource.Amount >= removed)
				vesselResource.Consume(removed, KERBALISM.ResourceBroker.GetOrCreate("CryoTank", KERBALISM.ResourceBroker.BrokerCategory.VesselSystem, brokerTitle));
		}
	}
}
