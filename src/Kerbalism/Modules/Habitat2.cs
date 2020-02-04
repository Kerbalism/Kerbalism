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

namespace KERBALISM
{
	public sealed class ModuleKsmHabitat : PartModule, ISpecifics, IModuleInfo, IPartCostModifier
	{
		#region FIELDS / PROPERTIES
		// general config
		[KSPField] public bool canPressurize = true;              // can the habitat be pressurized ?
		[KSPField] public bool canDepressurize = true;            // can the habitat be depressurized ? (note : can't EVA from pressurized habitats)
		[KSPField] public double maxShieldingFactor = 1.0;        // how much shielding can be applied, in % of the habitat surface (can be > 1.0)
		[KSPField] public double depressurizationSpeed = 10.0;    // liters/second
		[KSPField] public double reclaimAtmosphereFactor = 0.75;  // % of atmosphere that will be recovered when depressurizing (producing "reclaimResource" back)
		[KSPField] public bool canRetract = true;                 // if false, can't be retracted once deployed
		[KSPField] public bool deployWithPressure = false;        // if true, deploying is done by pressurizing
		[KSPField] public double deployECRate = 0.1;              // EC/s consumed while deploying / inflating
		[KSPField] public double accelerateECRate = 0.1;          // EC/s consumed while accelerating a centrifuge
		[KSPField] public double rotateECRate = 0.1;              // EC/s consumed to sustain the centrifuge rotation

		// volume / surface config
		[KSPField] public double volume = 0.0;  // habitable volume in m^3, deduced from model if not specified
		[KSPField] public double surface = 0.0; // external surface in m^2, deduced from model if not specified
		[KSPField] public Lib.VolumeAndSurfaceMethod volumeAndSurfaceMethod = Lib.VolumeAndSurfaceMethod.Best;
		[KSPField] public bool substractAttachementNodesSurface = true;

		// animations config
		[KSPField] public string deployAnim = string.Empty; // deploy / inflate animation, if any
		[KSPField] public bool deployAnimReverse = false;   // deploy / inflate animation is reversed

		[KSPField] public string rotateAnim = string.Empty;        // rotate animation, if any
		[KSPField] public bool rotateAnimReverse = false;          // rotate animation is reversed
		[KSPField] public bool rotateIsTransform = false;          // rotateAnim is not an animation, but a Transform
		[KSPField] public float rotateSpinRate = 20.0f;            // centrifuge transform rotation (deg/s)
		[KSPField] public float rotateSpinAccelerationRate = 1.0f; // centrifuge transform acceleration (deg/s/s)
		[KSPField] public bool rotateIVA = true;                   // should the IVA rotate with the transform ?

		[KSPField] public string counterweightAnim = string.Empty;        // inflate animation, if any
		[KSPField] public bool counterweightAnimReverse = false;          // rotate animation is reversed
		[KSPField] public bool counterweightIsTransform = false;          // rotateAnim is not an animation, but a Transform
		[KSPField] public float counterweightSpinRate = 20.0f;            // centrifuge transform rotation (deg/s)
		[KSPField] public float counterweightSpinAccelerationRate = 1.0f; // centrifuge transform acceleration (deg/s/s)

		// resources config
		[KSPField] public string reclaimResource = "Nitrogen"; // Nitrogen
		[KSPField] public string atmosphereResource = "Atmosphere"; //KsmAtmosphere
		[KSPField] public string wasteAtmosphereResource = "WasteAtmosphere"; // KsmWasteAtmosphere
		[KSPField] public string shieldingResource = "Shielding"; // KsmShielding

		// fixed characteristics determined at prefab compilation from OnLoad()
		// do not use these in configs, they are KSPField just so they are automatically copied over on part instancing
		[KSPField] public bool isDeployable;
		[KSPField] public bool isCentrifuge;
		[KSPField] public bool hasShielding;
		[KSPField] public int fixedComfortsMask;
		[KSPField] public float ShieldingCapacityCost;

