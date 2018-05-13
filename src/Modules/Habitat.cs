using System;
using KSP.Localization;


namespace KERBALISM
{

	public sealed class Habitat : PartModule, ISpecifics, IConfigurable
	{
		// config
		[KSPField] public double volume = 0.0;            // habitable volume in m^3, deduced from bounding box if not specified
		[KSPField] public double surface = 0.0;           // external surface in m^2, deduced from bounding box if not specified
		[KSPField] public string inflate = string.Empty;  // inflate animation, if any
		[KSPField] public bool   toggle = true;           // show the enable/disable toggle

		// persistence
		[KSPField(isPersistant = true)] public State state = State.enabled;
		[KSPField(isPersistant = true)] private double perctDeployed = 0;

		// rmb ui status strings
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Volume")] public string Volume;
		[KSPField(guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Habitat_Surface")] public string Surface;

		// animations
		Animator inflate_anim;

		[KSPField] public bool animBackwards;            // invert animation (case state is deployed but it is showing the part retracted)

		private bool hasCLS;
		private GravityRing gravityRing;
		private bool hasGravityRing;                     // Alpha test to create a habitat with GravityRing

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
			if (volume <= double.Epsilon) volume = Lib.PartVolume(part);

			// calculate habitat external surface
			if (surface <= double.Epsilon) surface = Lib.PartSurface(part);

			// set RMB UI status strings
			Volume = Lib.HumanReadableVolume(volume);
			Surface = Lib.HumanReadableSurface(surface);

			// hide toggle if specified
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// create animators
			inflate_anim = new Animator(part, inflate);

			perctDeployed = Lib.Level(part, "Atmosphere", true);

			if (perctDeployed == 1)
			{
				RefreshDialog();
				SetPassable(true);
			}

			// configure on start
			Configure(true);
		}

		public void Configure(bool enable)
		{
			if (enable)
			{
				// if never set, this is the case if:
				// - part is added in the editor
				// - module is configured first time either in editor or in flight
				// - module is added to an existing savegame
				if (!part.Resources.Contains("Atmosphere"))
				{
					// add internal atmosphere resources
					// - disabled habitats start with zero atmosphere
					Lib.AddResource(part, "Atmosphere", (state == State.enabled && Features.Pressure) ? volume : 0.0, volume);
					Lib.AddResource(part, "WasteAtmosphere", 0.0, volume);

					// add external surface shielding
					Lib.AddResource(part, "Shielding", 0.0, surface);

					// inflatable habitats can't be shielded (but still need the capacity)
					part.Resources["Shielding"].isTweakable = inflate.Length == 0;

					// if shielding feature is disabled, just hide it
					part.Resources["Shielding"].isVisible = Features.Shielding;
				}
			}
			else
			{
				Lib.RemoveResource(part, "Atmosphere", 0.0, volume);
				Lib.RemoveResource(part, "WasteAtmosphere", 0.0, volume);
				Lib.RemoveResource(part, "Shielding", 0.0, surface);
			}
		}

		void set_flow(bool b)
		{
			Lib.SetResourceFlow(part, "Atmosphere", b);
			Lib.SetResourceFlow(part, "WasteAtmosphere", b);
			Lib.SetResourceFlow(part, "Shielding", b);
		}

		State equalize()
		{
			// in flight
			if (Lib.IsFlight())
			{
				double atmosphereAmount = 0;
				double atmosphereMaxAmount = 0;
				double partsHabVolume = 0;

				// Get all habs non-inflatable in the vessel
				foreach (Habitat partHabitat in vessel.FindPartModulesImplementing<Habitat>())
				{
					if (partHabitat.inflate.Length == 0)
					{
						PartResource t = partHabitat.part.Resources["Atmosphere"];
						// If has the atmosphere resource
						if (t != null)
						{
							atmosphereAmount += t.amount;
							atmosphereMaxAmount += t.maxAmount;
							partsHabVolume += partHabitat.volume;
						}
					}
				}

				// case if it has only one hab part and it is inflate
				// sample: proto + inflate habitat
				if (atmosphereMaxAmount == 0) return State.equalizing;

				PartResource hab_atmo = part.Resources["Atmosphere"];

				// equalization succeeded if the level is 100%
				if (perctDeployed == 1)
				{
					SetPassable(true);
					RefreshDialog();
					if (hasGravityRing)
					{
						if (gravityRing.animBackwards) gravityRing.deployed = false;
						else gravityRing.deployed = true;
					}
					return State.enabled;
				}

				// determine equalization speed
				// we deal with the case where a big hab is sucking all atmosphere from the rest of the vessel
				double amount = Math.Min(partsHabVolume, volume) * equalize_speed * Kerbalism.elapsed_s;

				// the others habs pressure are higher or can consume until 50% of the no inflate module
				// 50% is temporary solution for do inflate faster
				if ((atmosphereAmount / atmosphereMaxAmount) > perctDeployed)
				{
					// clamp amount to what's available in the hab and what can fit in the part
					amount = Math.Min(amount, atmosphereAmount);
					amount = Math.Min(amount, hab_atmo.maxAmount - hab_atmo.amount);

					// consume from all enabled habs in the vessel that are not Inflate
					foreach (Habitat partHabitat in vessel.FindPartModulesImplementing<Habitat>())
					{
						if (partHabitat.inflate.Length == 0)
						{
							PartResource t = partHabitat.part.Resources["Atmosphere"];
							t.amount -= (amount * (t.amount / atmosphereAmount));
						}
					}

					// produce in the part
					hab_atmo.amount += amount;
				}

				// equalization still in progress
				return State.equalizing;
			}
			// in the editors
			else
			{
				// set amount to max capacity
				PartResource hab_atmo = part.Resources["Atmosphere"];
				hab_atmo.amount = hab_atmo.maxAmount;

				// return new state
				SetPassable(true);
				RefreshDialog();
				return State.enabled;
			}
		}

		State venting()
		{
			// in flight
			if (Lib.IsFlight())
			{
				double atmosphereAmount = 0;
				double atmosphereMaxAmount = 0;
				// Get all habs no inflate in the vessel
				foreach (Habitat partHabitat in vessel.FindPartModulesImplementing<Habitat>())
				{
					if (partHabitat.state == State.enabled || partHabitat.state == State.equalizing)
					{
						PartResource t = partHabitat.part.Resources["Atmosphere"];
						// If has the atmosphere resource
						if (t != null)
						{
							atmosphereAmount += t.amount;
							atmosphereMaxAmount += t.maxAmount;
						}
					}
				}

				// shortcuts
				PartResource atmo = part.Resources["Atmosphere"];
				PartResource waste = part.Resources["WasteAtmosphere"];

				// get level of atmosphere in part
				double hab_level = Lib.Level(part, "Atmosphere", true);

				// venting succeeded if the amount reached zero
				if (atmo.amount <= double.Epsilon && waste.amount <= double.Epsilon)
				{
					return State.disabled;
				}

				// how much to vent
				double rate = volume * equalize_speed * Kerbalism.elapsed_s;
				double atmo_k = atmo.amount / (atmo.amount + waste.amount);
				double waste_k = waste.amount / (atmo.amount + waste.amount);

				// produce from all enabled habs in the vessel
				foreach (Habitat partHabitat in vessel.FindPartModulesImplementing<Habitat>())
				{
					if (partHabitat.state == State.enabled || partHabitat.state == State.equalizing)
					{
						PartResource t = partHabitat.part.Resources["Atmosphere"];
						t.amount += (Math.Max(atmo.amount - rate * atmo_k, 0.0) * (t.amount / atmosphereAmount));
					}
				}

				// consume from the part, clamp amount to what's available
				atmo.amount = Math.Max(atmo.amount - rate * atmo_k, 0.0);
				waste.amount = Math.Max(waste.amount - rate * waste_k, 0.0);

				// venting still in progress
				return State.venting;
			}
			// in the editors
			else
			{
				// set amount to zero
				part.Resources["Atmosphere"].amount = 0.0;
				part.Resources["WasteAtmosphere"].amount = 0.0;

				// return new state
				return State.disabled;
			}
		}

		public void Update()
		{
			// update ui
			string status_str = string.Empty;
			switch (state)
			{
				case State.enabled:    status_str = Localizer.Format("#KERBALISM_Generic_ENABLED"); break;
				case State.disabled:   status_str = Localizer.Format("#KERBALISM_Generic_DISABLED"); break;
				case State.equalizing: status_str = inflate.Length == 0 ? Localizer.Format("#KERBALISM_Habitat_equalizing") : Localizer.Format("#KERBALISM_Habitat_inflating"); break;
				case State.venting:    status_str = inflate.Length == 0 ? Localizer.Format("#KERBALISM_Habitat_venting") : Localizer.Format("#KERBALISM_Habitat_deflating"); break;
			}
			Events["Toggle"].guiName = Lib.StatusToggle("Habitat", status_str);
		}

		public void FixedUpdate()
		{
			// if part is manned (even in the editor), force enabled
			if (Lib.IsManned(part) && state != State.enabled) state = State.equalizing;

			perctDeployed = Lib.Level(part, "Atmosphere", true);

			// state machine
			switch (state)
			{
				case State.enabled:
					set_flow(true);
					break;

				case State.disabled:
					set_flow(false);
					break;

				case State.equalizing:
					set_flow(true);
					state = equalize();
					break;

				case State.venting:
					set_flow(false);
					// Just do Venting when has no gravityRing or when the gravity ring is not spinning.
					if(hasGravityRing)
					{
						if(gravityRing.rotateIsTransform)
						{
							if (!gravityRing.rotate_transf.IsRotating()) state = venting();
						}
						else
						{
							if (!gravityRing.rotate_anim.playing()) state = venting();
						}
					}
					else state = venting();
					break;
			}

			// if there is an inflate animation, set still animation from pressure
			if (animBackwards) inflate_anim.still(Math.Abs(Lib.Level(part, "Atmosphere", true)-1));
			else inflate_anim.still(Lib.Level(part, "Atmosphere", true));

			// instant pressurization and scrubbing inside breathable atmosphere
			if (!Lib.IsEditor() && Cache.VesselInfo(vessel).breathable && inflate.Length == 0)
			{
				var atmo = part.Resources["Atmosphere"];
				var waste = part.Resources["WasteAtmosphere"];
				if (Features.Pressure) atmo.amount = atmo.maxAmount;
				if (Features.Poisoning) waste.amount = 0.0;
			}
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			// if manned, we can't depressurize
			if (Lib.IsManned(part) && (state == State.enabled || state == State.equalizing))
			{
				Message.Post(Lib.BuildString("Can't disable <b>", Lib.PartName(part), " habitat</b> while crew is inside"));
				return;
			}

			// state switching
			switch (state)
			{
				case State.enabled:    state = State.venting;    break;
				case State.disabled:   state = State.equalizing; break;
				case State.equalizing: state = State.venting;    break;
				case State.venting:    state = State.equalizing; break;
			}

			if (state == State.venting)
			{
				SetPassable(false);
				RefreshDialog();
				if (hasGravityRing)
				{
					if (gravityRing.animBackwards) gravityRing.deployed = true;
					else gravityRing.deployed = false;
				}
			}
		}

		// action groups
		[KSPAction("#KERBALISM_Habitat_Action")] public void Action(KSPActionParam param) { Toggle(); }

		// part tooltip
		public override string GetInfo()
		{
			return Specs().info();
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.add("volume", Lib.HumanReadableVolume(volume > double.Epsilon ? volume : Lib.PartVolume(part)));
			specs.add("surface", Lib.HumanReadableSurface(surface > double.Epsilon ? surface : Lib.PartSurface(part)));
			if (inflate.Length > 0) specs.add("Inflatable", "yes");
			return specs;
		}

		// return habitat volume in a vessel in m^3
		public static double tot_volume(Vessel v)
		{
			// we use capacity: this mean that partially pressurized parts will still count,
			return ResourceCache.Info(v, "Atmosphere").capacity;
		}

		// return habitat surface in a vessel in m^2
		public static double tot_surface(Vessel v)
		{
			// we use capacity: this mean that partially pressurized parts will still count,
			return ResourceCache.Info(v, "Shielding").capacity;
		}

		// return normalized pressure in a vessel
		public static double pressure(Vessel v)
		{
			// the pressure is simply the atmosphere level
			return ResourceCache.Info(v, "Atmosphere").level;
		}

		// return waste level in a vessel atmosphere
		public static double poisoning(Vessel v)
		{
			// the proportion of co2 in the atmosphere is simply the level of WasteAtmo
			return ResourceCache.Info(v, "WasteAtmosphere").level;
		}

		// return shielding factor in a vessel
		public static double shielding(Vessel v)
		{
			// the shielding factor is simply the level of shielding, scaled by the 'shielding efficiency' setting
			return ResourceCache.Info(v, "Shielding").level * Settings.ShieldingEfficiency;
		}

		// return living space factor in a vessel
		public static double living_space(Vessel v)
		{
			// living space is the volume per-capita normalized against an 'ideal living space' and clamped in an acceptable range
			return Lib.Clamp((tot_volume(v) / Lib.CrewCount(v)) / Settings.IdealLivingSpace, 0.1, 1.0);
		}

		// return a verbose description of shielding capability
		public static string shielding_to_string(double v)
		{
			return v <= double.Epsilon ? "none" : Lib.BuildString((20.0 * v / Settings.ShieldingEfficiency).ToString("F2"), " mm Pb");
		}

		// traduce living space value to string
		public static string living_space_to_string(double v)
		{
			if (v >= 0.99) return "ideal";
			else if (v >= 0.75) return "good";
			else if (v >= 0.5) return "modest";
			else if (v >= 0.25) return "poor";
			else return "cramped";
		}

		// enable/disable dialog "Transfer crew" on UI
		void RefreshDialog()
		{
			if (HighLogic.LoadedSceneIsEditor)
			{
				GameEvents.onEditorPartEvent.Fire(ConstructionEventType.PartTweaked, part);
				GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
			}
			else if (HighLogic.LoadedSceneIsFlight)
			{
				GameEvents.onVesselWasModified.Fire(this.vessel);
			}

			Lib.Debug("Refreshing Dialog");

			part.CheckTransferDialog();
			MonoUtilities.RefreshContextWindows(part);
		}

		void SetPassable(bool isPassable)
		{
			if (hasCLS)
			{
				// for each module
				foreach (PartModule m in part.Modules)
				{
					if (m.moduleName == "ModuleConnectedLivingSpace")
					{
						Lib.ReflectionValue(m, "passable", isPassable);
						Lib.Debug("Part '{0}', CLS has been {1}", part.partInfo.title, isPassable ? "enabled" : "disabled");
					}
				}
			}

			Lib.Debug("CrewCapacity: '{0}'", part.CrewCapacity);
			Lib.Debug("CrewTransferAvailable: '{0}'", isPassable);
			part.crewTransferAvailable = isPassable;
		}

		// habitat state
		public enum State
		{
			disabled,   // hab is disabled
			enabled,    // hab is enabled
			equalizing, // hab is equalizing (between disabled and enabled)
			venting     // hab is venting (between enabled and disabled)
		}

		// constants
		const double equalize_speed = 0.1; // equalization/venting speed per-second, in proportion to volume
	}

} // KERBALISM