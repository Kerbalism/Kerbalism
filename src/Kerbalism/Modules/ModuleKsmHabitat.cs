using Harmony;
using KSP.UI;
using KSP.UI.Screens.Flight;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static KERBALISM.HabitatData;
using static KERBALISM.HabitatLib;
using System.Collections;

namespace KERBALISM
{
	public sealed class ModuleKsmHabitat : PartModule, ISpecifics, IModuleInfo
	{
		#region FIELDS / PROPERTIES
		// general config
		[KSPField] public bool canPressurize = true;              // can the habitat be pressurized ?
		[KSPField] public double maxShieldingFactor = 1.0;        // how much shielding can be applied, in % of the habitat surface (can be > 1.0)
		[KSPField] public double depressurizationSpeed = -1.0;    // liters/second, auto scaled at 10 liters / second / √(m3) of habitat volume if not specified, adjustable in settings
		[KSPField] public double reclaimAtmosphereFactor = 0.75;  // % of atmosphere that will be recovered when depressurizing (producing "reclaimResource" back)
		[KSPField] public bool canRetract = true;                 // if false, can't be retracted once deployed
		[KSPField] public bool deployWithPressure = false;        // if true, deploying is done by pressurizing
		[KSPField] public double depressurizeECRate = 0.0;        // EC/s consumed while depressurizing
		[KSPField] public double deployECRate = 0.0;              // EC/s consumed while deploying / inflating
		[KSPField] public double accelerateECRate = 15.0;          // EC/s consumed while accelerating / decelerating a centrifuge
		[KSPField] public double rotateECRate = 2.0;              // EC/s consumed to sustain the centrifuge rotation

		// volume / surface config
		[KSPField] public double volume = 0.0;  // habitable volume in m^3, deduced from model if not specified
		[KSPField] public double surface = 0.0; // external surface in m^2, deduced from model if not specified
		[KSPField] public Lib.VolumeAndSurfaceMethod volumeAndSurfaceMethod = Lib.VolumeAndSurfaceMethod.Best;
		[KSPField] public bool substractAttachementNodesSurface = true;

		// resources config
		[KSPField] public string reclaimResource = "Nitrogen"; // Nitrogen
		[KSPField] public string shieldingResource = "Shielding"; // KsmShielding

		// animations config
		[KSPField] public string deployAnim = string.Empty; // deploy / inflate animation, if any
		[KSPField] public bool deployAnimReverse = false;   // deploy / inflate animation is reversed

		[KSPField] public string rotateAnim = string.Empty;        // rotate animation, if any
		[KSPField] public bool rotateIsReversed = false;           // inverse rotation direction
		[KSPField] public bool rotateIsTransform = false;          // rotateAnim is not an animation, but a transform
		[KSPField] public Vector3 rotateAxis = Vector3.forward;    // axis around which to rotate (transform only)
		[KSPField] public float rotateSpinRate = 30.0f;            // centrifuge rotation speed (deg/s)
		[KSPField] public float rotateAccelerationRate = 1.0f;     // centrifuge transform acceleration (deg/s/s)
		[KSPField] public bool rotateIVA = true;                   // should the IVA rotate with the transform ?

		[KSPField] public string counterweightAnim = string.Empty;        // inflate animation, if any
		[KSPField] public bool counterweightIsReversed = false;           // inverse rotation direction
		[KSPField] public bool counterweightIsTransform = false;          // rotateAnim is not an animation, but a Transform
		[KSPField] public Vector3 counterweightAxis = Vector3.forward;    // axis around which to rotate (transform only)
		[KSPField] public float counterweightSpinRate = 60.0f;            // counterweight rotation speed (deg/s)
		[KSPField] public float counterweightAccelerationRate = 2.0f;     // counterweight acceleration (deg/s/s)

		// fixed characteristics determined at prefab compilation from OnLoad()
		// do not use these in configs, they are KSPField just so they are automatically copied over on part instancing
		[KSPField] public bool isDeployable;
		[KSPField] public bool isCentrifuge;
		[KSPField] public bool hasShielding;
		[KSPField] public int baseComfortsMask;

		// internal state
		private HabitatData data;
		public HabitatData HabitatData => data;

		// animation handlers
		private Animator deployAnimator;
		private Transformator rotateAnimator;
		private Transformator counterweightAnimator;

		// caching frequently used things
		private VesselData vd;
		private ResourceWrapper atmoRes;
		private ResourceWrapper wasteRes;
		private ResourceWrapper shieldRes;
		private VesselResource atmoResInfo;
		private VesselResource wasteResInfo;
		private BaseEvent enableEvent;
		private BaseEvent pressureEvent;
		private BaseEvent deployEvent;
		private BaseEvent rotateEvent;
		private string reclaimResAbbr;

		public ResourceWrapper WasteRes => wasteRes;

		// static game wide volume / surface cache
		public static Dictionary<string, Lib.PartVolumeAndSurfaceInfo> habitatDatabase;
		public const string habitatDataCacheNodeName = "KERBALISM_HABITAT_INFO";
		public static string HabitatDataCachePath => Path.Combine(Lib.KerbalismRootPath, "HabitatData.cache");

		// rmb ui status strings
#if KSP15_16
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Pressure")]
		public string mainPawInfo;
#else
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Pressure", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public string mainPawInfo;
#endif

#if KSP15_16
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "State")]
		public string suitPawInfo;
