using System;
using System.Collections.Generic;
using UnityEngine;
using static KERBALISM.HabitatData;

namespace KERBALISM
{
	/// <summary>
	/// loaded/unloaded/editor state independant persisted data and logic used by the ModuleKsmHabitat module.
	/// </summary>
	public class HabitatData : ModuleData<ModuleKsmHabitat, HabitatData>
	{
		#region ENUMS AND TYPES

		public enum PressureState
		{
			Pressurized,
			Breatheable,
			AlwaysDepressurized,
			Depressurized,
			Pressurizing,
			DepressurizingAboveThreshold,
			DepressurizingBelowThreshold
		}

		public enum AnimState
		{
			Retracted,
			Deploying,
			Retracting,
			Deployed,
			Accelerating,
			Decelerating,
			Rotating,
			RotatingNotEnoughEC,
			Stuck

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

		#endregion

		#region FIELDS

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

		public AnimState animState = AnimState.Retracted;

		/// <summary> if deployable, is the habitat deployed ? </summary>
		//public bool isDeployed = false;

		///// <summary> if centrifuge, is the centrifuge spinning ? </summary>
		//public bool isRotating = false;

		/// <summary> crew count </summary>
		public int crewCount = 0;

		/// <summary> current atmosphere count (1 unit = 1 m3 of air at STP) </summary>
		public double atmoAmount = 0.0;

		/// <summary> current % of poisonous atmosphere (CO2)</summary>
		public double wasteLevel = 0.0; 

		/// <summary> current shielding count (1 unit = 1 m2 of fully shielded surface, see Radiation.ShieldingEfficiency) </summary>
		public double shieldingAmount = 0.0;

		/// <summary> used to know when to consume ec for deploy/retract and accelerate/decelerate centrifuges</summary>
		public double animTimer = 0.0;

		public double sunRadiation = 0.0; // TODO: IMPORTANT : this was in the codebase before but is unnused here. Check what this was for !!!!!

		public List<SunRadiationOccluder> sunRadiationOccluders = new List<SunRadiationOccluder>();

		private bool raytracedOnce = false;

		public ModuleKsmHabitat.HabitatUpdateHandler updateHandler;

		#endregion

		#region PROPERTIES

