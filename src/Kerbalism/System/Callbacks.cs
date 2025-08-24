using HarmonyLib;
using KSP.Localization;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
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
			GameEvents.onCrewOnEva.Add(this.ToEVA);
			GameEvents.onCrewBoardVessel.Add(this.FromEVA);
			GameEvents.onCommandSeatInteraction.Add(this.OnCommandSeatInteraction);
			GameEvents.onVesselRecovered.Add(this.VesselRecovered);
			GameEvents.onVesselRecoveryProcessingComplete.Add(this.VesselRecoveryProcessingComplete);
			GameEvents.onVesselTerminated.Add(this.VesselTerminated);
			GameEvents.onVesselWillDestroy.Add(this.VesselDestroyed);
			GameEvents.onNewVesselCreated.Add(this.VesselCreated);
			GameEvents.onPartCouple.Add(this.OnPartCouple);
			GameEvents.onPartCoupleComplete.Add(this.OnPartCoupleComplete);

#if (KSP111 || KSP112)
            // EVA construction events are going trough dedicated stock code paths that don't trigger the other events
            GameEvents.OnEVAConstructionModePartAttached.Add(this.OnEVAConstructionModePartAttached);
            GameEvents.OnEVAConstructionModePartDetached.Add(this.OnEVAConstructionModePartDetached);
#endif
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

			// add editor events
			GameEvents.onEditorShipModified.Add((sc) => Planner.Planner.EditorShipModifiedEvent(sc));
		}

#if (KSP111 || KSP112)
        // this require special handling as EVA construction doesn't trigger the other events
        private void OnEVAConstructionModePartAttached(Vessel partVessel, Part part)
        {
            VesselData.OnPartAboutToCouple(part, part);
            OnVesselModified(partVessel);
        }

        // an EVA construction detached part has no vessel and doesn't really exist anymore.
		// as far as we are concerned, it doesn't exist anymore.
        private void OnEVAConstructionModePartDetached(Vessel partVessel, Part part)
        {
			VesselData.OnPartWillDie(partVessel, part.flightID);
            OnVesselModified(partVessel);
        }
