using System;
using System.Collections;
using System.Collections.Generic;
using Harmony;
using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM
{
	// OnPartDie is not called for the root part
	// OnPartWillDie works but isn't available in 1.5/1.6
	// Until we drop 1.5/1.6 support, we use this patch instead
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("Die")]
	class Part_Die
	{
		static bool Prefix(Part __instance)
		{
			// replicate OnPartWillDie
			if (__instance.State == PartStates.DEAD)
				return true;

			Kerbalism.Callbacks.OnPartWillDie(__instance);

			return true; // continue to Part.Die()
		}
	}

	// Create a "OnPartAfterDecouple" event that happen after the decoupling is complete, 
	// and where you have access to the old vessel and the new vessel.
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("decouple")]
	class Part_decouple
	{
		static bool Prefix(Part __instance, out Vessel __state)
		{
			// get the vessel of the part, before decoupling
			__state = __instance.vessel;
			return true; // continue to Part.decouple()
		}

		static void Postfix(Part __instance, Vessel __state)
		{
			// only fire the event if a new vessel has been created
			if (__instance.vessel != null && __state != null && __instance.vessel != __state)
			{
				Kerbalism.Callbacks.OnPartAfterDecouple(__instance, __state, __instance.vessel);
			}
		}
	}

	// Create a "OnPartAfterUndock" event that happen after the undocking is complete, 
	// and where you have access to the old vessel and the new vessel.
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("Undock")]
	class Part_Undock
	{
		static bool Prefix(Part __instance, out Vessel __state)
		{
			// get the vessel of the part, before decoupling
			__state = __instance.vessel;
			return true; // continue to Part.decouple()
		}

		static void Postfix(Part __instance, Vessel __state)
		{
			// only fire the event if a new vessel has been created
			if (__instance.vessel != null && __state != null && __instance.vessel != __state)
			{
				Kerbalism.Callbacks.OnPartAfterUndock(__instance, __state, __instance.vessel);
			}
		}
	}

	public sealed class Callbacks
	{
		public static EventData<Part, Configure> onConfigure = new EventData<Part, Configure>("onConfigure");

		public Callbacks()
		{
			GameEvents.onPartCouple.Add(OnPartCouple);

			GameEvents.onCrewOnEva.Add(this.ToEVA);
			GameEvents.onCrewBoardVessel.Add(this.FromEVA);
			GameEvents.onVesselRecovered.Add(this.VesselRecovered);
			GameEvents.onVesselTerminated.Add(this.VesselTerminated);
			GameEvents.onVesselWillDestroy.Add(this.VesselDestroyed);
			GameEvents.onNewVesselCreated.Add(this.VesselCreated);
			GameEvents.onPartCouple.Add(this.VesselDock);

			GameEvents.OnVesselRollout.Add(this.VesselRollout);

			GameEvents.onVesselChange.Add((v) => { OnVesselModified(v); });
			GameEvents.onVesselStandardModification.Add((v) => { OnVesselStandardModification(v); });

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

		private void OnPartCouple(GameEvents.FromToAction<Part, Part> data)
		{
			VesselData.OnPartCouple(data);
		}

		// Called by an harmony patch, happens every time a part is decoupled (decouplers, joint failure...)
		// but only if a new vessel has been created in the process
		public void OnPartAfterUndock(Part part, Vessel oldVessel, Vessel newVessel)
		{
			VesselData.OnDecoupleOrUndock(oldVessel, newVessel);
		}

		// Called by an harmony patch, happens every time a part is undocked
		// but only if a new vessel has been created in the process
		public void OnPartAfterDecouple(Part part, Vessel oldVessel, Vessel newVessel)
		{
			VesselData.OnDecoupleOrUndock(oldVessel, newVessel);
		}

		// Called by an harmony patch, exactly the same as the stock OnPartWillDie (that is not available in 1.5/1.6)
		public void OnPartWillDie(Part p)
		{
			// do nothing in the editor
			if (Lib.IsEditor())
				return;

			// remove part from vesseldata
			VesselData.OnPartWillDie(p);

			// update vessel
			this.OnVesselModified(p.vessel);
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
				emitter.Recalculate();

			Cache.PurgeVesselCaches(vessel);
			//vessel.KerbalismData().UpdateOnVesselModified();
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
			Cache.PurgeVesselCaches(data.from.vessel);
			//Drive.Purge(data.from.vessel);

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

			// purge the caches
			ResourceCache.Purge(pv);
			Cache.PurgeVesselCaches(pv);
		}


		void VesselTerminated(ProtoVessel pv)
		{
			// forget all kerbals data
			foreach (ProtoCrewMember c in pv.GetVesselCrew())
				DB.KillKerbal(c.name, true);

			// purge the caches
			ResourceCache.Purge(pv);
			Cache.PurgeVesselCaches(pv);
			//Drive.Purge(pv);
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
			foreach (string key in DB.Kerbals().Keys)
			{
				if (!kerbals_alive.Contains(key))
					kerbals_dead.Add(key);
			}
			foreach (string n in kerbals_dead)
			{
				// we don't know if the kerbal really is dead, or if it is just not currently assigned to a mission
				DB.KillKerbal(n, false);
			}

			// purge the caches
			ResourceCache.Purge(v);		// works with loaded and unloaded vessels
			//Drive.Purge(v);				// works with loaded and unloaded vessels
			Cache.PurgeVesselCaches(v); // works with loaded and unloaded vessels
		}

		void VesselDock(GameEvents.FromToAction<Part, Part> e)
		{
			Cache.PurgeVesselCaches(e.from.vessel);
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
