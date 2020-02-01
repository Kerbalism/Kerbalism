using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static KERBALISM.HabitatData;

namespace KERBALISM
{
	public sealed class Habitat : PartModule, ISpecifics, IModuleInfo, IPartCostModifier
	{
		#region FIELDS / PROPERTIES
		// general config
		[KSPField] public bool canPressurize = true;              // can the habitat be pressurized ?
		[KSPField] public double maxShieldingFactor = 1.0;        // how much shielding can be applied, in % of the habitat surface
		[KSPField] public double depressurizationSpeed = 0.1;     // units/second/m3
		[KSPField] public bool requirePressureToDeploy = false;   // if true, the deploy state is linked to current pressure
		[KSPField] public double deployedPressureThreshold = 0.0; // % of pressure required for a deployable habitat to stay deployed 
		[KSPField] public double reclaimPressureThreshold = 0.0;  // % of Atmosphere that will be recovered when depressurizing (producing nitrogen back)
		[KSPField] public double deployECRate = 0.1;              // EC/s consumed while deploying / inflating
		[KSPField] public double accelerateECRate = 0.1;          // EC/s consumed while accelerating a centrifuge
		[KSPField] public double rotateECRate = 0.1;              // EC/s consumed to sustain the centrifuge rotation

		// volume / surface config
		[KSPField] public double volume = 0.0;  // habitable volume in m^3, deduced from model if not specified
		[KSPField] public double surface = 0.0; // external surface in m^2, deduced from model if not specified
		[KSPField] public Lib.VolumeAndSurfaceMethod volumeAndSurfaceMethod = Lib.VolumeAndSurfaceMethod.Best;
		[KSPField] public bool substractAttachementNodesSurface = true;

		// resources config
		[KSPField] public string AtmosphereResourceName = "KsmAtmosphere";
		[KSPField] public string WasteAtmosphereResourceName = "KsmWasteAtmosphere";
		[KSPField] public string ShieldingResourceName = "KsmShielding";

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

		// editor state persistence
		[KSPField(isPersistant = true)] public PressureState pressureState = PressureState.Pressurized;
		[KSPField(isPersistant = true)] public bool habitatEnabled = true;
		[KSPField(isPersistant = true)] public bool isDeployed = false;
		[KSPField(isPersistant = true)] public bool isRotating = false;
		[KSPField(isPersistant = true)] public int enabledComforts;

		// internal state
		private HabitatData habitatData;
		private float shieldingCost;

		// fixed characteristics shortcuts
		private bool isDeployable;
		private bool isGravityRing;

		// animations
		private Animator deployAnimator;
		private Animator rotateAnimator;
		private Animator counterweightAnimator;
		private Transformator rotateTranformator;
		private Transformator counterweightTransformator;

		// caching partresources
		private PartResource atmoResource;
		private PartResource wasteResource;
		private PartResource shieldingResource;

		// static volume / surface cache
		public static Dictionary<string, Lib.PartVolumeAndSurfaceInfo> habitatDatabase;
		public const string habitatDataCacheNodeName = "KERBALISM_HABITAT_INFO";
		public static string HabitatDataCachePath => Path.Combine(Lib.KerbalismRootPath, "HabitatData.cache");

		// rmb ui status strings
#if KSP15_16
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Volume")]
		public string Volume;
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Surface")]
		public string Surface;
#else
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Volume", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public string Volume;
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Surface", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public string Surface;
#endif

		#endregion

		#region INIT

		// volume / surface evaluation at prefab compilation
		public override void OnLoad(ConfigNode node)
		{
			// volume/surface calcs are quite slow and memory intensive, so we do them only once on the prefab
			// then get the prefab values from OnStart. Moreover, we cache the results in the 
			// Kerbalism\HabitatData.cache file and reuse those cached results on next game launch.
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				SetupAnimations();

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
			}
		}

		// pseudo-ctor
		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// get the base volume/surface from prefab
			if (volume <= 0.0 || surface <= 0.0)
			{
				Habitat prefab = part.partInfo.partPrefab.FindModuleImplementing<Habitat>();
				if (volume <= 0.0) volume = prefab.volume;
				if (surface <= 0.0) surface = prefab.surface;
			}

