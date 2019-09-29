using System;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
	public class Habitat: PartModule, ISpecifics
	{
		// config
		[KSPField] public double volume = 0.0;                      // habitable volume in m^3, deduced from bounding box if not specified
		[KSPField] public double surface = 0.0;                     // external surface in m^2, deduced from bounding box if not specified
		[KSPField] public string inflate = string.Empty;            // inflate animation, if any
		[KSPField] public bool inflatableUsingRigidWalls = false;   // can shielding be applied to inflatable structure?
		[KSPField] public bool toggle = true;                       // show the enable/disable toggle

		[KSPField] public double max_pressure = 1.0;                // max. sustainable pressure, in percent of sea level
		// for now this won't do anything

		// persistence
		[KSPField(isPersistant = true)] public State state = State.enabled;
		[KSPField(isPersistant = true)] private double perctDeployed = 0;

		// rmb ui status strings
#if KSP15_16
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Volume")]
		public string Volume;
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Surface")]
		public string Surface;
#else
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Volume", groupName = "Habitat", groupDisplayName = "Habitat")]
		public string Volume;
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Surface", groupName = "Habitat", groupDisplayName = "Habitat")]
		public string Surface;
#endif
		// animations
		Animator inflate_anim;

		[KSPField] public bool animBackwards;  // invert animation (case state is deployed but it is showing the part retracted)
		public bool needEqualize = false;      // Used to trigger the ResourceBalance

		private bool hasCLS;                   // Has CLS mod?
		private bool FixIVA = false;           // Used only CrewTransferred event, CrewTrans occur after FixedUpdate, then FixedUpdate needs to know to fix it
		private bool hasGravityRing;
		private GravityRing gravityRing;

		State prev_state;                      // State during previous GPU frame update
		private bool configured = false;       // true if configure method has been executed

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

			// calculate habitat internal volume
			if (volume <= double.Epsilon) volume = GetVolume();

			// calculate habitat external surface
			if (surface <= double.Epsilon) surface = GetSurface();

			// set RMB UI status strings
			Volume = Lib.HumanReadableVolume(volume);
			Surface = Lib.HumanReadableSurface(surface);

			// hide toggle if specified
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// create animators
			if (!hasGravityRing)
			{
				inflate_anim = new Animator(part, inflate);
			}

			// configure on start
			Configure();

			perctDeployed = Lib.Level(part, "Atmosphere", true);

			switch (this.state)
			{
				case State.enabled: Set_flow(true); break;
				case State.disabled: Set_flow(false); break;
				case State.pressurizing: Set_flow(true); break;
				case State.depressurizing: Set_flow(false); break;
			}

			if (Get_inflate_string().Length == 0) // not inflatable
			{
				SetPassable(true);
				UpdateIVA(true);
			}
			else
			{
				SetPassable(Math.Truncate(Math.Abs((perctDeployed + ResourceBalance.precision) - 1.0) * 100000) / 100000 <= ResourceBalance.precision);
				UpdateIVA(Math.Truncate(Math.Abs((perctDeployed + ResourceBalance.precision) - 1.0) * 100000) / 100000 <= ResourceBalance.precision);
			}

			if (Lib.IsFlight())
			{
				// For fix IVA when crewTransfered occur, add event to define flag for FixedUpdate
				GameEvents.onCrewTransferred.Add(UpdateCrew);
			}
		}

		public void OnDestroy()
		{
			GameEvents.onCrewTransferred.Remove(UpdateCrew);
		}

		string Get_inflate_string()
		{
			if (hasGravityRing)
			{
				return gravityRing.deploy;
			}
			return inflate;
		}

		public double GetVolume()
		{
			foreach(PartModule pm in part.Modules)
			{
				if (pm.moduleName == "SSTUModularPart")
				{
					Bounds bb = Lib.ReflectionCall<Bounds>(pm, "getModuleBounds", new Type[] { typeof(string) }, new string[] { "CORE" });
					return Lib.PartVolume(bb);
				}
			}
			return Lib.PartVolume(part);
		}

		public double GetSurface()
		{
			foreach (PartModule pm in part.Modules)
			{
				if (pm.moduleName == "SSTUModularPart")
				{
					Bounds bb = Lib.ReflectionCall<Bounds>(pm, "getModuleBounds", new Type[] { typeof(string) }, new string[] { "CORE" });
					return Lib.PartSurface(bb);
				}
			}
			return Lib.PartSurface(part);
		}

		bool Get_inflate_anim_backwards()
		{
			if (hasGravityRing)
			{
				return gravityRing.animBackwards;
			}
			return animBackwards;
		}

		Animator Get_inflate_anim()
		{
			if (hasGravityRing)
			{
				return gravityRing.deploy_anim;
			}
			return inflate_anim;
		}

		void Set_pressurized(bool pressurized)
		{
			if (hasGravityRing)
			{
				gravityRing.isHabitat = true;
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
				// - disabled habitats start with zero atmosphere
				Lib.AddResource(part, "Atmosphere", (state == State.enabled && Features.Pressure) ? volume * 1e3 : 0.0, volume * 1e3);
				Lib.AddResource(part, "WasteAtmosphere", 0.0, volume * 1e3);

				// add external surface shielding
				Lib.AddResource(part, "Shielding", 0.0, surface);

				// inflatable habitats can't be shielded (but still need the capacity) unless they have rigid walls
				part.Resources["Shielding"].isTweakable = (Get_inflate_string().Length == 0) || inflatableUsingRigidWalls;

				// if shielding feature is disabled, just hide it
				part.Resources["Shielding"].isVisible = Features.Shielding && part.Resources["Shielding"].isTweakable;

				configured = true;
			}
		}

		void Set_flow(bool b)
		{
			Lib.SetResourceFlow(part, "Atmosphere", b);
			Lib.SetResourceFlow(part, "WasteAtmosphere", b);
			Lib.SetResourceFlow(part, "Shielding", b);
		}

		State Depressurizing()
		{
			// in flight
			if (Lib.IsFlight())
			{
				// All module are empty
				bool cond1 = true;

				// check amounts
				foreach (string resource in ResourceBalance.resourceName)
				{
					if (part.Resources.Contains(resource))
						cond1 &= part.Resources[resource].amount <= double.Epsilon;
				}

				// are all modules empty?
				if (cond1) return State.disabled;

				// Depressurize still in progress
				return State.depressurizing;
			}
			// in the editors
			else
			{
				// set amount to zero
				foreach (string resource in ResourceBalance.resourceName)
				{
					if (part.Resources.Contains(resource))
						part.Resources[resource].amount = 0.0;
				}

				// return new state
				return State.disabled;
			}
		}

		State Pressurizing()
		{
			// in flight
			if (Lib.IsFlight())
			{
				// full pressure the level is 99.9999% deployed or more
				if (Math.Truncate(Math.Abs((perctDeployed + ResourceBalance.precision) - 1.0) * 100000) / 100000 <= ResourceBalance.precision)
				{
					SetPassable(true);
					UpdateIVA(true);
					return State.enabled;
				}
				return State.pressurizing;
			}
			// in the editors
			else
			{
				// The other resources in ResourceBalance are waste resources
				if (part.Resources.Contains("Atmosphere"))
					part.Resources["Atmosphere"].amount = part.Resources["Atmosphere"].maxAmount;

				// return new state
				return State.enabled;
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
				{
					Lib.EmptyResource(part, "Nitrogen");
				}
				configured = false;
			}

			// update ui
			string status_str = string.Empty;
			switch (state)
			{
				case State.enabled:
					if (Math.Truncate(Math.Abs((perctDeployed + ResourceBalance.precision) - 1.0) * 100000) / 100000 > ResourceBalance.precision)
					{
						// No inflatable can be enabled been pressurizing
						status_str = Localizer.Format("#KERBALISM_Habitat_pressurizing");
					}
					else
					{
						status_str = Localizer.Format("#KERBALISM_Generic_ENABLED");
					}
					Set_pressurized(true);
					break;
				case State.disabled:
					status_str = Localizer.Format("#KERBALISM_Generic_DISABLED");
					Set_pressurized(false);
					break;
				case State.pressurizing:
					status_str = Get_inflate_string().Length == 0 ? Localizer.Format("#KERBALISM_Habitat_pressurizing") : Localizer.Format("#KERBALISM_Habitat_inflating");
					status_str += string.Format("{0:p2}", perctDeployed);
					Set_pressurized(false);
					break;
				case State.depressurizing:
					status_str = Get_inflate_string().Length == 0 ? Localizer.Format("#KERBALISM_Habitat_depressurizing") : Localizer.Format("#KERBALISM_Habitat_deflating");
					status_str += string.Format("{0:p2}", perctDeployed);
					Set_pressurized(false);
					break;
			}

			Events["Toggle"].guiName = Lib.StatusToggle("Habitat", status_str);

			// Changing this animation when we expect rotation will not work because
			// Unity disables other animations when playing the inflation animation.
			if (prev_state != State.enabled)
			{
				Set_inflation();
			}
			prev_state = state;
		}

		public void FixedUpdate()
		{
			// if part is manned (even in the editor), force enabled
			if (Lib.IsCrewed(part) && state != State.enabled)
			{
				Set_flow(true);
				state = State.pressurizing;

				// Equalize run only in Flight mode
				needEqualize = Lib.IsFlight();
			}

			perctDeployed = Lib.Level(part, "Atmosphere", true);

			// Only handle crewTransferred & Toggle, this way has less calls in FixedUpdate
			// CrewTransferred Event occur after FixedUpdate, this must be check in crewtransferred
			if (FixIVA)
			{
				if (Get_inflate_string().Length == 0) // it is not inflatable (We always going to show and cross those habitats)
				{
					SetPassable(true);
					UpdateIVA(true);
				}
				else
				{
					// Inflatable modules shows IVA and are passable only in 99.9999% deployed
					SetPassable(Lib.IsCrewed(part) || Math.Truncate(Math.Abs((perctDeployed + ResourceBalance.precision) - 1.0) * 100000) / 100000 <= ResourceBalance.precision);
					UpdateIVA(Math.Truncate(Math.Abs((perctDeployed + ResourceBalance.precision) - 1.0) * 100000) / 100000 <= ResourceBalance.precision);
				}
				FixIVA = false;
			}

			// state machine
			switch (state)
			{
				case State.enabled:
					// In case it is losting pressure
					if (perctDeployed < Settings.PressureThreshold)
					{
						if (Get_inflate_string().Length != 0)         // it is inflatable
						{
							SetPassable(false || Lib.IsCrewed(part)); // Prevent to not lock a Kerbal into a the part
							UpdateIVA(false);
						}
						needEqualize = true;
						state = State.pressurizing;
					}
					break;

				case State.disabled:
					break;

				case State.pressurizing:
					state = Pressurizing();
					break;

				case State.depressurizing:
					// Just do Venting when has no gravityRing or when the gravity ring is not spinning.
					if (hasGravityRing && !gravityRing.Is_rotating()) state = Depressurizing();
					else if (!hasGravityRing) state = Depressurizing();
					break;
			}
		}

		private void Set_inflation()
		{
			// if there is an inflate animation, set still animation from pressure
			if (Get_inflate_anim_backwards()) Get_inflate_anim().Still(Math.Abs(Lib.Level(part, "Atmosphere", true) - 1));
			else Get_inflate_anim().Still(Lib.Level(part, "Atmosphere", true));
		}

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Habitat", groupDisplayName = "Habitat")]
#endif
		public void Toggle()
		{
			// if manned, we can't depressurize
			if (Lib.IsCrewed(part) && (state == State.enabled || state == State.pressurizing))
			{
				Message.Post(Lib.BuildString("Can't disable <b>", Lib.PartName(part), " habitat</b> while crew is inside"));
				return;
			}

			// Need be equalized
			needEqualize = true;
			FixIVA = true;

			// Every time that toggle bot be clicked, it will change the flow, better then call it every frame
			// state switching
			switch (state)
			{
				// Make Set_flow be called only once throgh the Toggle
				case State.enabled:        Set_flow(false); state = State.depressurizing; break;
				case State.disabled:       Set_flow(true);  state = State.pressurizing;   break;
				case State.pressurizing:   Set_flow(false); state = State.depressurizing; break;
				case State.depressurizing: Set_flow(true);  state = State.pressurizing;   break;
			}

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
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
			specs.Add("Volume", Lib.HumanReadableVolume(volume > double.Epsilon ? volume : Lib.PartVolume(part)));
			specs.Add("Surface", Lib.HumanReadableSurface(surface > double.Epsilon ? surface : Lib.PartSurface(part)));
			specs.Add("Pressurized", max_pressure >= Settings.PressureThreshold ? "yes" : "no");
			if (inflate.Length > 0) specs.Add("Inflatable", "yes");
			if(PhysicsGlobals.KerbalCrewMass > 0)
				specs.Add("Added mass per crew", Lib.HumanReadableMass(PhysicsGlobals.KerbalCrewMass));

			return specs;
		}

		// return habitat volume in a vessel in m^3
		public static double Tot_volume(Vessel v)
		{
			// we use capacity: this mean that partially pressurized parts will still count,
			return ResourceCache.GetResource(v, "Atmosphere").Capacity / 1e3;
		}

		// return habitat surface in a vessel in m^2
		public static double Tot_surface(Vessel v)
		{
			// we use capacity: this mean that partially pressurized parts will still count,
			return ResourceCache.GetResource(v, "Shielding").Capacity;
		}

		// return normalized pressure in a vessel
		public static double Pressure(Vessel v)
		{
			// the pressure is simply the atmosphere level
			return ResourceCache.GetResource(v, "Atmosphere").Level;
		}

		// return waste level in a vessel atmosphere
		public static double Poisoning(Vessel v)
		{
			// the proportion of co2 in the atmosphere is simply the level of WasteAtmo
			return ResourceCache.GetResource(v, "WasteAtmosphere").Level;
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
			return v <= double.Epsilon ? "none" : Lib.BuildString((20.0 * v / PreferencesStorm.Instance.shieldingEfficiency).ToString("F2"), " mm");
		}

		// traduce living space value to string
		public static string Living_space_to_string(double v)
		{
			if (v >= 0.99) return "ideal";
			else if (v >= 0.75) return "good";
			else if (v >= 0.5) return "modest";
			else if (v >= 0.25) return "poor";
			else return "cramped";
		}

		// enable/disable dialog "Transfer crew" on UI
		public void RefreshDialog()
		{
			if (HighLogic.LoadedSceneIsEditor)
			{
				GameEvents.onEditorPartEvent.Fire(ConstructionEventType.PartTweaked, part);
				if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
			}
			else if (HighLogic.LoadedSceneIsFlight)
			{
				GameEvents.onVesselWasModified.Fire(this.vessel);
			}

			part.CheckTransferDialog();
			MonoUtilities.RefreshContextWindows(part);
		}

		// Support Connected Living Space
		void SetPassable(bool isPassable)
		{
			if (hasCLS)
			{
				// for each module
				foreach (PartModule m in part.Modules)
				{
					if (m.moduleName == "ModuleConnectedLivingSpace")
					{
						Lib.DebugLog("Part '{0}', CLS has been {1}", part.partInfo.title, isPassable ? "enabled" : "disabled");
						Lib.ReflectionValue(m, "passable", isPassable);
					}
				}
			}

			Lib.DebugLog("CrewCapacity: '{0}'", part.CrewCapacity);
			Lib.DebugLog("CrewTransferAvailable: '{0}'", isPassable);
			part.crewTransferAvailable = isPassable;
		}

		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		// Enable/Disable IVA
		void UpdateIVA(bool ative)
		{
			if (Lib.IsFlight())
			{
				if (vessel.isActiveVessel)
				{
					if (ative)
					{
						Lib.DebugLog("Part '{0}', Spawning IVA.", part.partInfo.title);
						part.SpawnIVA();
					}
					else
					{
						Lib.DebugLog("Part '{0}', Destroying IVA.", part.partInfo.title);
						part.DespawnIVA();
					}
					RefreshDialog();
				}
			}
		}

		// Fix IVA when transfer crew
		void UpdateCrew(GameEvents.HostedFromToAction<ProtoCrewMember, Part> dat)
		{
			if (dat.to == part)
			{
				// Need be equalized
				// Enable flow for be pressurized
				Set_flow(true);
				needEqualize = true;
			}

			// Every time that crew be transfered, need update all IVAs for active Vessel
			FixIVA = vessel.isActiveVessel;
		}

		// habitat state
		public enum State
		{
			disabled,        // hab is disabled
			enabled,         // hab is enabled
			pressurizing,    // hab is pressurizing (between uninhabited and habitats)
			depressurizing,  // hab is depressurizing (between enabled and disabled)
		}

		public override string GetModuleDisplayName() { return "Habitat"; }
	}
}