		public bool IsDeployed
		{
			get
			{
				switch (animState)
				{
					case AnimState.Deployed:
					case AnimState.Accelerating:
					case AnimState.Decelerating:
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
					case AnimState.Stuck:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsRotationNominal => animState == AnimState.Rotating;
		public bool IsAccelerating => animState == AnimState.Accelerating;
		public bool IsDecelerating => animState == AnimState.Decelerating;
		public bool IsStuck => animState == AnimState.Stuck;

		public bool IsRotationEnabled
		{
			get
			{
				switch (animState)
				{
					case AnimState.Accelerating:
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsRotationStopped
		{
			get
			{
				switch (animState)
				{
					case AnimState.Retracted:
					case AnimState.Retracting:
					case AnimState.Deploying:
					case AnimState.Deployed:
						return true;
					default:
						return false;
				}
			}
		}

		/// <summary>
		/// Is the habitat pressurized above the pressure threshold
		/// Note that when false, it doesn't mean kerbals need to be in their suits if they are in breathable atmosphere.
		/// </summary>
		public bool IsPressurized
		{
			get
			{
				switch (pressureState)
				{
					case PressureState.Pressurized:
					case PressureState.DepressurizingAboveThreshold:
						return true;
					default:
						return false;
				}
			}
		}

		/// <summary>
		/// Are suits required. Note that this doesn't mean the habitat is depressurized.
		/// </summary>
		public bool RequireSuit
		{
			get
			{
				switch (pressureState)
				{
					case PressureState.AlwaysDepressurized:
					case PressureState.Depressurized:
					case PressureState.Pressurizing:
					case PressureState.DepressurizingBelowThreshold:
						return true;
					default:
						return false;
				}
			}
		}

		/// <summary>
		/// Are suits required. Note that this doesn't mean the habitat is depressurized.
		/// </summary>
		public bool IsFullyDepressurized
		{
			get
			{
				switch (pressureState)
				{
					case PressureState.AlwaysDepressurized:
					case PressureState.Breatheable:
					case PressureState.Depressurized:
						return true;
					default:
						return false;
				}
			}
		}

		#endregion

		#region LIFECYCLE

		public override void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule = null, ProtoPartSnapshot protoPart = null)
		{
			if (loadedModule != null)
				crewCount = Lib.CrewCount(loadedModule.part);
			else
				crewCount = Lib.CrewCount(protoPart);

			baseVolume = modulePrefab.volume;
			baseSurface = modulePrefab.surface;
			baseComfortsMask = modulePrefab.baseComfortsMask;
			animState = modulePrefab.isDeployable ? AnimState.Retracted : AnimState.Deployed;
			isEnabled = !modulePrefab.isDeployable;

			
			if (Lib.IsEditor)
			{
				if (!modulePrefab.canPressurize)
					pressureState = PressureState.AlwaysDepressurized;
				else if (modulePrefab.isDeployable && !IsDeployed)
					pressureState = PressureState.Depressurized;
				else
					pressureState = PressureState.Pressurized;
			}
			// part was created in flight (rescue, KIS...)
			else
			{
				// if part is manned (rescue vessel), force enabled and deployed
				if (crewCount > 0)
				{
					animState = AnimState.Deployed;
					isEnabled = true;
				}

				// don't pressurize. if it's a rescue, the player will likely go on EVA immediatly anyway, and in a case of a
				// part that was created in flight, it doesn't make sense to have it pre-pressurized.
				pressureState = modulePrefab.canPressurize ? PressureState.Depressurized : PressureState.AlwaysDepressurized;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			baseVolume = Lib.ConfigValue(node, "baseVolume", baseVolume);
			baseSurface = Lib.ConfigValue(node, "baseSurface", baseSurface);
			baseComfortsMask = Lib.ConfigValue(node, "baseComfortsMask", baseComfortsMask);
			isEnabled = Lib.ConfigValue(node, "habitatEnabled", isEnabled);
			pressureState = Lib.ConfigEnum(node, "pressureState", pressureState);
			animState = Lib.ConfigEnum(node, "animState", animState);
			crewCount = Lib.ConfigValue(node, "crewCount", crewCount);
			atmoAmount = Lib.ConfigValue(node, "atmoAmount", atmoAmount);
			wasteLevel = Lib.ConfigValue(node, "wasteLevel", wasteLevel);
			shieldingAmount = Lib.ConfigValue(node, "shieldingAmount", shieldingAmount);

			sunRadiation = Lib.ConfigValue(node, "sunRadiation", sunRadiation);

			sunRadiationOccluders.Clear();
			foreach (ConfigNode occluderNode in node.GetNodes("OCCLUDER"))
			{
				float distance = Lib.ConfigValue(occluderNode, "distance", 1f);
				float thickness = Lib.ConfigValue(occluderNode, "thickness", 1f);
				sunRadiationOccluders.Add(new SunRadiationOccluder(distance, thickness));
			}
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("baseVolume", baseVolume);
			node.AddValue("baseSurface", baseSurface);
			node.AddValue("baseComfortsMask", baseComfortsMask);
			node.AddValue("habitatEnabled", isEnabled);
			node.AddValue("pressureState", pressureState.ToString());
			node.AddValue("animState", animState.ToString());
			node.AddValue("crewCount", crewCount);
			node.AddValue("atmoAmount", atmoAmount);
			node.AddValue("wasteLevel", wasteLevel);
			node.AddValue("shieldingAmount", shieldingAmount);

			node.AddValue("sunRadiation", sunRadiation);

			foreach (SunRadiationOccluder occluder in sunRadiationOccluders)
			{
				ConfigNode occluderNode = node.AddNode("OCCLUDER");
				occluderNode.AddValue("distance", occluder.distance);
				occluderNode.AddValue("thickness", occluder.thickness);
			};
		}

		#endregion

		#region EVALUATION

		public override void OnVesselDataUpdate(VesselDataBase vd)
		{
			HabitatVesselData info = vd.Habitat;
			if (isEnabled)
			{
				switch (pressureState)
				{
					case PressureState.Breatheable:
						info.livingVolume += baseVolume;
						info.shieldingSurface += baseSurface;
						info.shieldingAmount += shieldingAmount;

						info.comfortMask |= baseComfortsMask;
						if (IsRotationNominal)
							info.comfortMask |= (int)Comfort.firmGround;

						info.pressurizedPartsCrewCount += crewCount;

						break;
					case PressureState.Pressurized:
					case PressureState.DepressurizingAboveThreshold:
						info.pressurizedVolume += baseVolume;
						info.livingVolume += baseVolume;
						info.pressurizedSurface += baseSurface;
						info.shieldingSurface += baseSurface;
						info.shieldingAmount += shieldingAmount;

						info.comfortMask |= baseComfortsMask;
						if (IsRotationNominal)
							info.comfortMask |= (int)Comfort.firmGround | (int)Comfort.exercice;

						info.pressurizedPartsAtmoAmount += atmoAmount;
						info.pressurizedPartsCrewCount += crewCount;

						// waste evaluation
						info.poisoningLevel += wasteLevel;
						info.wasteConsideredPartsCount++;
						break;
					case PressureState.AlwaysDepressurized:
					case PressureState.Depressurized:
					case PressureState.Pressurizing:
					case PressureState.DepressurizingBelowThreshold:
						if (crewCount > 0)
						{
							info.shieldingSurface += baseSurface;
							info.shieldingAmount += shieldingAmount;
							info.poisoningLevel += wasteLevel;
							info.wasteConsideredPartsCount++;
						}
						// waste in suits evaluation
						break;
				}

				// We do only one habitat radiation raytracing per vesseldata update 
				// to avoid the performance hit on large vessels.
				// Radiation raytracing is only done while loaded
				if (IsLoaded)
				{
					// if habitat was never raytraced, do it
					if (!raytracedOnce)
					{
						Radiation.RaytraceHabitatSunRadiation(vd.EnvMainSunDirection, this);
						raytracedOnce = true;
						info.raytraceNextModule = false;
						info.lastRaytracedModuleId = flightId;
					}
					// if habitat was raytraced on last update, flag the next iterated over habitat
					// to be raytraced in this vesseldata update
					else if (!info.raytraceNextModule && info.lastRaytracedModuleId == flightId)
					{
						info.raytraceNextModule = true;
					}
					// if last raytraced module was iterated over just before, raytrace this module
					else if (info.raytraceNextModule)
					{
						Radiation.RaytraceHabitatSunRadiation(vd.EnvMainSunDirection, this);
						info.raytraceNextModule = false;
						info.lastRaytracedModuleId = flightId;
					}
				}
			}
			else
			{
				switch (pressureState)
				{
					case PressureState.Breatheable:
						// nothing here
						break;
					case PressureState.Pressurized:
					case PressureState.DepressurizingAboveThreshold:
						info.pressurizedVolume += baseVolume;
						info.pressurizedSurface += baseSurface;
						info.pressurizedPartsAtmoAmount += atmoAmount;
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

		#endregion
	}
}
