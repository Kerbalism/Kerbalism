using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public partial class PartRadiationData
	{
		// Note : all fields here are temporary variables used for raycasting.
		// They are here for convenience, to avoid heap allocs and creating separate objects

		// optimization to avoid keeping computing radiation levels that don't matter
		private const double minFactor = 1e-9;

		// the hit point closest from rayOrigin
		private RaycastHit fromHit;
		private bool fromHitExists = false;
		// dot product between the ray and the surface normal (cosinus of the angle)
		private double fromHitNormalDot;

		// the hit point farthest from rayOrigin
		private RaycastHit toHit;
		private bool toHitExists = false;
		// dot product between the ray and the surface normal (cosinus of the angle)
		private double toHitNormalDot;

		// distance between the two hit points
		private double hitPenetration;

		/// <summary>
		/// Compute penetration and ray impact angle
		/// </summary>
		private void AnalyzeRaycastHit(Vector3 rayDir)
		{
			fromHitNormalDot = Math.Abs(Vector3.Dot(rayDir, fromHit.normal));
			toHitNormalDot = Math.Abs(Vector3.Dot(rayDir, toHit.normal));
			hitPenetration = (fromHit.point - toHit.point).magnitude;
		}

		/// <summary>
		/// Reset the "occluder has hits" flags.
		/// **Must** be called for every part that has been added to the occluder list after a OcclusionRaycast() call
		/// </summary>
		private void ResetRaycastHit()
		{
			toHitExists = false;
			fromHitExists = false;
		}

		public abstract class RaycastTask
		{
			protected PartRadiationData origin;

			public RaycastTask(PartRadiationData origin)
			{
				this.origin = origin;

				if (layerMask < 0)
				{
					layerMask = LayerMask.GetMask(new string[] { "Default" });
				}
			}

			public virtual void Raycast(RaycastTask nextTask)
			{
				if (nextTask == null || nextTask.origin != origin)
				{
					origin.raycastDone = true;
				}
			}

			// TODO : call this on scene changes
			public static void ClearLoadedPartsCache()
			{
				prdByTransforms.Clear();
			}

			private static RaycastHit[] hitsBuffer = new RaycastHit[500];

			private static Dictionary<int, PartRadiationData> prdByTransforms = new Dictionary<int, PartRadiationData>();

			private static int layerMask = -1;

			protected static List<PartRadiationData> hittedParts = new List<PartRadiationData>();

			/// <summary>
			/// Get the PartRadiationData that this transform belong to. If the transform isn't cached, return false.
			/// If the part doesn't exist anymore (unloaded, destroyed...), clean the cache.
			/// </summary>
			private static bool TryGetRadiationDataForTransformCached(Transform transform, out PartRadiationData partRadiationData)
			{
				if (prdByTransforms.TryGetValue(transform.GetInstanceID(), out partRadiationData))
				{
					if (partRadiationData.partData.LoadedPart.gameObject == null)
					{
						prdByTransforms.Remove(transform.GetInstanceID());
						return false;
					}
					return true;
				}
				return false;
			}

			/// <summary>
			/// Get the PartRadiationData that this transform belong to, and store it in the static cache.
			/// </summary>
			private static bool TryGetRadiationDataForTransform(Transform transform, out PartRadiationData partRadiationData)
			{
				Part hittedPart = transform.GetComponentInParent<Part>();
				if (hittedPart == null)
				{
					partRadiationData = null;
					return false;
				}

				partRadiationData = PartData.GetLoadedPartData(hittedPart).radiationData;
				prdByTransforms.Add(transform.GetInstanceID(), partRadiationData);
				return true;
			}

			/// <summary>
			/// Perform a bidirectional raycast to get every occluder along rayDir.
			/// This allow to compute the penetration depth inside each occluder, as well as the angle of impact.
			/// </summary>
			/// <param name="rayOrigin">Position of the part that we want to get occlusion for</param>
			/// <param name="rayDir">Normalized direction vector</param>
			protected static void OcclusionRaycast(Vector3 rayOrigin, Vector3 rayDir)
			{
				hittedParts.Clear();

				// raycast from the origin part in direction of the ray
				int hitCount = Physics.RaycastNonAlloc(rayOrigin, rayDir, hitsBuffer, 250f, layerMask);
				for (int i = 0; i < hitCount; i++)
				{
					// if the transform is known, fetch the corresponding occluding part
					if (TryGetRadiationDataForTransformCached(hitsBuffer[i].transform, out PartRadiationData partRadiationData))
					{
						if (!partRadiationData.IsOccluder)
						{
							continue;
						}

						// if the occluding part isn't marked as occluding for this raycast, save the hit and add the part to the occluder list
						if (!partRadiationData.fromHitExists)
						{
							partRadiationData.fromHitExists = true;
							partRadiationData.fromHit = hitsBuffer[i];
							hittedParts.Add(partRadiationData);
						}
						// if we are getting another hit for the same occluding part (multiple colliders), retain the hit that is the closest from the origin part
						else if (hitsBuffer[i].distance < partRadiationData.fromHit.distance)
						{
							partRadiationData.fromHit = hitsBuffer[i];
						}
					}
					// else find the occluding part that match this transform (and add it to the cache for next time)
					else
					{
						if (!TryGetRadiationDataForTransform(hitsBuffer[i].transform, out partRadiationData))
						{
							continue;
						}

						if (!partRadiationData.IsOccluder)
						{
							continue;
						}

						partRadiationData.fromHit = hitsBuffer[i];
						hittedParts.Add(partRadiationData);
					}
				}

				// now raycast in the opposite direction, toward the origin part
				hitCount = Physics.RaycastNonAlloc(rayOrigin + rayDir * 250f, -rayDir, hitsBuffer, 250f, layerMask);
				for (int i = 0; i < hitCount; i++)
				{
					// if the transform is known, fetch the corresponding occluding part
					if (TryGetRadiationDataForTransformCached(hitsBuffer[i].transform, out PartRadiationData partRadiationData))
					{
						// if the occuding part hasn't been hit on the first raycast, ignore it
						if (!partRadiationData.IsOccluder || !partRadiationData.fromHitExists)
						{
							continue;
						}

						// same logic as in the first raycast
						if (!partRadiationData.toHitExists)
						{
							partRadiationData.toHitExists = true;
							partRadiationData.toHit = hitsBuffer[i];
						}
						else if (hitsBuffer[i].distance < partRadiationData.fromHit.distance)
						{
							partRadiationData.toHit = hitsBuffer[i];
						}
					}
					else
					{
						if (!TryGetRadiationDataForTransform(hitsBuffer[i].transform, out partRadiationData))
						{
							continue;
						}

						if (!partRadiationData.IsOccluder || !partRadiationData.fromHitExists)
						{
							continue;
						}

						partRadiationData.toHit = hitsBuffer[i];

					}
				}

				// sort by distance, in reverse
				hittedParts.Sort((a, b) => b.toHit.distance.CompareTo(a.toHit.distance));
			}
		}

		private class SunRaycastTask : RaycastTask
		{
			public double sunRadiationFactor = 1.0;

			public SunRaycastTask(PartRadiationData origin) : base(origin) { }

			public override void Raycast(RaycastTask nextTask)
			{
				base.Raycast(nextTask);

				Vector3 sunDirection = origin.partData.vesselData.MainStarDirection;
				OcclusionRaycast(origin.partData.LoadedPart.WCoM, sunDirection);

				// Explaination :
				// When high energy charged particules from CME events hit a solid surface, three things happen :
				// - the charged particules loose some energy
				// - its travelling direction is slightly altered
				// - secondary particules (photons) are emitted
				// Those secondary particules are called bremsstrahlung radiation.
				// For medium to high energy (> 10 MeV) particules, the vast majority of bremsstrahlung is
				// emitted in same direction as the CME particules.
				// For our purpose, we use a very simplified model where we assume that all the blocked CME radiation
				// is converted into bremmstralung radiation in same direction as the original CME and with a 20° dispersion.

				// Note that we are computing a [0;1] factor here, not actual radiation level.
				double cmeRadiation = 1.0;
				double bremsstrahlung = 0.0;

				foreach (PartRadiationData prd in hittedParts)
				{
					// optimization to avoid keeping computing radiation levels that don't matter
					if (cmeRadiation + bremsstrahlung < minFactor || prd == origin)
					{
						prd.ResetRaycastHit();
						continue;
					}

					prd.AnalyzeRaycastHit(sunDirection);

					// get the high energy radiation that is blocked by the part, using "high energy" HVL.
					double partBremsstrahlung = cmeRadiation - prd.RemainingRadiation(cmeRadiation, true);

					// get the remaining high energy radiation
					cmeRadiation -= partBremsstrahlung;

					// get the remaining bremsstrahlung that hasn't been blocked by the part, using "low energy" HVL.
					bremsstrahlung = prd.RemainingRadiation(bremsstrahlung, false);

					// add the bremsstrahlung created by the CME radiation hitting the part
					// Assumption : the bremsstrahlung is emitted in the same direction as the original CME radiation, in a 20° cone
					double sqrDistance = (prd.toHit.point - origin.partData.LoadedPart.WCoM).sqrMagnitude;
					bremsstrahlung += partBremsstrahlung / Math.Max(1.0, 0.222 * Math.PI * sqrDistance);

					prd.ResetRaycastHit();
				}

				// factor in the origin part wall shielding
				cmeRadiation = origin.RemainingRadiation(cmeRadiation, true, true);
				bremsstrahlung = origin.RemainingRadiation(bremsstrahlung, false, true);

				sunRadiationFactor = Math.Max(cmeRadiation + bremsstrahlung, 0.0);
			}
		}

		private class EmitterRaycastTask : RaycastTask
		{
			private IRadiationEmitter emitter;

			private double reductionFactor = 0.0;
			private int emitterId;

			public EmitterRaycastTask(PartRadiationData origin, IRadiationEmitter emitter) : base(origin)
			{
				this.emitter = emitter;
				emitterId = emitter.ModuleId;
			}

			public EmitterRaycastTask(PartRadiationData origin, ConfigNode.Value value) : base(origin)
			{
				emitterId = Lib.Parse.ToInt(value.name);
				reductionFactor = Lib.Parse.ToDouble(value.value);
			}

			public void SaveToNode(ConfigNode emittersNode)
			{
				emittersNode.AddValue(emitterId.ToString(), reductionFactor.ToString());
			}

			/// <summary>
			/// To avoid creating/destructing objects when synchronizing the EmitterRaycastTask list in PartRadiationData,
			/// we just swap the emitter reference of the existing EmitterRaycastTask
			/// </summary>
			public void CheckEmitterHasChanged(IRadiationEmitter otherEmitter)
			{
				if (otherEmitter.ModuleId != emitterId)
				{
					emitter = otherEmitter;
					emitterId = otherEmitter.ModuleId;
					reductionFactor = 0.0;
				}
				else if (emitter == null)
				{
					emitter = otherEmitter;
				}
			}

			public double Radiation
			{
				get
				{
					if (emitter != null && emitter.IsActive)
					{
						return emitter.RadiationRate * reductionFactor;
					}

					return 0.0;
				}
			}

			public override void Raycast(RaycastTask nextTask)
			{
				base.Raycast(nextTask);

				Vector3 rayDir = origin.partData.LoadedPart.WCoM - emitter.RadiationData.partData.LoadedPart.WCoM;
				float distance = rayDir.magnitude;

				// compute initial radiation strength according to the emitter distance
				reductionFactor = KERBALISM.Radiation.DistanceRadiation(1.0, distance);
				rayDir /= distance;

				OcclusionRaycast(origin.partData.LoadedPart.WCoM, rayDir);

				foreach (PartRadiationData prd in hittedParts)
				{
					// optimization to avoid keeping computing radiation levels that don't matter
					// also make sure we ignore the origin part
					if (reductionFactor < minFactor || prd == origin)
					{
						prd.ResetRaycastHit();
						continue;
					}

					prd.AnalyzeRaycastHit(rayDir);
					reductionFactor = prd.RemainingRadiation(reductionFactor, false);
					prd.ResetRaycastHit();
				}

				// factor in the origin part wall shielding
				reductionFactor = origin.RemainingRadiation(reductionFactor, false, true);
			}
		}
	}
}