#else
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "State", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public string suitPawInfo;
#endif

		#endregion

		#region INIT

		// parsing configs at prefab compilation
		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				// Parse comforts
				baseComfortsMask = 0;
				foreach (string comfortString in node.GetValues("comfort"))
				{
#if KSP15_16 || KSP17
					Comfort comfort;
					try
					{
						comfort = (Comfort)Enum.Parse(typeof(Comfort), comfortString);
						fixedComfortsMask |= (int)comfort;
					}
					catch
					{
						Lib.Log($"Unrecognized comfort `{comfortString}` in ModuleKsmHabitat config for part {part.name}");
					}
#else
					if (Enum.TryParse(comfortString, out Comfort comfort))
						baseComfortsMask |= (int)comfort;
					else
						Lib.Log($"Unrecognized comfort `{comfortString}` in ModuleKsmHabitat config for part {part.partName}");
#endif
				}

				// instanciate animations from config
				SetupAnimations();

				// volume/surface calcs are quite slow and memory intensive, so we do them only once on the prefab
				// then get the prefab values from OnStart. Moreover, we cache the results in the 
				// Kerbalism\HabitatData.cache file and reuse those cached results on next game launch.
				if (volume <= 0.0 || surface <= 0.0)
				{
					if (habitatDatabase == null)
					{
						ConfigNode dbRootNode = ConfigNode.Load(HabitatDataCachePath);
						ConfigNode[] habInfoNodes = dbRootNode?.GetNodes(habitatDataCacheNodeName);
						habitatDatabase = new Dictionary<string, Lib.PartVolumeAndSurfaceInfo>();

						if (habInfoNodes != null)
						{
							for (int i = 0; i < habInfoNodes.Length; i++)
							{
								string partName = habInfoNodes[i].GetValue("partName") ?? string.Empty;
								if (!string.IsNullOrEmpty(partName) && !habitatDatabase.ContainsKey(partName))
									habitatDatabase.Add(partName, new Lib.PartVolumeAndSurfaceInfo(habInfoNodes[i]));
							}
						}
					}

					// SSTU specific support copypasted from the old system, not sure how well this works
					foreach (PartModule pm in part.Modules)
					{
						if (pm.moduleName == "SSTUModularPart")
						{
							Bounds bb = Lib.ReflectionCall<Bounds>(pm, "getModuleBounds", new Type[] { typeof(string) }, new string[] { "CORE" });
							if (bb != null)
							{
								if (volume <= 0.0) volume = Lib.BoundsVolume(bb) * 0.785398; // assume it's a cylinder
								if (surface <= 0.0) surface = Lib.BoundsSurface(bb) * 0.95493; // assume it's a cylinder
							}
							return;
						}
					}

					string configPartName = part.name.Replace('.', '_');
					Lib.PartVolumeAndSurfaceInfo partInfo;
					if (!habitatDatabase.TryGetValue(configPartName, out partInfo))
					{
						// set the model to the deployed state
						if (isDeployable)
							deployAnimator.Still(1f);

						// get surface and volume
						partInfo = Lib.GetPartVolumeAndSurface(part, Settings.VolumeAndSurfaceLogging);

						habitatDatabase.Add(configPartName, partInfo);
					}

					partInfo.GetUsingMethod(
						volumeAndSurfaceMethod != Lib.VolumeAndSurfaceMethod.Best ? volumeAndSurfaceMethod : partInfo.bestMethod,
						out double infoVolume, out double infoSurface, substractAttachementNodesSurface);

					if (volume <= 0.0) volume = infoVolume;
					if (surface <= 0.0) surface = infoSurface;
				}

				// determine habitat permanent state based on if animations exists and are valid
				isDeployable = deployAnimator.IsDefined;
				isCentrifuge = rotateAnimator.IsDefined;
				hasShielding = Features.Radiation && maxShieldingFactor > 0.0;

				if (isDeployable && deployWithPressure)
					canRetract = false; // inflatables can't be retracted

				// precalculate shielding cost and add resources
				double volumeLiters = M3ToL(volume);
				double currentVolumeLiters = canPressurize ? volumeLiters : 0.0;
				Lib.AddResource(part, Settings.HabitatAtmoResource, currentVolumeLiters, volumeLiters);
				Lib.AddResource(part, Settings.HabitatWasteResource, 0.0, volumeLiters);

				// adjust depressurization speed if not specified
				if (depressurizationSpeed < 0.0)
					depressurizationSpeed = 10.0 * Math.Sqrt(volume);

				if (hasShielding)
				{
					Lib.AddResource(part, shieldingResource, 0.0, surface * maxShieldingFactor);
				}
			}
			else
			{
				ConfigNode editorDataNode = node.GetNode("EditorHabitatData");

				if (editorDataNode != null)
					data = new HabitatData(editorDataNode);
			}
		}

		// this is only for editor <--> editor and editor -> flight persistence
		public override void OnSave(ConfigNode node)
		{
			if (Lib.IsEditor() && data != null)
			{
				ConfigNode habDataNode = node.AddNode("EditorHabitatData");
				data.Save(habDataNode);
			}
		}

		// pseudo-ctor
		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			bool isFlight = Lib.IsFlight();

			// setup animations / transformators
			SetupAnimations();

			// get references to stuff we need often
			atmoRes = new PartResourceWrapper(part.Resources[Settings.HabitatAtmoResource]);
			wasteRes = new PartResourceWrapper(part.Resources[Settings.HabitatWasteResource]);
			shieldRes = hasShielding ? new PartResourceWrapper(part.Resources[shieldingResource]) : null;

			if (isFlight)
			{
				vd = vessel.KerbalismData();
				atmoResInfo = (VesselResource)vd.ResHandler.GetResource(Settings.HabitatAtmoResource);
				wasteResInfo = (VesselResource)vd.ResHandler.GetResource(Settings.HabitatWasteResource);
			}

			enableEvent = Events["ToggleHabitat"];
			pressureEvent = Events["TogglePressure"];
			deployEvent = Events["ToggleDeploy"];
			rotateEvent = Events["ToggleRotate"];

			reclaimResAbbr = PartResourceLibrary.Instance.GetDefinition(reclaimResource).abbreviation;

			// get persistent data
			// data will be restored from OnLoad (and therefore not null) only in the following cases :
			// - Part created in the editor from a saved ship (not a freshly instantiated part from the part list)
			// - Part created in flight from a just launched vessel
			if (data == null)
			{
				// in flight, we should have the data stored in VesselData > PartData, unless the part was created in flight (KIS...)
				if (isFlight)
					data = HabitatData.GetFlightReferenceFromPart(part);

				// if all other cases, this is newly instantiated from prefab part. Create the data object and put the right values.
				if (data == null)
				{
					data = new HabitatData
					{
						crewCount = Lib.CrewCount(part),
						baseVolume = volume,
						baseSurface = surface,
						baseComfortsMask = baseComfortsMask,
						isDeployed = !isDeployable,
						isEnabled = !isDeployable
					};

					if (!canPressurize)
						data.pressureState = PressureState.AlwaysDepressurized;
					if (isDeployable)
						data.pressureState = PressureState.Depressurized;
					else
						data.pressureState = PressureState.Pressurized;

					// part was created in flight (KIS...) : set the VesselData / PartData reference
					if (isFlight)
						HabitatData.SetFlightReferenceFromPart(part, data);
				}
			}
			else if (isFlight)
			{
				HabitatData.SetFlightReferenceFromPart(part, data);
			}


			// acquire a reference to the partmodule
			data.module = this;

			// ensure proper initialization by calling the right event for the current state
			if (!canPressurize)
				data.pressureState = PressureState.AlwaysDepressurizedStartEvt;
			else
			{
				switch (data.pressureState)
				{
					case PressureState.Pressurized:
					case PressureState.Pressurizing:
						data.pressureState = PressureState.PressurizingStartEvt; break;
					case PressureState.DepressurizingAboveThreshold:
					case PressureState.DepressurizingBelowThreshold:
					case PressureState.Depressurized:
						data.pressureState = PressureState.DepressurizingStartEvt; break;
					case PressureState.Breatheable:
						data.pressureState = PressureState.BreatheableStartEvt; break;
					case PressureState.AlwaysDepressurized:
						data.pressureState = PressureState.AlwaysDepressurizedStartEvt; break;
				}
			}

			// setup animations state
			if (isDeployable)
			{
				deployAnimator.Still(data.isDeployed ? 1f : 0f);
			}

			if (isCentrifuge && data.isRotating)
			{
				rotateAnimator.StartSpin(true);
				counterweightAnimator.StartSpin(true);
			}
				

