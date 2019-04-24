using System;
using System.Collections;
using System.Collections.Generic;
using KSP.UI.Screens;
using KSP.UI.Screens.SpaceCenter.MissionSummaryDialog;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Callbacks
	{
		public Callbacks()
		{
			GameEvents.onCrewOnEva.Add(this.ToEVA);
			GameEvents.onCrewBoardVessel.Add(this.FromEVA);
			GameEvents.onVesselRecoveryProcessing.Add(this.VesselRecoveryProcessing);
			GameEvents.onVesselRecovered.Add(this.VesselRecovered);
			GameEvents.onVesselTerminated.Add(this.VesselTerminated);
			GameEvents.onVesselWillDestroy.Add(this.VesselDestroyed);
			GameEvents.onPartCouple.Add(this.VesselDock);

			GameEvents.onVesselChange.Add(this.PurgePartsCache);
			GameEvents.onVesselStandardModification.Add(this.PurgePartsCache);

			GameEvents.onPartDie.Add(this.PartDestroyed);
			GameEvents.OnTechnologyResearched.Add(this.TechResearched);
			GameEvents.onGUIEditorToolbarReady.Add(this.AddEditorCategory);

			GameEvents.onGUIAdministrationFacilitySpawn.Add(() => visible = false);
			GameEvents.onGUIAdministrationFacilityDespawn.Add(() => visible = true);

			GameEvents.onGUIAstronautComplexSpawn.Add(() => visible = false);
			GameEvents.onGUIAstronautComplexDespawn.Add(() => visible = true);

			GameEvents.onGUIMissionControlSpawn.Add(() => visible = false);
			GameEvents.onGUIMissionControlDespawn.Add(() => visible = true);

			GameEvents.onGUIRnDComplexSpawn.Add(() => visible = false);
			GameEvents.onGUIRnDComplexDespawn.Add(() => visible = true);

			GameEvents.onHideUI.Add(() => visible = false);
			GameEvents.onShowUI.Add(() => visible = true);

			GameEvents.onGUILaunchScreenSpawn.Add((_) => visible = false);
			GameEvents.onGUILaunchScreenDespawn.Add(() => visible = true);

			GameEvents.onGameSceneSwitchRequested.Add((_) => visible = false);
			GameEvents.onGUIApplicationLauncherReady.Add(() => visible = true);

			GameEvents.CommNet.OnNetworkInitialized.Add(() => Kerbalism.Fetch.StartCoroutine(NetworkInitialized()));

			// add editor events
			GameEvents.onEditorShipModified.Add((sc) => Planner.Planner.EditorShipModifiedEvent(sc));
		}

		public IEnumerator NetworkInitialized()
		{
			yield return new WaitForSeconds(2);
			Lib.DebugLog("NetworkInitialized");
			Communications.NetworkInitialized = true;
			RemoteTech.Startup();
		}

		void PurgePartsCache(Vessel vessel)
		{
			Cache.Purge(vessel);
		}

		void ToEVA(GameEvents.FromToAction<Part, Part> data)
		{
			// get total crew in the origin vessel
			double tot_crew = Lib.CrewCount(data.from.vessel) + 1.0;

			// get vessel resources handler
			Vessel_resources resources = ResourceCache.Get(data.from.vessel);

			// setup supply resources capacity in the eva kerbal
			Profile.SetupEva(data.to);

			String prop_name = Lib.EvaPropellantName();

			// for each resource in the kerbal
			for (int i = 0; i < data.to.Resources.Count; ++i)
			{
				// get the resource
				PartResource res = data.to.Resources[i];

				// eva prop is handled differently
				if (res.resourceName == prop_name)
				{
					continue;
				}

				double quantity = Math.Min(resources.Info(data.from.vessel, res.resourceName).amount / tot_crew, res.maxAmount);
				// remove resource from vessel
				quantity = data.from.RequestResource(res.resourceName, quantity);

				// add resource to eva kerbal
				data.to.RequestResource(res.resourceName, -quantity);
			}

			// take as much of the propellant as possible. just imagine: there are 1.3 units left, and 12 occupants
			// in the ship. you want to send out an engineer to fix the chemical plant that produces monoprop,
			// and have to get from one end of the station to the other with just 0.1 units in the tank...
			// nope.
			double evaPropQuantity = data.from.RequestResource(prop_name, Lib.EvaPropellantCapacity());

			// We can't just add the monoprop here, because that doesn't always work. It might be related
			// to the fact that stock KSP wants to add 5 units of monoprop to new EVAs. Instead of fighting KSP here,
			// we just let it do it's thing and set our amount later in EVA.cs - which seems to work just fine.
			Cache.VesselInfo(data.to.vessel).evaPropQuantity = evaPropQuantity;

			// Airlock loss
			resources.Consume(data.from.vessel, "Nitrogen", PreferencesLifeSupport.Instance.evaAtmoLoss, "airlock");

			// show warning if there is little or no EVA propellant in the suit
			if (evaPropQuantity <= 0.05 && !Lib.Landed(data.from.vessel))
			{
				Message.Post(Severity.danger,
					Lib.BuildString("There isn't any <b>", prop_name, "</b> in the EVA suit"), "Don't let the ladder go!");
			}

			// turn off headlamp light, to avoid stock bug that show them for a split second when going on eva
			KerbalEVA kerbal = data.to.FindModuleImplementing<KerbalEVA>();
			EVA.HeadLamps(kerbal, false);

			// execute script
			DB.Vessel(data.from.vessel).computer.Execute(data.from.vessel, ScriptType.eva_out);
		}


		void FromEVA(GameEvents.FromToAction<Part, Part> data)
		{
			String prop_name = Lib.EvaPropellantName();

			// for each resource in the eva kerbal
			for (int i = 0; i < data.from.Resources.Count; ++i)
			{
				// get the resource
				PartResource res = data.from.Resources[i];

				// add leftovers to the vessel
				data.to.RequestResource(res.resourceName, -res.amount);
			}

			// merge drives data
			Drive.Transfer(data.from.vessel, data.to.vessel, true);

			// forget vessel data
			DB.vessels.Remove(Lib.VesselID(data.from.vessel));
			Drive.Purge(data.from.vessel);

			// execute script
			DB.Vessel(data.to.vessel).computer.Execute(data.to.vessel, ScriptType.eva_in);
		}


		void VesselRecoveryProcessing(ProtoVessel v, MissionRecoveryDialog dialog, float score)
		{
			// note:
			// this function accumulate science stored in drives on recovery,
			// and visualize the data in the recovery dialog window

			// do nothing if science system is disabled, or in sandbox mode
			if (!Features.Science || HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX)
				return;

			var vesselID = Lib.VesselID(v);
			// get the drive data from DB
			if (!DB.vessels.ContainsKey(vesselID))
				return;

			foreach (Drive drive in Drive.GetDrives(v))
			{
				// for each file in the drive
				foreach (KeyValuePair<string, File> p in drive.files)
				{
					// shortcuts
					string filename = p.Key;
					File file = p.Value;

					// de-buffer partially transmitted data
					file.size += file.buff;
					file.buff = 0.0;

					// get subject
					ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(filename);

					// credit science
					float credits = Science.Credit(filename, file.size, false, v, (float)file.science_cap);

					// create science widged
					ScienceSubjectWidget widged = ScienceSubjectWidget.Create
					(
					  subject,            // subject
					  (float)file.size,   // data gathered
					  credits,            // science points
					  dialog              // recovery dialog
					);

					// add widget to dialog
					dialog.AddDataWidget(widged);

					// add science credits to total
					dialog.scienceEarned += (float)credits;
				}

				// for each sample in the drive
				// for each file in the drive
				foreach (KeyValuePair<string, Sample> p in drive.samples)
				{
					// shortcuts
					string filename = p.Key;
					Sample sample = p.Value;

					// get subject
					ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(filename);

					// credit science
					float credits = Science.Credit(filename, sample.size, false, v, (float)sample.science_cap);

					// create science widged
					ScienceSubjectWidget widged = ScienceSubjectWidget.Create
					(
					  subject,            // subject
					  (float)sample.size, // data gathered
					  credits,            // science points
					  dialog              // recovery dialog
					);

					// add widget to dialog
					dialog.AddDataWidget(widged);

					// add science credits to total
					dialog.scienceEarned += (float)credits;
				}
			}
		}


		void VesselRecovered(ProtoVessel pv, bool b)
		{
			// note: this is called multiple times when a vessel is recovered

			// for each crew member
			foreach (ProtoCrewMember c in pv.GetVesselCrew())
			{
				// avoid creating kerbal data in db again,
				// as this function may be called multiple times
				if (!DB.ContainsKerbal(c.name))
					continue;

				// set roster status of eva dead kerbals
				if (DB.Kerbal(c.name).eva_dead)
				{
					c.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
				}

				// reset kerbal data of recovered kerbals
				DB.RecoverKerbal(c.name);
			}

			DB.vessels.Remove(Lib.VesselID(pv));

			// purge the caches
			Cache.Purge(pv);
			ResourceCache.Purge(pv);
			Drive.Purge(pv);
		}


		void VesselTerminated(ProtoVessel pv)
		{
			// forget all kerbals data
			foreach (ProtoCrewMember c in pv.GetVesselCrew())
				DB.KillKerbal(c.name, true);

			DB.vessels.Remove(Lib.VesselID(pv));

			// purge the caches
			Cache.Purge(pv);
			ResourceCache.Purge(pv);
			Drive.Purge(pv);
		}


		void VesselDestroyed(Vessel v)
		{
			DB.vessels.Remove(Lib.VesselID(v));

			// rescan the damn kerbals
			// - vessel crew is empty at destruction time
			// - we can't even use the flightglobal roster, because sometimes it isn't updated yet at this point
			HashSet<string> kerbals_alive = new HashSet<string>();
			HashSet<string> kerbals_dead = new HashSet<string>();
			foreach (Vessel ov in FlightGlobals.Vessels)
			{
				foreach (ProtoCrewMember c in Lib.CrewList(ov))
					kerbals_alive.Add(c.name);
			}
			foreach (KeyValuePair<string, KerbalData> p in DB.Kerbals())
			{
				if (!kerbals_alive.Contains(p.Key))
					kerbals_dead.Add(p.Key);
			}
			foreach (string n in kerbals_dead)
			{
				// we don't know if the kerbal really is dead, or if it is just not currently assigned to a mission
				DB.KillKerbal(n, false);
			}

			// purge the caches
			Cache.Purge(v);
			ResourceCache.Purge(v);
			Drive.Purge(v);
		}

		void VesselDock(GameEvents.FromToAction<Part, Part> e)
		{
			var fromVessel = e.from.vessel;
			DB.vessels.Remove(Lib.VesselID(fromVessel));

			// note:
			//  we do not forget vessel data here, it just became inactive
			//  and ready to be implicitly activated again on undocking
			//  we do however tweak the data of the vessel being docked a bit,
			//  to avoid states getting out of sync, leading to unintuitive behaviours
			VesselData vd = DB.Vessel(fromVessel);
			vd.msg_belt = false;
			vd.msg_signal = false;
			vd.storm_age = 0.0;
			vd.storm_time = 0.0;
			vd.storm_state = 0;
			vd.supplies.Clear();
			vd.scansat_id.Clear();

			Cache.Purge(e.from.vessel);
		}

		void PartDestroyed(Part p)
		{
			// do nothing in the editor
			if (Lib.IsEditor())
				return;

			var vi = Cache.VesselInfo(p.vessel);
			if (!vi.is_valid)
				return;

			Cache.Purge(p.vessel);
			DB.drives.Remove(p.flightID);
		}

		void AddEditorCategory()
		{
			if (PartLoader.LoadedPartsList.Find(k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0) != null)
			{
				RUI.Icons.Selectable.Icon icon = new RUI.Icons.Selectable.Icon("Kerbalism", Icons.category_normal, Icons.category_selected);
				PartCategorizer.Category category = PartCategorizer.Instance.filters.Find(k => string.Equals(k.button.categoryName, "filter by function", StringComparison.OrdinalIgnoreCase));
				PartCategorizer.AddCustomSubcategoryFilter(category, "Kerbalism", "Kerbalism", icon, k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0);
			}
		}

		void TechResearched(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> data)
		{
			if (data.target != RDTech.OperationResult.Successful)
				return;

			// collect unique configure-related unlocks
			HashSet<string> labels = new HashSet<string>();
			foreach (AvailablePart p in PartLoader.LoadedPartsList)
			{
				foreach (Configure cfg in p.partPrefab.FindModulesImplementing<Configure>())
				{
					foreach (ConfigureSetup setup in cfg.Setups())
					{
						if (setup.tech == data.host.techID)
						{
							labels.Add(Lib.BuildString(setup.name, " in ", cfg.title));
						}
					}
				}

				// add unique configure-related unlocks
				foreach (string label in labels)
				{
					Message.Post
					(
					  "<color=#00ffff><b>PROGRESS</b></color>\nOur scientists just made a breakthrough",
					  Lib.BuildString("We now have access to <b>", label, "</b>")
					);
				}
			}
		}

		public bool visible;
	}


} // KERBALISM
