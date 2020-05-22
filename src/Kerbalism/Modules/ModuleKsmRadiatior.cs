using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class RadiatorData : ModuleData<ModuleKsmRadiator, RadiatorData>
	{

	}

	public class ModuleKsmRadiator : KsmPartModule<ModuleKsmRadiator, RadiatorData>
	{
		[KSPField] public double surface = 1.0;            // radiator surface
		[KSPField] public bool doubleSided = false;                  // optional factor on emissivity, affect output rate
		[KSPField] public bool isDeployable = false;                  // optional factor on emissivity, affect output rate
		[KSPField] public double flowCapacity = 0.1;		// nominal heat radiative dissipation capacity, in kW, at 273.15 K, per m²
		[KSPField] public string inputResource = "ElectricCharge"; // resource consumed to make the pump work
		[KSPField] public double inputResourceRate = 0.1; // input resource rate at flowCapacity, per m²
		[KSPField] public string heatResource = Settings.aboveThEnergyRes; // heat pseudo-resource
		
	}
}