#if DEBUG
			Events["LogVolumeAndSurface"].active = true;
#else
			Events["LogVolumeAndSurface"].active = Settings.VolumeAndSurfaceLogging;
#endif

		}

		public void OnDestroy()
		{
			// clear loaded module reference to avoid memory leaks
			if (data != null)
				data.module = null;
		}

		private void SetupAnimations()
		{
			deployAnimator = new Animator(part, deployAnim, deployAnimReverse);

			if (rotateIsTransform)
				rotateAnimator = new Transformator(part, rotateAnim, rotateAxis, rotateSpinRate, rotateAccelerationRate, rotateIsReversed, rotateIVA);
			else
				rotateAnimator = new Transformator(part, rotateAnim, rotateSpinRate, rotateAccelerationRate, rotateIsReversed);

			if (counterweightIsTransform)
				counterweightAnimator = new Transformator(part, counterweightAnim, counterweightAxis, counterweightSpinRate, counterweightAccelerationRate, counterweightIsReversed, false);
			else
				counterweightAnimator = new Transformator(part, counterweightAnim, counterweightSpinRate, counterweightAccelerationRate, counterweightIsReversed);
		}

		#endregion

		public void Update()
		{
			if (isCentrifuge && data.isDeployed)
			{
				rotateAnimator.Update();
				counterweightAnimator.Update();
				data.isRotating = rotateAnimator.IsSpinning;
			}

			// I can't believe I never used that before...
			if (part.PartActionWindow == null)
				return;

			int pawState = (enableEvent.active ? 1 << 0 : 0) | (pressureEvent.active ? 1 << 1 : 0) | (deployEvent.active ? 1 << 2 : 0) | (rotateEvent.active ? 1 << 3 : 0);

			enableEvent.active = data.isDeployed;
			pressureEvent.active = canPressurize && data.isDeployed;
			deployEvent.active = isDeployable && !data.isRotating && !(data.isDeployed && !canRetract);
			rotateEvent.active = isCentrifuge && data.isDeployed;

			int newPawState = (enableEvent.active ? 1 << 0 : 0) | (pressureEvent.active ? 1 << 1 : 0) | (deployEvent.active ? 1 << 2 : 0) | (rotateEvent.active ? 1 << 3 : 0);

			if (pawState != newPawState)
				part.PartActionWindow.displayDirty = true;

			double habPressure = atmoRes.Amount / atmoRes.Capacity;

			mainPawInfo =
				Lib.Color(habPressure > Settings.PressureThreshold, habPressure.ToString("0.00 atm"), Lib.Kolor.Green, Lib.Kolor.Orange)
				+ volume.ToString(" (0.0 m3)")
				+ " Crew: " + data.crewCount + "/" + part.CrewCapacity;

			suitPawInfo = (data.isEnabled ? "Enabled - " : "Disabled - ") + data.pressureState.ToString();

			if (enableEvent.active)
			{
				enableEvent.guiName = data.isEnabled ? "Disable habitat" : "Enable habitat";
			}

			if (pressureEvent.active)
			{
				switch (data.pressureState)
				{
					case PressureState.Pressurized:
						pressureEvent.guiName = "Depressurize : "
						+ Lib.HumanReadableAmountCompact(atmoRes.Amount * reclaimAtmosphereFactor) + " " + reclaimResAbbr + " reclaimed"; // maybe the % is better ?
						break;
					case PressureState.Depressurized:
					case PressureState.Breatheable:
						pressureEvent.guiName = "Pressurize";
						break;
					case PressureState.Pressurizing:
						pressureEvent.guiName = "Pressurizing...";
						break;
					case PressureState.DepressurizingAboveThreshold:
					case PressureState.DepressurizingBelowThreshold:
						pressureEvent.guiName = "Depressurizing : " + Lib.HumanReadableCountdown(atmoRes.Amount / depressurizationSpeed);
						break;
				}
			}

			if (deployEvent.active)
			{
				if (data.isDeployed)
					deployEvent.guiName = deployWithPressure ? "Deflate" : "Retract";
				else
					deployEvent.guiName = deployWithPressure ? "Inflate" : "Deploy";
			}

			if (rotateEvent.active)
			{
				if (!data.isRotating)
					rotateEvent.guiName = "Start rotation";
				else if (rotateAnimator.IsStopping)
					rotateEvent.guiName = "Start rotation (stopping...)";
				else if (rotateAnimator.IsAccelerating)
					rotateEvent.guiName = "Stop rotation (starting...)";
				else
					rotateEvent.guiName = "Stop rotation";
			}
		}

