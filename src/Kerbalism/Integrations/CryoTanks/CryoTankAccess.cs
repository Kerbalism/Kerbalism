using System.Collections;

namespace KERBALISM
{
	internal static class CryoTankAccess
	{
		internal static IList GetFuels(PartModule tank)
		{
			return CryoTanks.GetFuels(tank);
		}

		internal static string GetFuelName(object fuelEntry) => CryoTanks.GetFuelName(fuelEntry);

		internal static float GetBoiloffRate(object fuelEntry) => CryoTanks.GetBoiloffRate(fuelEntry);

		internal static float GetFuelCoolingCost(object fuelEntry) => CryoTanks.GetFuelCoolingCost(fuelEntry);
	}
}