#endif

		// Called when two vessels are about to be merged, while their state is not yet changed.
		private void OnPartCouple(GameEvents.FromToAction<Part, Part> data)
		{
			VesselData.OnPartAboutToCouple(data.from, data.to);
			Cache.PurgeVesselCaches(data.from.vessel);
		}

		// Called when the merging process is done, and only the merged vessel remains
		private void OnPartCoupleComplete(GameEvents.FromToAction<Part, Part> data)
		{
			VesselData.OnPartCoupleComplete(data.from, data.to);
			OnVesselModified(data.to.vessel);
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
		}

		private void OnCommandSeatInteraction(KerbalEVA kerbalEVA, bool isInteractionEnter)
		{
			if (isInteractionEnter)
            {
                // grant free pressure and supplies for EVA kerbals launching in an external command seat
				// This works because technically, when launching in an external seat, the kerbal is
				// spawned as a separate vessel, which then auto-board the launched vessel / external seat.
                if (kerbalEVA.vessel.situation == Vessel.Situations.PRELAUNCH)
				{
                    PartResource atmoRes = kerbalEVA.part.Resources[Habitat.AtmoResName];
                    if (atmoRes != null)
                        atmoRes.amount = atmoRes.maxAmount;

                    Profile.SetupEva(kerbalEVA.part, true);
                }
            }
			else
			{
				// When leaving a command chair, fill supplies (EC / oxygen in the default profile)
				// and EVA propellant. We don't need to handle the rest (Atmo/WasteAtmo) like for EVAing
				// because the EVA kerbal already existed and was "connected" to the vessel before leaving.
				// For this same reason, we also don't need to handle anything when boarding an external
				// command seat, as the EVA kerbal doesn't disappear, but is actually "docked" to the vessel,
				// and becoming part of it.
                KerbalSeat leavedSeat = Lib.ReflectionValue<KerbalSeat>(kerbalEVA, "kerbalSeat");
                if (leavedSeat != null)
				{
                    // compute the share of supplies that this eva kerbal will receive from the vessel
                    double resourceMaxShare = 1.0 / (Lib.CrewCount(leavedSeat.vessel) + 1.0);

                    VesselResources vesselresources = ResourceCache.Get(leavedSeat.vessel);
                    foreach (Supply supply in Profile.supplies)
                    {
                        if (supply.on_eva == 0.0)
                            continue;

						PartResource res = kerbalEVA.part.Resources[supply.resource];
						if (res == null || !res.flowState)
							continue;

                        // compute the share of resources that this eva kerbal will receive from the vessel
                        double transferred = Math.Min(vesselresources.GetResource(leavedSeat.vessel, supply.resource).Amount * resourceMaxShare, res.maxAmount - res.amount);

                        // remove resource from vessel
                        transferred = leavedSeat.part.RequestResource(res.resourceName, transferred);

                        // add resource to eva kerbal
                        kerbalEVA.part.RequestResource(res.resourceName, -transferred);
                    }

					// refill self-consuming scrubber process resource(s)
					// this can be exploited if the vessel has no scrubbing capability, but not doing it would make external seats pretty much useless
					foreach (Process process in Profile.processes)
					{
						if (process.inputs.ContainsKey(Habitat.WasteAtmoResName))
						{
							foreach (string modifier in process.modifiers)
							{
								if (process.inputs.ContainsKey(modifier))
								{
									PartResource nonregenScrubberRes = kerbalEVA.part.Resources[modifier];
									if (nonregenScrubberRes != null)
										nonregenScrubberRes.amount = nonregenScrubberRes.maxAmount;

                                }
                            }
						}
					}

                    FillEVAPropellant(leavedSeat.part, kerbalEVA.part);
                }
            }
        }

		void ToEVA(GameEvents.FromToAction<Part, Part> data)
		{
			// Start a coroutine for doing resource transfers once the kerbal EVA is started (this is too early here)
			data.to.StartCoroutine(DelayedOnEVA(data.from, data.to));
		}

		IEnumerator DelayedOnEVA(Part hatchPart, Part evaKerbalPart)
		{
            // wait until the Habitat module on the Kerbal has gone through it OnStart()/Configure() so the resources exists.
            while (!evaKerbalPart.started)
				yield return new WaitForFixedUpdate();

			// compute the share of resources that this eva kerbal will receive from the vessel
			double resourceMaxShare = 1.0 / (Lib.CrewCount(hatchPart.vessel) + 1.0);

			VesselResources vesselresources = ResourceCache.Get(hatchPart.vessel);
			for (int i = 0; i < evaKerbalPart.Resources.Count; ++i)
			{
				PartResource res = evaKerbalPart.Resources[i];
				if (!res.flowState) // ignore if no flow state as the RequestResource call won't work
					continue;

				double transferred;

				// waste atmosphere is a concentration of a resource within the habitat, so take proportionally
				if (res.resourceName == Habitat.WasteAtmoResName)
					transferred = vesselresources.GetResource(hatchPart.vessel, res.resourceName).Level * res.maxAmount;
				// all other resources are pumped to fill up the outgoing EVA suit
				else
					transferred = Math.Min(vesselresources.GetResource(hatchPart.vessel, res.resourceName).Amount * resourceMaxShare, res.maxAmount);

				// remove resource from vessel
				transferred = hatchPart.RequestResource(res.resourceName, transferred);

				// add resource to eva kerbal
				evaKerbalPart.RequestResource(res.resourceName, -transferred);
			}

			// Airlock loss
			vesselresources.Consume(hatchPart.vessel, Habitat.AtmoResName, Settings.LifeSupportAtmoLoss, ResourceBroker.Generic);

			FillEVAPropellant(hatchPart, evaKerbalPart);

            OnVesselModified(hatchPart.vessel);
			OnVesselModified(evaKerbalPart.vessel);

			// execute script
			hatchPart.vessel.KerbalismData().computer.Execute(hatchPart.vessel, ScriptType.eva_out);
		}

		void FillEVAPropellant(Part hatchPart, Part evaKerbalPart)
		{
            string evaPropName = Lib.EvaPropellantName();
            double evaPropQuantity = 0.0;
            bool hasJetPack = false;

#if KSP18 || KSP110

			hasJetPack = true;
			double evaPropCapacity = Lib.EvaPropellantCapacity();

			// take as much of the propellant as possible. just imagine: there are 1.3 units left, and 12 occupants
			// in the ship. you want to send out an engineer to fix the chemical plant that produces monoprop,
			// and have to get from one end of the station to the other with just 0.1 units in the tank...
			// nope.
			evaPropQuantity = hatchPart.RequestResource(evaPropName, evaPropCapacity);

			// Stock KSP adds 5 units of monoprop to EVAs. We want to limit that amount
			// to whatever was available in the ship, so we don't magically create EVA prop out of nowhere
			Lib.SetResource(evaKerbalPart, evaPropName, evaPropQuantity, evaPropCapacity);
#else
            // Since KSP 1.11, EVA prop is stored on "EVA jetpack" inventory part, and filled in the editor, removing
            // the need for handling where the EVA propellant comes from (there is no more magic refill in stock).
            // However, stock doesn't provide any way to refill the jetpack, so we still handle that.

            KerbalEVA kerbalEVA = evaKerbalPart.FindModuleImplementing<KerbalEVA>();
            List<ProtoPartResourceSnapshot> propContainers = new List<ProtoPartResourceSnapshot>();
            if (kerbalEVA.ModuleInventoryPartReference != null)
            {
                foreach (StoredPart storedPart in kerbalEVA.ModuleInventoryPartReference.storedParts.Values)
                {
                    // Note : the "evaJetpack" string is hardcoded in the KSP source
                    if (storedPart.partName == "evaJetpack")
                    {
                        hasJetPack = true;
                    }

                    ProtoPartResourceSnapshot prop = storedPart.snapshot.resources.Find(p => p.resourceName == evaPropName);
                    if (prop != null)
                    {
                        propContainers.Add(prop);
                    }
                }
            }

            if (propContainers.Count > 0)
            {
                bool transferred = false;
                foreach (ProtoPartResourceSnapshot propContainer in propContainers)
                {
                    if (propContainer.amount < propContainer.maxAmount)
                    {
                        double vesselPropTransferred = hatchPart.RequestResource(evaPropName, propContainer.maxAmount - propContainer.amount);
                        if (vesselPropTransferred > 0.0)
                        {
                            transferred = true;
                            propContainer.amount = Math.Min(propContainer.amount + vesselPropTransferred, propContainer.maxAmount);
                        }
                    }
                    evaPropQuantity += propContainer.amount;
                }

                if (hasJetPack && transferred && kerbalEVA.ModuleInventoryPartReference != null)
                    GameEvents.onModuleInventoryChanged.Fire(kerbalEVA.ModuleInventoryPartReference);
            }
#endif

            // show warning if there is little or no EVA propellant in the suit
            if (hasJetPack && evaPropQuantity <= 0.05 && !Lib.Landed(hatchPart.vessel))
            {
                Message.Post(Severity.danger,
                    Local.CallBackMsg_EvaNoMP.Format("<b>" + evaPropName + "</b>"), Local.CallBackMsg_EvaNoMP2);//Lib.BuildString("There isn't any <<1>> in the EVA JetPack")"Don't let the ladder go!"
            }
        }


		void FromEVA(GameEvents.FromToAction<Part, Part> data)
		{
			// contract configurator calls this event with both parts being the same when it adds a passenger
			if (data.from == data.to)
				return;

			// for each resource in the eva kerbal
			for (int i = 0; i < data.from.Resources.Count; ++i)
			{
				// get the resource
				PartResource res = data.from.Resources[i];

				// add leftovers to the vessel
				data.to.RequestResource(res.resourceName, -res.amount);
			}

#if !KSP18 && !KSP110

			string evaPropName = Lib.EvaPropellantName();
			if (evaPropName != "EVA Propellant")
			{
				KerbalEVA kerbalEVA = data.from.FindModuleImplementing<KerbalEVA>();
				List<ProtoPartResourceSnapshot> propContainers = new List<ProtoPartResourceSnapshot>();
				if (kerbalEVA.ModuleInventoryPartReference != null)
				{
					foreach (StoredPart storedPart in kerbalEVA.ModuleInventoryPartReference.storedParts.Values)
					{
						ProtoPartResourceSnapshot propContainer = storedPart.snapshot.resources.Find(p => p.resourceName == evaPropName);
						if (propContainer != null && propContainer.amount > 0.0)
						{
							propContainers.Add(propContainer);
						}
					}
				}

				if (propContainers.Count > 0)
				{
					// get vessel resources handler
					ResourceInfo evaPropOnVessel = ResourceCache.GetResource(data.to.vessel, evaPropName);
					double storageAvailable = evaPropOnVessel.Capacity - evaPropOnVessel.Amount;

					foreach (ProtoPartResourceSnapshot propContainer in propContainers)
					{
						double stored = Math.Min(propContainer.amount, storageAvailable);
						storageAvailable -= stored;
						evaPropOnVessel.Produce(stored, ResourceBroker.Generic);
						propContainer.amount = Math.Max(propContainer.amount - stored, 0.0);

						if (storageAvailable <= 0.0)
							break;
					}

					// Explaination :
					// - The ProtoCrewMember has already been removed from the EVA part and added to the vessel part
					// - It's inventory has already been saved
					// - The stock ModuleInventoryPart.RefillEVAPropellantOnBoarding() method has already been called
					// So to set the correct amount of EVA prop, we :
					// - Harmony patch ModuleInventoryPart.RefillEVAPropellantOnBoarding() so it doesn't refill anything
					// - Grab the ProtoCrewMember on the vessel part
					// - Call the SaveInventory() method again, with the modified amount on the inventory StoredPart
					data.to.protoModuleCrew[data.to.protoModuleCrew.Count - 1].SaveInventory(kerbalEVA.ModuleInventoryPartReference);
				}
			}
#endif

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

		// Hack the stock recovery dialog to show our science results
		private void VesselRecoveryProcessingComplete(ProtoVessel pv, MissionRecoveryDialog dialog, float recoveryFactor)
		{
			VesselRecovery_OnVesselRecovered.OnVesselRecoveryProcessingComplete(dialog);
		}

		void VesselTerminated(ProtoVessel pv)
		{
			// forget all kerbals data
			foreach (ProtoCrewMember c in pv.GetVesselCrew())
				DB.KillKerbal(c.name, true);

			// purge the caches
			ResourceCache.Purge(pv);
			Cache.PurgeVesselCaches(pv);

			// delete data on unloaded vessels only (this is handled trough OnPartWillDie for loaded vessels)
			if (pv.vesselRef != null && !pv.vesselRef.loaded)
				Drive.DeleteDrivesData(pv.vesselRef);
		}

		void VesselCreated(Vessel v)
		{
			if (Serenity.GetModuleGroundExpControl(v) != null)
				v.vesselName = Lib.BuildString(Lib.BodyDisplayName(v.mainBody), " Site ", Lib.Greek());
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
			Cache.PurgeVesselCaches(v); // works with loaded and unloaded vessels

			// delete data on unloaded vessels only (this is handled trough OnPartWillDie for loaded vessels)
			if (!v.loaded)
				Drive.DeleteDrivesData(v);
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
				// workaround for FindModulesImplementing nullrefs in 1.8 when called on the strange kerbalEVA_RD_Exp prefab
				// due to the (private) cachedModuleLists being null on it
				if (p.partPrefab.Modules.Count == 0)
					continue;

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
					  "<color=#00ffff><b>" + Local.CallBackMsg_PROGRESS + "</b></color>\n" + Local.CallBackMsg_PROGRESS2,//PROGRESS""Our scientists just made a breakthrough
					  Lib.BuildString("We now have access to \n<b>", label, "</b>")
					);
				}
			}
		}

		public bool visible;
	}


} // KERBALISM
