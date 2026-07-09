using System.Collections;

namespace KERBALISM
{
	internal static class SystemHeatCryoTankAccess
	{
		internal static IEnumerable GetFuels(PartModule tank)
		{
			return IntegrationReflection.GetField<IEnumerable>(tank, "fuels");
		}

		internal static string GetFuelName(object fuelEntry) => IntegrationReflection.GetString(fuelEntry, "fuelName");

		internal static float GetBoiloffRate(object fuelEntry) => IntegrationReflection.GetFloat(fuelEntry, "boiloffRate");

		internal static float GetCryoTemperature(object fuelEntry)
		{
			float temp = IntegrationReflection.GetFloat(fuelEntry, "cryoTemperature");
			if (temp > 0f)
				return temp;
			return IntegrationReflection.GetFloat(fuelEntry, "CryocoolerTemperature");
		}

		internal static float GetCoolingHeatCost(object fuelEntry)
		{
			float value = IntegrationReflection.GetFloat(fuelEntry, "coolingHeatCost");
			if (value > 0f)
				return value;
			return IntegrationReflection.GetFloat(fuelEntry, "CoolingHeatCost");
		}
	}
}
