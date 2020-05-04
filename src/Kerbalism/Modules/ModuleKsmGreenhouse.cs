using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class GreenhouseData : ModuleData<ModuleKsmGreenhouse, GreenhouseData>
	{

	}

	public class ModuleKsmGreenhouse : KsmPartModule<ModuleKsmGreenhouse, GreenhouseData>
	{
		[KSPField] public double minLight;       // minimum lighting flux required for growth, in W/m^2
		[KSPField] public double minPressure;    // minimum pressure required for growth, in sea level atmospheres (optional)
		[KSPField] public double maxRadiation;   // maximum radiation allowed for growth in rad/s, considered after shielding is applied (optional)
		[KSPField] public double setupDuration;   // maximum radiation allowed for growth in rad/s, considered after shielding is applied (optional)

		/*
		PRODUCTION_RECIPE
		{
			INPUT
			{
				name = KsmWasteAtmosphere
				substitute = Oxygen
				rate = 0.1
			}

			INPUT
			{
				name = Ammonia
				rate = 0.02
			}

			INPUT
			{
				name = Water
				rate = 0.02
			}

			OUTPUT
			{
				name = Food
				rate = 0.02
			}

			OUTPUT
			{
				name = Oxygen
				rate = 0.02
				dumpByDefault = true
			}
		}

		SETUP_RECIPE
		{
			INPUT
			{
				name = KsmWasteAtmosphere
				substitute = Oxygen
				rate = 0.1
			}

			INPUT
			{
				name = Ammonia
				rate = 0.02
			}

			INPUT
			{
				name = Water
				rate = 0.02
			}

			INPUT
			{
				name = Substrate
				rate = 0.02
			}

			OUTPUT
			{
				name = Oxygen
				rate = 0.1
				dumpByDefault = true
			}
		}

		*/
	}
}