		// editor state persistence
		[KSPField(isPersistant = true)] public PressureState pressureState = PressureState.Pressurized;
		[KSPField(isPersistant = true)] public bool habitatEnabled = true;
		[KSPField(isPersistant = true)] public bool isDeployed = false;
		[KSPField(isPersistant = true)] public bool isRotating = false;

		// internal state
		private HabitatData data;
		public HabitatData HabitatData => data;

		// animations
		private Animator deployAnimator;
		private Animator rotateAnimator;
		private Animator counterweightAnimator;
		private Transformator rotateTranformator;
		private Transformator counterweightTransformator;

		// caching frequently used things
		private PartResource atmoRes;
		private PartResource wasteRes;
		private PartResource shieldRes;
		private BaseEvent pressureEvent;
		private double volumeLiters;

		// static volume / surface cache
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
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Status")]
		public string suitPawInfo;
#else
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Pressure in suits", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
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
				fixedComfortsMask = 0;
				foreach (string comfortString in node.GetValues("comfort"))
				{
#if KSP15_16 || KSP17
					Comfort comfort;
					try
					{
						comfort = (Comfort)Enum.Parse(typeof(Comfort), comfortString);
						fixedComfortsMask |= 1 << (int)comfort;
					}
					catch
					{
						Lib.Log($"Unrecognized comfort `{comfortString}` in ModuleKsmHabitat config for part {part.name}");
					}
#else
					if (Enum.TryParse(comfortString, out Comfort comfort))
						fixedComfortsMask |= 1 << (int)comfort;
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
							deployAnimator.Still(1.0);

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
				isDeployable = deployAnimator != null && deployAnimator.IsDefined;
				isCentrifuge = (rotateTranformator != null && rotateTranformator.IsDefined) || (rotateAnimator != null && rotateAnimator.IsDefined);
				hasShielding = Features.Radiation && maxShieldingFactor > 0.0;

				// ensure proper initialization
				if (!canPressurize || pressureState == PressureState.Depressurized)
					pressureState = PressureState.DepressurizingStart;
				else
					pressureState = PressureState.PressurizingStart;

				// precalculate shielding cost and add resources
				volumeLiters = M3ToL(volume);
				double currentVolumeLiters = canPressurize ? volumeLiters : 0.0;
				atmoRes = Lib.AddResource(part, atmosphereResource, volumeLiters, volumeLiters);
				wasteRes = Lib.AddResource(part, wasteAtmosphereResource, 0.0, volumeLiters);

				if (hasShielding)
				{
					shieldRes = Lib.AddResource(part, shieldingResource, 0.0, surface * maxShieldingFactor);
					ShieldingCapacityCost = (float)(shieldRes.maxAmount * shieldRes.info.unitCost);
				}
			}
		}

		// pseudo-ctor
		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			volumeLiters = M3ToL(volume);

			// setup animations and deduce if the hab is deployable / centrifuge
			SetupAnimations();

			// get internal state data
			if (Lib.IsFlight())
				data = vessel.KerbalismData().Parts.Get(part.flightID).Habitat;

			// if not created (editor/new vessel), initialize the data and setup pseudo-resources
			if (data == null)
			{
				data = new HabitatData(this);
				data.habitatEnabled = habitatEnabled;
				data.pressureState = pressureState;
				data.isDeployed = isDeployed;
				data.isRotating = isRotating;
				data.crewCount = Lib.CrewCount(part);
				ToggleHabitat(habitatEnabled);

				if (Lib.IsFlight())
					vessel.KerbalismData().Parts.Get(part.flightID).Habitat = data;
			}

			data.module = this;

			atmoRes = part.Resources[atmosphereResource];
			wasteRes = part.Resources[wasteAtmosphereResource];
			if (hasShielding)
				shieldRes = part.Resources[shieldingResource];

			ToggleHabitat(data.habitatEnabled);
				
			if (part.isVesselEVA)
				Events["ToggleHabitat"].active = false;
			else
				Events["ToggleHabitat"].guiName = data.habitatEnabled ? "Disable habitat" : "Enable habitat";

