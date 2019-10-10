using System;
using System.Collections;
using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Callbacks
	{
		public Callbacks()
		{
			GameEvents.onCrewOnEva.Add(this.ToEVA);
			GameEvents.onCrewBoardVessel.Add(this.FromEVA);
			GameEvents.onVesselRecovered.Add(this.VesselRecovered);
			GameEvents.onVesselTerminated.Add(this.VesselTerminated);
			GameEvents.onVesselWillDestroy.Add(this.VesselDestroyed);
			GameEvents.onNewVesselCreated.Add(this.VesselCreated);
			GameEvents.onPartCouple.Add(this.VesselDock);

			GameEvents.OnVesselRollout.Add(this.VesselRollout);

			GameEvents.onGameStatePostLoad.Add(this.GameStateLoad);

			GameEvents.onVesselChange.Add((v) => { OnVesselModified(v); });
			GameEvents.onVesselStandardModification.Add((v) => { OnVesselStandardModification(v); });

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

		private void GameStateLoad(ConfigNode data)
		{
			Cache.PurgeAllCaches();
		}

		private void OnVesselStandardModification(Vessel vessel)
		{
			// avoid this being called on vessel launch, when vessel is not yet properly initialized
			if (!vessel.loaded && vessel.protoVessel == null) return;
			OnVesselModified(vessel);
		}

		private void OnVesselModified(Vessel vessel)
		{
			foreach(var emitter in vessel.FindPartModulesImplementing<Emitter>())
			{
				emitter.Recalculate();
			}

			Cache.PurgeVesselCaches(vessel);
			vessel.KerbalismData().UpdateOnVesselModified(vessel);
		}

		public IEnumerator NetworkInitialized()
		{
			yield return new WaitForSeconds(2);
			Communications.NetworkInitialized = true;
			RemoteTech.Startup();
		}

		void ToEVA(GameEvents.FromToAction<Part, Part> data)
		{
			OnVesselModified(data.from.vessel);
			OnVesselModified(data.to.vessel);

			// get total crew in the origin vessel
			double tot_crew = Lib.CrewCount(data.from.vessel) + 1.0;

			// get vessel resources handler
			VesselResources resources = ResourceCache.Get(data.from.vessel);

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

				double quantity = Math.Min(resources.GetResource(data.from.vessel, res.resourceName).Amount / tot_crew, res.maxAmount);
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
			// don't put that into Cache.VesselInfo because that can be deleted before we get there
			Cache.SetVesselObjectsCache(data.to.vessel, "eva_prop", evaPropQuantity);

			// Airlock loss
			resources.Consume(data.from.vessel, "Nitrogen", Settings.LifeSupportAtmoLoss, "airlock");

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
			data.from.vessel.KerbalismData().computer.Execute(data.from.vessel, ScriptType.eva_out);
		}


		void FromEVA(GameEvents.FromToAction<Part, Part> data)
		{
			// contract configurator calls this event with both parts being the same when it adds a passenger
			if (data.from == data.to)
				return;

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

			// forget EVA vessel data
			data.from.vessel.KerbalismDataDelete();
			Cache.PurgeVesselCaches(data.from.vessel);
			Drive.Purge(data.from.vessel);

			// update boarded vessel
			this.OnVesselModified(data.to.vessel);

			// execute script
			data.to.vessel.KerbalismData().computer.Execute(data.to.vessel, ScriptType.eva_in);
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

			// delete the vessel data
			pv.KerbalismDataDelete();

			// purge the caches
			ResourceCache.Purge(pv);
			Drive.Purge(pv);
			Cache.PurgeVesselCaches(pv);
		}


		void VesselTerminated(ProtoVessel pv)
		{
			// forget all kerbals data
			foreach (ProtoCrewMember c in pv.GetVesselCrew())
				DB.KillKerbal(c.name, true);

			// delete the vessel data
			pv.KerbalismDataDelete();

			// purge the caches
			ResourceCache.Purge(pv);
			Drive.Purge(pv);
			Cache.PurgeVesselCaches(pv);
		}

		void VesselCreated(Vessel v)
		{
#if !KSP15_16
			if (Serenity.GetModuleGroundExpControl(v) != null)
				v.vesselName = Lib.BuildString(v.mainBody.name, " Site ", Lib.Greek());
#endif
		}

		void VesselDestroyed(Vessel v)
		{
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

			// delete the vessel data
			v.KerbalismDataDelete();

			// purge the caches
			ResourceCache.Purge(v);
			Drive.Purge(v);
			Cache.PurgeVesselCaches(v);
		}

		void VesselDock(GameEvents.FromToAction<Part, Part> e)
		{
			Vessel dockingVessel = e.from.vessel;
			// note:
			//  we do not delete vessel data here, it just became inactive
			//  and ready to be implicitly activated again on undocking
			//  we do however tweak the data of the vessel being docked a bit,
			//  to avoid states getting out of sync, leading to unintuitive behaviours
			dockingVessel.KerbalismData().UpdateOnDock();
			Cache.PurgeVesselCaches(dockingVessel);

			// Update docked to vessel
			this.OnVesselModified(e.to.vessel);
		}

		void VesselRollout(ShipConstruct newVessel)
		{
			var vessel = FlightGlobals.ActiveVessel;
			foreach (var m in vessel.FindPartModulesImplementing<IModuleRollout>())
			{
				m.OnRollout();
			}
		}

		void PartDestroyed(Part p)
		{
			// do nothing in the editor
			if (Lib.IsEditor())
				return;

			// only on valid vessels
			if (!p.vessel.KerbalismIsValid()) return;

			// update vessel
			this.OnVesselModified(p.vessel);

			// remove drive
			if (DB.drives.ContainsKey(p.flightID))
				DB.drives[p.flightID].Purge(p.flightID);
		}

		void AddEditorCategory()
		{
			if (PartLoader.LoadedPartsList.Find(k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0) != null)
			{
				RUI.Icons.Selectable.Icon icon = new RUI.Icons.Selectable.Icon("Kerbalism", Textures.category_normal, Textures.category_selected);
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
