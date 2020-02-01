using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class HabitatData
	{
		public enum PressureState
		{
			Pressurized,
			Depressurized,
			Pressurizing,
			Depressurizing
		}

		public enum Comfort
		{
			FirmGround = 1 << 0,
			NotAlone = 1 << 1,
			CallHome = 1 << 2,
			Exercice = 1 << 3,
			Panorama = 1 << 4,
			Plants = 1 << 5
		}

		public struct SunRadiationOccluders
		{
			public double distance = 1.0;
			public double thickness = 1.0;
		}

		public bool isEnabled; // can the habitat be occupied and does it count for global pressure/volume/comforts/radiation
		public PressureState pressureState; // is the habitat pressurized
		public bool isDeployed; // if deployable, is the habitat deployed ?
		public bool isRotating; // if centrifuge, is the centrifuge spinning ?
		public double volume; // habitable volume in m3
		public double surface; // surface in m2
		public double pressure; // [0;1] pressure in % of 1 atm
		public int enabledComforts; // bitmask of currently available comforts
		public double poisoning; // [0;1] % of CO2 in the air
		public double radiationShielding; // radiation shielding level, 1.0 is 
		public double sunRadiation;
		public List<SunRadiationOccluders> sunRadiationOccluders = new List<SunRadiationOccluders>();

		public HabitatData(Habitat habitatModule)
		{
			isEnabled = habitatModule.habitatEnabled;
			pressureState = habitatModule.pressureState;
			isDeployed = habitatModule.isDeployed;
			isRotating = habitatModule.isRotating;
			enabledComforts = habitatModule.enabledComforts;

			if (isEnabled)
			{
				surface = habitatModule.surface;

				if (pressureState == PressureState.Depressurized)
					volume = Lib.CrewCount(habitatModule.part) * Settings.PressureSuitVolume;
				else
					volume = habitatModule.volume;
			}
			else
			{
				surface = 0.0;
				volume = 0.0;
			}
		}
	}
}