			if (canPressurize)
			{
				pressureEvent = Events["TogglePressure"];
				pressureEvent.active = true;
			}

			if (isDeployable && (canRetract || !data.isDeployed))
			{
				Events["ToggleDeploy"].active = true;
				Events["ToggleDeploy"].guiName = data.isDeployed ? "Retract" : "Deploy";
			}

			if (isCentrifuge)
			{
				Events["ToggleRotate"].active = true;
				Events["ToggleRotate"].guiName = data.isRotating ? "Stop rotation" : "Start rotation";
			}

#if DEBUG
			Events["LogVolumeAndSurface"].active = true;
#else
			Events["LogVolumeAndSurface"].active = Settings.VolumeAndSurfaceLogging;
#endif

		}

		public void OnDestroy()
		{
			// avoid memory leaks
			if (data != null && data.module != null)
				data.module = null;
		}

		private void SetupAnimations()
		{
			if (deployAnim != string.Empty)
				deployAnimator = new Animator(part, deployAnim, deployAnimReverse);

			if (rotateAnim != string.Empty)
			{
				if (rotateIsTransform)
					rotateTranformator = new Transformator(part, rotateAnim, rotateSpinRate, rotateSpinAccelerationRate, rotateIVA);
				else
					rotateAnimator = new Animator(part, rotateAnim, rotateAnimReverse);
			}

			if (counterweightAnim != string.Empty)
			{
				if (rotateIsTransform)
					rotateTranformator = new Transformator(part, counterweightAnim, counterweightSpinRate, counterweightSpinAccelerationRate);
				else
					counterweightAnimator = new Animator(part, counterweightAnim, counterweightAnimReverse);
			}
		}


		#endregion

		public void Update()
		{
			double suitsVolume = data.crewCount * M3ToL(Settings.PressureSuitVolume);
			double habPressure;
			if (data.pressureState == PressureState.Depressurized)
				habPressure = 0.0;
			else
				habPressure = (atmoRes.maxAmount - suitsVolume) > 0.0 ? Lib.Clamp((atmoRes.amount - suitsVolume) / (atmoRes.maxAmount - suitsVolume), 0.0, 1.0) : 0.0;

			mainPawInfo =
				Lib.Color(habPressure > Settings.PressureThreshold, habPressure.ToString("0.00 atm"), Lib.Kolor.Green, Lib.Kolor.Orange)
				+ volume.ToString(" (0.0 m3)")
				+ " - Crew: " + data.crewCount + "/" + part.CrewCapacity;

			if (data.pressureState != PressureState.Pressurized && data.crewCount > 0)
			{
				suitPawInfo = Math.Min(atmoRes.amount / suitsVolume, 1.0).ToString("0.00 atm");
				Fields["suitPawInfo"].guiActive = true;
				Fields["suitPawInfo"].guiActiveEditor = true;
			}
			else
			{
				Fields["suitPawInfo"].guiActive = false;
				Fields["suitPawInfo"].guiActiveEditor = false;
			}

			if (canPressurize)
			{
				switch (data.pressureState)
				{
					case PressureState.Pressurized:
						pressureEvent.guiName = "Depressurize" + Lib.HumanReadableCountdown((data.atmoAmount - suitsVolume) / depressurizationSpeed);
						break;
					case PressureState.Depressurized:
						pressureEvent.guiName = "Pressurize";
						break;
					case PressureState.Pressurizing:
						pressureEvent.guiName = "Pressurizing...";
						break;
					case PressureState.Depressurizing:
						pressureEvent.guiName = "Depressurizing : " + Lib.HumanReadableCountdown((data.atmoAmount - suitsVolume) / depressurizationSpeed);
						break;
				}
			}
		}

