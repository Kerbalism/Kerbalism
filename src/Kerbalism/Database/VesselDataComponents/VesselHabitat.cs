using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static KERBALISM.HabitatData;

namespace KERBALISM
{
	// Data structure holding the vessel wide habitat state, evaluated from VesselData
	public class HabitatVesselData
	{
		/// <summary> volume (m3) of enabled and under breathable conditions habitats</summary>
		public double livingVolume = 0.0;

		/// <summary> livingVolume (m3) per kerbal</summary>
		public double volumePerCrew = 0.0;

		/// <summary> [0.0 : 1.0] factor : living volume per kerbal normalized against the ideal living space as defined in settings</summary>
		public double livingSpaceFactor = 0.0;

		/// <summary> surface (m2) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressurizedSurface = 0.0;

		/// <summary> volume (m3) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressurizedVolume = 0.0;

		/// <summary> pressure (atm) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressureAtm = 0.0;

		/// <summary> [0.0 ; 1.0] amount of crew members not living with their helmets (pressurized hab / outside air) vs total crew count</summary>
		public double pressureFactor = 0.0;

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

		/// <summary> [0.0 ; 1.0] factor : sum of all enabled comfort bonuses</summary>
		public double comfortFactor = 0.0;

		public double emittersRadiation = 0.0;

		public List<HabitatData> Habitats { get; private set; } = new List<HabitatData>();

		private int habitatRaytraceNextIndex = -1;

		public void ResetBeforeModulesUpdate(VesselDataBase vd)
		{
			livingVolume = volumePerCrew = livingSpaceFactor
				= pressurizedSurface = pressurizedVolume = pressureAtm
				= pressureFactor = poisoningLevel = shieldingSurface
				= shieldingAmount = shieldingModifier = comfortFactor = 0.0;

			comfortMask = 0;

			// the list of habitats will iterated over by every radiation emitter/shield, so build the list once.
			Habitats.Clear();
			foreach (HabitatData habitat in vd.ModuleDatasOfType<HabitatData>())
			{
				Habitats.Add(habitat);
			}
		}

		public void EvaluateAfterModuleUpdate(VesselDataBase vd)
		{
			double pressurizedPartsAtmoAmount = 0.0; // for calculating pressure level : all pressurized parts, enabled or not
			int pressurizedPartsCrewCount = 0; // crew in all pressurized parts, pressure modifier = pressurizedPartsCrewCount / totalCrewCount
			int wasteConsideredPartsCount = 0;
			int radiationConsideredPartsCount = 0;

			for (int i = 0; i < Habitats.Count; i++)
			{
				HabitatData habitat = Habitats[i];
				if (habitat.isEnabled)
				{
					switch (habitat.pressureState)
					{
						case PressureState.Breatheable:
							livingVolume += habitat.baseVolume;
							shieldingSurface += habitat.baseSurface;
							shieldingAmount += habitat.shieldingAmount;

							comfortMask |= habitat.baseComfortsMask;
							if (habitat.IsRotationNominal)
								comfortMask |= (int)Comfort.firmGround;

							pressurizedPartsCrewCount += habitat.crewCount;
							emittersRadiation += habitat.localRadiation;
							radiationConsideredPartsCount++;

							break;
						case PressureState.Pressurized:
						case PressureState.DepressurizingAboveThreshold:
							pressurizedVolume += habitat.baseVolume;
							livingVolume += habitat.baseVolume;
							pressurizedSurface += habitat.baseSurface;
							shieldingSurface += habitat.baseSurface;
							shieldingAmount += habitat.shieldingAmount;

							comfortMask |= habitat.baseComfortsMask;
							if (habitat.IsRotationNominal)
								comfortMask |= (int)Comfort.firmGround | (int)Comfort.exercice;

							pressurizedPartsAtmoAmount += habitat.atmoAmount;
							pressurizedPartsCrewCount += habitat.crewCount;

							// waste evaluation
							poisoningLevel += habitat.wasteLevel;
							wasteConsideredPartsCount++;
							emittersRadiation += habitat.localRadiation;
							radiationConsideredPartsCount++;
							break;
						case PressureState.AlwaysDepressurized:
						case PressureState.Depressurized:
						case PressureState.Pressurizing:
						case PressureState.DepressurizingBelowThreshold:
							if (habitat.crewCount > 0)
							{
								shieldingSurface += habitat.baseSurface;
								shieldingAmount += habitat.shieldingAmount;
								poisoningLevel += habitat.wasteLevel;
								wasteConsideredPartsCount++;
								emittersRadiation += habitat.localRadiation;
								radiationConsideredPartsCount++;
							}
							// waste in suits evaluation
							break;
					}

					// radiation raytracing is only done while loaded.
					if (vd.LoadedOrEditor && habitat.loadedModule != null)
					{
						// if partHabitatRaytraceNextIndex was reset to -1, raytrace all parts
						if (habitatRaytraceNextIndex < 0)
						{
							Radiation.RaytraceHabitatSunRadiation(vd.EnvMainSunDirection, habitat);
							habitatRaytraceNextIndex = 0;
						}
						// else only raytrace one habitat per update cycle to preserve performance
						else if (i == habitatRaytraceNextIndex)
						{
							Radiation.RaytraceHabitatSunRadiation(vd.EnvMainSunDirection, habitat);
							habitatRaytraceNextIndex = (habitatRaytraceNextIndex + 1) % Habitats.Count;
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
							pressurizedVolume += habitat.baseVolume;
							pressurizedSurface += habitat.baseSurface;
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

				// this is incremented from the RadiationEmitterData update, so always reset it
				habitat.localRadiation = 0.0;
			}

			int crewCount = vd.CrewCount;
			volumePerCrew = crewCount > 0 ? livingVolume / crewCount : 0.0;
			livingSpaceFactor = Math.Min(volumePerCrew / PreferencesComfort.Instance.livingSpace, 1.0);
			pressureAtm = pressurizedVolume > 0.0 ? pressurizedPartsAtmoAmount / pressurizedVolume : 0.0;

			pressureFactor = crewCount > 0 ? ((double)pressurizedPartsCrewCount / (double)crewCount) : 0.0; // 0.0 when pressurized, 1.0 when depressurized

			poisoningLevel = wasteConsideredPartsCount > 0 ? poisoningLevel / wasteConsideredPartsCount : 0.0;
			shieldingModifier = shieldingSurface > 0.0 ? Radiation.ShieldingEfficiency(shieldingAmount / shieldingSurface) : 0.0;

			if (vd.EnvLanded) comfortMask |= (int)Comfort.firmGround | (int)Comfort.exercice;
			if (crewCount > 0) comfortMask |= (int)Comfort.notAlone;

			if (vd.ConnectionInfo.Linked && vd.ConnectionInfo.DataRate > 0.0)
				comfortMask |= (int)Comfort.callHome;

			comfortFactor = HabitatLib.GetComfortFactor(comfortMask);

			if (radiationConsideredPartsCount > 1)
			{
				emittersRadiation /= radiationConsideredPartsCount;
			}
			
		}

	}
}
