using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
    public class Habitat : PartModule, ISpecifics, IModuleInfo, IPartCostModifier
	{
		// habitat state
		public enum State
		{
			/// <summary> hab is enabled : it is deployed/inflated, can hold crew and its atmo can flow </summary>
			enabled = 0,
			/// <summary> hab is disabled : it might or might not be deployed/inflated, it can't hold crew and its atmo can't flow</summary>
			disabled = 1,
			/// <summary> hab is being inflated and going to the enabled state, only applies if deployable and inflateRequiresPressure is true</summary>
			inflating = 2,
			/// <summary> hab is being deployed and going to the enabled state, only applies if deployable and inflateRequiresPressure is false</summary>
			deploying = 3,
			/// <summary> hab has been deployed and is waiting for pressure level to be enough for the vessel to be kept pressurized before going to the enabled state</summary>
			waitingForPressure = 4,
			/// <summary> hab is being retracted and going to the disabled state, only applies if deployable</summary>
			retracting = 5,
			/// <summary> hab is waiting for the gravity ring to stop its rotation to be able to go in the retracting state</summary>
			waitingForGravityRing = 6,
			/// <summary> depreciated, kept around for backwark compat</summary>
			pressurizing = 2,
			/// <summary> depreciated, kept around for backwark compat</summary>
			depressurizing = 0
		}

		// volume / surface cache
		public static Dictionary<string, Lib.PartVolumeAndSurfaceInfo> habitatDatabase;
		public const string habitatDataCacheNodeName = "KERBALISM_HABITAT_INFO";
		public static string HabitatDataCachePath => Path.Combine(Lib.KerbalismRootPath, "HabitatData.cache");

		// config
		[KSPField] public double volume = 0.0;                      // habitable volume in m^3, deduced from bounding box if not specified
        [KSPField] public double surface = 0.0;                     // external surface in m^2, deduced from bounding box if not specified
        [KSPField] public string inflate = string.Empty;            // inflate/deploy animation, if any
		[KSPField] public bool animBackwards;                       // invert animation (case state is deployed but it is showing the part retracted)
		[KSPField] public bool inflatableUsingRigidWalls = false;   // can shielding be applied to inflatable structure?
        [KSPField] public bool toggle = true;                       // show the enable/disable toggle
		[KSPField] [Obsolete] public double max_pressure = 1.0;     // depreciated as this was never implemented properly, replaced by the nonPressurizable field
		[KSPField] public bool nonPressurizable = false;            // if true, the part can't be pressurized
		[KSPField] public bool inflateRequiresPressure = true;      // if false, inflating/deploying doesn't require pressurizing and can be done freely
		[KSPField] public bool canRetract = false;                  // if false, the part can't be retracted once inflated/deployed

		// method to use for calculating volume and surface
		[KSPField] public Lib.VolumeAndSurfaceMethod volumeAndSurfaceMethod = Lib.VolumeAndSurfaceMethod.Best;
		[KSPField] public bool substractAttachementNodesSurface = true;

		// persistence
		[KSPField(isPersistant = true)] public State state = State.enabled;
        [KSPField(isPersistant = true)] private double perctDeployed = 0.0;

        // rmb ui status strings
        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Volume", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
        public string Volume;
        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Surface", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
        public string Surface;

        // animations
        Animator deployAnimator;

        private bool hasCLS;                   // Has CLS mod?
        private bool hasGravityRing;
        private GravityRing gravityRing;
		private int crewCapacity;

		private PartResource atmosphereRes;
		private PartResource wasteAtmosphereRes;
		private PartResource shieldingRes;

        private bool configured = false;       // true if configure method has been executed
		private float shieldingCost;

		private bool AtmoFlowState { get => atmosphereRes.flowState; set => atmosphereRes.flowState = value; }
		private bool WasteAtmoFlowState { get => wasteAtmosphereRes.flowState; set => wasteAtmosphereRes.flowState = value; }
		private bool ShieldingFlowState { get => shieldingRes.flowState; set => shieldingRes.flowState = value; }

		private bool IsDeployable => deployAnimator.IsDefined;
		private bool IsFullyDeployed => perctDeployed == 1.0;
		private bool IsFullyRetracted => perctDeployed == 0.0;

		// volume / surface evaluation at prefab compilation
		public override void OnLoad(ConfigNode node)
		{
			// backward compat for the depreciated max_pressure field
			if (max_pressure < Settings.PressureThreshold)
				nonPressurizable = true;

			// volume/surface calcs are quite slow and memory intensive, so we do them only once on the prefab
			// then get the prefab values from OnStart. Moreover, we cache the results in the 
			// Kerbalism\HabitatData.cache file and reuse those cached results on next game launch.
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				// Find deploy/retract animations, either here on in the gravityring module
				// then set the part to the deployed state before doing the volume/surface calcs
				// if part has Gravity Ring, find it.
				gravityRing = part.FindModuleImplementing<GravityRing>();
				hasGravityRing = gravityRing != null;

				// create animators and set the model to the deployed state
				if (hasGravityRing)
				{
					gravityRing.isDeployedByHabitat = true;
					deployAnimator = gravityRing.GetDeployAnimator();
				}
				if (deployAnimator == null || !deployAnimator.IsDefined)
				{
					deployAnimator = new Animator(part, inflate);
					deployAnimator.reversed = animBackwards;
				}

				if (deployAnimator.IsDefined)
					deployAnimator.Still(1.0);

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

            // check if has Connected Living Space mod
            hasCLS = Lib.HasAssembly("ConnectedLivingSpace");

            // if part has Gravity Ring, find it.
            gravityRing = part.FindModuleImplementing<GravityRing>();
			hasGravityRing = gravityRing != null;

			// if gravity ring has a deploy animation, use it
			// otherwise, use the deploy animation defined in the habitat
			if (hasGravityRing)
			{
				gravityRing.isDeployedByHabitat = true;
				deployAnimator = gravityRing.GetDeployAnimator();
			}
			if (deployAnimator == null || !deployAnimator.IsDefined)
			{
				deployAnimator = new Animator(part, inflate);
				deployAnimator.reversed = animBackwards;
			}

			crewCapacity = part.partInfo.partPrefab.CrewCapacity;

			if (volume <= 0.0 || surface <= 0.0)
			{
				Habitat prefab = part.partInfo.partPrefab.FindModuleImplementing<Habitat>();
				if (volume <= 0.0) volume = prefab.volume;
				if (surface <= 0.0) surface = prefab.surface;
			}

			// set RMB UI status strings
			Volume = Lib.HumanReadableVolume(volume);
            Surface = Lib.HumanReadableSurface(surface);

            // hide toggle if specified
            Events["Toggle"].active = toggle;
            //Actions["Action"].active = toggle;

#if DEBUG
			Events["LogVolumeAndSurface"].active = true;
#else
			Events["LogVolumeAndSurface"].active = Settings.VolumeAndSurfaceLogging;
#endif

			// add the cost of shielding to the base part cost
			shieldingCost = (float)surface * PartResourceLibrary.Instance.GetDefinition("Shielding").unitCost;

			// configure on start
			Configure();

			foreach (PartResource res in part.Resources)
			{
				switch (res.resourceName)
				{
					case "Atmosphere":      atmosphereRes = res; break;
					case "WasteAtmosphere": wasteAtmosphereRes = res; break;
					case "Shielding":       shieldingRes = res; break;
				}
			}

			if (IsDeployable)
				deployAnimator.Still(perctDeployed);

			switch (this.state)
            {
                case State.enabled: SetStateEnabled(); break;
                case State.disabled: SetStateDisabled(); break;
                case State.inflating: SetStateInflating(); break;
				case State.deploying: SetStateDeploying(); break;
				case State.waitingForPressure: SetStateWaitingForPressure(); break;
				case State.retracting: SetStateRetracting(); break;
				case State.waitingForGravityRing: SetStateRetracting(); break;
			}
        }

		private void SetGravityRingPressurizedState(bool pressurized)
        {
            if (hasGravityRing)
            {
                gravityRing.isDeployedByHabitat = true;
                gravityRing.deployed = pressurized;
            }
        }

        public void Configure()
        {
            // if never set, this is the case if:
            // - part is added in the editor
            // - module is configured first time either in editor or in flight
            // - module is added to an existing savegame
            if (!part.Resources.Contains("Atmosphere"))
            {
				// add internal atmosphere resources
				double atmoCapacity = volume * 1e3;
				double atmoAmount;
				if (state == State.enabled || (IsDeployable && !inflateRequiresPressure))
					atmoAmount = atmoCapacity;
				else
					atmoAmount = 0.0;

				Lib.AddResource(part, "Atmosphere", atmoAmount, atmoCapacity);
				Lib.AddResource(part, "WasteAtmosphere", 0.0, atmoCapacity);

				// add external surface shielding
				shieldingRes = Lib.AddResource(part, "Shielding", 0.0, surface);

				// inflatable habitats can't be shielded (but still need the capacity) unless they have rigid walls
				shieldingRes.isTweakable = !IsDeployable || inflatableUsingRigidWalls;

				// if shielding feature is disabled, just hide it
				shieldingRes.isVisible = Features.Shielding && shieldingRes.isTweakable;

                configured = true;
            }
        }

        public void Update()
        {
            // The first time an existing save game is loaded with Kerbalism installed,
            // MM will to any existing vessels add Nitrogen with the correct capacities as set in default.cfg but they will have zero amounts,
            // this is not the case for any newly created vessels in the editor.
            if (configured)
            {
                if (state == State.enabled && Features.Pressure)
                    Lib.FillResource(part, "Nitrogen");
                else
                    Lib.EmptyResource(part, "Nitrogen");

                configured = false;
            }

			if (part.IsPAWVisible())
			{
				string status_str = string.Empty;
				switch (state)
				{
					case State.enabled:
						status_str = Local.Generic_ENABLED;
						break;
					case State.disabled:
						status_str = Local.Generic_DISABLED;
						break;
					case State.inflating:
						status_str = $"{Local.Habitat_inflating} ({perctDeployed:p2})";
						break;
					case State.deploying:
						status_str = $"Deploying ({perctDeployed:p2})";
						break;
					case State.waitingForPressure:
						double progress = atmosphereRes.amount / atmosphereRes.maxAmount / Settings.PressureThreshold;
						status_str = $"Pressurizing ({progress:p2})";
						break;
					case State.retracting:
						status_str = $"Retracting ({1.0 - perctDeployed:p2})";
						break;
					case State.waitingForGravityRing:
						status_str = $"Stopping rotation...";
						break;

				}

				Events["Toggle"].guiName = Lib.StatusToggle(Local.StatuToggle_Habitat, status_str);//"Habitat"

			}
        }

        public void FixedUpdate()
        {
			// in the editor, force enabled if the part becomes crewed
			if (Lib.IsEditor() && state != State.enabled && Lib.IsCrewed(part))
				Toggle();

			switch (state)
			{
				case State.inflating:
					if (Lib.IsEditor())
					{
						perctDeployed = deployAnimator.Playing() ? deployAnimator.NormalizedTime : 1.0;
						atmosphereRes.amount = atmosphereRes.maxAmount * perctDeployed;
						if (IsFullyDeployed)
							SetStateEnabled();
					}
					else
					{
						double atmoLevel = atmosphereRes.amount / atmosphereRes.maxAmount;
						perctDeployed = Math.Max(perctDeployed, atmoLevel);
						//double animState = DeployAnimIsBackwards ? 1.0 - perctDeployed : perctDeployed;
						deployAnimator.Still(perctDeployed);
						if (IsFullyDeployed)
							SetStateEnabled();
					}
					break;
				case State.deploying:
					perctDeployed = deployAnimator.Playing() ? deployAnimator.NormalizedTime : 1.0;
					if (IsFullyDeployed)
					{
						double pressureLevel = atmosphereRes.amount / atmosphereRes.maxAmount;
						if (pressureLevel > Settings.PressureThreshold)
							SetStateEnabled();
						else
							SetStateWaitingForPressure();
					}
					break;
				case State.waitingForPressure:
					{
						double pressureLevel = atmosphereRes.amount / atmosphereRes.maxAmount;
						if (pressureLevel > Settings.PressureThreshold)
							SetStateEnabled();
					}
					break;
				case State.retracting:
					perctDeployed = deployAnimator.Playing() ? deployAnimator.NormalizedTime : 0.0;
					if (Lib.IsEditor() && inflateRequiresPressure)
						atmosphereRes.amount = atmosphereRes.maxAmount * perctDeployed;
					if (IsFullyRetracted)
						SetStateDisabled();
					break;
				case State.waitingForGravityRing:
					if (!gravityRing.IsRotating())
						SetStateRetracting();
					break;
			}
		}

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
        public void Toggle()
        {
			switch (state)
			{
				case State.enabled:
					if (IsDeployable && (canRetract || Lib.IsEditor()))
						SetStateRetracting();
					else
						SetStateDisabled();
					break;
				case State.disabled:
					if (IsDeployable && !IsFullyDeployed)
					{
						if (inflateRequiresPressure)
							SetStateInflating();
						else
							SetStateDeploying();
					}
					else
					{
						SetStateEnabled();
					}
					break;
				case State.inflating:
				case State.deploying:
				case State.waitingForPressure:
					if (canRetract || Lib.IsEditor())
						SetStateRetracting();
					else
						SetStateDisabled();
					break;
				case State.retracting:
				case State.waitingForGravityRing:
					if (inflateRequiresPressure)
						SetStateInflating();
					else
						SetStateDeploying();
					break;
			}

			// refresh VAB/SPH ui
			if (Lib.IsEditor())
				GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

		private void SetStateEnabled()
		{
			state = State.enabled;
			part.CrewCapacity = crewCapacity;
			part.crewTransferAvailable = true;
			AtmoFlowState = !nonPressurizable;
			WasteAtmoFlowState = true;
			ShieldingFlowState = true;
			SetCLSPassable(true);
			UpdateIVA(true);

			if (Lib.IsEditor())
				atmosphereRes.amount = atmosphereRes.maxAmount;

			if (hasGravityRing)
				gravityRing.deployed = true;
		}

		private void SetStateDisabled()
		{
			if (Lib.IsCrewed(part))
			{
				Message.Post(Local.Habitat_postmsg.Format(part.partInfo.title));//"Can't disable <b><<1>> habitat</b> while crew is inside"
				SetStateEnabled();
				return;
			}

			state = State.disabled;
			part.CrewCapacity = 0;
			part.crewTransferAvailable = false;
			AtmoFlowState = false;
			WasteAtmoFlowState = false;
			ShieldingFlowState = false;
			SetCLSPassable(false);
			UpdateIVA(false);

			if (hasGravityRing)
				gravityRing.deployed = false;
		}

		private void SetStateInflating()
		{
			state = State.inflating;
			part.CrewCapacity = 0;
			part.crewTransferAvailable = false;
			AtmoFlowState = true;
			WasteAtmoFlowState = false;
			ShieldingFlowState = false;
			SetCLSPassable(false);
			UpdateIVA(false);

			if (Lib.IsEditor())
				deployAnimator.Play(false, false, 5.0, perctDeployed);

			if (hasGravityRing)
				gravityRing.deployed = false;
		}

		private void SetStateDeploying()
		{
			state = State.deploying;
			part.CrewCapacity = 0;
			part.crewTransferAvailable = false;
			AtmoFlowState = false;
			WasteAtmoFlowState = false;
			ShieldingFlowState = false;
			SetCLSPassable(false);
			UpdateIVA(false);
			deployAnimator.Play(false, false, Lib.IsEditor() ? 5.0 : 1.0, perctDeployed);

			if (hasGravityRing)
				gravityRing.deployed = false;
		}

		private void SetStateWaitingForPressure()
		{
			if (Lib.IsEditor())
			{
				SetStateEnabled();
				return;
			}

			state = State.waitingForPressure;
			part.CrewCapacity = 0;
			part.crewTransferAvailable = false;
			AtmoFlowState = !nonPressurizable;
			WasteAtmoFlowState = false;
			ShieldingFlowState = false;
			SetCLSPassable(false);
			UpdateIVA(false);

			if (hasGravityRing)
				gravityRing.deployed = false;
		}

		private void SetStateRetracting()
		{
			if (Lib.IsCrewed(part))
			{
				Message.Post(Local.Habitat_postmsg.Format(part.partInfo.title));//"Can't disable <b><<1>> habitat</b> while crew is inside"
				SetStateEnabled();
				return;
			}

			state = State.retracting;
			part.CrewCapacity = 0;
			part.crewTransferAvailable = false;
			AtmoFlowState = false;
			WasteAtmoFlowState = false;
			ShieldingFlowState = false;
			SetCLSPassable(false);
			UpdateIVA(false);

			if (inflateRequiresPressure)
			{
				atmosphereRes.amount = 0;
				wasteAtmosphereRes.amount = 0;
			}

			if (hasGravityRing)
			{
				gravityRing.deployed = false;
				if (gravityRing.IsRotating())
					state = State.waitingForGravityRing;
				else
					deployAnimator.Play(true, false, Lib.IsEditor() ? 5.0 : 1.0, perctDeployed);
			}
			else
			{
				deployAnimator.Play(true, false, Lib.IsEditor() ? 5.0 : 1.0, perctDeployed);
			}
				
		}

		// action groups
		[KSPAction("#KERBALISM_Habitat_Action")] public void Action(KSPActionParam param) { Toggle(); }

		// part tooltip
		public override string GetInfo()
        {
            return Specs().Info();
        }

        // specifics support
        public Specifics Specs()
        {
            Specifics specs = new Specifics();
            specs.Add(Local.Habitat_info1, Lib.HumanReadableVolume(volume > 0.0 ? volume : Lib.PartBoundsVolume(part)) + (volume > 0.0 ? "" : " (bounds)"));//"Volume"
            specs.Add(Local.Habitat_info2, Lib.HumanReadableSurface(surface > 0.0 ? surface : Lib.PartBoundsSurface(part)) + (surface > 0.0 ? "" : " (bounds)"));//"Surface"
            specs.Add(Local.Habitat_info3, nonPressurizable ? Local.Habitat_no : Local.Habitat_yes);//"Pressurized""no""yes"
			if (IsDeployable) specs.Add(Local.Habitat_info4, Local.Habitat_yes);//"Inflatable""yes"

            return specs;
        }

		public static void EqualizePressure(List<Habitat> habitats, out double nonPressurizableHabsVolume)
		{
			nonPressurizableHabsVolume = 0.0;

			double atmoTotalAmount = 0.0, atmoTotalCapacity = 0.0;
			double wasteTotalAmount = 0.0, wasteTotalCapacity = 0.0;

			for (int i = habitats.Count; i-- > 0;)
			{
				Habitat hab = habitats[i];
				if (hab.state == State.enabled)
				{
					if (hab.nonPressurizable)
					{
						nonPressurizableHabsVolume += hab.atmosphereRes.maxAmount;
					}
					else
					{
						atmoTotalAmount += hab.atmosphereRes.amount;
						atmoTotalCapacity += hab.atmosphereRes.maxAmount;
						wasteTotalAmount += hab.wasteAtmosphereRes.amount;
						wasteTotalCapacity += hab.wasteAtmosphereRes.maxAmount;
					}
				}
			}

			if (atmoTotalAmount == 0.0 && wasteTotalAmount == 0.0)
				return;

			for (int i = habitats.Count; i-- > 0;)
			{
				Habitat hab = habitats[i];
				if (hab.state == State.enabled && !hab.nonPressurizable)
				{
					hab.atmosphereRes.amount = atmoTotalAmount * (hab.atmosphereRes.maxAmount / atmoTotalCapacity);
					hab.wasteAtmosphereRes.amount = wasteTotalAmount * (hab.wasteAtmosphereRes.maxAmount / wasteTotalCapacity);
				}
			}
		}

		// return habitat volume in a vessel in m^3
		public static double Tot_volume(Vessel v)
        {
			double enabledVolume = ResourceCache.GetResource(v, "Atmosphere").Capacity;
			enabledVolume += v.KerbalismData().EnvHabitatInfo.nonPressurizableHabsVolume;
			return enabledVolume / 1e3;
        }

        // return habitat surface in a vessel in m^2
        public static double Tot_surface(Vessel v)
        {
			return ResourceCache.GetResource(v, "Shielding").Capacity;
        }

        // return normalized pressure in a vessel
        public static double Pressure(Vessel v)
        {
			ResourceInfo atmoRes = ResourceCache.GetResource(v, "Atmosphere");
			double enabledVolume = atmoRes.Capacity;
			enabledVolume += v.KerbalismData().EnvHabitatInfo.nonPressurizableHabsVolume;
			return enabledVolume > 0.0 ? atmoRes.Amount / enabledVolume : 0.0;
        }

        // return waste level in a vessel atmosphere
        public static double Poisoning(Vessel v)
        {
			ResourceInfo wasteAtmoRes = ResourceCache.GetResource(v, "WasteAtmosphere");
			double enabledVolume = wasteAtmoRes.Capacity;
			enabledVolume += v.KerbalismData().EnvHabitatInfo.nonPressurizableHabsVolume;
			return enabledVolume > 0.0 ? wasteAtmoRes.Amount / enabledVolume : 0.0;
        }

        /// <summary>
        /// Return vessel shielding factor.
        /// </summary>
        public static double Shielding(Vessel v)
        {
            return Radiation.ShieldingEfficiency(ResourceCache.GetResource(v, "Shielding").Level);
        }

        // return living space factor in a vessel
        public static double Living_space(Vessel v)
        {
            // living space is the volume per-capita normalized against an 'ideal living space' and clamped in an acceptable range
            return Lib.Clamp(Volume_per_crew(v) / PreferencesComfort.Instance.livingSpace, 0.1, 1.0);
        }

        public static double Volume_per_crew(Vessel v)
        {
            // living space is the volume per-capita normalized against an 'ideal living space' and clamped in an acceptable range
            return Tot_volume(v) / Math.Max(1, Lib.CrewCount(v));
        }

        // return a verbose description of shielding capability
        public static string Shielding_to_string(double v)
        {
            return v <= double.Epsilon ? Local.Habitat_none : Lib.BuildString((20.0 * v / PreferencesRadiation.Instance.shieldingEfficiency).ToString("F2"), " mm");//"none"
        }

        // traduce living space value to string
        public static string Living_space_to_string(double v)
        {
            if (v >= 0.99) return Local.Habitat_Summary1;//"ideal"
            else if (v >= 0.75) return Local.Habitat_Summary2;//"good"
            else if (v >= 0.5) return Local.Habitat_Summary3;//"modest"
            else if (v >= 0.25) return Local.Habitat_Summary4;//"poor"
            else return Local.Habitat_Summary5;//"cramped"
        }

        // Support Connected Living Space
        void SetCLSPassable(bool isPassable)
        {
            if (hasCLS)
            {
                foreach (PartModule m in part.Modules)
                {
                    if (m.moduleName == "ModuleConnectedLivingSpace")
                    {
                        Lib.LogDebug("Part '{0}', CLS has been {1}", Lib.LogLevel.Message, part.partInfo.title, isPassable ? "enabled" : "disabled");
                        Lib.ReflectionValue(m, "passable", isPassable);
                    }
                }
            }

            Lib.LogDebug("CrewCapacity: '{0}'", Lib.LogLevel.Message, part.CrewCapacity);
            Lib.LogDebug("CrewTransferAvailable: '{0}'", Lib.LogLevel.Message, isPassable);
        }

        // Enable/Disable IVA
        private void UpdateIVA(bool ative)
        {
            if (Lib.IsFlight())
            {
                if (vessel.isActiveVessel)
                {
                    if (ative)
                    {
                        Lib.LogDebugStack("Part '{0}', Spawning IVA.", Lib.LogLevel.Message, part.partInfo.title);
                        part.SpawnIVA();
                    }
                    else
                    {
                        Lib.LogDebugStack("Part '{0}', Destroying IVA.", Lib.LogLevel.Message, part.partInfo.title);
                        part.DespawnIVA();
                    }
					
				}

				part.CheckTransferDialog();
				GameEvents.onVesselWasModified.Fire(this.vessel);
			}
			else
			{
				GameEvents.onEditorPartEvent.Fire(ConstructionEventType.PartTweaked, part);
			}

			if (part.PartActionWindow != null && part.PartActionWindow.isActiveAndEnabled)
				part.PartActionWindow.displayDirty = true;
		}

        public override string GetModuleDisplayName() { return Local.Habitat; }//"Habitat"

		public string GetModuleTitle() => Local.Habitat;

		public Callback<Rect> GetDrawModulePanelCallback() => null;

		public string GetPrimaryField()
		{
			string s = string.Empty;

			if (nonPressurizable)
				s += Lib.BuildString(Lib.Color("Unpressurized", Lib.Kolor.Orange, true), "\n");

			s += Lib.BuildString(Lib.Bold(Local.Habitat + " " + Local.Habitat_info1), // "Habitat" + "Volume"
				" : ", Lib.HumanReadableVolume(volume));

			return s;
		}

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => shieldingCost;
		public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

		[KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "[Debug] log volume/surface", active = false, groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public void LogVolumeAndSurface()
		{
			Lib.GetPartVolumeAndSurface(part, true);
		}
	}
}