#region FIXEDUPDATE

		public void SyncAfterCrewTransfered()
		{
			atmoRes.maxAmount = M3ToL(data.enabledVolume);
			wasteRes.maxAmount = M3ToL(data.enabledVolume);
			atmoRes.amount = M3ToL(data.atmoAmount);
			wasteRes.amount = M3ToL(data.wasteAmount);
		}

		public void FixedUpdate()
		{
			bool isEditor = Lib.IsEditor();
			VesselData vd = isEditor ? null : vessel.KerbalismData();

			// constantly disable shielding flow when habitat is disabled, so it isn't accounted for in the resource sim
			if (hasShielding)
				shieldRes.flowState = data.habitatEnabled;

			// TODO : This will conflict with inflatables deployement. Maybe there is a better way to handle that ?
			if (!isEditor && vd.EnvBreathable)
			{
				atmoRes.maxAmount = volumeLiters;
				wasteRes.maxAmount = volumeLiters;
				atmoRes.amount = atmoRes.maxAmount;
				wasteRes.amount = 0.0;
				atmoRes.flowState = false;
				wasteRes.flowState = false;
				data.pressureState = PressureState.Pressurized;
			}
			else
			{
				switch (data.pressureState)
				{
					case PressureState.Pressurized:
						// if atmo drop below the kerbal in suits (or 0) volume, switch to depressurized state
						if (atmoRes.amount <= data.crewCount * M3ToL(Settings.PressureSuitVolume))
							data.pressureState = PressureState.DepressurizingStart;
						// if pressure drop below the minimum habitable pressure, switch to partial pressure state
						else if (atmoRes.amount / atmoRes.maxAmount < Settings.PressureThreshold)
							data.pressureState = PressureState.PressureDropped;
						break;

					case PressureState.PressureDropped:
						// set enabled volume to the kerbal in suits volume (or 0)
						data.enabledVolume = data.crewCount * Settings.PressureSuitVolume;

						// make the kerbals put their helmets
						if (data.crewCount > 0 && vessel.isActiveVessel && part.internalModel != null)
							Lib.RefreshIVAAndPortraits(); 

						// go to pressurizing state
						data.pressureState = PressureState.Pressurizing;
						break;

					case PressureState.Depressurized:
						break;

					case PressureState.PressurizingStart:
						atmoRes.maxAmount = volumeLiters;
						wasteRes.maxAmount = volumeLiters;
						atmoRes.flowState = true;
						wasteRes.flowState = true;

						if (isEditor)
							data.pressureState = PressureState.PressurizingEnd;
						else
							data.pressureState = PressureState.Pressurizing;

						break;

					case PressureState.Pressurizing:

						// if pressure go back to the minimum habitable pressure, switch to pressurized state
						if (atmoRes.amount / atmoRes.maxAmount > Settings.PressureThreshold)
							data.pressureState = PressureState.PressurizingEnd;
						break;

					case PressureState.PressurizingEnd:
						data.enabledVolume = volume;

						if (isEditor)
						{
							atmoRes.amount = volumeLiters;
							wasteRes.amount = 0.0;
						}
						else
						{
							// make the kerbals remove their helmets
							// this works in conjunction with the SpawnCrew prefix patch that check if the part is pressurized or not on spawning the IVA.
							if (data.crewCount > 0 && vessel.isActiveVessel && part.internalModel != null)
								Lib.RefreshIVAAndPortraits();
						}

						data.pressureState = PressureState.Pressurized;
						break;

					case PressureState.DepressurizingStart:
						data.enabledVolume = data.crewCount * Settings.PressureSuitVolume;
						atmoRes.flowState = false;
						wasteRes.flowState = false;

						if (isEditor)
						{
							data.pressureState = PressureState.DepressurizingEnd;
						}
						else
						{
							// make the kerbals put their helmets
							// this works in conjunction with the SpawnCrew prefix patch that check if the part is pressurized or not on spawning the IVA.
							if (data.crewCount > 0 && vessel.isActiveVessel && part.internalModel != null)
								Lib.RefreshIVAAndPortraits();

							data.pressureState = PressureState.Depressurizing;
						}

						break;

					case PressureState.Depressurizing:

						double atmoTarget = data.crewCount * M3ToL(Settings.PressureSuitVolume);

						if (atmoRes.amount <= atmoTarget)
						{
							data.pressureState = PressureState.DepressurizingEnd;
							break;
						}

						double newAtmoAmount = atmoRes.amount - (depressurizationSpeed * Kerbalism.elapsed_s);
						newAtmoAmount = Math.Max(newAtmoAmount, atmoTarget);

						wasteRes.amount *= atmoRes.amount > 0.0 ? newAtmoAmount / atmoRes.amount : 1.0;
						if (wasteRes.amount < 0.0) wasteRes.amount = 0.0;

						if (reclaimAtmosphereFactor > 0.0 && newAtmoAmount / atmoRes.maxAmount >= 1.0 - reclaimAtmosphereFactor)
						{
							ResourceCache.Produce(vessel, reclaimResource, atmoRes.amount - newAtmoAmount, ResourceBroker.Habitat);
						}

						atmoRes.amount = newAtmoAmount;
						break;

					case PressureState.DepressurizingEnd:

						atmoRes.flowState = true;
						wasteRes.flowState = true;

						double depressurizedVolumeL = M3ToL(data.crewCount * Settings.PressureSuitVolume);
						atmoRes.maxAmount = depressurizedVolumeL;
						wasteRes.maxAmount = depressurizedVolumeL;

						if (isEditor)
						{
							atmoRes.amount = atmoRes.maxAmount;
							wasteRes.amount = 0.0;
						}
						else
						{
							wasteRes.amount = Math.Min(wasteRes.amount, wasteRes.maxAmount);
						}

						data.pressureState = PressureState.Depressurized;
						break;
				}
			}

			if (data.habitatEnabled)
			{
				switch (data.pressureState)
				{
					case PressureState.Pressurized:
					case PressureState.Depressurized:
						data.atmoAmount = LToM3(atmoRes.amount);
						data.wasteAmount = LToM3(wasteRes.amount);
						break;
					default:
						data.atmoAmount = Math.Min(LToM3(atmoRes.amount), data.enabledVolume);
						data.wasteAmount = LToM3(M3ToL(data.enabledVolume) * (wasteRes.amount / wasteRes.maxAmount));
						break;
				}
				data.shieldingAmount = hasShielding ? shieldRes.amount : 0.0;
			}
			else
			{
				data.enabledVolume = 0.0;
				data.atmoAmount = 0.0;
				data.wasteAmount = 0.0;
				data.shieldingAmount = 0.0;
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
			if (isDeployable && !data.habitatEnabled && !data.isDeployed)
			{
				ToggleDeploy();
				return;
			}

			if (data.habitatEnabled && Lib.IsCrewed(part))
			{
				if (Lib.IsEditor())
				{
					// TODO : prevent adding crew to disabled habitats through the editor crew assignement dialog
					Lib.EditorClearPartCrew(part);
				}
				else
				{
					List<ProtoCrewMember> crewLeft = Lib.TryTransferCrewElsewhere(part, false);

					if (crewLeft.Count > 0)
					{
						string message = "Not enough crew capacity in the vessel to transfer those Kerbals :\n";
						crewLeft.ForEach(a => message += a.displayName + "\n");
						Message.Post($"Habitat in {part.partInfo.title} couldn't be disabled.", message);
						return;
					}
					else
					{
						Message.Post($"Habitat in {part.partInfo.title} has been disabled.", "Crew was transfered in the rest of the vessel");
					}
				}
			}

			if (data.habitatEnabled)
			{
				if (data.isRotating)
					ToggleRotate();

				Events["ToggleHabitat"].guiName = "Enable habitat";
				//part.crewTransferAvailable = false;
				habitatEnabled = false; // keep state in sync in the editor, but we don't care for that field in flight
				ToggleHabitat(false);

			}
			else
			{
				Events["ToggleHabitat"].guiName = "Disable habitat";
				//part.crewTransferAvailable = true;
				habitatEnabled = true; // keep state in sync in the editor, but we don't care for that field in flight
				ToggleHabitat(true);
			}
		}

		private void ToggleHabitat(bool enabled)
		{
			data.habitatEnabled = enabled;

			if (enabled)
			{
				data.enabledSurface = surface;

				if (data.pressureState == PressureState.Depressurized)
				{
					data.enabledVolume = data.crewCount * Settings.PressureSuitVolume;
					data.enabledComfortsMask = 0;
				}
				else
				{
					data.enabledVolume = volume;
					data.enabledComfortsMask = fixedComfortsMask;
				}
					
				if (data.isRotating)
					data.enabledComfortsMask |= 1 << (int)Comfort.FirmGround;
			}
			else
			{
				data.enabledSurface = 0.0;
				data.enabledVolume = 0.0;
				data.enabledComfortsMask = 0;
			}
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
			switch (data.pressureState)
			{
				case PressureState.Pressurized:
				case PressureState.Pressurizing:
					data.pressureState = PressureState.DepressurizingStart;
					break;
				case PressureState.Depressurized:
				case PressureState.Depressurizing:
					data.pressureState = PressureState.PressurizingStart;
					break;
				
			}
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
			if (data.isDeployed)
			{
				if (data.habitatEnabled)
				{
					ToggleHabitat();

					if (data.habitatEnabled)
						return;
				}

				deployAnimator.Play(false, false);
			}
			else
			{
				deployAnimator.Play(true, false, ToggleHabitat);
			}
		}

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = false)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = false, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
#endif
		public void ToggleRotate()
		{

		}





		// action groups
		//[KSPAction("Enable/Disable Habitat")] public void Action(KSPActionParam param) { Toggle(); }

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
			Specifics specs = new Specifics();
			specs.Add(Local.Habitat_info1, Lib.HumanReadableVolume(volume > 0.0 ? volume : Lib.PartBoundsVolume(part)) + (volume > 0.0 ? "" : " (bounds)"));//"Volume"
			specs.Add(Local.Habitat_info2, Lib.HumanReadableSurface(surface > 0.0 ? surface : Lib.PartBoundsSurface(part)) + (surface > 0.0 ? "" : " (bounds)"));//"Surface"
			//if (inflate.Length > 0) specs.Add(Local.Habitat_info4, Local.Habitat_yes);//"Inflatable""yes"
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

