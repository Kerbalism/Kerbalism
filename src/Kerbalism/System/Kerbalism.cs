using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Harmony;
using KSP.UI.Screens;
using KSP.Localization;
using System.Collections;

namespace KERBALISM
{
	/// <summary>
	/// Main initialization class : for everything that isn't save-game dependant.
	/// For save-dependant things, or things that require the game to be loaded do it in Kerbalism.OnLoad()
	/// </summary>
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class KerbalismCoreSystems : MonoBehaviour
	{
		public void Start()
		{
			// reset the save game initialized flag
			Kerbalism.IsSaveGameInitDone = false;

			// things in here will be only called once per KSP launch, after loading
			// nearly everything is available at this point, including the Kopernicus patched bodies.
			if (!Kerbalism.IsCoreMainMenuInitDone)
			{
				Kerbalism.IsCoreMainMenuInitDone = true;
			}

			// things in here will be called every the player goes to the main menu 
			RemoteTech.EnableInSPC();                   // allow RemoteTech Core to run in the Space Center
		}
	}

	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] { GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR })]
	public sealed class Kerbalism : ScenarioModule
	{
		#region declarations

		/// <summary> global access </summary>
		public static Kerbalism Fetch { get; private set; } = null;

		/// <summary> Is the one-time main menu init done. Becomes true after loading, when the the main menu is shown, and never becomes false again</summary>
		public static bool IsCoreMainMenuInitDone { get; set; } = false;

		/// <summary> Is the one-time on game load init done. Becomes true after the first OnLoad() of a game, and never becomes false again</summary>
		public static bool IsCoreGameInitDone { get; set; } = false;

		/// <summary> Is the savegame (or new game) first load done. Becomes true after the first OnLoad(), and false when returning to the main menu</summary>
		public static bool IsSaveGameInitDone { get; set; } = false;

		// used to setup KSP callbacks
		public static Callbacks Callbacks { get; private set; }

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

		// equivalent to TimeWarp.fixedDeltaTime
		// note: stored here to avoid converting it to double every time
		public static double elapsed_s;

		// number of steps from last warp blending
		private static uint warp_blending;

		/// <summary>Are we in an intermediary timewarp speed ?</summary>
		public static bool WarpBlending => warp_blending > 2u;

		// last savegame unique id
		static Guid savegameGuid;

		/// <summary> real time of last game loaded event </summary>
		public static float gameLoadTime = 0.0f;

		public static bool SerenityEnabled { get; private set; }

		#endregion

		#region initialization & save/load

		//  constructor
		public Kerbalism()
		{
			// enable global access
			Fetch = this;

			SerenityEnabled = Expansions.ExpansionsLoader.IsExpansionInstalled("Serenity");
		}

		private void OnDestroy()
		{
			Fetch = null;
		}

		private void Start()
		{
			Lib.LogDebug("Kerbalism.Start() called");
		}

		public override void OnLoad(ConfigNode node)
		{
			// everything in there will be called only one time : the first time a game is loaded from the main menu
			if (!IsCoreGameInitDone)
			{
				try
				{
					ModuleData.Init(Assembly.GetExecutingAssembly());
					PartModuleAPI.Init();
					Sim.Init();         // find suns (Kopernicus support)
					Radiation.Init();   // create the radiation fields
					ScienceDB.Init();   // build the science database (needs Sim.Init() and Radiation.Init() first)
					Science.Init();     // register the science hijacker

					// static graphic components
					LineRenderer.Init();
					ParticleRenderer.Init();
					Highlighter.Init();

					// UI
					Textures.Init();                      // set up the icon textures
					UI.Init();                            // message system, main gui, launcher
					KsmGui.KsmGuiMasterController.Init(); // setup the new gui framework

					// part prefabs post-comilation hacks. Require ScienceDB.Init() to have run first 
					PartPrefabsPostLoadCompilation.Compile();

					// Create KsmGui windows
					new ScienceArchiveWindow();

					// GameEvents callbacks
					Callbacks = new Callbacks();

					SubStepSim.Init();
				}
				catch (Exception e)
				{
					ErrorManager.AddError(true, "CORE INITIALIZATION FAILED", e.ToString());
				}
				IsCoreGameInitDone = true;
			}

			// everything in there will be called every time a savegame (or a new game) is loaded from the main menu
			if (!IsSaveGameInitDone)
			{
				try
				{
					Message.Clear();
					Cache.Init();

					// prepare storm data
					foreach (CelestialBody body in FlightGlobals.Bodies)
					{
						if (Storm.Skip_body(body))
							continue;
						Storm_data sd = new Storm_data { body = body };
						storm_bodies.Add(sd);
					}
				}
				catch (Exception e)
				{
					ErrorManager.AddError(true, "SAVEGAME LOAD FAILED", e.ToString());
				}

				IsSaveGameInitDone = true;
			}

			// eveything else will be called on every OnLoad() call :
			// - save/load
			// - every scene change
			// - in various semi-random situations (thanks KSP)

			// Fix for background IMGUI textures being dropped on scene changes since KSP 1.8
			Styles.ReloadBackgroundStyles();

			// always clear the caches
			Cache.Clear();

			// deserialize our database
			try
			{
				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Load");
				DB.Load(node);
				UnityEngine.Profiling.Profiler.EndSample();
			}
			catch (Exception e)
			{
				ErrorManager.AddError(true, "SAVEGAME LOAD FAILED", e.ToString());
				ErrorManager.CheckErrors(true);
				return;
			}

			if (DB.version != null && DB.version < DB.LAST_SUPPORTED_VERSION)
			{
				string error = "Cannot load save games from Kerbalism versions before " + DB.LAST_SUPPORTED_VERSION;
				error += "\n\nQuit the game using this popup to avoid corrupting your save and downgrade your Kerbalism version, or start a new game.";
				ErrorManager.AddError(true, "OLD SAVE GAME: this save is from Kerbalism " + DB.version, error);
			}

			ErrorManager.CheckErrors(true);

			// detect if this is a different savegame
			if (DB.Guid != savegameGuid)
			{
				// clear caches
				Message.all_logs.Clear();

				// sync main window pos from db
				UI.Sync();

				// remember savegame id
				savegameGuid = DB.Guid;
			}

			Kerbalism.gameLoadTime = Time.time;
		}

		public override void OnSave(ConfigNode node)
		{
			if (!enabled) return;

			// serialize data
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Save");
			DB.Save(node);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		#endregion

		#region fixedupdate

		public bool firstFU = true; 
		void FixedUpdate()
		{
			if (firstFU)
			{
				Lib.LogDebug("First FixedUpdate !");
				firstFU = false;
			}

			SubStepSim.OnFixedUpdate();
			Sim.OnFixedUpdate();

			// remove control locks in any case
			Misc.ClearLocks();

			// do nothing if paused (note : this is true in the editor)
			if (Lib.IsPaused())
				return;

			// convert elapsed time to double only once
			double fixedDeltaTime = TimeWarp.fixedDeltaTime;

			// and detect warp blending
			if (Math.Abs(fixedDeltaTime - elapsed_s) < 0.001)
				warp_blending = 0;
			else
				++warp_blending;

			// update elapsed time
			elapsed_s = fixedDeltaTime;

			// store info for oldest unloaded vessel
			double last_time = 0.0;
			Guid last_id = Guid.Empty;
			Vessel last_v = null;
			VesselData last_vd = null;

			// credit science at regular interval
			ScienceDB.CreditScienceBuffers(elapsed_s);

			foreach (VesselData vd in DB.VesselDatas)
			{
				vd.EarlyUpdate();
			}

			// for each vessel
			foreach (Vessel v in FlightGlobals.Vessels)
			{
				// get vessel data
				if (!v.TryGetVesselDataNoError(out VesselData vd))
				{
					// flags have an empty Guid, we never create a VesselData and never process them
					if (v.id == Guid.Empty)
						continue;

					Lib.LogDebug($"Creating VesselData for new vessel {v.vesselName}");
					vd = new VesselData(v);
					DB.AddNewVesselData(vd);
				}

				// update the vessel data validity
				vd.OnFixedUpdate(v);

				// set locks for active vessel
				if (v.isActiveVessel)
				{
					Misc.SetLocks(v);
				}

				// maintain eva dead animation and helmet state
				if (v.loaded && v.isEVA)
				{
					EVA.Update(v);
				}

				// do nothing else for invalid vessels
				if (!vd.IsSimulated)
					continue;

				// if loaded
				if (v.loaded)
				{
					// get resource cache
					VesselResHandler resources = vd.ResHandler;

					//UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.VesselDataEval");
					// update the vessel info
					vd.Evaluate(false, elapsed_s);
					//UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.Radiation");
					// show belt warnings
					Radiation.BeltWarnings(v, vd);

					// update storm data
					Storm.Update(v, vd, elapsed_s);
					UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.Comms");
					CommsMessages.Update(v, vd, elapsed_s);
					UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.Science");
					// transmit science data
					Science.Update(v, vd, elapsed_s);
					UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.Profile");
					// apply rules
					Profile.Execute(v, vd, resources, elapsed_s);
					UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.ResourceAPI");
					// part module resource updates
					ResourceAPI.ResourceUpdate(v, vd, resources, elapsed_s);
					UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.Resource");
					// apply deferred requests
					resources.ResourceUpdate(vd, v, VesselResHandler.VesselState.Loaded, elapsed_s);
					UnityEngine.Profiling.Profiler.EndSample();

					Supply.SendMessages(vd);

					// call automation scripts
					vd.computer.Automate(v, vd, resources);

					// remove from unloaded data container
					unloaded.Remove(vd.VesselId);
				}
				// if unloaded
				else
				{
					// get unloaded data, or create an empty one
					Unloaded_data ud;
					if (!unloaded.TryGetValue(vd.VesselId, out ud))
					{
						ud = new Unloaded_data();
						unloaded.Add(vd.VesselId, ud);
					}

					// accumulate time
					ud.time += elapsed_s;

					// maintain oldest entry
					if (ud.time > last_time)
					{
						last_time = ud.time;
						last_v = v;
						last_vd = vd;
					}
				}
			}

			// at most one vessel gets background processing per physics tick :
			// if there is a vessel that is not the currently loaded vessel, then
			// we will update the vessel whose most recent background update is the oldest
			if (last_v != null)
			{
				// get resource cache
				VesselResHandler resources = last_vd.ResHandler;

				//UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.VesselDataEval");
				// update the vessel info (high timewarp speeds reevaluation)
				last_vd.Evaluate(false, last_time);
				//UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.Radiation");
				// show belt warnings
				Radiation.BeltWarnings(last_v, last_vd);

				// update storm data
				Storm.Update(last_v, last_vd, last_time);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.Comms");
				CommsMessages.Update(last_v, last_vd, last_time);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.Profile");
				// apply rules
				Profile.Execute(last_v, last_vd, resources, last_time);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.Background");
				// simulate modules in background
				Background.Update(last_v, last_vd, resources, last_time);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.Science");
				// transmit science	data
				Science.Update(last_v, last_vd, last_time);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.Resource");
				// apply deferred requests
				resources.ResourceUpdate(last_vd, last_v.protoVessel, VesselResHandler.VesselState.Unloaded, last_time);
				UnityEngine.Profiling.Profiler.EndSample();

				Supply.SendMessages(last_vd);

				// call automation scripts
				last_vd.computer.Automate(last_v, last_vd, resources);

				// remove from unloaded data container
				unloaded.Remove(last_vd.VesselId);
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

		#endregion

		#region Update and GUI

		void Update()
		{
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
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.UI.Update");
			UI.Update(Callbacks.visible);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		void OnGUI()
		{
			UI.On_gui(Callbacks.visible);
		}

		#endregion
	}

	public sealed class MapCameraScript : MonoBehaviour
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

		public static void SetLocks(Vessel v)
		{
			// lock controls for EVA death
			if (EVA.IsDead(v))
			{
				InputLockManager.SetControlLock(ControlTypes.EVA_INPUT, "eva_dead_lock");
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
					// workaround for FindModulesImplementing nullrefs in 1.8 when called on the strange kerbalEVA_RD_Exp prefab
					// due to the (private) cachedModuleLists being null on it
					if (p.partPrefab.Modules.Count == 0)
						continue;

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
					if (i >= 5 && labels.Count > i + 1)
					{
						rnd.node_description.text += Lib.BuildString("\n• <color=#00ffff>(+", (labels.Count - i).ToString(), " more)</color>");
						break;
					}
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
					Message.Post(Local.Messagesmuted, Local.Messagesmuted_subtext);//"Messages muted""Be careful out there"
					Message.Mute();
				}
				else
				{
					Message.Unmute();
					Message.Post(Local.Messagesunmuted);//"Messages unmuted"
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
			if (v == null) return;
			v.TryGetVesselData(out VesselData vd);
			if (!vd.IsSimulated) return;

			// call scripts with 1-5 key
			if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
			{ vd.computer.Execute(v, ScriptType.action1); }
			if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
			{ vd.computer.Execute(v, ScriptType.action2); }
			if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
			{ vd.computer.Execute(v, ScriptType.action3); }
			if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
			{ vd.computer.Execute(v, ScriptType.action4); }
			if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
			{ vd.computer.Execute(v, ScriptType.action5); }
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
				Reputation.Instance.AddReputation(-Settings.KerbalDeathReputationPenalty, TransactionReasons.Any);
			}
		}

		// trigger a random breakdown event
		public static void Breakdown(Vessel v, ProtoCrewMember c)
		{
			// constants
			const double res_penalty = 0.1;        // proportion of food lost on 'depressed' and 'wrong_valve'

			// get a supply resource at random
			VesselResource res = null;
			if (Profile.supplies.Count > 0)
			{
				Supply supply = Profile.supplies[Lib.RandomInt(Profile.supplies.Count)];
				v.TryGetVesselData(out VesselData vd);
				res = vd.ResHandler.GetResource(supply.resource);
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
			if (res != null && res.Amount > double.Epsilon)
				events.Add(KerbalBreakdown.wrong_valve);

			// choose a breakdown event
			KerbalBreakdown breakdown = events[Lib.RandomInt(events.Count)];

			// generate message
			string text = "";
			string subtext = "";
			switch (breakdown)
			{
				case KerbalBreakdown.mumbling:
					text = Local.Kerbalmumbling;//"$ON_VESSEL$KERBAL has been in space for too long"
					subtext = Local.Kerbalmumbling_subtext;//"Mumbling incoherently"
					break;
				case KerbalBreakdown.fat_finger:
					text = Local.Kerbalfatfinger_subtext;//"$ON_VESSEL$KERBAL is pressing buttons at random on the control panel"
					subtext = Local.Kerbalfatfinger_subtext;//"Science data has been lost"
					break;
				case KerbalBreakdown.rage:
					text = Local.Kerbalrage;//"$ON_VESSEL$KERBAL is possessed by a blind rage"
					subtext = Local.Kerbalrage_subtext;//"A component has been damaged"
					break;
				case KerbalBreakdown.wrong_valve:
					text = Local.Kerbalwrongvalve;//"$ON_VESSEL$KERBAL opened the wrong valve"
					subtext = res.Name + " " + Local.Kerbalwrongvalve_subtext;//has been lost"
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
					res.Consume(res.Amount * res_penalty, ResourceBroker.Generic);
					break;
			}

			// remove reputation
			if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
			{
				Reputation.Instance.AddReputation(-Settings.KerbalBreakdownReputationPenalty, TransactionReasons.Any);
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