			// get internal state data
			if (Lib.IsEditor())
			{
				habitatData = new HabitatData(this);
			}
			else
			{

			}

			// setup animations and deduce if the hab is deployable / centrifuge
			SetupAnimations();



			
			SetupPseudoResources();



			// set RMB UI status strings
			Volume = Lib.HumanReadableVolume(volume);
			Surface = Lib.HumanReadableSurface(surface);

			// hide toggle if specified
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

#if DEBUG
			Events["LogVolumeAndSurface"].active = true;
#else
			Events["LogVolumeAndSurface"].active = Settings.VolumeAndSurfaceLogging;
#endif

		}

		private void SetupAnimations()
		{
			if (deployAnim != string.Empty)
			{
				deployAnimator = new Animator(part, deployAnim, deployAnimReverse);
				isDeployable = deployAnimator.IsDefined;
			}

			if (rotateAnim != string.Empty)
			{
				if (rotateIsTransform)
				{
					rotateTranformator = new Transformator(part, rotateAnim, rotateSpinRate, rotateSpinAccelerationRate, rotateIVA);
					isGravityRing = rotateTranformator.IsDefined;
				}
				else
				{
					rotateAnimator = new Animator(part, rotateAnim, rotateAnimReverse);
					isGravityRing = rotateAnimator.IsDefined;
				}
			}

			if (counterweightAnim != string.Empty)
			{
				if (rotateIsTransform)
					rotateTranformator = new Transformator(part, counterweightAnim, counterweightSpinRate, counterweightSpinAccelerationRate);
				else
					counterweightAnimator = new Animator(part, counterweightAnim, counterweightAnimReverse);
			}
		}

		private void SetupPseudoResources()
		{
			// if never set, this is the case if:
			// - part is added in the editor
			// - module is configured first time either in editor or in flight
			// - module is added to an existing savegame
			if (!part.Resources.Contains(Settings.VolumeResource))
			{
				double currentVolume;
				double currentSurface;
				if (habitatEnabled)
				{
					currentSurface = surface;

					if (pressureState == PressureState.Depressurized)
						currentVolume = Lib.CrewCount(part) * Settings.PressureSuitVolume;
					else
						currentVolume = volume;
				}
				else
				{
					currentSurface = 0.0;
					currentVolume = 0.0;
				}

				// add volume/surface pseudo resources
				volumeResource = Lib.AddResource(part, Settings.VolumeResource, 0.0, currentVolume);
				surfaceResource = Lib.AddResource(part, Settings.SurfaceResource, 0.0, currentSurface);

				// add atmosphere pseudo resources
				atmoResource = Lib.AddResource(part, Settings.AtmosphereResource, (pressureState == State.Pressurized) ? currentVolume : 0.0, currentVolume);
				wasteResource = Lib.AddResource(part, Settings.WasteAtmosphereResource, 0.0, currentVolume);

				// add external surface shielding
				shieldingResource = Lib.AddResource(part, Settings.ShieldingResource, 0.0, currentSurface * maxShieldingFactor);
			}
			else
			{
				volumeResource = part.Resources[Settings.VolumeResource];
				surfaceResource = part.Resources[Settings.SurfaceResource];
				atmoResource = part.Resources[Settings.AtmosphereResource];
				wasteResource = part.Resources[Settings.WasteAtmosphereResource];
				shieldingResource = part.Resources[Settings.ShieldingResource];
			}

			// add the cost of shielding to the base part cost
			shieldingCost = (float)shieldingResource.amount * shieldingResource.info.unitCost;

			// if shielding feature is disabled, just hide it
			shieldingResource.isVisible = Features.Shielding;
		}


		#endregion

		public void Update()
		{
			// update ui
			string status_str = string.Empty;
			switch (state)
			{
				case State.Enabled: status_str = "enabled"; break;
				case State.Disabled: status_str = "disabled"; break;
				case State.Pressurizing: status_str = inflate.Length == 0 ? "equalizing..." : "inflating..."; break;
				case State.Depressurizing: status_str = inflate.Length == 0 ? "venting..." : "deflating..."; break;
			}
			Events["Toggle"].guiName = Lib.StatusToggle("Habitat", status_str);

			// if there is an inflate animation, set still animation from pressure
			inflate_anim.Still(Lib.Level(part, "Atmosphere", true));
		}




		#region FIXEDUPDATE

		public void FixedUpdate()
		{
			currentPressure = atmoResource.amount / atmoResource.maxAmount;

			// 
			if (requirePressureToDeploy && isDeployed)
			{
				deployAnimator.Still(currentPressure);
			}

			// state machine
			switch (pressureState)
			{
				case State.Pressurized:
					CheckBreatheableAtmosphere();
					SetPseudoResourcesFlow(true);
					break;

				case State.Depressurized:
					SetPseudoResourcesFlow(false);
					break;

				case State.Pressurizing:
					CheckBreatheableAtmosphere();
					SetPseudoResourcesFlow(true);
					pressureState = Pressurize();
					break;

				case State.Depressurizing:
					SetPseudoResourcesFlow(false);
					pressureState = Depressurize();
					break;
			}
		}

		private State Pressurize()
		{
			// in flight
			if (Lib.IsFlight())
			{
				if (currentPressure == 1.0)
				{
					effectiveVolume = volume;

					return State.Pressurized;
				}
					

				// equalization still in progress
				return State.Pressurizing;
			}
			// in the editors
			else
			{
				// set amount to max capacity
				atmoResource.amount = atmoResource.maxAmount;

				// return new state
				return State.Pressurized;
			}
		}

		private State Depressurize()
		{
			// in flight
			if (Lib.IsFlight())
			{
				// shortcuts
				PartResource atmo = part.Resources["Atmosphere"];
				PartResource waste = part.Resources["WasteAtmosphere"];

				// venting succeeded if the amount reached zero
				if (atmo.amount == 0.0 && waste.amount == 0.0) return State.Disabled;

				// how much to vent
				double rate = volume * depressurizationSpeed * Kerbalism.elapsed_s;
				double atmo_k = atmo.amount / (atmo.amount + waste.amount);
				double waste_k = waste.amount / (atmo.amount + waste.amount);

				// consume from the part, clamp amount to what's available
				atmo.amount = Math.Max(atmo.amount - rate * atmo_k, 0.0);
				waste.amount = Math.Max(waste.amount - rate * waste_k, 0.0);

				// venting still in progress
				return State.Depressurizing;
			}
			// in the editors
			else
			{
				// set amount to zero
				part.Resources["Atmosphere"].amount = 0.0;
				part.Resources["WasteAtmosphere"].amount = 0.0;

				// return new state
				return State.Disabled;
			}
		}

		private void CheckBreatheableAtmosphere()
		{
			// instant pressurization and scrubbing inside breathable atmosphere
			if (!Lib.IsEditor() && vessel.KerbalismData().EnvBreathable && inflate.Length == 0)
			{
				var atmo = part.Resources["Atmosphere"];
				var waste = part.Resources["WasteAtmosphere"];
				if (Features.Pressure) atmo.amount = atmo.maxAmount;
				if (Features.Poisoning) waste.amount = 0.0;
			}
		}

		void SetPseudoResourcesFlow(bool enabled)
		{
			Lib.SetResourceFlow(part, "Atmosphere", enabled);
			Lib.SetResourceFlow(part, "WasteAtmosphere", enabled);
			Lib.SetResourceFlow(part, "Shielding", enabled);
		}

		#endregion


		#region STATE LOGIC

		private void DisableHabitat()
		{
			habitatEnabled = false;

			// disable shielding, but save the amount first
			disabledShieldingAmount = shieldingResource.amount;
			shieldingResource.amount = 0.0;
			shieldingResource.isTweakable = false;

			// remove surface and volume from the vessel total
			surfaceResource.maxAmount = 0.0;
			volumeResource.maxAmount = 0.0;

			// disable comforts
			enabledComforts &= ~(int)Comfort.Exercice;
			enabledComforts &= ~(int)Comfort.Panorama;
			enabledComforts &= ~(int)Comfort.Plants;
		}

		#endregion

		#region UI INTERACTIONS

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
#endif
		public void ToggleHabitat()
		{
			// - disable comforts
			// - disable shielding
			// - disable volume

			// if manned, we can't depressurize
			if (habitatEnabled && Lib.IsCrewed(part))
			{
				if (Lib.IsFlight())
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
						Message.Post($"Habitat in {part.partInfo.title} has be disabled.", "Crew was transfered in the rest of the vessel");
						habitatEnabled = false;
					}




				}
				else
				{

				}

				Message.Post(Lib.BuildString("Can't disable <b>", Lib.PartName(part), " habitat</b> while crew is inside"));
				return;
			}

			// state switching
			switch (state)
			{
				case State.Enabled: state = State.Depressurizing; break;
				case State.Disabled: state = State.Pressurizing; break;
				case State.Pressurizing: state = State.Depressurizing; break;
				case State.Depressurizing: state = State.Pressurizing; break;
			}
		}

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
#endif
		public void TogglePressure()
		{


			// state switching
			switch (state)
			{
				case State.Enabled: state = State.Depressurizing; break;
				case State.Disabled: state = State.Pressurizing; break;
				case State.Pressurizing: state = State.Depressurizing; break;
				case State.Depressurizing: state = State.Pressurizing; break;
			}
		}


		// action groups
		[KSPAction("Enable/Disable Habitat")] public void Action(KSPActionParam param) { Toggle(); }

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
			if (inflate.Length > 0) specs.Add(Local.Habitat_info4, Local.Habitat_yes);//"Inflatable""yes"
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

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => shieldingCost;
		public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

		#endregion

		#region STATIC METHODS

		// return habitat volume in a vessel in m^3
		public static double GetVolume(Vessel v, VesselResources vesselres)
		{
			// we use capacity: this mean that partially pressurized parts will still count,
			return vesselres.GetResource(v, Settings.VolumeResource).Capacity; // / 1e3; ?????
		}

		// return habitat surface in a vessel in m^2
		public static double GetSurface(Vessel v, VesselResources vesselres)
		{
			// we use capacity: this mean that partially pressurized parts will still count,
			return vesselres.GetResource(v, Settings.SurfaceResource).Capacity;
		}

		// return normalized pressure in a vessel
		public static double GetPressure(Vessel v, VesselResources vesselres)
		{
			// the pressure is simply the atmosphere level
			return vesselres.GetResource(v, Settings.AtmosphereResource).Level;
		}

		// return waste level in a vessel atmosphere
		public static double GetPoisoning(Vessel v, VesselResources vesselres)
		{
			// the proportion of co2 in the atmosphere is simply the level of WasteAtmo
			return vesselres.GetResource(v, Settings.WasteAtmosphereResource).Level;
		}

		// return shielding factor in a vessel
		public static double Shielding(Vessel v, VesselResources vesselres)
		{
			return Radiation.ShieldingEfficiency(vesselres.GetResource(v, Settings.ShieldingResource).Amount / vesselres.GetResource(v, Settings.SurfaceResource).Capacity);
		}

		// return living space factor in a vessel
		public static double NormalizedLivingSpace(Vessel v, double vesselVolume)
		{
			// living space is the volume per-capita normalized against an 'ideal living space' and clamped in an acceptable range
			return Lib.Clamp(VolumePerCrew(v, vesselVolume) / PreferencesComfort.Instance.livingSpace, 0.1, 1.0);
		}

		public static double VolumePerCrew(Vessel v, double vesselVolume)
		{
			// living space is the volume per-capita normalized against an 'ideal living space' and clamped in an acceptable range
			return vesselVolume / Math.Max(1, Lib.CrewCount(v));
		}

		// return a verbose description of shielding capability
		public static string ShieldingToString(double v)
		{
			return v <= double.Epsilon ? Local.Habitat_none : Lib.BuildString((20.0 * v / PreferencesRadiation.Instance.shieldingEfficiency).ToString("F2"), " mm");//"none"
		}

		// traduce living space value to string
		public static string NormalizedLivingSpaceToString(double normalizedLivingSpace)
		{
			if (normalizedLivingSpace >= 0.99) return Local.Habitat_Summary1;//"ideal"
			else if (normalizedLivingSpace >= 0.75) return Local.Habitat_Summary2;//"good"
			else if (normalizedLivingSpace >= 0.5) return Local.Habitat_Summary3;//"modest"
			else if (normalizedLivingSpace >= 0.25) return Local.Habitat_Summary4;//"poor"
			else return Local.Habitat_Summary5;//"cramped"
		}

		#endregion



	}
}
