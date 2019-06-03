using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Harmony;
using KSP.UI.Screens;


namespace KERBALISM
{


	/// <summary> Main class, instantiated during Main menu scene.</summary>
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class KerbalismMain: MonoBehaviour
	{
		public void Start()
		{
			Icons.Initialize();				// set up the icon textures
			RemoteTech.EnableInSPC();		// allow RemoteTech Core to run in the Space Center

			// Set the loaded trigger to false, this we will load a new
			// settings after selecting a save game. This is necessary
			// for switching between saves without shutting down the KSP
			// instance.
			//Settings.Instance.SettingsLoaded = false;
		}
	}

	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] { GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR })]
	public sealed class Kerbalism: ScenarioModule
	{
		// permit global access
		public static Kerbalism Fetch { get; private set; } = null;

		//  constructor
		public Kerbalism()
		{
			// enable global access
			Fetch = this;
			Communications.NetworkInitialized = false;
			Communications.NetworkInitializing = false;
		}

		private void OnDestroy()
		{
			Fetch = null;
		}

		public override void OnLoad(ConfigNode node)
		{
			// deserialize data
			DB.Load(node);

			Communications.NetworkInitialized = false;
			Communications.NetworkInitializing = false;

			// initialize everything just once
			if (!initialized)
			{
				// add supply resources to pods
				Profile.SetupPods();

				// initialize subsystems
				Cache.Init();
				ResourceCache.Init();
				Radiation.Init();
				Science.Init();
				LineRenderer.Init();
				ParticleRenderer.Init();
				Highlighter.Init();
				UI.Init();

#if !KSP16 && !KSP15 && !KSP14
				Serenity.Init();
#endif

				// prepare storm data
				foreach (CelestialBody body in FlightGlobals.Bodies)
				{
					if (Storm.Skip_body(body))
						continue;
					Storm_data sd = new Storm_data
					{
						body = body
					};
					storm_bodies.Add(sd);
				}

				// various tweaks to the part icons in the editor
				Misc.TweakPartIcons();

				// setup callbacks
				callbacks = new Callbacks();

				// everything was initialized
				initialized = true;
			}

			// detect if this is a different savegame
			if (DB.uid != savegame_uid)
			{
				// clear caches
				Cache.Clear();
				ResourceCache.Clear();
				Message.all_logs.Clear();

				// sync main window pos from db
				UI.Sync();

				// remember savegame id
				savegame_uid = DB.uid;
			}
		}

		public override void OnSave(ConfigNode node)
		{
			// serialize data
			DB.Save(node);
		}

		void FixedUpdate()
		{
			// remove control locks in any case
			Misc.ClearLocks();

			// do nothing if paused
			if (Lib.IsPaused())
				return;

			// maintain elapsed_s, converting to double only once
			// and detect warp blending
			double fixedDeltaTime = TimeWarp.fixedDeltaTime;
			if (Math.Abs(fixedDeltaTime - elapsed_s) > double.Epsilon)
				warp_blending = 0;
			else
				++warp_blending;
			elapsed_s = fixedDeltaTime;

			// evict oldest entry from vessel cache
			Cache.Update();

			// store info for oldest unloaded vessel
			double last_time = 0.0;
			Vessel last_v = null;
			Vessel_info last_vi = null;
			VesselData last_vd = null;
			Vessel_resources last_resources = null;

			// for each vessel
			foreach (Vessel v in FlightGlobals.Vessels)
			{
				// get vessel info from the cache
				Vessel_info vi = Cache.VesselInfo(v);

				// set locks for active vessel
				if (v.isActiveVessel)
				{
					Misc.SetLocks(v, vi);
				}

				// maintain eva dead animation and helmet state
				if (v.loaded && v.isEVA)
				{
					EVA.Update(v);
				}

				// keep track of rescue mission kerbals, and gift resources to their vessels on discovery
				if (v.loaded && vi.is_vessel)
				{
					// manage rescue mission mechanics
					Misc.ManageRescueMission(v);
				}

				// do nothing else for invalid vessels
				if (!vi.is_valid)
					continue;

				// get vessel data from db
				VesselData vd = DB.Vessel(v);

				// get resource cache
				Vessel_resources resources = ResourceCache.Get(v);

				// if loaded
				if (v.loaded)
				{
					// get most used resource
					Resource_info ec = resources.Info(v, "ElectricCharge");

					// show belt warnings
					Radiation.BeltWarnings(v, vi, vd);

					// update storm data
					Storm.Update(v, vi, vd, elapsed_s);

					Communications.Update(v, vi, vd, ec, elapsed_s);

					// Habitat equalization
					ResourceBalance.Equalizer(v);

					// transmit science data
					Science.Update(v, vi, vd, resources, elapsed_s);

					// apply rules
					Profile.Execute(v, vi, vd, resources, elapsed_s);

					// apply deferred requests
					resources.Sync(v, elapsed_s);

					// call automation scripts
					vd.computer.Automate(v, vi, resources);

					// remove from unloaded data container
					unloaded.Remove(vi.id);
				}
				// if unloaded
				else
				{
					// get unloaded data, or create an empty one
					Unloaded_data ud;
					if (!unloaded.TryGetValue(vi.id, out ud))
					{
						ud = new Unloaded_data();
						unloaded.Add(vi.id, ud);
					}

					// accumulate time
					ud.time += elapsed_s;

					// maintain oldest entry
					if (ud.time > last_time)
					{
						last_time = ud.time;
						last_v = v;
						last_vi = vi;
						last_vd = vd;
						last_resources = resources;
					}
				}
			}

			// at most one vessel gets background processing per physics tick
			// if there is a vessel that is not the currently loaded vessel, then
			// we will update the vessel whose most recent background update is the oldest
			if (last_v != null)
			{
				// get most used resource
				Resource_info last_ec = last_resources.Info(last_v, "ElectricCharge");

				// show belt warnings
				Radiation.BeltWarnings(last_v, last_vi, last_vd);

				// update storm data
				Storm.Update(last_v, last_vi, last_vd, last_time);

				Communications.Update(last_v, last_vi, last_vd, last_ec, last_time);

				// apply rules
				Profile.Execute(last_v, last_vi, last_vd, last_resources, last_time);

				// simulate modules in background
				Background.Update(last_v, last_vi, last_vd, last_resources, last_time);

				// transmit science	data
				Science.Update(last_v, last_vi, last_vd, last_resources, last_time);

				// apply deferred requests
				last_resources.Sync(last_v, last_time);

				// call automation scripts
				last_vd.computer.Automate(last_v, last_vi, last_resources);

				// remove from unloaded data container
				unloaded.Remove(last_vi.id);
			}

			// update storm data for one body per-step
			if (storm_bodies.Count > 0)
			{
				storm_bodies.ForEach(k => k.time += elapsed_s);
				Storm_data sd = storm_bodies[storm_index];
				Storm.Update(sd.body, sd.time);
				sd.time = 0.0;
				storm_index = (storm_index + 1) % storm_bodies.Count;
			}
		}

		void Update()
		{
			if (!Communications.NetworkInitializing)
			{
				Communications.NetworkInitializing = true;
				StartCoroutine(callbacks.NetworkInitialized());
			}

			// attach map renderer to planetarium camera once
			if (MapView.MapIsEnabled && map_camera_script == null)
				map_camera_script = PlanetariumCamera.Camera.gameObject.AddComponent<MapCameraScript>();

			// process keyboard input
			Misc.KeyboardInput();

			// add description to techs
			Misc.TechDescriptions();

			// set part highlight colors
			Highlighter.Update();

			// prepare gui content
			UI.Update(callbacks.visible);
		}

		void OnGUI()
		{
			UI.On_gui(callbacks.visible);
		}

		// used to setup KSP callbacks
		static Callbacks callbacks;

		// the rendering script attached to map camera
		static MapCameraScript map_camera_script;

		// store time until last update for unloaded vessels
		// note: not using reference_wrapper<T> to increase readability
		sealed class Unloaded_data { public double time; }; //< reference wrapper
		static Dictionary<Guid, Unloaded_data> unloaded = new Dictionary<Guid, Unloaded_data>();

		// used to update storm data on one body per step
		static int storm_index;
		class Storm_data { public double time; public CelestialBody body; };
		static List<Storm_data> storm_bodies = new List<Storm_data>();

		// used to initialize everything just once
		static bool initialized;

		// equivalent to TimeWarp.fixedDeltaTime
		// note: stored here to avoid converting it to double every time
		public static double elapsed_s;

		// number of steps from last warp blending
		public static uint warp_blending;

		// last savegame unique id
		static int savegame_uid;
	}

	public sealed class MapCameraScript: MonoBehaviour
	{
		void OnPostRender()
		{
			// do nothing when not in map view
			// - avoid weird situation when in some user installation MapIsEnabled is true in the space center
			if (!MapView.MapIsEnabled || HighLogic.LoadedScene == GameScenes.SPACECENTER)
				return;

			// commit all geometry
			Radiation.Render();

			// render all committed geometry
			LineRenderer.Render();
			ParticleRenderer.Render();
		}
	}

	// misc functions
	public static class Misc
	{
		public static void ClearLocks()
		{
			// remove control locks
			InputLockManager.RemoveControlLock("eva_dead_lock");
			InputLockManager.RemoveControlLock("no_signal_lock");
		}

		public static void SetLocks(Vessel v, Vessel_info vi)
		{
			// lock controls for EVA death
			if (EVA.IsDead(v))
			{
				InputLockManager.SetControlLock(ControlTypes.EVA_INPUT, "eva_dead_lock");
			}
		}

		public static void ManageRescueMission(Vessel v)
		{
			// true if we detected this was a rescue mission vessel
			bool detected = false;

			// deal with rescue missions
			foreach (ProtoCrewMember c in Lib.CrewList(v))
			{
				// get kerbal data
				KerbalData kd = DB.Kerbal(c.name);

				// flag the kerbal as not rescue at prelaunch
				if (v.situation == Vessel.Situations.PRELAUNCH)
					kd.rescue = false;

				// if the kerbal belong to a rescue mission
				if (kd.rescue)
				{
					// remember it
					detected = true;

					// flag the kerbal as non-rescue
					// note: enable life support mechanics for the kerbal
					kd.rescue = false;

					// show a message
					Message.Post(Lib.BuildString("We found <b>", c.name, "</b>"), Lib.BuildString((c.gender == ProtoCrewMember.Gender.Male ? "He" : "She"), "'s still alive!"));
				}
			}

			// gift resources
			if (detected)
			{
				var reslib = PartResourceLibrary.Instance.resourceDefinitions;
				var parts = Lib.GetPartsRecursively(v.rootPart);

				// give the vessel some propellant usable on eva
				string monoprop_name = Lib.EvaPropellantName();
				double monoprop_amount = Lib.EvaPropellantCapacity();
				foreach (var part in parts)
				{
					if (part.CrewCapacity > 0 || part.FindModuleImplementing<KerbalEVA>() != null)
					{
						if (Lib.Capacity(part, monoprop_name) <= double.Epsilon)
						{
							Lib.AddResource(part, monoprop_name, 0.0, monoprop_amount);
						}
						break;
					}
				}
				ResourceCache.Produce(v, monoprop_name, monoprop_amount, "rescue");

				// give the vessel some supplies
				Profile.SetupRescue(v);
			}
		}

		public static void TechDescriptions()
		{
			var rnd = RDController.Instance;
			if (rnd == null)
				return;
			var selected = RDController.Instance.node_selected;
			if (selected == null)
				return;
			var techID = selected.tech.techID;
			if (rnd.node_description.text.IndexOf("<i></i>\n", StringComparison.Ordinal) == -1) //< check for state in the string
			{
				rnd.node_description.text += "<i></i>\n"; //< store state in the string

				// collect unique configure-related unlocks
				HashSet<string> labels = new HashSet<string>();
				foreach (AvailablePart p in PartLoader.LoadedPartsList)
				{
					foreach (Configure cfg in p.partPrefab.FindModulesImplementing<Configure>())
					{
						foreach (ConfigureSetup setup in cfg.Setups())
						{
							if (setup.tech == selected.tech.techID)
							{
								labels.Add(Lib.BuildString(setup.name, " to ", cfg.title));
							}
						}
					}
				}

				// add unique configure-related unlocks
				// avoid printing text over the "available parts" section
				int i = 0;
				foreach (string label in labels)
				{
					rnd.node_description.text += Lib.BuildString("\n• <color=#00ffff>", label, "</color>");
					i++;
					if(i >= 5 && labels.Count > i + 1)
					{
						rnd.node_description.text += Lib.BuildString("\n• <color=#00ffff>(+", (labels.Count - i).ToString(), " more)</color>");
						break;
					}
				}
			}
		}

		public static void TweakPartIcons()
		{
			foreach (AvailablePart p in PartLoader.LoadedPartsList)
			{
				// scale part icons of the radial container variants
				switch (p.name)
				{
					case "kerbalism-container-radial-small":
						p.iconPrefab.transform.GetChild(0).localScale *= 0.60f;
						p.iconScale *= 0.60f;
						break;
					case "kerbalism-container-radial-medium":
						p.iconPrefab.transform.GetChild(0).localScale *= 0.85f;
						p.iconScale *= 0.85f;
						break;
					case "kerbalism-container-radial-big":
						p.iconPrefab.transform.GetChild(0).localScale *= 1.10f;
						p.iconScale *= 1.10f;
						break;
					case "kerbalism-container-radial-huge":
						p.iconPrefab.transform.GetChild(0).localScale *= 1.33f;
						p.iconScale *= 1.33f;
						break;
					case "kerbalism-container-inline-375":
						p.iconPrefab.transform.GetChild(0).localScale *= 1.33f;
						p.iconScale *= 1.33f;
						break;
				}

				// force a non-lexical order in the editor
				switch (p.name)
				{
					case "kerbalism-container-inline-0625":
						p.title = Lib.BuildString("<size=1><color=#00000000>00</color></size>", p.title);
						break;
					case "kerbalism-container-inline-125":
						p.title = Lib.BuildString("<size=1><color=#00000000>01</color></size>", p.title);
						break;
					case "kerbalism-container-inline-250":
						p.title = Lib.BuildString("<size=1><color=#00000000>02</color></size>", p.title);
						break;
					case "kerbalism-container-inline-375":
						p.title = Lib.BuildString("<size=1><color=#00000000>03</color></size>", p.title);
						break;
					case "kerbalism-container-radial-small":
						p.title = Lib.BuildString("<size=1><color=#00000000>04</color></size>", p.title);
						break;
					case "kerbalism-container-radial-medium":
						p.title = Lib.BuildString("<size=1><color=#00000000>05</color></size>", p.title);
						break;
					case "kerbalism-container-radial-big":
						p.title = Lib.BuildString("<size=1><color=#00000000>06</color></size>", p.title);
						break;
					case "kerbalism-container-radial-huge":
						p.title = Lib.BuildString("<size=1><color=#00000000>07</color></size>", p.title);
						break;
					case "kerbalism-greenhouse":
						p.title = Lib.BuildString("<size=1><color=#00000000>08</color></size>", p.title);
						break;
					case "kerbalism-gravityring":
						p.title = Lib.BuildString("<size=1><color=#00000000>09</color></size>", p.title);
						break;
					case "kerbalism-activeshield":
						p.title = Lib.BuildString("<size=1><color=#00000000>10</color></size>", p.title);
						break;
					case "kerbalism-chemicalplant":
						p.title = Lib.BuildString("<size=1><color=#00000000>11</color></size>", p.title);
						break;
				}
			}
		}

		public static void KeyboardInput()
		{
			// mute/unmute messages with keyboard
			if (Input.GetKeyDown(KeyCode.Pause))
			{
				if (!Message.IsMuted())
				{
					Message.Post("Messages muted", "Be careful out there");
					Message.Mute();
				}
				else
				{
					Message.Unmute();
					Message.Post("Messages unmuted");
				}
			}

			// toggle body info window with keyboard
			if (MapView.MapIsEnabled && Input.GetKeyDown(KeyCode.B))
			{
				UI.Open(BodyInfo.Body_info);
			}

			// call action scripts
			// - avoid creating vessel data for invalid vessels
			Vessel v = FlightGlobals.ActiveVessel;
			if (v != null && DB.vessels.ContainsKey(Lib.VesselID(v)))
			{
				// get computer
				Computer computer = DB.Vessel(v).computer;

				// call scripts with 1-5 key
				if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
				{ computer.Execute(v, ScriptType.action1); }
				if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
				{ computer.Execute(v, ScriptType.action2); }
				if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
				{ computer.Execute(v, ScriptType.action3); }
				if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
				{ computer.Execute(v, ScriptType.action4); }
				if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
				{ computer.Execute(v, ScriptType.action5); }
			}
		}

		// return true if the vessel is a rescue mission
		public static bool IsRescueMission(Vessel v)
		{
			// if at least one of the crew is flagged as rescue, consider it a rescue mission
			foreach (var c in Lib.CrewList(v))
			{
				if (DB.Kerbal(c.name).rescue)
					return true;
			}


			// not a rescue mission
			return false;
		}

		// kill a kerbal
		// note: you can't kill a kerbal while iterating over vessel crew list, do it outside the loop
		public static void Kill(Vessel v, ProtoCrewMember c)
		{
			// if on pod
			if (!v.isEVA)
			{
				// if vessel is loaded
				if (v.loaded)
				{
					// find part
					Part part = null;
					foreach (Part p in v.parts)
					{
						if (p.protoModuleCrew.Find(k => k.name == c.name) != null)
						{ part = p; break; }
					}

					// remove kerbal from part
					part.RemoveCrewmember(c);

					// and from vessel
					v.RemoveCrew(c);

					// then kill it
					c.Die();
				}
				// if vessel is not loaded
				else
				{
					// find proto part
					ProtoPartSnapshot part = null;
					foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
					{
						if (p.HasCrew(c.name))
						{ part = p; break; }
					}

					// remove from part
					part.RemoveCrew(c.name);

					// and from vessel
					v.protoVessel.RemoveCrew(c);

					// flag as dead
					c.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
				}

				// forget kerbal data
				DB.KillKerbal(c.name, true);
			}
			// else it must be an eva death
			else
			{
				// flag as eva death
				DB.Kerbal(c.name).eva_dead = true;

				// rename vessel
				v.vesselName = c.name + "'s body";
			}

			// remove reputation
			if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
			{
				Reputation.Instance.AddReputation(-PreferencesBasic.Instance.deathPenalty, TransactionReasons.Any);
			}
		}

		// trigger a random breakdown event
		public static void Breakdown(Vessel v, ProtoCrewMember c)
		{
			// constants
			const double res_penalty = 0.1;        // proportion of food lost on 'depressed' and 'wrong_valve'

			// get a supply resource at random
			Resource_info res = null;
			if (Profile.supplies.Count > 0)
			{
				Supply supply = Profile.supplies[Lib.RandomInt(Profile.supplies.Count)];
				res = ResourceCache.Info(v, supply.resource);
			}

			// compile list of events with condition satisfied
			List<KerbalBreakdown> events = new List<KerbalBreakdown>
			{
				KerbalBreakdown.mumbling //< do nothing, here so there is always something that can happen
			};
			if (Lib.HasData(v))
				events.Add(KerbalBreakdown.fat_finger);
			if (Reliability.CanMalfunction(v))
				events.Add(KerbalBreakdown.rage);
			if (res != null && res.amount > double.Epsilon)
				events.Add(KerbalBreakdown.wrong_valve);

			// choose a breakdown event
			KerbalBreakdown breakdown = events[Lib.RandomInt(events.Count)];

			// generate message
			string text = "";
			string subtext = "";
			switch (breakdown)
			{
				case KerbalBreakdown.mumbling:
					text = "$ON_VESSEL$KERBAL has been in space for too long";
					subtext = "Mumbling incoherently";
					break;
				case KerbalBreakdown.fat_finger:
					text = "$ON_VESSEL$KERBAL is pressing buttons at random on the control panel";
					subtext = "Science data has been lost";
					break;
				case KerbalBreakdown.rage:
					text = "$ON_VESSEL$KERBAL is possessed by a blind rage";
					subtext = "A component has been damaged";
					break;
				case KerbalBreakdown.wrong_valve:
					text = "$ON_VESSEL$KERBAL opened the wrong valve";
					subtext = res.resource_name + " has been lost";
					break;
			}

			// post message first so this one is shown before malfunction message
			Message.Post(Severity.breakdown, Lib.ExpandMsg(text, v, c), subtext);

			// trigger the event
			switch (breakdown)
			{
				case KerbalBreakdown.mumbling:
					break; // do nothing
				case KerbalBreakdown.fat_finger:
					Lib.RemoveData(v);
					break;
				case KerbalBreakdown.rage:
					Reliability.CauseMalfunction(v);
					break;
				case KerbalBreakdown.wrong_valve:
					res.Consume(res.amount * res_penalty, "breakdown");
					break;
			}

			// remove reputation
			if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
			{
				Reputation.Instance.AddReputation(-PreferencesBasic.Instance.breakdownPenalty, TransactionReasons.Any);
			}
		}

		// breakdown events
		public enum KerbalBreakdown
		{
			mumbling,         // do nothing (in case all conditions fail)
			fat_finger,       // data has been canceled
			rage,             // components have been damaged
			wrong_valve       // supply resource has been lost
		}
	}


} // KERBALISM