#region FIXEDUPDATE

		private void FixedUpdate()
		{
			CommonUpdate(vessel, vd, data, atmoRes, wasteRes, shieldRes, Kerbalism.elapsed_s);
		}

		public static void BackgroundUpdate(Vessel v, VesselData vd, ProtoPartSnapshot protoPart, ModuleKsmHabitat prefab, double elapsed_s)
		{
			PartData partData;
			if (!vd.Parts.TryGet(protoPart.flightID, out partData))
			{
				Lib.Log($"PartData wasn't found for {protoPart.partName} with id {protoPart.flightID}", Lib.LogLevel.Error);
				return;
			}

			if (partData.Habitat == null)
			{
				Lib.Log($"HabitatData wasn't found for {protoPart.partName} with id {protoPart.flightID}", Lib.LogLevel.Error);
				return;
			}

			HabitatData data = partData.Habitat;
			data.module = prefab;

			ResourceWrapper atmoRes = null;
			ResourceWrapper wasteRes = null;
			ResourceWrapper shieldRes = null;

			foreach (ProtoPartResourceSnapshot protoResource in protoPart.resources)
			{
				if (protoResource.resourceName == Settings.HabitatAtmoResource)
					atmoRes = new ProtoPartResourceWrapper(protoResource);
				else if (protoResource.resourceName == Settings.HabitatWasteResource)
					wasteRes = new ProtoPartResourceWrapper(protoResource);
				else if (protoResource.resourceName == data.module.shieldingResource)
					shieldRes = new ProtoPartResourceWrapper(protoResource);
			}

			if (atmoRes == null)
			{
				Lib.Log($"Atmosphere resource wasn't found on {protoPart.partName}", Lib.LogLevel.Warning);
				return;
			}
				
			CommonUpdate(v, vd, data, atmoRes, wasteRes, shieldRes, elapsed_s);
		}

		public static void CommonUpdate(Vessel vessel, VesselData vd, HabitatData data, ResourceWrapper atmoRes, ResourceWrapper wasteRes, ResourceWrapper shieldRes, double elapsed_s)
		{




			bool isFlight = !Lib.IsEditor(); // note : tracking station and space center are considered as flight

			VesselResource atmoResInfo = null;
			VesselResource wasteResInfo = null;

			if (isFlight)
			{
				atmoResInfo = (VesselResource)vd.ResHandler.GetResource(Settings.HabitatAtmoResource);
				wasteResInfo = (VesselResource)vd.ResHandler.GetResource(Settings.HabitatWasteResource);
			}

			// TODO: refactor that huge switch into a proper state machine :
			// - change the transitory states into methods in order not to have updates cycles spent in those states
			// - have the whole thing callable independently of the loaded state
			switch (data.pressureState)
			{
				case PressureState.Pressurized:
					// if pressure drop below the minimum habitable pressure, switch to partial pressure state
					if (atmoRes.Amount / atmoRes.Capacity < Settings.PressureThreshold)
						data.pressureState = PressureState.PressureDroppedEvt;
					break;

				case PressureState.PressureDroppedEvt:

					// make the kerbals put their helmets
					if (isFlight && data.crewCount > 0 && vessel.isActiveVessel && data.module.part.internalModel != null)
						Lib.RefreshIVAAndPortraits();

					// go to pressurizing state
					data.pressureState = PressureState.Pressurizing;

					break;

				case PressureState.BreatheableStartEvt:
					atmoRes.FlowState = false;

					wasteRes.Capacity = M3ToL(data.module.volume);
					wasteRes.FlowState = false;

					// make the kerbals take out their helmets
					if (isFlight && data.crewCount > 0 && vessel.isActiveVessel && data.module.part.internalModel != null)
						Lib.RefreshIVAAndPortraits();

					data.pressureState = PressureState.Breatheable;
					break;

				case PressureState.Breatheable:

					//if (!isEditor)
					//	atmoResInfo.equalizeMode = ResourceInfo.EqualizeMode.Disabled;

					if (isFlight && !vd.EnvInBreathableAtmosphere)
					{
						if (data.module.canPressurize)
							data.pressureState = PressureState.PressureDroppedEvt;
						else
							data.pressureState = PressureState.AlwaysDepressurizedStartEvt;
						break;
					}

					atmoRes.Amount = Math.Min(vd.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);
					wasteRes.Amount = 0.0; // magic scrubbing

					break;

				case PressureState.AlwaysDepressurizedStartEvt:
					atmoRes.FlowState = false;

					wasteRes.Capacity = data.crewCount * Settings.PressureSuitVolume;
					wasteRes.FlowState = true;

					if (isFlight && data.crewCount > 0 && vessel.isActiveVessel && data.module.part.internalModel != null)
						Lib.RefreshIVAAndPortraits();

					data.pressureState = PressureState.AlwaysDepressurized;
					break;

				case PressureState.AlwaysDepressurized:

					if (isFlight)
					{
						if (vd.EnvInBreathableAtmosphere)
						{
							data.pressureState = PressureState.BreatheableStartEvt;
							break;
						}
						else if (vd.EnvInOxygenAtmosphere)
						{
							atmoRes.Amount = Math.Min(vd.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);
						}
					}

					break;

				case PressureState.Depressurized:

					if (isFlight)
					{
						if (data.module.isDeployable && data.module.deployWithPressure && !data.isDeployed)
						{
							break;
						}

						if (vd.EnvInBreathableAtmosphere)
						{
							data.pressureState = PressureState.BreatheableStartEvt;
							break;
						}
						else if (vd.EnvInOxygenAtmosphere)
						{
							atmoRes.Amount = Math.Min(vd.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);
						}
					}
					break;

				case PressureState.PressurizingStartEvt:

					if (isFlight)
						atmoResInfo.equalizeMode = VesselResource.EqualizeMode.Disabled;

					atmoRes.FlowState = true;

					if (isFlight)
						data.pressureState = PressureState.Pressurizing;
					else
						data.pressureState = PressureState.PressurizingEndEvt;


					break;

				case PressureState.Pressurizing:

					if (isFlight)
						atmoResInfo.equalizeMode = VesselResource.EqualizeMode.Disabled;

					if (vessel.loaded && data.module.deployWithPressure && !data.isDeployed)
						data.module.deployAnimator.Still(Math.Min((float)(atmoRes.Amount / (atmoRes.Capacity * Settings.PressureThreshold)), 1f));

					// if pressure go back to the minimum habitable pressure, switch to pressurized state
					if (atmoRes.Amount / atmoRes.Capacity > Settings.PressureThreshold)
						data.pressureState = PressureState.PressurizingEndEvt;
					break;

				case PressureState.PressurizingEndEvt:

					wasteRes.Capacity = M3ToL(data.module.volume);
					wasteRes.FlowState = true;

					if (isFlight)
					{
						// make the kerbals remove their helmets
						// this works in conjunction with the SpawnCrew prefix patch that check if the part is pressurized or not on spawning the IVA.
						if (data.crewCount > 0 && vessel.isActiveVessel && data.module.part.internalModel != null)
							Lib.RefreshIVAAndPortraits();

						if (data.module.isDeployable && data.module.deployWithPressure)
						{
							data.module.deployAnimator.Still(1f);
							OnDeployCallback(data, true);
						}
					}
					else
					{
						atmoRes.Amount = M3ToL(data.module.volume);

						if (data.module.isDeployable && data.module.deployWithPressure)
							data.module.deployAnimator.Play(false, false, data.module.OnDeployCallbackLoaded, 5f);
					}

					data.pressureState = PressureState.Pressurized;
					break;

				case PressureState.DepressurizingStartEvt:
					atmoRes.FlowState = false;
					wasteRes.FlowState = false;

					if (!isFlight)
						data.pressureState = PressureState.DepressurizingEndEvt;
					else if (atmoRes.Amount / atmoRes.Capacity >= Settings.PressureThreshold)
						data.pressureState = PressureState.DepressurizingAboveThreshold;
					else
						data.pressureState = PressureState.DepressurizingPassThresholdEvt;

					break;

				case PressureState.DepressurizingPassThresholdEvt:

					double suitsVolume = data.crewCount * Settings.PressureSuitVolume;
					// make the CO2 level in the suit the same as the current CO2 level in the part by adjusting the amount
					// We only do it if the part is crewed, because since it discard nearly all the CO2 in the part, it can
					// be exploited to remove CO2, by stopping the depressurization immediatly.
					if (data.crewCount > 0)
						wasteRes.Amount *= suitsVolume / wasteRes.Capacity;

					wasteRes.Capacity = suitsVolume;
					wasteRes.FlowState = true; // kerbals are now in their helmets and CO2 won't be vented anymore, let the suits CO2 level equalize with the vessel CO2 level

					// make the kerbals put their helmets
					// this works in conjunction with the SpawnCrew prefix patch that check if the part is pressurized or not on spawning the IVA.
					if (isFlight && data.crewCount > 0 && vessel.isActiveVessel && data.module.part.internalModel != null)
						Lib.RefreshIVAAndPortraits();

					data.pressureState = PressureState.DepressurizingBelowThreshold;

					break;

				case PressureState.DepressurizingAboveThreshold:
				case PressureState.DepressurizingBelowThreshold:

					// if fully deprussirized, do to the depressurized state
					if (atmoRes.Amount <= 0.0)
					{
						data.pressureState = PressureState.DepressurizingEndEvt;
						break;
					}
					// if external pressure is less than the hab pressure, stop depressurization and go to the breathable state
					else if (vd.EnvInOxygenAtmosphere && atmoRes.Amount / atmoRes.Capacity < vd.EnvStaticPressure)
					{
						data.pressureState = PressureState.DepressurizingEndEvt;
						break;
					}
					// pressure is going below the survivable threshold : time for kerbals to put their helmets
					else if (data.pressureState == PressureState.DepressurizingAboveThreshold && atmoRes.Amount / atmoRes.Capacity < Settings.PressureThreshold)
					{
						data.pressureState = PressureState.DepressurizingPassThresholdEvt;
					}

					double newAtmoAmount = atmoRes.Amount - (data.module.depressurizationSpeed * elapsed_s);
					newAtmoAmount = Math.Max(newAtmoAmount, 0.0);

					// we only vent CO2 when the kerbals aren't yet in their helmets
					if (data.pressureState == PressureState.DepressurizingAboveThreshold)
					{
						wasteRes.Amount *= atmoRes.Amount > 0.0 ? newAtmoAmount / atmoRes.Amount : 1.0;
						wasteRes.Amount = Lib.Clamp(wasteRes.Amount, 0.0, wasteRes.Capacity);
					}

					// convert the atmosphere into the reclaimed resource if the pressure is above the pressure threshold defined in the module config 
					if (data.module.reclaimAtmosphereFactor > 0.0 && newAtmoAmount / atmoRes.Capacity >= 1.0 - data.module.reclaimAtmosphereFactor)
					{
						ResourceCache.Produce(vessel, data.module.reclaimResource, atmoRes.Amount - newAtmoAmount, ResourceBroker.Habitat);
					}

					atmoRes.Amount = newAtmoAmount;

					break;

				case PressureState.DepressurizingEndEvt:

					atmoRes.FlowState = false;

					if (!isFlight)
					{
						atmoRes.Amount = 0.0;
						wasteRes.Amount = 0.0;

						wasteRes.Capacity = data.crewCount * Settings.PressureSuitVolume;
						wasteRes.FlowState = true;
						data.pressureState = PressureState.Depressurized;

						if (data.module.isDeployable && data.module.deployWithPressure && data.module.deployAnimator.NormalizedTime != 0f)
							data.module.deployAnimator.Play(true, false, null, 5f);
					}
					else 
					{
						if (vd.EnvInBreathableAtmosphere)
						{
							wasteRes.Capacity = M3ToL(data.module.volume);
							wasteRes.FlowState = false;
							data.pressureState = PressureState.Breatheable; // don't go to breathable start to avoid resetting the portraits, we already have locked flowstate anyway
						}
						else
						{
							wasteRes.Capacity = data.crewCount * Settings.PressureSuitVolume;
							wasteRes.FlowState = true;
							data.pressureState = PressureState.Depressurized;
						}
					}
					
					break;
			}

			// synchronize resource amounts to the persisted data
			data.atmoAmount = LToM3(atmoRes.Amount);
			data.shieldingAmount = data.module.hasShielding ? shieldRes.Amount : 0.0;
			data.wasteLevel = wasteRes.Capacity > 0.0 ? wasteRes.Amount / wasteRes.Capacity : 0.0;

			// set equalizaton mode if it hasn't been explictely disabled in the breathable / depressurizing states
			if (isFlight)
			{
				if (atmoResInfo.equalizeMode == VesselResource.EqualizeMode.NotSet)
					atmoResInfo.equalizeMode = VesselResource.EqualizeMode.Enabled;

				if (wasteResInfo.equalizeMode == VesselResource.EqualizeMode.NotSet)
					wasteResInfo.equalizeMode = VesselResource.EqualizeMode.Enabled;
			}
		}


