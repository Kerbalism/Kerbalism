using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public class PartRadiationData
	{
		public const string NODENAME_RADIATION = "RADIATION";
		public const string NODENAME_EMITTERS = "EMITTERS";

		#region TYPES

		private class OccluderMaterial
		{
			private double thickness;
			private double lowHVL;
			private double highHVL;

			public void Set(double thickness, double lowHVL, double highHVL)
			{
				this.thickness = thickness;
				this.lowHVL = lowHVL;
				this.highHVL = highHVL;
			}

			public void Set(double thickness, double density)
			{
				this.thickness = thickness;
				lowHVL = Radiation.waterHVL_Gamma1MeV / density;
				highHVL = Radiation.waterHVL_Gamma25MeV / density;
			}

			public double RadiationFactor(double thicknessFactor, bool highPowerRad)
			{
				return Math.Pow(0.5, (thickness * thicknessFactor) / (highPowerRad ? highHVL : lowHVL));
			}
		}

		private struct CoilArrayShielding
		{
			private RadiationCoilData.ArrayEffectData array;
			private double protectionFactor;

			public CoilArrayShielding(RadiationCoilData.ArrayEffectData array, double protectionFactor)
			{
				this.array = array;
				this.protectionFactor = protectionFactor;
			}

			public double RadiationRemoved => array.RadiationRemoved * protectionFactor;
		}

		private struct SunOccluder
		{
			public PartRadiationData occluderData;
			public double thicknessFactor;
			public double distanceSqr;

			public SunOccluder(PartRadiationData occluderData, double thicknessFactor, double distanceSqr)
			{
				this.occluderData = occluderData;
				this.thicknessFactor = thicknessFactor;
				this.distanceSqr = distanceSqr;
			}
		}

		private struct EmittersRadiation
		{
			public int emitterID;
			public double radiation;

			public EmittersRadiation(int emitterID, double radiation)
			{
				this.emitterID = emitterID;
				this.radiation = radiation;
			}
		}

		#endregion

		#region FIELDS

		// occluders static cache
		private static List<SunOccluder> occluders = new List<SunOccluder>();

		// rad/s received by that part
		public double radiationRate;

		// total radiation dose received since launch
		public double accumulatedRadiation;

		public double elapsedSecSinceLastUpdate;

		// for solar storm radiation, proportion of radiation blocked by all parts between this part and the sun.
		private double sunRadiationFactor;

		// all active radiation shields whose protecting field include this part.
		private List<CoilArrayShielding> radiationCoilArrays;

		// This is used to scale down IRadiationEmitters effect on this part.
		// obtained by physic raytracing between that part and every IRadiationModifier on the vessel
		// key is the IRadiationEmitter (ModuleData) id, value is [0;1] factor applied to the IRadiationEmitter radiation
		private List<EmittersRadiation> emittersRadiation;
		private int nextEmitterToUpdate = 0;

		// very guesstimated thickness of the part : part bounding box volume, cube rooted
		private float bbThickness;

		// guesstimated part surface
		private double bbSurface;

		// occluding stats for the part structural mass. Fixed at part creation.
		private OccluderMaterial intrinsicOcclusion;

		// occluding stats for the part resources mass.
		// For now, recalculated on loaded vessels only, but it should be technically possible to do it for unloaded vessels too
		// And while this would make sense 
		private List<OccluderMaterial> resourcesOcclusion = new List<OccluderMaterial>();

		private PartData partData;

		private bool? isReceiver;

		#endregion

		#region PROPERTIES

		public bool IsReceiver
		{
			get
			{
				if (isReceiver == null)
				{
					foreach (ModuleData md in partData.modules)
					{
						if (md is IRadiationReceiver)
						{
							if (radiationCoilArrays == null)
								radiationCoilArrays = new List<CoilArrayShielding>();

							if (emittersRadiation == null)
								emittersRadiation = new List<EmittersRadiation>();

							isReceiver = true;
							break;
						}
					}

					if (isReceiver == null)
					{
						isReceiver = false;
					}
				}

				return (bool)isReceiver;
			}
		}

		public bool IsOccluder { get; private set; }

		#endregion

		#region LIFECYLE

		public PartRadiationData(PartData partData)
		{
			this.partData = partData;

			IsOccluder =
				partData.PartPrefab.physicalSignificance == Part.PhysicalSignificance.FULL
				&& partData.PartPrefab.attachMode == AttachModes.STACK
				&& partData.PartPrefab.mass + partData.PartPrefab.GetResourceMass() > 0.25
				&& Lib.PartBoundsVolume(partData.PartPrefab, false) > 0.25;
		}

		public static void LoadRadiationData(PartData partData, ConfigNode partDataNode)
		{
			ConfigNode radNode = partDataNode.GetNode(NODENAME_RADIATION);
			if (radNode == null)
				return;

			partData.radiationData.radiationRate = Lib.ConfigValue(partDataNode, "radRate", 0.0);
			partData.radiationData.accumulatedRadiation = Lib.ConfigValue(partDataNode, "radAcc", 0.0);

			ConfigNode emittersNode = partDataNode.GetNode(NODENAME_EMITTERS);
			if (emittersNode != null)
			{
				partData.radiationData.emittersRadiation = new List<EmittersRadiation>();
				foreach (ConfigNode.Value value in emittersNode.values)
				{
					EmittersRadiation emitter = new EmittersRadiation(Lib.Parse.ToInt(value.name), Lib.Parse.ToDouble(value.value));
					partData.radiationData.emittersRadiation.Add(emitter);
				}
			}
		}

		public static bool SaveRadiationData(PartData partData, ConfigNode partDataNode)
		{
			if (!partData.radiationData.IsReceiver)
				return false;

			ConfigNode radiationNode = partDataNode.AddNode(NODENAME_RADIATION);
			radiationNode.AddValue("radRate", partData.radiationData.radiationRate);
			radiationNode.AddValue("radAcc", partData.radiationData.accumulatedRadiation);

			ConfigNode emittersNode = new ConfigNode(NODENAME_EMITTERS);
			bool hasEmitter = false;
			foreach (var emitterData in partData.radiationData.emittersRadiation)
			{
				hasEmitter |= true;
				emittersNode.AddValue(emitterData.emitterID.ToString(), emitterData.radiation.ToString());
			}

			if (hasEmitter)
				radiationNode.AddNode(emittersNode);

			return true;
		}

		#endregion

		#region EVALUATION

		private bool debug = false;
		public void Update()
		{
			// TODO : should we consider emissions and occlusion from other nearby (~ 250m max) loaded vessels ?
			// It feels relevant for EVA and when all considered vessels are landed...
			// But if evaluating in-flight EVA there is a risk of saving radiation values that won't stay true while unloaded...

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.Update");

			if (IsOccluder)
			{
				// TODO : remove that in favor of a static dictionary of per part stats
				GetOccluderStats();
			}

			if (partData.vesselData.LoadedOrEditor)
			{
				if (IsReceiver)
				{
					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.SunRadiation");
					sunRadiationFactor = GetSunRadiationFactor(partData.vesselData.EnvMainSunDirection);
					UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.Update.Arrays");
					radiationCoilArrays.Clear();
					foreach (RadiationCoilData array in partData.vesselData.PartCache.RadiationArrays)
					{
						double protection = array.loadedModule.GetPartProtectionFactor(partData.LoadedPart);
						if (protection > 0.0)
							radiationCoilArrays.Add(new CoilArrayShielding(array.effectData, protection));

					}
					UnityEngine.Profiling.Profiler.EndSample();

				}

				if (IsOccluder)
				{
					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.Update.Occluders");
					UpdateOcclusionStats();
					UnityEngine.Profiling.Profiler.EndSample();
				}
			}



			if (IsReceiver)
			{
				radiationRate = partData.vesselData.EnvRadiation;

				// first scale down "ambiant" radiation by the part own shielding
				// TODO : we might want to only use "wall" shielding resources here
				// Add an extra bool in the OccluderMaterial class ?
				if (IsOccluder)
					radiationRate = RemainingRadiation(radiationRate, 1.0, false);

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.Update.Emitters");

				// then add the radiation from all active emitters on the vessel, scaled down by in-between occluders
				List<IRadiationEmitter> emitters = partData.vesselData.PartCache.RadiationEmitters;
				int vesselEmittersCount = emitters.Count;
				int knownEmittersCount = emittersRadiation.Count;
				for (int i = 0; i < vesselEmittersCount; i++)
				{
					IRadiationEmitter emitter = emitters[i];
					// On loaded vessels, recalculate the emitter impact
					if (partData.vesselData.LoadedOrEditor)
					{
						// If a new emitter has been added on the vessel (or the first time this is called), add it
						if (knownEmittersCount < vesselEmittersCount)
						{
							emittersRadiation.Add(new EmittersRadiation(emitter.ModuleId, GetRadiationFromEmitter(emitter)));
							knownEmittersCount++;
						}
						// Detect if an emitter has been removed by comparing the module ids at the same index in both list
						else if (emittersRadiation[i].emitterID != emitter.ModuleId)
						{
							emittersRadiation[i] = new EmittersRadiation(emitter.ModuleId, GetRadiationFromEmitter(emitter));
						}
						// Otherwise, update a single emitter per call to have performance being part count agnostic.
						// Doing this ensure accurate results in case the vessel geometry changes (robotics...)
						else if (nextEmitterToUpdate == i)
						{
							emittersRadiation[i] = new EmittersRadiation(emitter.ModuleId, GetRadiationFromEmitter(emitter));
							nextEmitterToUpdate = (nextEmitterToUpdate + 1) % knownEmittersCount;
						}
					}

					if (emitter.IsActive)
					{
						radiationRate += emittersRadiation[i].radiation;
					}
				}

				// In case emitters were removed from the vessel, trim the list
				if (knownEmittersCount > vesselEmittersCount)
				{
					emittersRadiation.RemoveRange(vesselEmittersCount - 1, knownEmittersCount - vesselEmittersCount);
				}

				UnityEngine.Profiling.Profiler.EndSample();

				// add storm radiation, if there is a storm
				if (partData.vesselData.EnvStorm)
				{
					radiationRate += partData.vesselData.EnvStormRadiation * sunRadiationFactor;
				}

				// then substract magnetic shields effects
				foreach (CoilArrayShielding arrayData in radiationCoilArrays)
				{
					radiationRate -= arrayData.RadiationRemoved;
				}

				// clamp to nominal
				radiationRate = Math.Max(radiationRate, Radiation.Nominal);

				// accumulate total radiation received by this part
				accumulatedRadiation += radiationRate * elapsedSecSinceLastUpdate;
			}

			elapsedSecSinceLastUpdate = 0.0;

			UnityEngine.Profiling.Profiler.EndSample();

			if (!debug && partData.vesselData.LoadedOrEditor)
			{
				debug = true;

				if (partData.vesselData.LoadedOrEditor && IsReceiver)
				{
					partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(radiationRate)), this));
					partData.LoadedPart.Fields[nameof(radiationRate)].guiName = "Radiation";
					partData.LoadedPart.Fields[nameof(radiationRate)].guiFormat = "F7";
					partData.LoadedPart.Fields[nameof(radiationRate)].guiUnits = "rad/s";

					partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(sunRadiationFactor), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
					partData.LoadedPart.Fields[nameof(sunRadiationFactor)].guiFormat = "F2";
				}

				//partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(radiationRate)), this));
				//partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(accumulatedRadiation)), this));
				//partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(sunRadiationFactor), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
				//partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(bbSurface), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
				//partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(bbThickness), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
				//partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), intrinsicOcclusion.GetType().GetField("thickness", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), intrinsicOcclusion));
			}
		}

		public void GetOccluderStats()
		{
			// TODO : Those BB-based approximations will likely result in over-occlusion from "non-compact" shaped parts.
			// And using the model based volume/surface methods has drawbacks :
			// - performance impact on the first KSP launch
			// - doesn't work for ingame shape-changing parts : mesh-switched, procedural or tweakscaled.
			// an alternate real-time friendly solution could be to use the drag cube surface,
			// and linearly scaling down the bb derived thickness by comparing the bb surface and the drag cube surface.
			Bounds partBounds = Lib.GetPartBounds(partData.PartPrefab);
			// part thickness is approximated as the length of the cube whose volume is the voume of the cylinder fitting in it's bounding box volume.
			bbThickness = Mathf.Pow((float)(Lib.BoundsVolume(partBounds) * Lib.boundsCylinderVolumeFactor), 1f / 3f);
			// same for surface
			bbSurface = Lib.BoundsSurface(partBounds) * Lib.boundsCylinderSurfaceFactor;

			// thickness = volume / surface

			// wall thickness is approximated as the length of a cube, where the cube volume
			// assumption : half the part structural mass is "walls" of ~ aluminium density
			float wallThickness = Mathf.Pow((partData.PartPrefab.mass * 0.5f) / 2.7f, 1f / 3f) / (float)bbSurface;

			if (intrinsicOcclusion == null)
			{
				intrinsicOcclusion = new OccluderMaterial();
			}
			
			intrinsicOcclusion.Set(wallThickness, Radiation.aluminiumHVL_Gamma1MeV, Radiation.aluminiumHVL_Gamma25MeV);
		}

		private void UpdateOcclusionStats()
		{
			int listCapacity = resourcesOcclusion.Count;
			int occluderIndex = -1;

			for (int i = 0; i < partData.LoadedPart.Resources.Count; i++)
			{
				PartResource res = partData.LoadedPart.Resources[i];

				if (res.amount < 1e-06 || res.info.density == 0.0)
					continue;

				occluderIndex++;
				if (occluderIndex >= listCapacity)
				{
					resourcesOcclusion.Add(new OccluderMaterial());
					listCapacity++;
				}
					
				double resVolume = (res.amount * res.info.volume) / 1000.0;
				if (Radiation.shieldingResources.TryGetValue(res.info.id, out Radiation.ResourceOcclusion occlusionData))
				{
					double thickness = occlusionData.onPartWalls
						? (resVolume / bbSurface) * 2.0 // the resource is a material "spread out" on the part surface
						: Math.Pow(resVolume, 1.0 / 3.0); // the resource is a solid cube stored in the part center
					resourcesOcclusion[occluderIndex].Set(thickness, occlusionData.lowHVL, occlusionData.highHVL);
				}
				else
				{
					double thickness = Math.Pow(resVolume, 1.0 / 3.0);
					resourcesOcclusion[occluderIndex].Set(thickness, res.info.RealDensity());
				}
			}

			while (listCapacity > occluderIndex + 1)
			{
				resourcesOcclusion.RemoveAt(listCapacity - 1);
				listCapacity--;
			}
		}

		private double RemainingRadiation(double initialRadiation, double thicknessFactor, bool highPowerRad)
		{
			initialRadiation *= intrinsicOcclusion.RadiationFactor(thicknessFactor, highPowerRad);

			foreach (OccluderMaterial occluder in resourcesOcclusion)
			{
				initialRadiation *= occluder.RadiationFactor(thicknessFactor, highPowerRad);
			}
			return initialRadiation;
		}

		private double GetSunRadiationFactor(Vector3 sunDirection)
		{
			Vector3 rayOrigin = partData.LoadedPart.transform.position;
			Vector3 rayDir = sunDirection;

			occluders.Clear();

			foreach (PartRadiationData occluderData in partData.vesselData.PartCache.RadiationOccluders)
			{
				if (occluderData == this)
					continue;

				Vector3 occluderPosition = occluderData.partData.LoadedPart.transform.position;
				if (!Lib.RaySphereIntersectionFloat(rayOrigin, rayDir, occluderPosition, occluderData.bbThickness * 0.5f, out Vector3 hitPosition, out float differenceLengthSquared))
					continue;

				// get the shortest distance between the ray hit position and the axis between the emitter and occluder
				float hitDistanceSqr = Lib.RayPointDistanceSquared(rayOrigin, (rayOrigin - occluderPosition).normalized, hitPosition);
				// based on the part half-thickness, determine a factor for how much the ray deviate from the axis
				float deviation = hitDistanceSqr / (occluderData.bbThickness * occluderData.bbThickness * 0.25f);
				// then get a factor for how much that impact that occluder ability to block incoming radiation
				float thicknessFactor = 1f - deviation;
				// just ignore occluders whose hit deviation was more than the half-thickness (can happen due to fp errors)
				if (thicknessFactor <= 0f)
					continue;

				occluders.Add(new SunOccluder(occluderData, thicknessFactor, differenceLengthSquared));
			}

			if (occluders.Count == 0)
				return 1.0;

			// sort by distance, in reverse
			occluders.Sort((a, b) => b.distanceSqr.CompareTo(a.distanceSqr));

			double remaining = 1.0;
			double factor = 0.0;
			foreach (SunOccluder occluder in occluders)
			{
				double bremsstrahlung = remaining - occluder.occluderData.RemainingRadiation(remaining, occluder.thicknessFactor, true);
				remaining -= bremsstrahlung;
				factor += Radiation.DistanceSqrRadiation(bremsstrahlung, occluder.distanceSqr);

				if (remaining < Radiation.Nominal)
				{
					remaining = 0.0;
					break;
				}
			}
			factor += remaining;

			// since this is directional, only account for half the part thickness
			if (IsOccluder)
				factor = RemainingRadiation(factor, 0.5, true);

			return factor;
		}

		private double GetRadiationFromEmitter(IRadiationEmitter emitter)
		{
			Vector3 rayOrigin = partData.LoadedPart.transform.position;
			Vector3 emitterPos = emitter.RadiationData.partData.LoadedPart.transform.position;
			Vector3 rayDir = emitterPos - rayOrigin;
			double radiation = Radiation.DistanceSqrRadiation(emitter.RadiationRate, rayDir.sqrMagnitude);
			rayDir = rayDir.normalized;

			foreach (PartRadiationData occluderData in partData.vesselData.PartCache.RadiationOccluders)
			{
				// TODO : special handling for "this", occlusion should matter but using only half the part thickness
				if (occluderData == this)
					continue;

				Vector3 occluderPosition = occluderData.partData.LoadedPart.transform.position;
				// TODO : we might need to use a special version of that method that still resturn true
				// when the ray origin is inside the sphere. Example : for a pod with a heat shield attached, the heat shield
				// sphere will likely enclose the pod position, but it is still a valid occluder.
				if (!Lib.RaySphereIntersectionFloat(rayOrigin, rayDir, occluderPosition, occluderData.bbThickness * 0.5f, out Vector3 hitPosition))
					continue;

				// get the shortest distance between the ray hit position and the axis between the emitter and occluder
				float hitDistanceSqr = Lib.RayPointDistanceSquared(rayOrigin, rayDir, hitPosition);
				// based on the part half-thickness, determine a factor for how much the ray deviate from the axis
				float deviation = hitDistanceSqr / (occluderData.bbThickness * occluderData.bbThickness * 0.25f);
				// then get a factor for how much that impact that occluder ability to block incoming radiation
				float thicknessFactor = 1f - deviation;
				// just ignore occluders whose hit deviation was more than the half-thickness (can happen due to fp errors)
				if (thicknessFactor <= 0f)
					continue;

				radiation = occluderData.RemainingRadiation(radiation, thicknessFactor, false);

				if (radiation < Radiation.Nominal)
					return 0.0;
			}

			return radiation;
		}

		#endregion
	}
}
