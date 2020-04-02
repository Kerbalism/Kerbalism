using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public class RadiationCoilArray
	{
		// Vector dot product thresholds used to check if coils are at same "height"
		// This is only useful for the initial array creation search.
		// For the update check, the "distance to center" check will exclude
		// any coil that would deviate to much along the long axis
		private const float minCoplanarDot = -0.025f;
		private const float maxCoplanarDot = 0.025f;

		// Vector dot product threshold for the parallelism between the cylinder
		// axis and each coil up vector.
		private const float minParallelismDot = 0.95f;

		// Vector dot product threshold for checking if a coil is pointing
		// toward the mean center of all the other considered coils positions
		private const float minCenterAlignementDot = 0.98f;

		// position threshold used to check if the coils are evenly disposed
		// in a a proper radial configuration, and if they are a the same height
		private const float maxDistanceError = 0.1f;

		// variables determined at the array creation
		public List<RadiationCoilData> coils = new List<RadiationCoilData>();
		public RadiationCoilData masterCoil;
		private Transform masterTransform;
		private float cylinderLength;
		private float optimalDiameter;
		private float radiusOffset;
		private double radiationRemovedAtOptimalDiameter;
		private GameObject capsule;
		private Transform cylinder;
		private Transform sphereBottom;
		private Transform sphereTop;

		// variables reevaluated constantly on loaded vessels
		private Vector3 capsuleWorldCenter;
		private Quaternion capsuleWorldRotation;
		private float diameter = -1f;
		private float radiusSqr;
		private float cylinderLengthSqr;
		private Vector3 topToBottom;
		public double radiationRemoved;
		private bool isDeployed;

		// state
		public bool running;
		private string chargeID = Guid.NewGuid().ToString();
		public VesselVirtualResource charge;


		public RadiationCoilArray(ModuleKsmRadiationCoil masterModule)
		{
			coils.Add(masterModule.moduleData);
			masterCoil = masterModule.moduleData;
			masterTransform = masterModule.transform;
			cylinderLength = masterModule.effectLength;
			cylinderLengthSqr = Mathf.Sqrt(cylinderLength);
			optimalDiameter = masterModule.optimalDistance;
			radiationRemovedAtOptimalDiameter = masterModule.radiationRemoved;
			radiusOffset = masterModule.effectRadiusOffset;
		}

		public static bool FindArray(ModuleKsmRadiationCoil module)
		{
			RadiationCoilArray array = new RadiationCoilArray(module);

			List<Part> parts;
			if (Lib.IsEditor)
				parts = EditorLogic.fetch.ship.parts;
			else
				parts = module.vessel.parts;

			Vector3 capsuleCenterWorld = Vector3.zero;
			foreach (Part otherPart in parts)
			{
				foreach (PartModule partModule in otherPart.Modules)
				{
					if (partModule is ModuleKsmRadiationCoil otherModule)
					{
						if (otherModule == module)
						{
							capsuleCenterWorld += otherModule.transform.position;
							break;
						}
							
						if (otherModule.effectLength != array.cylinderLength
							|| otherModule.optimalDistance != array.optimalDiameter
							|| otherModule.radiationRemoved != array.radiationRemovedAtOptimalDiameter)
							break;

						Vector3 dirToOtherCoil = otherModule.transform.position - array.masterTransform.position;

						// dot product between the coil long axis and the direction to the other coil
						// coils are in the same array only if they are more or less coplanar
						float coplanarDot = Vector3.Dot(dirToOtherCoil.normalized, array.masterTransform.up);
						if (coplanarDot < minCoplanarDot || coplanarDot > maxCoplanarDot)
						{
							Lib.LogDebug($"Excluding non-coplanar coil, dot={coplanarDot.ToString("F3")}");
							break;
						}

						float parallelismDot = Vector3.Dot(otherModule.transform.up, array.masterTransform.up);
						if (parallelismDot < minParallelismDot)
						{
							Lib.LogDebug($"Excluding non-parallel coil, dot={parallelismDot.ToString("F3")}");
							break;
						}

						array.coils.Add(otherModule.moduleData);
						capsuleCenterWorld += otherModule.transform.position;
						break;
					}
				}
			}

			if (array.coils.Count < 2)
				return false;

			// We now have a first batch of candidates that are on the same plane and with the same
			// vertical orientation. But we also need to check that they are at the same distance from 
			// the center of that preleminary array, and that they are evenly disposed around it.
			// That's why this is a two-loop operation, because we need a preleminary center to check
			// against.
			capsuleCenterWorld /= array.coils.Count;
			float radius = (capsuleCenterWorld - array.masterTransform.position).magnitude;

			foreach (RadiationCoilData coil in array.coils)
			{
				Vector3 toCenter = capsuleCenterWorld - coil.loadedModule.transform.position;
				if (Vector3.Dot(toCenter.normalized, coil.loadedModule.transform.forward) < minCenterAlignementDot)
				{
					Lib.LogDebug($"Excluding coil with bad radial alignement, dot={Vector3.Dot(toCenter.normalized, coil.loadedModule.transform.forward).ToString("F3")}");
					return false;
				}

				if (Math.Abs(toCenter.magnitude - radius) > maxDistanceError)
				{
					Lib.LogDebug($"Excluding unevenly placed coil, distance from center={toCenter.magnitude.ToString("F3")}, average distance={radius.ToString("F3")}");
					return false;
				}
			}

			// And a third loop because we don't want to set moduledatas references
			// if the array is rejected.
			foreach (RadiationCoilData coil in array.coils)
			{
				coil.array = array;
				coil.isMaster = false;
			}

			array.masterCoil.isMaster = true;
			array.radiationRemovedAtOptimalDiameter *= array.coils.Count;

			array.InstantiateCapsule();
			array.UpdateCapsule();

			return true;
		}

		public bool VerifyCoilsPosition()
		{
			Vector3 capsuleCenterWorld = Vector3.zero;
			foreach (RadiationCoilData coil in coils)
			{
				capsuleCenterWorld += coil.loadedModule.transform.position;

				if (coil == masterCoil)
					continue;

				// dot product between the coil long axis and the direction to the other coil
				// coils are in the same array only if they are more or less coplanar
				float parallelismDot = Vector3.Dot(coil.loadedModule.transform.up, masterTransform.up);
				if (parallelismDot < minParallelismDot)
				{
					Lib.LogDebug($"Excluding non-parallel coil, dot={parallelismDot.ToString("F3")}");
					return false;
				}
			}

			capsuleCenterWorld /= coils.Count;
			float radius = (capsuleCenterWorld - masterTransform.position).magnitude;

			foreach (RadiationCoilData coil in coils)
			{
				Vector3 toCenter = capsuleCenterWorld - coil.loadedModule.transform.position;
				if (Vector3.Dot(toCenter.normalized, coil.loadedModule.transform.forward) < minCenterAlignementDot)
				{
					Lib.LogDebug($"Excluding misaligned coil, dot={Vector3.Dot(toCenter.normalized, coil.loadedModule.transform.forward).ToString("F3")}");
					return false;
				}

				if (Math.Abs(toCenter.magnitude - radius) > maxDistanceError)
				{
					Lib.LogDebug($"Excluding uneven placed coil, distance from center={toCenter.magnitude.ToString("F3")}, average distance={radius.ToString("F3")}");
					return false;
				}
			}

			return true;
		}

		public void SetVisible(bool visible)
		{
			capsule.SetActive(visible);
		}

		public void Destroy()
		{
			if (capsule != null)
			{
				capsule.DestroyGameObject();
			}
			
			foreach (RadiationCoilData coil in coils)
			{
				coil.array = null;
				coil.isMaster = false;
			}
		}

		private void InstantiateCapsule()
		{
			capsule = UnityEngine.Object.Instantiate(GameDatabase.Instance.GetModel("Kerbalism/Models/RadiationCapsuleEffect"));
			capsule.transform.SetParent(null);
			capsule.SetActive(true);
			foreach (Transform transform in capsule.GetComponentsInChildren<Transform>())
			{
				switch (transform.name)
				{
					case "cylinder": cylinder = transform; break;
					case "sphereBottom": sphereBottom = transform; break;
					case "sphereTop": sphereTop = transform; break;
				}
			}
		}

		public void UpdateCapsule()
		{
			capsuleWorldCenter = Vector3.zero;
			Vector3 capsuleAxisWorld = Vector3.zero;
			isDeployed = true;
			foreach (RadiationCoilData coil in coils)
			{
				capsuleWorldCenter += coil.loadedModule.transform.position;
				capsuleAxisWorld += coil.loadedModule.transform.up;
				isDeployed &= coil.isDeployed;
			}
			capsuleWorldCenter /= coils.Count;
			capsuleAxisWorld /= coils.Count;

			diameter = ((capsuleWorldCenter - masterTransform.position).magnitude + radiusOffset) * 2f;
			radiusSqr = Mathf.Sqrt(diameter * 0.5f);
			radiationRemoved = radiationRemovedAtOptimalDiameter * (optimalDiameter / diameter);

			capsuleWorldRotation = Quaternion.LookRotation(masterTransform.forward, capsuleAxisWorld);

			capsule.transform.position = capsuleWorldCenter;
			capsule.transform.rotation = capsuleWorldRotation; //Quaternion.Euler(90f, 0f, 0f);

			cylinder.localScale = new Vector3(diameter, cylinderLength, diameter);
			sphereBottom.localPosition = new Vector3(0f, cylinderLength * -0.5f, 0f);
			sphereBottom.localScale = new Vector3(diameter, diameter, diameter);
			sphereTop.localPosition = new Vector3(0f, cylinderLength * 0.5f, 0f);
			sphereTop.localScale = new Vector3(diameter, diameter, diameter);
			topToBottom = sphereBottom.position - sphereTop.position;
		}

		public double IsPartProtected(Part part)
		{
			int pointCount = 1;
			int protectedPointCount = 0;

			if (IsPointInCapsule(part.transform.position))
			{
				protectedPointCount++;
			}

			if (part.attachNodes.Count > 0)
			{
				foreach (AttachNode node in part.attachNodes)
				{
					if (node.nodeType == AttachNode.NodeType.Stack)
					{
						pointCount++;
						if (IsPointInCapsule(node.nodeTransform.position))
						{
							protectedPointCount++;
						}

					}
				}
			}

			return radiationRemoved * ((double)protectedPointCount / (double)pointCount);
		}

		private bool IsPointInCapsule(Vector3 point)
		{
			// First, check against the two spheres, that's the fastest
			// If the distance between the point and the sphere center is less
			// than the sphere radius, then the point is inside the sphere.
			if ((point - sphereTop.position).sqrMagnitude <= radiusSqr)
				return true;

			if ((point - sphereBottom.position).sqrMagnitude <= radiusSqr)
				return true;

			if (PointInCylinder(sphereTop.position, topToBottom, cylinderLengthSqr, radiusSqr, point) >= 0f)
				return true;

			return false;
		}

		// if point is inside cylinder, return distance squared from the cylinder axis to the the point
		// else return -1f;
		private float PointInCylinder(Vector3 cylTop, Vector3 topToBottom, float lengthSqr, float radiusSqr, Vector3 point)
		{
			Vector3 topToPoint = point - cylTop;

			// Dot the d and pd vectors to see if point lies behind the cylinder cap
			float dot = topToPoint.x * topToBottom.x + topToPoint.y * topToBottom.y + topToPoint.z * topToBottom.z;

			// If dot is less than zero the point is behind the cylTop cap.
			// If greater than the cylinder axis line segment length squared
			// then the point is outside the other end cap at bottom.
			if (dot < 0.0f || dot > lengthSqr )
			{
				return -1.0f;
			}
			else 
			{
				// Point lies within the parallel caps, so find
				// distance squared from point to cylinder axis, using the fact that sin^2 + cos^2 = 1
				float distanceSqr = (topToPoint.x * topToPoint.x + topToPoint.y * topToPoint.y + topToPoint.z * topToPoint.z) - dot * dot / lengthSqr;

				if (distanceSqr > radiusSqr)
				{
					return -1.0f;
				}
				else
				{
					return distanceSqr;     // return distance squared to axis
				}
			}
		}
	}

	public class RadiationCoilData : ModuleData<ModuleKsmRadiationCoil, RadiationCoilData>
	{
		public RadiationCoilArray array;
		public bool isMaster;
		public bool isDeployed;
	}

	public class ModuleKsmRadiationCoil : KsmPartModule<ModuleKsmRadiationCoil, RadiationCoilData>
	{
		[KSPField] public float effectLength = 1f;
		[KSPField] public float effectRadiusOffset = 0.1f;
		[KSPField] public float optimalDistance = 2f;
		[KSPField] public double radiationRemoved;
		[KSPField] public double maxAirPressureAtm;
		[KSPField] public double ecChargeRequired;
		[KSPField] public double ecChargeRate;
		[KSPField] public double ecRunningRate;
		[KSPField] public string deployAnim;
		[KSPField] public bool deployAnimReverse;


		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Effect", groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public string effectInfo; // Effect: -1.2 rad/h (8 coils)
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Charging", groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public string chargeInfo; // Charging: 478.00 kEC needed / Charge keeping: -4.2 EC/s

		private Animator deployAnimator;

		public override void OnStart(StartState state)
		{
			part.OnEditorDetach += OnDetach;
			part.AddOnMouseEnter(PartOnMouseEnter);
			part.AddOnMouseExit(PartOnMouseExit);

			deployAnimator = new Animator(part, deployAnim, deployAnimReverse);
		}

		private void PartOnMouseExit(Part p)
		{
			if (moduleData.array == null)
				return;

			moduleData.array.SetVisible(false);

			foreach (RadiationCoilData data in moduleData.array.coils)
			{
				data.loadedModule.part.Highlight(false);
			}
		}

		private void PartOnMouseEnter(Part p)
		{
			if (moduleData.array == null)
				return;

			moduleData.array.SetVisible(true);

			foreach (RadiationCoilData data in moduleData.array.coils)
			{
				data.loadedModule.part.Highlight(Color.blue);
			}
		}

		public override void OnDestroy()
		{
			OnDetach();
			part.OnEditorDetach -= OnDetach;
			part.RemoveOnMouseEnter(PartOnMouseEnter);
			part.RemoveOnMouseExit(PartOnMouseExit);
			base.OnDestroy();
		}

		private void OnDetach()
		{
			if (moduleData != null && moduleData.array != null)
			{
				moduleData.array.Destroy();
			}
		}

		private void Update()
		{
			if (moduleData.array != null)
			{
				if (moduleData.isMaster)
				{
					if (!moduleData.array.VerifyCoilsPosition())
					{
						moduleData.array.Destroy();
						Lib.LogDebug("coils position is invalid");
					}
					else
					{
						moduleData.array.UpdateCapsule();
					}
				}
			}

			if (moduleData.array != null)
			{
				// Effect: -1.2 rad/h (8 coils)
				effectInfo = Lib.BuildString(
					"-", Lib.HumanReadableRadiation(moduleData.array.radiationRemoved, false, false),
					" (", moduleData.array.coils.Count.ToString(), " ", "coils", ")");

				// Charge: 478.00 kEC needed / Charge : -4.2 EC/s
				chargeInfo = Lib.BuildString(Lib.HumanReadableAmountCompact(ecChargeRequired), " EC needed"); 
			}
		}





		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Deploy coil", active = true, groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public void Deploy()
		{
			deployAnimator.Play(moduleData.isDeployed, false, OnDeploy, Lib.IsEditor ? 5f : 1f);
		}

		private void OnDeploy()
		{
			if (!moduleData.isDeployed)
			{
				RadiationCoilArray.FindArray(this);

				if (moduleData.array != null)
				{
					foreach (RadiationCoilData other in moduleData.array.coils)
					{
						if (other == moduleData)
							continue;

						if (!other.isDeployed)
						{
							other.loadedModule.deployAnimator.Play(false, false, null, Lib.IsEditor ? 5f : 1f);
							other.isDeployed = true;
						}
					}
				}
			}

			moduleData.isDeployed = !moduleData.isDeployed;

		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Connect coil array", active = true, groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public void Check()
		{
			if (moduleData.array != null)
				moduleData.array.Destroy();

			RadiationCoilArray.FindArray(this);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Activate coil array", active = true, groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public void Activate()
		{
			if (moduleData.array == null)
				RadiationCoilArray.FindArray(this);

			if (moduleData.array == null)
				return;

			moduleData.array.running = true;

		}



	}
}
