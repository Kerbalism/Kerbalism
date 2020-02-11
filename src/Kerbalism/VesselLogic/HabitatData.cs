using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	// Data structure holding the vessel wide habitat state, evaluated from VesselData
	public class HabitatVesselData
	{
		public int habitatRaytraceNextIndex = -1;

		/// <summary> volume (m3) of enabled and under breathable conditions habitats</summary>
		public double livingVolume = 0.0;

		/// <summary> livingVolume (m3) per kerbal</summary>
		public double volumePerCrew = 0.0;

		/// <summary> [0.1 : 1.0] factor : living volume per kerbal normalized against the ideal living space as defined in settings</summary>
		public double livingSpaceModifier = 0.0;

		/// <summary> surface (m2) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressurizedSurface = 0.0;

		/// <summary> volume (m3) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressurizedVolume = 0.0;

		/// <summary> pressure (atm) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressureAtm = 0.0;

		/// <summary> [0.0 ; 1.0] factor : amount of crew members not living with their helmets (pressurized hab / outside air) vs total crew count</summary>
		public double pressureModifier = 0.0;

		/// <summary> [0.0 ; 1.0] % of CO2 in the air (averaged in case there is a mix of pressurized / outside air habitats)</summary>
		public double poisoningLevel = 0.0;

		/// <summary> surface (m2) of all enabled habitats, excluding depressurized habitats that aren't crewed</summary>
		public double shieldingSurface = 0.0;

		/// <summary> amount of shielding resource (1 unit = 1m2 of 20mm thick pb) for all enabled habitats, excluding depressurized habitats that aren't crewed</summary>
		public double shieldingAmount = 0.0;

		/// <summary> [0.0 ; 1.0] factor : proportion of radiation blocked by shielding (see Radiation.ShieldingEfficiency())</summary>
		public double shieldingModifier = 0.0;

		/// <summary> bitmask of available comforts (see Comfort enum) : comforts in disabled or depressurized habs are ignored</summary>
		public int comfortMask = 0;

		/// <summary> [0.1 ; 1.0] factor : sum of all enabled comfort bonuses </summary>
		public double comfortModifier = 0.0;

		public void Reset()
		{
			livingVolume = volumePerCrew = livingSpaceModifier
				= pressurizedSurface = pressurizedVolume = pressureAtm
				= pressureModifier = poisoningLevel = shieldingSurface
				= shieldingAmount = shieldingModifier = comfortModifier
				= 0.0;
			comfortMask = 0;
		}

	}

	/// <summary>
	/// loaded/unloaded/editor state independant persisted data and logic used by the ModuleKsmHabitat module.
	/// </summary>
	public class HabitatData
	{
		public enum PressureState
		{
			Pressurized,
			PressureDroppedEvt,
			BreatheableStartEvt,
			Breatheable,
			AlwaysDepressurized,
			AlwaysDepressurizedStartEvt,
			Depressurized,
			PressurizingStartEvt,
			Pressurizing,
			PressurizingEndEvt,
			DepressurizingStartEvt,
			DepressurizingAboveThreshold,
			DepressurizingPassThresholdEvt,
			DepressurizingBelowThreshold,
			DepressurizingEndEvt
		}

		public enum Comfort
		{
			firmGround = 1 << 0,
			notAlone = 1 << 1,
			callHome = 1 << 2,
			exercice = 1 << 3,
			panorama = 1 << 4,
			plants = 1 << 5
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

		/// <summary> habitat volume in m3 </summary>
		public double baseVolume = 0.0;

		/// <summary> habitat surface in m2 </summary>
		public double baseSurface = 0.0;

		/// <summary> bitmask of comforts provided by the habitat</summary>
		public int baseComfortsMask = 0;

		/// <summary> can the habitat be occupied and does it count for global pressure/volume/comforts/radiation </summary>
		public bool isEnabled = false;

		/// <summary> pressure state </summary>
		public PressureState pressureState = PressureState.AlwaysDepressurized;

		/// <summary> if deployable, is the habitat deployed ? </summary>
		public bool isDeployed = false;

		/// <summary> if centrifuge, is the centrifuge spinning ? </summary>
		public bool isRotating = false;

		/// <summary> crew count </summary>
		public int crewCount = 0;

		/// <summary> current atmosphere count (1 unit = 1 m3 of air at STP) </summary>
		public double atmoAmount = 0.0;

		/// <summary> current % of poisonous atmosphere (CO2)</summary>
		public double wasteLevel = 0.0; 

		/// <summary> current shielding count (1 unit = 1 m2 of fully shielded surface, see Radiation.ShieldingEfficiency) </summary>
		public double shieldingAmount = 0.0;

		/// <summary> used to know when to consume ec on unloaded vessels for deploy/retract and accelerate/decelerate centrifuges</summary>
		public double animationTimer = 0.0;

		public double sunRadiation = 0.0; // TODO: IMPORTANT : this was in the codebase before but is unnused here. Check what this was for !!!!!

		public List<SunRadiationOccluder> sunRadiationOccluders = new List<SunRadiationOccluder>();

		public ModuleKsmHabitat module = null;

		public HabitatData() { }

		public HabitatData(ConfigNode habitatNode)
		{
			baseVolume = Lib.ConfigValue(habitatNode, "baseVolume", baseVolume);
			baseSurface = Lib.ConfigValue(habitatNode, "baseSurface", baseSurface);
			baseComfortsMask = Lib.ConfigValue(habitatNode, "baseComfortsMask", baseComfortsMask);
			isEnabled = Lib.ConfigValue(habitatNode, "habitatEnabled", isEnabled);
			pressureState = Lib.ConfigEnum(habitatNode, "pressureState", pressureState);
			isDeployed = Lib.ConfigValue(habitatNode, "isDeployed", isDeployed);
			isRotating = Lib.ConfigValue(habitatNode, "isRotating", isRotating);
			crewCount = Lib.ConfigValue(habitatNode, "crewCount", crewCount);
			atmoAmount = Lib.ConfigValue(habitatNode, "atmoAmount", atmoAmount);
			wasteLevel = Lib.ConfigValue(habitatNode, "wasteLevel", wasteLevel);
			shieldingAmount = Lib.ConfigValue(habitatNode, "shieldingAmount", shieldingAmount);

			sunRadiation = Lib.ConfigValue(habitatNode, "sunRadiation", sunRadiation);

			sunRadiationOccluders.Clear();
			foreach (ConfigNode occluderNode in habitatNode.GetNodes("occluder"))
			{
				float distance = Lib.ConfigValue(occluderNode, "distance", 1f);
				float thickness = Lib.ConfigValue(occluderNode, "thickness", 1f);
				sunRadiationOccluders.Add(new SunRadiationOccluder(distance, thickness));
			}
		}

		public void Save(ConfigNode habitatNode)
		{
			habitatNode.AddValue("baseVolume", baseVolume);
			habitatNode.AddValue("baseSurface", baseSurface);
			habitatNode.AddValue("baseComfortsMask", baseComfortsMask);
			habitatNode.AddValue("habitatEnabled", isEnabled);
			habitatNode.AddValue("pressureState", pressureState.ToString());
			habitatNode.AddValue("isDeployed", isDeployed);
			habitatNode.AddValue("isRotating", isRotating);
			habitatNode.AddValue("crewCount", crewCount);
			habitatNode.AddValue("atmoAmount", atmoAmount);
			habitatNode.AddValue("wasteLevel", wasteLevel);
			habitatNode.AddValue("shieldingAmount", shieldingAmount);

			habitatNode.AddValue("sunRadiation", sunRadiation);

			foreach (SunRadiationOccluder occluder in sunRadiationOccluders)
			{
				ConfigNode occluderNode = habitatNode.AddNode("occluder");
				occluderNode.AddValue("distance", occluder.distance);
				occluderNode.AddValue("thickness", occluder.thickness);
			}
		}

		public static void SetFlightReferenceFromPart(Part part, HabitatData data) => part.vessel.KerbalismData().Parts.Get(part.flightID).Habitat = data;

		public static HabitatData GetFlightReferenceFromPart(Part part) => part.vessel.KerbalismData().Parts.Get(part.flightID).Habitat;

		public static HabitatData GetFlightReferenceFromProtoPart(Vessel vessel, ProtoPartSnapshot part) => vessel.KerbalismData().Parts.Get(part.flightID).Habitat;

		public static void EvaluateHabitat(HabitatVesselData info, List<HabitatData> habitats, ConnectionInfo connection, bool landed, int crewCount, Vector3d mainSunDrection, bool isLoadedVessel)
		{
			// TODO : fix the Waste management for kerbal in depressurized habs :
			// As it is, manned depressurized parts CO2 level is ignored, and waste produced by the kerbals in them will be added
			// to the pressurized parts, if any, or just ignored if all parts are depressurized. This isn't very satisfactory.
			// So maybe do things differently for waste :
			// - set the depressurized parts waste capacity to part.crewCount * suitVolume
			// - enable waste flow (and equalization) at all times excepted when a part is depressurizing below threshold
			// - calculate CO2 level with waste.amount / (waste.capacity in all non-breathable state parts)
			// - transfer waste.amount between parts on crew / EVA transfer 

			info.Reset();

			double pressurizedPartsAtmoAmount = 0.0; // for calculating pressure level : all pressurized parts, enabled or not
			int pressurizedPartsCrewCount = 0; // crew in all pressurized parts, pressure modifier = pressurizedPartsCrewCount / totalCrewCount
			int wasteConsideredPartsCount = 0;

			for (int i = 0; i < habitats.Count; i++)
			{
				HabitatData habitat = habitats[i];
				if (habitat.isEnabled)
				{
					switch (habitat.pressureState)
					{
						case PressureState.Breatheable:
							info.livingVolume += habitat.baseVolume;
							info.shieldingSurface += habitat.baseSurface;
							info.shieldingAmount += habitat.shieldingAmount;

							info.comfortMask |= habitat.baseComfortsMask;
							if (habitat.isRotating)
								info.comfortMask |= (int)Comfort.firmGround;

							pressurizedPartsCrewCount += habitat.crewCount;

							break;
						case PressureState.Pressurized:
						case PressureState.DepressurizingAboveThreshold:
							info.pressurizedVolume += habitat.baseVolume;
							info.livingVolume += habitat.baseVolume;
							info.pressurizedSurface += habitat.baseSurface;
							info.shieldingSurface += habitat.baseSurface;
							info.shieldingAmount += habitat.shieldingAmount;

							info.comfortMask |= habitat.baseComfortsMask;
							if (habitat.isRotating)
								info.comfortMask |= (int)Comfort.firmGround;

							pressurizedPartsAtmoAmount += habitat.atmoAmount;
							pressurizedPartsCrewCount += habitat.crewCount;

							// waste evaluation
							info.poisoningLevel += habitat.wasteLevel;
							wasteConsideredPartsCount++;
							break;
						case PressureState.AlwaysDepressurized:
						case PressureState.Depressurized:
						case PressureState.Pressurizing:
						case PressureState.DepressurizingBelowThreshold:
							if (habitat.crewCount > 0)
							{
								info.shieldingSurface += habitat.baseSurface;
								info.shieldingAmount += habitat.shieldingAmount;
								info.poisoningLevel += habitat.wasteLevel;
								wasteConsideredPartsCount++;
							}
							// waste in suits evaluation
							break;
					}

					// radiation raytracing is only done while loaded.
					if (isLoadedVessel && habitat.module != null)
					{
						// if partHabitatRaytraceNextIndex was reset to -1, raytrace all parts
						if (info.habitatRaytraceNextIndex < 0)
						{
							Radiation.RaytraceHabitatSunRadiation(mainSunDrection, habitat);
							info.habitatRaytraceNextIndex = 0;
						}
						// else only raytrace one habitat per update cycle to preserve performance
						else if (i == info.habitatRaytraceNextIndex)
						{
							Radiation.RaytraceHabitatSunRadiation(mainSunDrection, habitat);
							info.habitatRaytraceNextIndex = (info.habitatRaytraceNextIndex + 1) % habitats.Count;
						}
					}
				}
				else
				{
					switch (habitat.pressureState)
					{
						case PressureState.Breatheable:
							// nothing here
							break;
						case PressureState.Pressurized:
						case PressureState.DepressurizingAboveThreshold:
							info.pressurizedVolume += habitat.baseVolume;
							info.pressurizedSurface += habitat.baseSurface;
							pressurizedPartsAtmoAmount += habitat.atmoAmount;
							// waste evaluation
							break;
						case PressureState.AlwaysDepressurized:
						case PressureState.Depressurized:
						case PressureState.Pressurizing:
						case PressureState.DepressurizingBelowThreshold:
							// waste in suits evaluation
							break;
					}
				}
			}

			info.volumePerCrew = crewCount > 0 ? info.livingVolume / crewCount : 0.0;
			info.livingSpaceModifier = Lib.Clamp(info.volumePerCrew / PreferencesComfort.Instance.livingSpace, 0.1, 1.0);
			info.pressureAtm = info.pressurizedVolume > 0.0 ? pressurizedPartsAtmoAmount / info.pressurizedVolume : 0.0;
			info.pressureModifier = crewCount > 0 ? (double)pressurizedPartsCrewCount / (double)crewCount : info.pressurizedVolume > 0.0 ? 1.0 : 0.0;
			info.poisoningLevel = wasteConsideredPartsCount > 0 ? info.poisoningLevel / wasteConsideredPartsCount : 0.0;
			info.shieldingModifier = info.shieldingSurface > 0.0 ? Radiation.ShieldingEfficiency(info.shieldingAmount / info.shieldingSurface) : 0.0;

			if (landed) info.comfortMask |= (int)Comfort.firmGround;
			if (crewCount > 1) info.comfortMask |= (int)Comfort.notAlone;
			if (connection.linked && connection.rate > 0.0) info.comfortMask |= (int)Comfort.callHome;

			info.comfortModifier = HabitatLib.GetComfortFactor(info.comfortMask);
		}

	}
}