#region IPartCostModifier

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => ShieldingCapacityCost;
		public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

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

			bool needHelmets = pd.Habitat.pressureState != PressureState.Pressurized;

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

		public static double GetComfortFactor(int comfortMask)
		{
			double factor = 0.1;
			if ((comfortMask & (int)Comfort.FirmGround) != 0) factor += PreferencesComfort.Instance.firmGround;
			if ((comfortMask & (int)Comfort.NotAlone) != 0) factor += PreferencesComfort.Instance.notAlone;
			if ((comfortMask & (int)Comfort.CallHome) != 0) factor += PreferencesComfort.Instance.callHome;
			if ((comfortMask & (int)Comfort.Exercice) != 0) factor += PreferencesComfort.Instance.exercise;
			if ((comfortMask & (int)Comfort.Panorama) != 0) factor += PreferencesComfort.Instance.panorama;
			if ((comfortMask & (int)Comfort.Plants) != 0) factor += PreferencesComfort.Instance.plants;
			return Lib.Clamp(factor, 0.1, 1.0);
		}

		public static string ComfortTooltip(int comfortMask, double comfortFactor)
		{
			string yes = Lib.BuildString("<b><color=#00ff00>", Local.Generic_YES, " </color></b>");
			string no = Lib.BuildString("<b><color=#ffaa00>", Local.Generic_NO, " </color></b>");
			return Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t{1}\n", Local.Comfort_firmground, (comfortMask & (int)Comfort.FirmGround) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_exercise, (comfortMask & (int)Comfort.FirmGround) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_notalone, (comfortMask & (int)Comfort.FirmGround) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_callhome, (comfortMask & (int)Comfort.FirmGround) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_panorama, (comfortMask & (int)Comfort.FirmGround) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_plants, (comfortMask & (int)Comfort.FirmGround) != 0 ? yes : no),
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
