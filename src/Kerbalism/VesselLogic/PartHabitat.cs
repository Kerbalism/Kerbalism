using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	/// <summary>
	/// Vessel state independant structure for the Habitat PartModule. Try not to put any logic in here.
	/// </summary>
	public class PartHabitat
	{
		public enum PressureState
		{
			Pressurized,
			PressureDropped,
			BreatheableStart,
			Breatheable,
			Depressurized,
			PressurizingStart,
			Pressurizing,
			PressurizingEnd,
			DepressurizingStart,
			Depressurizing,
			DepressurizingEnd
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


		public struct SunRadiationOccluder
		{
			public float distance;
			public float thickness;

			public SunRadiationOccluder(float distance, float thickness)
			{
				this.distance = distance;
				this.thickness = thickness;
			}
		}

		/// <summary> can the habitat be occupied and does it count for global pressure/volume/comforts/radiation </summary>
		public bool habitatEnabled;

		/// <summary> pressure state </summary>
		public PressureState pressureState;

		/// <summary> if deployable, is the habitat deployed ? </summary>
		public bool isDeployed;

		/// <summary> if centrifuge, is the centrifuge spinning ? </summary>
		public bool isRotating;

		/// <summary> crew count </summary>
		public int crewCount;

		/// <summary> current habitable and pressurized volume in m3 </summary>
		public double enabledVolume;

		/// <summary> current atmosphere count (1 unit = 1 m3 of air at STP) </summary>
		public double atmoAmount;

		/// <summary> current poisonous atmosphere count (1 unit = 1 m3 of CO2 at STP) </summary>
		public double wasteAmount; 

		/// <summary> current shielding count (1 unit = 1 m2 of fully shielded surface, see Radiation.ShieldingEfficiency) </summary>
		public double shieldingAmount;

		/// <summary> current surface in m2 </summary>
		public double enabledSurface;

		/// <summary> bitmask of currently available comforts </summary>
		public int enabledComfortsMask;

		public double sunRadiation;
		public List<SunRadiationOccluder> sunRadiationOccluders = new List<SunRadiationOccluder>();

		public ModuleKsmHabitat module;

		public PartHabitat(ModuleKsmHabitat habitatModule = null)
		{
			module = habitatModule;
		}

		public void Save(ConfigNode habitatNode)
		{
			habitatNode.AddValue("habitatEnabled", habitatEnabled);
			habitatNode.AddValue("pressureState", pressureState.ToString());
			habitatNode.AddValue("isDeployed", isDeployed);
			habitatNode.AddValue("isRotating", isRotating);
			habitatNode.AddValue("crewCount", crewCount);
			habitatNode.AddValue("enabledVolume", enabledVolume);
			habitatNode.AddValue("atmoAmount", atmoAmount);
			habitatNode.AddValue("wasteAmount", wasteAmount);
			habitatNode.AddValue("shieldingAmount", shieldingAmount);
			habitatNode.AddValue("enabledSurface", enabledSurface);
			habitatNode.AddValue("enabledComfortsMask", enabledComfortsMask);
			habitatNode.AddValue("sunRadiation", sunRadiation);

			foreach (SunRadiationOccluder occluder in sunRadiationOccluders)
			{
				ConfigNode occluderNode = habitatNode.AddNode("occluder");
				occluderNode.AddValue("distance", occluder.distance);
				occluderNode.AddValue("thickness", occluder.thickness);
			}
		}

		public PartHabitat(ConfigNode habitatNode)
		{
			habitatEnabled = Lib.ConfigValue(habitatNode, "habitatEnabled", habitatEnabled);
			pressureState = Lib.ConfigEnum(habitatNode, "pressureState", pressureState);
			isDeployed = Lib.ConfigValue(habitatNode, "isDeployed", isDeployed);
			isRotating = Lib.ConfigValue(habitatNode, "isRotating", isRotating);
			crewCount = Lib.ConfigValue(habitatNode, "crewCount", crewCount);
			enabledVolume = Lib.ConfigValue(habitatNode, "enabledVolume", enabledVolume);
			atmoAmount = Lib.ConfigValue(habitatNode, "atmoAmount", atmoAmount);
			wasteAmount = Lib.ConfigValue(habitatNode, "wasteAmount", wasteAmount);
			shieldingAmount = Lib.ConfigValue(habitatNode, "shieldingAmount", shieldingAmount);
			enabledSurface = Lib.ConfigValue(habitatNode, "enabledSurface", enabledSurface);
			enabledComfortsMask = Lib.ConfigValue(habitatNode, "enabledComfortsMask", enabledComfortsMask);
			sunRadiation = Lib.ConfigValue(habitatNode, "sunRadiation", sunRadiation);

			sunRadiationOccluders.Clear();
			foreach (ConfigNode occluderNode in habitatNode.GetNodes("occluder"))
			{
				float distance = Lib.ConfigValue(occluderNode, "distance", 1f);
				float thickness = Lib.ConfigValue(occluderNode, "thickness", 1f);
				sunRadiationOccluders.Add(new SunRadiationOccluder(distance, thickness));
			}
		}
	}
}