#endregion

#region ENABLE / DISABLE LOGIC & UI

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
#endif
		public void ToggleHabitat()
		{
			TryToggleHabitat(data, part);
		}

		public static bool TryToggleHabitat(HabitatData data, bool isLoaded)
		{
			if (data.isEnabled && data.crewCount > 0)
			{
				if (!isLoaded)
				{
					Message.Post($"Can't disable a crewed habitat on an unloaded vessel");
					return false;
				}

				if (Lib.IsEditor())
				{
					Lib.EditorClearPartCrew(data.module.part);
				}
				else
				{
					List<ProtoCrewMember> crewLeft = Lib.TryTransferCrewElsewhere(data.module.part, false);

					if (crewLeft.Count > 0)
					{
						string message = "Not enough crew capacity in the vessel to transfer those Kerbals :\n";
						crewLeft.ForEach(a => message += a.displayName + "\n");
						Message.Post($"Habitat in {data.module.part.partInfo.title} couldn't be disabled.", message);
						return false;
					}
					else
					{
						Message.Post($"Habitat in {data.module.part.partInfo.title} has been disabled.", "Crew was transfered in the rest of the vessel");
					}
				}
			}

			if (data.isEnabled)
			{
				if (data.isRotating)
					TryToggleRotate(data, isLoaded);

				data.isEnabled = false;
			}
			else
			{
				if (!data.isDeployed)
					return false;

				data.isEnabled = true;
			}
			return true;
		}

		#endregion

		#region ENABLE / DISABLE PRESSURE & UI

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = false)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = false, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
#endif
		public void TogglePressure()
		{
			TryTogglePressure(data);
		}

		/// <summary> try to deploy or retract the habitat. isLoaded must be set to true in the editor and for a loaded vessel, false for an unloaded vessel</summary>
		public static bool TryTogglePressure(HabitatData data)
		{
			if (!data.module.canPressurize || !data.isDeployed)
			{
				return false;
			}

			switch (data.pressureState)
			{
				case PressureState.Pressurized:
				case PressureState.Pressurizing:
					data.pressureState = PressureState.DepressurizingStartEvt;
					break;
				case PressureState.Breatheable:
				case PressureState.Depressurized:
				case PressureState.DepressurizingAboveThreshold:
				case PressureState.DepressurizingBelowThreshold:
					data.pressureState = PressureState.PressurizingStartEvt;
					break;
			}

			return true;
		}

		#endregion

		#region DEPLOY & ROTATE

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = false)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = false, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
#endif
		public void ToggleDeploy()
		{
			TryToggleDeploy(data, true);
		}

		// TODO : we need a way to handle unloaded EC consumption of deploy
		/// <summary> try to deploy or retract the habitat. isLoaded must be set to true in the editor and for a loaded vessel, false for an unloaded vessel</summary>
		public static bool TryToggleDeploy(HabitatData data, bool isLoaded)
		{
			if (!data.module.isDeployable)
				return false;

			bool isEditor = Lib.IsEditor();

			if (data.isDeployed)
			{
				if (!data.module.canRetract)
					return false;

				if (isEditor)
				{
					if (data.module.canPressurize && data.pressureState != PressureState.Depressurized)
						data.pressureState = PressureState.DepressurizingStartEvt;

					if (data.isEnabled && !TryToggleHabitat(data, isLoaded))
						return false;
				}
				else
				{
					if (!(data.pressureState == PressureState.Depressurized || data.pressureState == PressureState.Breatheable || data.pressureState == PressureState.AlwaysDepressurized))
					{
						Message.Post($"Can't retract \n{data.module.part.partInfo.title}", "It's still pressurized !");
						return false;
					}

					if (data.isEnabled && !TryToggleHabitat(data, isLoaded))
					{
						return false;
					}
				}

				data.isDeployed = false;

				if (data.module.deployWithPressure)
					data.pressureState = PressureState.DepressurizingStartEvt;
				else
					data.module.deployAnimator.Play(true, false, null, isEditor ? 5f : 1f);

			}
			else
			{
				if (data.module.deployWithPressure)
					data.pressureState = PressureState.PressurizingStartEvt;
				else if (isLoaded)
					data.module.deployAnimator.Play(false, false, data.module.OnDeployCallbackLoaded, isEditor ? 5f : 1f);
				else
					OnDeployCallback(data, isLoaded);
			}

			return true;

		}

		private static void OnDeployCallback(HabitatData data, bool isLoaded)
		{
			data.isDeployed = true;

			if (!data.isEnabled)
				TryToggleHabitat(data, isLoaded);
		}

		private void OnDeployCallbackLoaded()
		{
			OnDeployCallback(data, true);
		}

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = false)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = false, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
#endif
		public void ToggleRotate()
		{
			bool isEditor = Lib.IsEditor();
			if (data.isRotating)
			{
				rotateAnimator.StopSpin(isEditor);
				counterweightAnimator.StopSpin(isEditor);
			}
			else if (data.isEnabled && (!isDeployable || (isDeployable && data.isDeployed)))
			{
				rotateAnimator.StartSpin(isEditor);
				counterweightAnimator.StartSpin(isEditor);
			}
		}

		// TODO : we need a way to handle unloaded EC consumption of acceleration
		public static void TryToggleRotate(HabitatData data, bool isLoaded)
		{
			bool isEditor = Lib.IsEditor();
			if (data.isRotating)
			{
				if (isLoaded)
				{
					data.module.rotateAnimator.StopSpin(isEditor);
					data.module.counterweightAnimator.StopSpin(isEditor);
				}

			}
			else if (data.isEnabled && (!data.module.isDeployable || (data.module.isDeployable && data.isDeployed)))
			{
				if (isLoaded)
				{
					data.module.rotateAnimator.StartSpin(isEditor);
					data.module.counterweightAnimator.StartSpin(isEditor);
				}

			}
		}


		// debug
