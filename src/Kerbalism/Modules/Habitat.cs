#pragma warning disable CS0612

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace KERBALISM
{
    public class Habitat : PartModule, ISpecifics, IModuleInfo, IPartCostModifier
	{
		public const string AtmoResName = "Atmosphere";
		public const string WasteAtmoResName = "WasteAtmosphere";
		public const string ShieldingResName = "Shielding";

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
			deploying,
			/// <summary> hab has been deployed and is waiting for pressure level to be enough for the vessel to be kept pressurized before going to the enabled state</summary>
			waitingForPressure,
			/// <summary> hab is being retracted and going to the disabled state, only applies if deployable</summary>
			retracting,
			/// <summary> hab is waiting for the gravity ring to stop its rotation to be able to go in the retracting state</summary>
			waitingForGravityRing,
			/// <summary> inflatable is being pressurized by equalizing its pressure with all enabled habitats</summary>
			inflatingAndEqualizing,
			/// <summary> deployable is being pressurized by equalizing its pressure with all enabled habitats</summary>
			waitingForPressureAndEqualizing,
			/// <summary> special unescapable state for EVA kerbals</summary>
			evaKerbal,
			/// <summary> depreciated, kept around for backward compat</summary>
			pressurizing = 2,
			/// <summary> depreciated, kept around for backward compat</summary>
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
        [KSPField(isPersistant = true)] public double perctDeployed = 0.0;

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
		private KerbalEVA kerbalEVA;

		private PartResource atmosphereRes;
		private PartResource wasteAtmosphereRes;
		private PartResource shieldingRes;

		private BaseEvent toggleEvent;
		private BaseEvent ventEvent;
		private BaseEvent equalizeEvent;

		private float shieldingCost;

		private bool AtmoFlowState { get => atmosphereRes.flowState; set => atmosphereRes.flowState = value; }
		private bool WasteAtmoFlowState { get => wasteAtmosphereRes.flowState; set => wasteAtmosphereRes.flowState = value; }
		private bool ShieldingFlowState { get => shieldingRes.flowState; set => shieldingRes.flowState = value; }

		private bool IsDeployable => deployAnimator.IsDefined;
		private bool IsFullyDeployed => perctDeployed == 1.0;
		private bool IsFullyRetracted => perctDeployed == 0.0;

		private bool IsDeadEVA => state == State.evaKerbal && (part.protoModuleCrew.Count == 0 || DB.Kerbal(part.protoModuleCrew[0].name).eva_dead);

		private bool CanEqualize
		{
			get
			{
				if (!(state == State.inflating || state == State.waitingForPressure))
					return false;
				ResourceInfo vesselAtmoRes = ResourceCache.GetResource(vessel, AtmoResName);
				// don't allow unless decent pressurization capacity is active, or we are in a breathable atmo
				if (vesselAtmoRes.Rate < 1.0 && !vessel.KerbalismData().EnvBreathable)
					return false;
				double atmoAmountNeededToPressurize = Math.Max(0.0, (atmosphereRes.maxAmount * Settings.PressureThreshold) - atmosphereRes.amount);
				// don't allow unless the vessel contains more than twice the atmo needed to pressurize this hab
				if (vesselAtmoRes.Amount < atmoAmountNeededToPressurize * 2.0)
					return false;
				// don't allow unless there is enough nitrogen to pressurize this hab
				if (ResourceCache.GetResource(vessel, "Nitrogen").Amount < atmoAmountNeededToPressurize)
					return false;

				return true;
			}
		}

		private bool CanVentAtmosphere => atmosphereRes.amount > 0 && (state == State.disabled || state == State.enabled);

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

			toggleEvent = Events[nameof(Toggle)];
			ventEvent = Events[nameof(Vent)];
			equalizeEvent = Events[nameof(Equalize)];
			ventEvent.guiName = Local.Habitat_Vent;

			// hide toggle if specified
			toggleEvent.active = toggle;

#if DEBUG
			Events["LogVolumeAndSurface"].active = true;
#else
			Events["LogVolumeAndSurface"].active = Settings.VolumeAndSurfaceLogging;
#endif

			// add the cost of shielding to the base part cost
			shieldingCost = (float)surface * PartResourceLibrary.Instance.GetDefinition(ShieldingResName).unitCost;

			// configure on start
			Configure();

			foreach (PartResource res in part.Resources)
			{
				switch (res.resourceName)
				{
					case AtmoResName: atmosphereRes = res; break;
					case WasteAtmoResName: wasteAtmosphereRes = res; break;
					case ShieldingResName: shieldingRes = res; break;
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
				case State.inflatingAndEqualizing: SetStateEqualizing(); break;
				case State.waitingForPressureAndEqualizing: SetStateEqualizing(); break;
				case State.evaKerbal: SetStateEVAKerbal(); break;
			}

			UpdatePAW();
		}

        public void Configure()
        {
            // if never set, this is the case if:
            // - part is added in the editor
            // - module is configured first time either in editor or in flight
            // - module is added to an existing savegame
            if (!part.Resources.Contains(AtmoResName) || !part.Resources.Contains(WasteAtmoResName) || !part.Resources.Contains(ShieldingResName))
            {
				// add internal atmosphere resources
				double atmoCapacity = volume * 1e3;
				double atmoAmount;
				if (!nonPressurizable && (state == State.enabled || (IsDeployable && !inflateRequiresPressure)))
					atmoAmount = atmoCapacity;
				else
					atmoAmount = 0.0;

				Lib.AddResource(part, AtmoResName, atmoAmount, atmoCapacity);
				Lib.AddResource(part, WasteAtmoResName, 0.0, atmoCapacity);

				// add external surface shielding
				shieldingRes = Lib.AddResource(part, ShieldingResName, 0.0, surface);

				// inflatable habitats can't be shielded (but still need the capacity) unless they have rigid walls
				shieldingRes.isTweakable = !IsDeployable || inflatableUsingRigidWalls;

				// if shielding feature is disabled, just hide it
				shieldingRes.isVisible = Features.Shielding && shieldingRes.isTweakable;

				// add the EVA kerbal supply resources defined in the profile
				if (state == State.evaKerbal)
					Profile.SetupEva(part, false);

				// fill nitrogen on saves or ships that weren't created whith Kerbalism installed
				if (part.Resources.Contains("Nitrogen"))
					Lib.FillResource(part, "Nitrogen");
            }
        }

        public void Update()
        {
			if (part.IsPAWVisible())
				UpdatePAW();
		}

		private void UpdatePAW()
		{
			string status_str = string.Empty;
			string pressure;
			switch (state)
			{
				case State.enabled:
					pressure = Lib.HumanReadableNormalizedPressure(atmosphereRes.amount / atmosphereRes.maxAmount);
					status_str = Lib.BuildString(Local.Generic_ENABLED, " (", pressure, ")");
					break;
				case State.disabled:
					pressure = Lib.HumanReadableNormalizedPressure(atmosphereRes.amount / atmosphereRes.maxAmount);
					status_str = Lib.BuildString(Local.Generic_DISABLED, " (", pressure, ")");
					break;
				case State.inflating:
					status_str = Lib.BuildString(Local.Habitat_inflating, " (", perctDeployed.ToString("p2"), ")");// "inflating"
					break;
				case State.deploying:
					status_str = Lib.BuildString(Local.Habitat_deploying, " (", perctDeployed.ToString("p2"), ")");// "deploying"
					break;
				case State.waitingForPressure:
					double progress = atmosphereRes.amount / atmosphereRes.maxAmount / Settings.PressureThreshold;
					status_str = Lib.BuildString(Local.Habitat_pressurizing, " (", progress.ToString("p2"), ")");// "pressurizing"
					break;
				case State.retracting:
					status_str = Lib.BuildString(Local.Habitat_retracting, " (", (1.0 - perctDeployed).ToString("p2"), ")");// "retracting"
					break;
				case State.waitingForGravityRing:
					status_str = Local.Habitat_stopRotation; // "stopping rotation..."
					break;
				case State.inflatingAndEqualizing:
				case State.waitingForPressureAndEqualizing:
					status_str = Lib.BuildString(Local.Habitat_equalizing, " (", perctDeployed.ToString("p2"), ")");// "equalizing"
					break;
			}

			toggleEvent.guiName = Lib.StatusToggle(Local.StatuToggle_Habitat, status_str);//"Habitat"

			ventEvent.active = CanVentAtmosphere;

			if (state == State.inflatingAndEqualizing || state == State.waitingForPressureAndEqualizing)
			{
				equalizeEvent.active = true;
				equalizeEvent.guiName = Local.Habitat_stopEqualize; // "Stop equalizing pressure"
			}
			else if (CanEqualize)
			{
				equalizeEvent.active = true;
				equalizeEvent.guiName = Local.Habitat_equalize; // "Equalize pressure"
			}
			else
			{
				equalizeEvent.active = false;
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
				case State.inflatingAndEqualizing:
					if (Lib.IsEditor())
					{
						// inflating requires reaching Settings.PressureThreshold
						perctDeployed = deployAnimator.Playing() ? deployAnimator.NormalizedTime : 1.0;
						atmosphereRes.amount = atmosphereRes.maxAmount * perctDeployed * Settings.PressureThreshold;
						if (IsFullyDeployed)
							SetStateEnabled();
					}
					else
					{
						double deployLevel = atmosphereRes.amount / (atmosphereRes.maxAmount * Settings.PressureThreshold);
						perctDeployed = Lib.Clamp(deployLevel, perctDeployed, 1.0);
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
				case State.waitingForPressureAndEqualizing:
					{
						double pressureLevel = atmosphereRes.amount / atmosphereRes.maxAmount;
						if (pressureLevel > Settings.PressureThreshold)
							SetStateEnabled();
					}
					break;
				case State.retracting:
					perctDeployed = deployAnimator.Playing() ? deployAnimator.NormalizedTime : 0.0;
					if (IsFullyRetracted)
						SetStateDisabled();
					break;
				case State.waitingForGravityRing:
					if (!gravityRing.IsRotating())
						SetStateRetracting();
					break;
				case State.evaKerbal:
					EVAUpdate();
					break;
			}
		}

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = false,
			guiName = "_", active = true, guiActiveUncommand = true,
			groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public void Vent()
		{
			if (CanVentAtmosphere)
			{
				atmosphereRes.amount = 0.0;
				wasteAtmosphereRes.amount = 0.0;
			}
		}

		[KSPAction("#KERBALISM_Habitat_Action")]
		public void Action(KSPActionParam param) => Toggle();

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true,
			guiName = "_", active = true, guiActiveUncommand = true,
			groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
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
				case State.inflatingAndEqualizing:
				case State.waitingForPressureAndEqualizing:
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

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = false,
			guiName = "_", active = true, guiActiveUncommand = true,
			groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public void Equalize()
		{
			switch (state)
			{
				case State.inflating:
				case State.waitingForPressure:
					if (CanEqualize)
						SetStateEqualizing();
					break;
				case State.inflatingAndEqualizing:
					SetStateInflating();
					break;
				case State.waitingForPressureAndEqualizing:
					SetStateWaitingForPressure();
					break;
			}
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
			UpdateIVAAndUIAndFireEvents(true);

			if (Lib.IsEditor() && !nonPressurizable)
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
			UpdateIVAAndUIAndFireEvents(false);

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
			UpdateIVAAndUIAndFireEvents(false);

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
			UpdateIVAAndUIAndFireEvents(false);
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
			UpdateIVAAndUIAndFireEvents(false);

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
			UpdateIVAAndUIAndFireEvents(false);

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

		private void SetStateEqualizing()
		{
			if (state == State.inflating)
				state = State.inflatingAndEqualizing;
			else if (state == State.waitingForPressure)
				state = State.waitingForPressureAndEqualizing;
			part.CrewCapacity = 0;
			part.crewTransferAvailable = false;
			AtmoFlowState = true;
			WasteAtmoFlowState = false;
			ShieldingFlowState = false;
			SetCLSPassable(false);
			UpdateIVAAndUIAndFireEvents(false);
			if (hasGravityRing)
				gravityRing.deployed = false;
		}

		private void SetStateEVAKerbal()
		{
			kerbalEVA = part.FindModuleImplementing<KerbalEVA>();
			AtmoFlowState = true;
			WasteAtmoFlowState = true;
			ShieldingFlowState = true;
			toggleEvent.active = false;
			ventEvent.active = false;
			equalizeEvent.active = false;
		}

		/// <summary>
		/// Update hab state on unloaded vessels (replicating FixedUpdate()), called from VesselHabitatInfo.Update() 
		/// </summary>
		internal static void BackgroundUpdate(HabitatWrapper hab)
		{
			switch (hab.State)
			{
				case State.inflating:
				case State.inflatingAndEqualizing:
					{
						double deployLevel = hab.AtmoResource.Amount / (hab.AtmoResource.MaxAmount * Settings.PressureThreshold);
						hab.PerctDeployed = Lib.Clamp(deployLevel, hab.PerctDeployed, 1.0);
						if (hab.PerctDeployed == 1.0)
							BackgroundSetStateEnabled(hab);
					}

					break;
				case State.deploying:
					{
						hab.PerctDeployed = 1.0; // insta-deploy on unloaded vessels
						double pressureLevel = hab.AtmoResource.Amount / hab.AtmoResource.MaxAmount;
						if (pressureLevel > Settings.PressureThreshold)
							BackgroundSetStateEnabled(hab);
						else
							BackgroundSetStateWaitingForPressure(hab);
					}
					break;
				case State.waitingForPressure:
				case State.waitingForPressureAndEqualizing:
					{
						double pressureLevel = hab.AtmoResource.Amount / hab.AtmoResource.MaxAmount;
						if (pressureLevel > Settings.PressureThreshold)
							BackgroundSetStateEnabled(hab);
					}
					break;
				case State.retracting:
					hab.PerctDeployed = 0.0; // insta-retract on unloaded vessels
					BackgroundSetStateDisabled(hab);
					break;
				case State.waitingForGravityRing:
					BackgroundSetStateRetracting(hab);
					break;
			}
		}

		private static void BackgroundSetStateEnabled(HabitatWrapper hab)
		{
			hab.State = State.enabled;
			hab.AtmoResource.FlowState = !hab.NonPressurizable;
			hab.WasteAtmoResource.FlowState = true;
			hab.ShieldingResource.FlowState = true;
		}

		private static void BackgroundSetStateWaitingForPressure(HabitatWrapper hab)
		{
			hab.State = State.waitingForPressure;
			hab.AtmoResource.FlowState = !hab.NonPressurizable;
			hab.WasteAtmoResource.FlowState = false;
			hab.ShieldingResource.FlowState = false;
		}

		private static void BackgroundSetStateDisabled(HabitatWrapper hab)
		{
			hab.State = State.disabled;
			hab.AtmoResource.FlowState = false;
			hab.WasteAtmoResource.FlowState = false;
			hab.ShieldingResource.FlowState = false;
		}

		private static void BackgroundSetStateRetracting(HabitatWrapper hab)
		{
			if (hab.InflateRequiresPressure)
			{
				hab.AtmoResource.Amount = 0;
				hab.WasteAtmoResource.Amount = 0;
			}
			// go directly to the disabled state as there is no animation to handle
			BackgroundSetStateDisabled(hab);
		}

		private void EVAUpdate()
		{
			ResourceInfo ec = ResourceCache.GetResource(vessel, "ElectricCharge");

			// determine if headlamps need ec
			// - not required if there is no EC capacity in eva kerbal (no ec supply in profile)
			// - not required if no EC cost for headlamps is specified (set by the user)
			if (ec.Capacity > 0.0 && Settings.HeadLampsCost > 0.0)
			{
				// consume EC for the headlamps
				if (kerbalEVA.lampOn)
					ec.Consume(Settings.HeadLampsCost * TimeWarp.fixedDeltaTime, ResourceBroker.Light);

				if (ec.Amount > 0.0)
				{
					if (kerbalEVA.lampOn && !kerbalEVA.headLamp.activeSelf)
						kerbalEVA.headLamp.SetActive(true);
				}
				else
				{
					if (kerbalEVA.headLamp.activeSelf)
						kerbalEVA.headLamp.SetActive(false);
				}
			}

			if (IsDeadEVA)
				EVADeadUpdate();
		}

		private void EVADeadUpdate()
		{
			// set kerbal to the 'freezed' unescapable state, if it isn't already in it
			// how it works:
			// - kerbal animations and ragdoll state are driven by a finite-state-machine (FSM)
			// - this function is called every frame for all active eva kerbals flagged as dead
			// - if the FSM current state is already 'freezed', we do nothing and this function is a no-op
			// - we create an 'inescapable' state called 'freezed'
			// - we switch the FSM to that state using an ad-hoc event from current state
			// - once the 'freezed' state is set, the FSM cannot switch to any other states
			// - the animator of the object is stopped to stop any left-over animations from previous state
			if (!string.IsNullOrEmpty(kerbalEVA.fsm.currentStateName) && kerbalEVA.fsm.currentStateName != "freezed")
			{
				// create freezed state
				KFSMState freezed = new KFSMState("freezed");

				// create freeze event
				KFSMEvent eva_freeze = new KFSMEvent("EVAfreeze")
				{
					GoToStateOnEvent = freezed,
					updateMode = KFSMUpdateMode.MANUAL_TRIGGER
				};
				kerbalEVA.fsm.AddEvent(eva_freeze, kerbalEVA.fsm.CurrentState);

				// trigger freeze event
				kerbalEVA.fsm.RunEvent(eva_freeze);

				// stop animations
				kerbalEVA.GetComponent<Animation>().Stop();
				kerbalExpressionSystem expressionSystem = kerbalEVA.GetComponent<kerbalExpressionSystem>();
				if (expressionSystem != null)
				{
					expressionSystem.enabled = false;
					expressionSystem.animator.speed = 0f;
				}
			}

			// disable all modules beside the Habitat, KerbalEVA and FlagDecal
			for (int i = part.Modules.Count; i-- > 0;)
			{
				PartModule m = part.Modules[i];
				// 
				if (m == this || m is KerbalEVA || m is FlagDecal)
					continue;

				m.isEnabled = false;
				m.enabled = false;
			}

			// remove plant flag action
			kerbalEVA.flagItems = 0;
		}

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

        private void UpdateIVAAndUIAndFireEvents(bool ivaActive)
        {
            if (Lib.IsFlight())
            {
                if (vessel.isActiveVessel)
                {
                    if (ivaActive)
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
				s += Lib.BuildString(Lib.Color(Local.Habitat_Unpressurized, Lib.Kolor.Orange, true), "\n"); // "Unpressurized"

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

		private static string[] EVAKerbalPartNames => new string[]
		{
			"kerbalEVA",
			"kerbalEVASlimSuitFemale",
			"kerbalEVAfemaleFuture",
			"kerbalEVAFuture",
			"kerbalEVAfemale",
			"kerbalEVASlimSuit",
			"kerbalEVAfemaleVintage",
			"kerbalEVAVintage"
		};

		internal static void AddHabitatToEVAKerbalPrefabs()
		{
			foreach (string evaKerbalPartName in EVAKerbalPartNames)
			{
				AvailablePart ap = PartLoader.getPartInfoByName(evaKerbalPartName);
				if (ap == null)
					continue;

				Part prefab = ap.partPrefab;
				Habitat habitat = prefab.FindModuleImplementing<Habitat>();

				// backward compatibility (and failsafe) : EVA Kerbals didn't have
				// the module on previous versions, ensure it is added
				if (habitat == null)
				{
					habitat = (Habitat)prefab.AddModule(nameof(Habitat), forceAwake: true);
					// deduce the hab volume from the atmo resource, if present
					// previous versions used to have a MM patch directly adding
					// atmo / wasteAtmo to the EVAKerbal parts
					PartResource atmoRes = prefab.Resources[AtmoResName];
					if (atmoRes != null && atmoRes.maxAmount > 0.0)
						habitat.volume = atmoRes.maxAmount / 1e3;
					else
						habitat.volume = 0.33;

					prefab.Resources.dict.Remove(Lib.GetDefinition(AtmoResName).id);
					prefab.Resources.dict.Remove(Lib.GetDefinition(WasteAtmoResName).id);

					habitat.surface = 1.5; // human spacesuit surface ~ 4 m²
				}

				habitat.state = State.evaKerbal;
			}
		}
	}
}
