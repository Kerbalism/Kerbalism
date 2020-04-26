using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public class RadiationCoilData : ModuleData<ModuleKsmRadiationCoil, RadiationCoilData>
	{
		public class ArrayEffectData
		{
			public string chargeId;
			public PartVirtualResource charge;
			public double chargeRate;
			public double maxRadiation;
			public bool charging;
			public bool discharging;

			public double RadiationRemoved => maxRadiation * charge.Level;

			public ArrayEffectData(ModuleKsmRadiationCoil masterModule, List<ModuleKsmRadiationCoil> coilModules)
			{
				chargeId = Guid.NewGuid().ToString();
				charge = new PartVirtualResource(chargeId);
				charge.SetCapacity(0.0);
				masterModule.moduleData.VesselData.ResHandler.AddPartVirtualResource(charge);

				foreach (ModuleKsmRadiationCoil coilModule in coilModules)
				{
					coilModule.moduleData.CreateChargeResource(chargeId);
				}

				chargeRate = coilModules.Count * masterModule.ecChargeRate;
			}

			public ArrayEffectData(ConfigNode coilDataNode)
			{
				chargeId = Lib.ConfigValue(coilDataNode, "chargeId", string.Empty);
				chargeRate = Lib.ConfigValue(coilDataNode, "chargeRate", 0.0);
				maxRadiation = Lib.ConfigValue(coilDataNode, "maxRadiation", 0.0);
				charging = Lib.ConfigValue(coilDataNode, "charging", false);
				discharging = Lib.ConfigValue(coilDataNode, "discharging", false);
			}

			public void Save(ConfigNode coilDataNode)
			{
				coilDataNode.AddValue("chargeId", chargeId);
				coilDataNode.AddValue("chargeRate", chargeRate);
				coilDataNode.AddValue("maxRadiation", maxRadiation);
				coilDataNode.AddValue("charging", charging);
				coilDataNode.AddValue("discharging", discharging);
			}

			public void ChargeUpdate(RadiationCoilData masterCoil, double elapsedSec)
			{
				if (charge.Level > 0.0)
				{
					masterCoil.VesselData.ResHandler.Consume(charge.Name, masterCoil.modulePrefab.chargeLossRate * charge.Level * elapsedSec, ResourceBroker.CoilArray);
				}

				// note : the discharging recipe has to be added first for discharging 
				// to have priority over charging when both are enabled
				if (discharging)
				{
					Recipe dischargingRecipe = new Recipe(ResourceBroker.CoilArray);
					dischargingRecipe.AddInput(charge.Name, chargeRate * elapsedSec);
					dischargingRecipe.AddOutput("ElectricCharge", chargeRate * elapsedSec, false);
					masterCoil.VesselData.ResHandler.AddRecipe(dischargingRecipe);
				}

				if (charging)
				{
					Recipe chargingRecipe = new Recipe(ResourceBroker.CoilArray);
					chargingRecipe.AddInput("ElectricCharge", chargeRate * elapsedSec);
					chargingRecipe.AddOutput(charge.Name, chargeRate * elapsedSec, false);
					masterCoil.VesselData.ResHandler.AddRecipe(chargingRecipe);
				}
			}

		}

		public ArrayEffectData effectData;
		public bool isDeployed;

		public void CreateEffectData(ModuleKsmRadiationCoil masterModule, List<ModuleKsmRadiationCoil> coilModules)
		{
			effectData = new ArrayEffectData(masterModule, coilModules);
		}

		public void UpdateMaxRadiation(double maxRadiation)
		{
			effectData.maxRadiation = maxRadiation;
		}

		public override void OnFixedUpdate(double elapsedSec)
		{
			if (effectData != null)
			{
				effectData.ChargeUpdate(this, elapsedSec);
			}
		}

		public override void OnStart()
		{
			if (effectData != null)
			{
				effectData.charge = (PartVirtualResource)VesselData.ResHandler.GetResource(effectData.chargeId);
			}
		}

		public void CreateChargeResource(string chargeId)
		{
			// create the virtual resource capacity on the part
			PartResourceData chargeRes = new PartResourceData(VesselData, chargeId, 0.0, loadedModule.ecChargeRequired);
			partData.virtualResources.Add(chargeRes);
		}

		public void RemoveChargeResource(string chargeId)
		{
			// remove the virtual resource capacity on the part
			int resIndex = partData.virtualResources.FindIndex(p => p.Resource.Name == chargeId);
			partData.virtualResources[resIndex].Remove();
			partData.virtualResources.RemoveAt(resIndex);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue(nameof(isDeployed), isDeployed);

			if (effectData != null)
			{
				effectData.Save(node);
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			isDeployed = Lib.ConfigValue(node, nameof(isDeployed), false);

			if (node.HasValue("chargeId"))
			{
				effectData = new ArrayEffectData(node);
			}
		}
	}

	public class ModuleKsmRadiationCoil : KsmPartModule<ModuleKsmRadiationCoil, RadiationCoilData>
	{
		private class ArrayEffect
		{
			// Vector dot product thresholds used to check if coils are at same "height"
			// This is only useful for the initial array creation search, since
			// in the capsule update, the "distance to center" check will exclude
			// any coil whose position that would deviate to much along the long axis
			private const float minCoplanarDot = -0.025f;
			private const float maxCoplanarDot = 0.025f;

			// Vector dot product threshold for the parallelism between the cylinder
			// axis and each coil up vector.
			private const float minParallelismDot = 0.95f;

			// Vector dot product threshold for checking if a coil is pointing
			// toward the mean center of all the other considered coils positions
			private const float minCenterAlignementDot = 0.98f;

			// position threshold used to check if the coils are evenly disposed
			// in a proper radial configuration, and if they are a the same height
			private const float maxDistanceError = 0.1f;

			// variables determined at the array creation
			public List<ModuleKsmRadiationCoil> coils = new List<ModuleKsmRadiationCoil>();
			public ModuleKsmRadiationCoil masterCoil;
			//private RadiationCoilData.ArrayEffectData effectData;
			private Transform masterTransform;
			private float cylinderLength;
			private float optimalDiameter;
			private float radiusOffset;
			private GameObject capsule;
			private Transform cylinder;
			private Transform sphereBottom;
			private Transform sphereTop;
			private double radiationRemovedAtOptimalDiameter;

			// variables reevaluated constantly
			private Vector3 capsuleWorldCenter;
			private Quaternion capsuleWorldRotation;
			private float diameter = -1f;
			private float radiusSqr;
			private float cylinderLengthSqr;
			private Vector3 topToBottom;

			public ArrayEffect(ModuleKsmRadiationCoil masterModule)
			{
				masterTransform = masterModule.transform;
				cylinderLength = masterModule.effectLength;
				cylinderLengthSqr = Mathf.Sqrt(cylinderLength);
				optimalDiameter = masterModule.optimalDistance;
				radiationRemovedAtOptimalDiameter = masterModule.radiationRemoved;
				radiusOffset = masterModule.effectRadiusOffset;
			}

			public static bool FindArray(ModuleKsmRadiationCoil masterModule, RadiationCoilData.ArrayEffectData existingEffect = null)
			{
				ArrayEffect effect = new ArrayEffect(masterModule);
				effect.coils.Add(masterModule);
				effect.masterCoil = masterModule;

				List<Part> parts;

				if (Lib.IsEditor)
				{
					parts = EditorLogic.fetch.ship.parts;
				}
				else
				{
					parts = masterModule.vessel.parts;
				}

				if (existingEffect != null)
				{
					List<Part> effectParts = new List<Part>();

					// note : we can't directly check the ModuleData as their LoadedModule references may not be set yet in other parts
					foreach (Part part in parts)
					{
						foreach (PartModule module in part.Modules)
						{
							if (module is ModuleKsmRadiationCoil coilModule
								&& coilModule.moduleData.partData.virtualResources.Exists(p => p.Resource.Name == existingEffect.chargeId))
							{
								effectParts.Add(part);
								break;
							}
						}
					}

					parts = effectParts;
				}

				Vector3 capsuleCenterWorld = Vector3.zero;
				foreach (Part otherPart in parts)
				{
					foreach (PartModule partModule in otherPart.Modules)
					{
						if (partModule is ModuleKsmRadiationCoil otherModule)
						{
							if (otherModule == masterModule)
							{
								capsuleCenterWorld += otherModule.transform.position;
								break;
							}

							if (otherModule.effectLength != effect.cylinderLength
								|| otherModule.optimalDistance != effect.optimalDiameter
								|| otherModule.radiationRemoved != effect.radiationRemovedAtOptimalDiameter)
								break;

							Vector3 dirToOtherCoil = otherModule.transform.position - effect.masterTransform.position;

							// dot product between the coil long axis and the direction to the other coil
							// coils are in the same array only if they are more or less coplanar
							float coplanarDot = Vector3.Dot(dirToOtherCoil.normalized, effect.masterTransform.up);
							if (coplanarDot < minCoplanarDot || coplanarDot > maxCoplanarDot)
							{
								Lib.LogDebug($"Excluding non-coplanar coil, dot={coplanarDot.ToString("F3")}");
								break;
							}

							float parallelismDot = Vector3.Dot(otherModule.transform.up, effect.masterTransform.up);
							if (parallelismDot < minParallelismDot)
							{
								Lib.LogDebug($"Excluding non-parallel coil, dot={parallelismDot.ToString("F3")}");
								break;
							}

							effect.coils.Add(otherModule);
							capsuleCenterWorld += otherModule.transform.position;
							break;
						}
					}
				}

				if (effect.coils.Count < 2)
					return false;

				// We now have a first batch of candidates that are on the same plane and with the same
				// vertical orientation. But we also need to check that they are at the same distance from 
				// the center of that preleminary array, and that they are evenly disposed around it.
				// That's why this is a two-loop operation, because we need a preleminary center to check
				// against.
				capsuleCenterWorld /= effect.coils.Count;
				float radius = (capsuleCenterWorld - effect.masterTransform.position).magnitude;

				foreach (ModuleKsmRadiationCoil coil in effect.coils)
				{
					Vector3 toCenter = capsuleCenterWorld - coil.transform.position;
					if (Vector3.Dot(toCenter.normalized, coil.transform.forward) < minCenterAlignementDot)
					{
						Lib.LogDebug($"Excluding coil with bad radial alignement, dot={Vector3.Dot(toCenter.normalized, coil.transform.forward).ToString("F3")}");
						return false;
					}

					if (Math.Abs(toCenter.magnitude - radius) > maxDistanceError)
					{
						Lib.LogDebug($"Excluding unevenly placed coil, distance from center={toCenter.magnitude.ToString("F3")}, average distance={radius.ToString("F3")}");
						return false;
					}
				}

				if (existingEffect != null && effect.coils.Count != parts.Count)
					return false;

				// At this point we are sure a valid array exists
				foreach (ModuleKsmRadiationCoil coil in effect.coils)
					coil.arrayEffect = effect;

				if (existingEffect == null)
				{
					masterModule.moduleData.CreateEffectData(masterModule, effect.coils);
				}
				
				effect.radiationRemovedAtOptimalDiameter *= effect.coils.Count;
				effect.InstantiateCapsule();
				effect.UpdateEffect();
				return true;
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

			public void Destroy()
			{
				capsule.DestroyGameObject();

				string chargeId = masterCoil.moduleData.effectData.charge.Name;

				foreach (ModuleKsmRadiationCoil coil in coils)
				{
					coil.arrayEffect = null;
					coil.moduleData.RemoveChargeResource(chargeId);
				}

				// clear master coil reference
				masterCoil.moduleData.effectData = null;
			}

			public bool VerifyCoilsPosition()
			{
				Vector3 capsuleCenterWorld = Vector3.zero;
				foreach (ModuleKsmRadiationCoil coil in coils)
				{
					capsuleCenterWorld += coil.transform.position;

					if (coil == masterCoil)
						continue;

					// dot product between the coil long axis and the direction to the other coil
					// coils are in the same array only if they are more or less coplanar
					float parallelismDot = Vector3.Dot(coil.transform.up, masterTransform.up);
					if (parallelismDot < minParallelismDot)
					{
						Lib.LogDebug($"Excluding non-parallel coil, dot={parallelismDot.ToString("F3")}");
						return false;
					}
				}

				capsuleCenterWorld /= coils.Count;
				float radius = (capsuleCenterWorld - masterTransform.position).magnitude;

				foreach (ModuleKsmRadiationCoil coil in coils)
				{
					Vector3 toCenter = capsuleCenterWorld - coil.transform.position;
					if (Vector3.Dot(toCenter.normalized, coil.transform.forward) < minCenterAlignementDot)
					{
						Lib.LogDebug($"Excluding misaligned coil, dot={Vector3.Dot(toCenter.normalized, coil.transform.forward).ToString("F3")}");
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

			public void UpdateEffect()
			{
				capsuleWorldCenter = Vector3.zero;
				Vector3 capsuleAxisWorld = Vector3.zero;
				foreach (ModuleKsmRadiationCoil coil in coils)
				{
					capsuleWorldCenter += coil.transform.position;
					capsuleAxisWorld += coil.transform.up;
				}
				capsuleWorldCenter /= coils.Count;
				capsuleAxisWorld /= coils.Count;

				diameter = ((capsuleWorldCenter - masterTransform.position).magnitude + radiusOffset) * 2f;
				radiusSqr = Mathf.Sqrt(diameter * 0.5f);
				masterCoil.moduleData.UpdateMaxRadiation(radiationRemovedAtOptimalDiameter * (optimalDiameter / diameter));

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

			public double GetPartProtectionFactor(Part part)
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
							if (IsPointInCapsule(part.transform.TransformPoint(node.position)))
							{
								protectedPointCount++;
							}
						}
					}
				}

				return (double)protectedPointCount / (double)pointCount;
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
				if (dot < 0.0f || dot > lengthSqr)
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

		[KSPField] public float effectLength = 1f;
		[KSPField] public float effectRadiusOffset = 0.1f;
		[KSPField] public float optimalDistance = 2f;
		[KSPField] public double radiationRemoved = 0.0001;
		[KSPField] public double maxAirPressureAtm;
		[KSPField] public double ecChargeRequired = 2500.0;
		[KSPField] public double ecChargeRate = 25.0;
		[KSPField] public double chargeLossRate = 0.5;
		[KSPField] public string deployAnim;
		[KSPField] public bool deployAnimReverse;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Effect", groupName = "radiationShield", groupDisplayName = "Radiation shield")]
		public string effectInfo;
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Charge", groupName = "radiationShield", groupDisplayName = "Radiation shield")]
		public string chargeInfo;

		private Animator deployAnimator;

		[KSPField(groupName = "radiationShield", groupDisplayName = "Radiation shield")]
		[UI_Toggle(scene = UI_Scene.All, requireFullControl = false, affectSymCounterparts = UI_Scene.None)]
		public bool charging;

		[KSPField(groupName = "radiationShield", groupDisplayName = "Radiation shield")]
		[UI_Toggle(scene = UI_Scene.All, requireFullControl = false, affectSymCounterparts = UI_Scene.None)]
		public bool discharging;

		private BaseField chargingField;
		private BaseField dischargingField;

		private ArrayEffect arrayEffect;

		public override void OnStart(StartState state)
		{
			part.OnEditorDetach += OnDetach;
			part.AddOnMouseEnter(PartOnMouseEnter);
			part.AddOnMouseExit(PartOnMouseExit);

			deployAnimator = new Animator(part, deployAnim, deployAnimReverse);
			deployAnimator.Still(moduleData.isDeployed ? 1f : 0f);

			chargingField = Fields["charging"];
			dischargingField = Fields["discharging"];

			chargingField.OnValueModified += OnToggleCharging;
			dischargingField.OnValueModified += OnToggleDischarging;

			chargingField.guiName = "Charging";
			dischargingField.guiName = "Discharging";

			((UI_Toggle)chargingField.uiControlFlight).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)chargingField.uiControlFlight).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);
			((UI_Toggle)chargingField.uiControlEditor).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)chargingField.uiControlEditor).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);

			((UI_Toggle)dischargingField.uiControlFlight).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)dischargingField.uiControlFlight).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);
			((UI_Toggle)dischargingField.uiControlEditor).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)dischargingField.uiControlEditor).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);
		}

		public override void OnStartFinished(StartState state)
		{
			// rebuild the array
			if (moduleData.effectData != null && !ArrayEffect.FindArray(this, moduleData.effectData))
			{
				// if the array wasn't found or has changed, remove the charge resource
				foreach (RadiationCoilData coil in moduleData.VesselData.Parts.AllModulesOfType<RadiationCoilData>())
				{
					for (int i = coil.partData.virtualResources.Count - 1; i >= 0; i--)
					{
						if (coil.partData.virtualResources[i].Resource.Name == moduleData.effectData.chargeId)
						{
							coil.partData.virtualResources[i].Remove();
							coil.partData.virtualResources.RemoveAt(i);
						}
					}
				}
				moduleData.effectData = null;
			}
		}

		public double GetPartProtectionFactor(Part part)
		{
			if (arrayEffect == null)
				return 0.0;

			return arrayEffect.GetPartProtectionFactor(part);
		}

		private void PartOnMouseExit(Part p)
		{
			if (arrayEffect == null)
				return;

			arrayEffect.SetVisible(false);

			foreach (ModuleKsmRadiationCoil coil in arrayEffect.coils)
			{
				coil.part.Highlight(false);
			}
		}

		private void PartOnMouseEnter(Part p)
		{
			if (arrayEffect == null)
				return;

			arrayEffect.SetVisible(true);

			foreach (ModuleKsmRadiationCoil coil in arrayEffect.coils)
			{
				coil.part.Highlight(Color.blue);
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
			if (arrayEffect != null)
			{
				arrayEffect.Destroy();
			}
		}

		private void Update()
		{
			if (arrayEffect != null)
			{
				if (arrayEffect.masterCoil == this)
				{
					if (!arrayEffect.VerifyCoilsPosition())
					{
						arrayEffect.Destroy();
						Lib.LogDebug("coils position is invalid");
					}
					else
					{
						arrayEffect.UpdateEffect();
					}
				}
			}

			if (arrayEffect != null)
			{
				RadiationCoilData.ArrayEffectData effectData = arrayEffect.masterCoil.moduleData.effectData;
				bool notCharged = effectData.charge.Amount == 0.0;

				Fields["chargeInfo"].guiActive = true;
				
				Events["Check"].active = notCharged;
				chargingField.guiActive = true;
				dischargingField.guiActive = true;

				Events["Deploy"].guiActive = notCharged;
				Events["Deploy"].guiName = moduleData.isDeployed ? "Retract" : "Deploy";

				// Effect: -1.2 rad/h (max -2.0 rad/h)
				effectInfo = Lib.BuildString(
					Lib.HumanReadableRadiation(effectData.RadiationRemoved, false, false),
					" (", "max", " -", Lib.HumanReadableRadiation(effectData.maxRadiation, false, false), ")");

				// Charge: 0.0/478.00 kEC / Rate : -4.2 EC/s
				chargeInfo = Lib.BuildString(Lib.HumanReadableStorage(effectData.charge.Amount, effectData.charge.Capacity), "EC", " - ", "Coils", ": ", arrayEffect.coils.Count.ToString());
			}
			else
			{
				Fields["chargeInfo"].guiActive = false;
				
				Events["Check"].active = moduleData.isDeployed;
				chargingField.guiActive = false;
				dischargingField.guiActive = false;

				Events["Deploy"].guiActive = true;
				Events["Deploy"].guiName = moduleData.isDeployed ? "Retract" : "Deploy";

				effectInfo = Lib.Color("No coil array detected", Lib.Kolor.Yellow, true);
			}
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Deploy coil", active = true, groupName = "radiationShield", groupDisplayName = "Radiation shield")]
		public void Deploy()
		{
			if (moduleData.isDeployed && arrayEffect != null)
			{
				if (arrayEffect.masterCoil.moduleData.effectData.charge.Amount > 0.0)
				{
					Message.Post("Can't retract a charged coil !");
					return;
				}
				else
				{
					arrayEffect.Destroy();
				}
			}

			if (!moduleData.isDeployed)
			{
				ArrayEffect.FindArray(this);
				DeployCoilsInArray();
			}

			deployAnimator.Play(moduleData.isDeployed, false, OnDeploy, Lib.IsEditor ? 5f : 1f);
		}

		private void OnDeploy()
		{
			moduleData.isDeployed = !moduleData.isDeployed;
		}

		private void DeployCoilsInArray()
		{
			if (arrayEffect != null)
			{
				foreach (ModuleKsmRadiationCoil other in arrayEffect.coils)
				{
					if (other == this)
						continue;

					if (!other.moduleData.isDeployed)
					{
						other.deployAnimator.Play(false, false, () => other.moduleData.isDeployed = true, Lib.IsEditor ? 5f : 1f);
					}
				}
			}
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Reconnect coil array", active = true, groupName = "radiationShield", groupDisplayName = "Radiation shield")]
		public void Check()
		{
			if (arrayEffect != null)
			{
				if (arrayEffect.masterCoil.moduleData.effectData.charge.Amount > 0.0)
				{
					Message.Post("Can't reconnect a charged coil array!");
					return;
				}
				arrayEffect.Destroy();
			}

			ArrayEffect.FindArray(this);
			DeployCoilsInArray();
		}

		private void OnToggleDischarging(object arg)
		{
			arrayEffect.masterCoil.moduleData.effectData.discharging = discharging;

			foreach (ModuleKsmRadiationCoil other in arrayEffect.coils)
			{
				if (other == this)
					continue;

				other.discharging = discharging;
			}
		}

		private void OnToggleCharging(object arg)
		{
			arrayEffect.masterCoil.moduleData.effectData.charging = charging;

			foreach (ModuleKsmRadiationCoil other in arrayEffect.coils)
			{
				if (other == this)
					continue;

				other.charging = charging;
			}
		}
	}
}