#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "[Debug] log volume/surface", active = false, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
#endif
		public void LogVolumeAndSurface() => Lib.GetPartVolumeAndSurface(part, true);

		#endregion

#region UI INFO

		// specifics support
		public Specifics Specs()
		{
			string ecAbbr = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").abbreviation;

			Specifics specs = new Specifics();
			specs.Add(Local.Habitat_info1, Lib.HumanReadableVolume(volume > 0.0 ? volume : Lib.PartBoundsVolume(part)) + (volume > 0.0 ? "" : " (bounds)"));//"Volume"
			specs.Add(Local.Habitat_info2, Lib.HumanReadableSurface(surface > 0.0 ? surface : Lib.PartBoundsSurface(part)) + (surface > 0.0 ? "" : " (bounds)"));//"Surface"
			specs.Add("");

			if (!canPressurize)
			{
				specs.Add(Lib.Color("Unpressurized", Lib.Kolor.Orange, true));
				specs.Add(Lib.Italic("Living in a suit is stressful"));
			}
			else
			{
				specs.Add("Depressurization", depressurizationSpeed.ToString("0.0 L/s"));
				if (canPressurize && reclaimAtmosphereFactor > 0.0)
				{
					double reclaimedAmount = reclaimAtmosphereFactor * M3ToL(volume);
					specs.Add(Lib.Bold(reclaimAtmosphereFactor.ToString("P0")) + " " + "reclaimed",  Lib.HumanReadableAmountCompact(reclaimedAmount) + " " + PartResourceLibrary.Instance.GetDefinition(reclaimResource).abbreviation);
					if (depressurizeECRate > 0.0)
						specs.Add("Require", Lib.Color(Lib.HumanReadableRate(depressurizeECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
				}
			}

			specs.Add("");
			if (maxShieldingFactor > 0.0)
				specs.Add("Shielding", "max" + " " + maxShieldingFactor.ToString("P0"));
			else
				specs.Add(Lib.Color("No radiation shielding", Lib.Kolor.Orange, true));

			

			if (isDeployable)
			{
				specs.Add("");
				specs.Add(Lib.Color(deployWithPressure ? Local.Habitat_info4 : "Deployable", Lib.Kolor.Cyan));

				if (deployWithPressure || !canRetract)
					specs.Add("Non-retractable");

				if (deployECRate > 0.0)
					specs.Add("Require", Lib.HumanReadableRate(deployECRate, "F3", ecAbbr));
			}

			if (isCentrifuge)
			{
				specs.Add("");
				specs.Add(Lib.Color("Gravity ring", Lib.Kolor.Cyan));
				specs.Add("Comfort bonus", Settings.ComfortFirmGround.ToString("P0"));
				specs.Add("Acceleration", Lib.Color(Lib.HumanReadableRate(accelerateECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
				specs.Add("Steady state", Lib.Color(Lib.HumanReadableRate(rotateECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
			}

			if (baseComfortsMask > 0)
			{
				specs.Add("");
				specs.Add(Lib.Color("Comfort", Lib.Kolor.Cyan), ComfortCommaList(baseComfortsMask));
				specs.Add("Bonus", GetComfortFactor(baseComfortsMask, false).ToString("P0"));
			}

			return specs;
		}

		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}

		public override string GetModuleDisplayName() { return Local.Habitat; }//"Habitat"

		public string GetModuleTitle() => Local.Habitat;

		public Callback<Rect> GetDrawModulePanelCallback() => null;

		public string GetPrimaryField()
		{
			return Lib.BuildString(
				Lib.Bold(Local.Habitat + " " + Local.Habitat_info1), // "Habitat" + "Volume"
				" : ",
				Lib.HumanReadableVolume(volume > 0.0 ? volume : Lib.PartBoundsVolume(part)),
				volume > 0.0 ? "" : " (bounds)");
		}

#endregion

	}


	[HarmonyPatch(typeof(InternalModel))]
	[HarmonyPatch("SpawnCrew")]
	class InternalModel_SpawnCrew
	{
		static bool Prefix(InternalModel __instance)
		{
			
			if (!__instance.part.vessel.KerbalismData().Parts.TryGet(__instance.part.flightID, out PartData pd))
				return true;

			if (pd.Habitat == null)
				return true;

			bool needHelmets =
				!(pd.Habitat.pressureState == PressureState.Pressurized
				|| pd.Habitat.pressureState == PressureState.Breatheable
				|| pd.Habitat.pressureState == PressureState.DepressurizingAboveThreshold);

			foreach (InternalSeat internalSeat in __instance.seats)
			{
				internalSeat.allowCrewHelmet = needHelmets;
			}

			return true;

		}
	}

	#region STATIC METHODS

	public static class HabitatLib
	{
		public static double M3ToL(double cubicMeters) => cubicMeters * 1000.0;

		public static double LToM3(double liters) => liters * 0.001;

		public static double GetComfortFactor(int comfortMask, bool clamped = true)
		{


			double factor = clamped ? 0.1 : 0.0;

			if (PreferencesComfort.Instance != null)
			{
				if ((comfortMask & (int)Comfort.firmGround) != 0) factor += PreferencesComfort.Instance.firmGround;
				if ((comfortMask & (int)Comfort.notAlone) != 0) factor += PreferencesComfort.Instance.notAlone;
				if ((comfortMask & (int)Comfort.callHome) != 0) factor += PreferencesComfort.Instance.callHome;
				if ((comfortMask & (int)Comfort.exercice) != 0) factor += PreferencesComfort.Instance.exercise;
				if ((comfortMask & (int)Comfort.panorama) != 0) factor += PreferencesComfort.Instance.panorama;
				if ((comfortMask & (int)Comfort.plants) != 0) factor += PreferencesComfort.Instance.plants;
			}
			else
			{
				if ((comfortMask & (int)Comfort.firmGround) != 0) factor += Settings.ComfortFirmGround;
				if ((comfortMask & (int)Comfort.notAlone) != 0) factor += Settings.ComfortNotAlone;
				if ((comfortMask & (int)Comfort.callHome) != 0) factor += Settings.ComfortCallHome;
				if ((comfortMask & (int)Comfort.exercice) != 0) factor += Settings.ComfortExercise;
				if ((comfortMask & (int)Comfort.panorama) != 0) factor += Settings.ComfortPanorama;
				if ((comfortMask & (int)Comfort.plants) != 0) factor += Settings.ComfortPlants;
			}

			return Math.Min(factor, 1.0);
		}

		public static string ComfortCommaList(int comfortMask)
		{
			string[] comforts = new string[6];
			if ((comfortMask & (int)Comfort.firmGround) != 0) comforts[0] = Local.Comfort_firmground;
			if ((comfortMask & (int)Comfort.notAlone) != 0) comforts[1] = Local.Comfort_notalone;
			if ((comfortMask & (int)Comfort.callHome) != 0) comforts[2] = Local.Comfort_callhome;
			if ((comfortMask & (int)Comfort.exercice) != 0) comforts[3] = Local.Comfort_exercise;
			if ((comfortMask & (int)Comfort.panorama) != 0) comforts[4] = Local.Comfort_panorama;
			if ((comfortMask & (int)Comfort.plants) != 0) comforts[5] = Local.Comfort_plants;

			string list = string.Empty;
			for (int i = 0; i < 6; i++)
			{
				if (!string.IsNullOrEmpty(comforts[i]))
				{
					if (list.Length > 0) list += ", ";
					list += comforts[i];
				}
			}
			return list;
		}

		public static string ComfortTooltip(int comfortMask, double comfortFactor)
		{
			string yes = Lib.BuildString("<b><color=#00ff00>", Local.Generic_YES, " </color></b>");
			string no = Lib.BuildString("<b><color=#ffaa00>", Local.Generic_NO, " </color></b>");
			return Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t{1}\n", Local.Comfort_firmground, (comfortMask & (int)Comfort.firmGround) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_exercise, (comfortMask & (int)Comfort.exercice) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_notalone, (comfortMask & (int)Comfort.notAlone) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_callhome, (comfortMask & (int)Comfort.callHome) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_panorama, (comfortMask & (int)Comfort.panorama) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_plants, (comfortMask & (int)Comfort.plants) != 0 ? yes : no),
				String.Format("<i>{0,-14}</i>\t{1}", Local.Comfort_factor, Lib.HumanReadablePerc(comfortFactor))
			);
		}

		public static string ComfortSummary(double comfortFactor)
		{
			if (comfortFactor >= 0.99) return Local.Module_Comfort_Summary1;//"ideal"
			else if (comfortFactor >= 0.66) return Local.Module_Comfort_Summary2;//"good"
			else if (comfortFactor >= 0.33) return Local.Module_Comfort_Summary3;//"modest"
			else if (comfortFactor > 0.1) return Local.Module_Comfort_Summary4;//"poor"
			else return Local.Module_Comfort_Summary5;//"none"
		}



		// traduce living space value to string
		public static string LivingSpaceFactorToString(double livingSpaceFactor)
		{
			if (livingSpaceFactor >= 0.99) return Local.Habitat_Summary1;//"ideal"
			else if (livingSpaceFactor >= 0.75) return Local.Habitat_Summary2;//"good"
			else if (livingSpaceFactor >= 0.5) return Local.Habitat_Summary3;//"modest"
			else if (livingSpaceFactor >= 0.25) return Local.Habitat_Summary4;//"poor"
			else return Local.Habitat_Summary5;//"cramped"
		}
	}
	#endregion
}
