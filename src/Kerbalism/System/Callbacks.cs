using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using KSP.UI.Screens;
using UnityEngine;
using KSP.Localization;
using KSP.UI;

namespace KERBALISM
{
	#region HARMONY EVENTS

	// OnPartDie is not called for the root part
	// OnPartWillDie works but isn't available in 1.5/1.6
	// Until we drop 1.5/1.6 support, we use this patch instead
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("Die")]
	class Part_Die
	{
		static void Prefix(Part __instance)
		{
			// replicate OnPartWillDie
			if (__instance.State == PartStates.DEAD)
				return;

			Kerbalism.Callbacks.OnPartWillDie(__instance);

			return; // continue to Part.Die()
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

	// Create a OnAttemptBoard event that allow to prevent boardinga vessel from EVA
	// Called before any check has been done so the boarding can still fail due to stock restrictions
	// Called before anything (experiments/inventory...) has been transfered to the boarded vessel
	[HarmonyPatch(typeof(KerbalEVA))]
	[HarmonyPatch("BoardPart")]
	class KerbalEVA_BoardPart
	{
		static bool Prefix(KerbalEVA __instance, Part p)
		{
			// continue to BoardPart() if AttemptBoard return true 
			return Kerbalism.Callbacks.AttemptBoard(__instance, p);
		}
	}

	#endregion

	public sealed class Callbacks
	{
		public static EventData<Part, Configure> onConfigure = new EventData<Part, Configure>("onConfigure");

		#region CALLBACKS REGISRATION

		public Callbacks()
		{
			GameEvents.onPartCouple.Add(OnPartCouple);

			GameEvents.onCrewOnEva.Add(this.ToEVA);
			GameEvents.onCrewBoardVessel.Add(this.FromEVA);

			// in editor crew assignement trough the assignement dialog
			BaseCrewAssignmentDialog.onCrewDialogChange.Add(EditorCrewChanged);

			// habitat / EVA crew transfer handling
			GameEvents.onCrewTransferred.Add(CrewTransferred);
			GameEvents.onCrewTransferSelected.Add(CrewTransferSelected);
			GameEvents.onAttemptEva.Add(AttemptEVA);

			GameEvents.onVesselRecovered.Add(this.VesselRecovered);
			GameEvents.onVesselTerminated.Add(this.VesselTerminated);
			GameEvents.onVesselWillDestroy.Add(this.VesselDestroyed);
			GameEvents.onNewVesselCreated.Add(this.VesselCreated);
			GameEvents.onPartCouple.Add(this.VesselDock);

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

			GameEvents.onPartDestroyed.Add(OnEditorPartDestroyed);

			GameEvents.onVesselRecoveryProcessingComplete.Add(OnVesselRecoveryProcessingComplete);

			// add editor events
			GameEvents.onEditorShipModified.Add(OnEditorShipModified);
		}

		#endregion

		#region UI

		public bool visible;

		void AddEditorCategory()
		{
			if (PartLoader.LoadedPartsList.Find(k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0) != null)
			{
				RUI.Icons.Selectable.Icon icon = new RUI.Icons.Selectable.Icon("Kerbalism", Textures.category_normal, Textures.category_selected);
				PartCategorizer.Category category = PartCategorizer.Instance.filters.Find(k => string.Equals(k.button.categoryName, "filter by function", StringComparison.OrdinalIgnoreCase));
				PartCategorizer.AddCustomSubcategoryFilter(category, "Kerbalism", "Kerbalism", icon, k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0);
			}
		}

		#endregion

		#region EDITOR EVENTS

		private void OnEditorShipModified(ShipConstruct data)
		{
			ModuleKsmExperiment.CheckEditorExperimentMultipleRun();
			Planner.Planner.EditorShipModifiedEvent(data);
		}

		private void OnEditorPartDestroyed(Part part)
		{
			if (!Lib.IsEditor)
				return;

			Lib.LogDebug($"Removing destroyed part: {part.persistentId} ({part.partInfo.title})");
			VesselDataShip.LoadedParts.Remove(part);
		}

		private static bool crewAssignementRefreshWasJustFiredFromCrewChanged = false;

		private void EditorCrewChanged(VesselCrewManifest crewManifest)
		{
			if (crewAssignementRefreshWasJustFiredFromCrewChanged)
			{
				crewAssignementRefreshWasJustFiredFromCrewChanged = false;
				return;
			}

			// TODO : this fail (hard) when called from the launchpad interface crew transfer dialog
			// The vessel doesn't exists there, and doesn't look there is any built-in reference to the shipconstruct / saved data
			// So this is gonna be tricky to handle.
			// Side note : check how mods that launch vessels from other places than the editor are handling that (KCT ? EPL ?)

			if (KSP.UI.Screens.VesselSpawnDialog.Instance != null)
			{
				return;
			}

			bool needRefresh = false;
			foreach (PartCrewManifest partManifest in crewManifest.PartManifests)
			{
				if (partManifest.NoSeats())
					continue;

				ModuleKsmHabitat habitat = EditorLogic.fetch.ship.parts.Find(p => p.craftID == partManifest.PartID)?.FindModuleImplementing<ModuleKsmHabitat>();

				if (habitat == null || habitat.moduleData == null)
					continue;

				HabitatData habData = habitat.moduleData;

				if (!habData.isEnabled)
				{
					habData.crewCount = 0;

					foreach (ProtoCrewMember crew in partManifest.GetPartCrew())
						if (crew != null)
							Message.Post($"Can't put {Lib.Bold(crew.displayName)} in {Lib.Bold(habitat.part.partInfo.title)}", "Habitat is disabled !");

					for (int i = 0; i < partManifest.partCrew.Length; i++)
					{
						if (!string.IsNullOrEmpty(partManifest.partCrew[i]))
						{
							partManifest.RemoveCrewFromSeat(i);

							needRefresh |= true;
						}
					}
				}
				else
				{
					int crewCount = 0;
					for (int i = 0; i < partManifest.partCrew.Length; i++)
					{
						if (!string.IsNullOrEmpty(partManifest.partCrew[i]))
						{
							crewCount++;
						}
					}
					habData.crewCount = crewCount;
				}
			}

			if (needRefresh)
			{
				// RefreshCrewLists() will call the current method trough the EditorCrewChanged event, so avoid an useless recursion.
				crewAssignementRefreshWasJustFiredFromCrewChanged = true;
				CrewAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, false, true);
			}
		}

		#endregion

		#region EVA EVENTS

		private static bool ignoreNextBoardAttemptDriveCheck = false;

		public bool AttemptBoard(KerbalEVA instance, Part targetPart)
		{
			bool canBoard = false;
			if (targetPart != null && targetPart.TryGetModuleDataOfType(out HabitatData habitatData))
			{
				canBoard =
					habitatData.pressureState == HabitatData.PressureState.Depressurized
					|| habitatData.pressureState == HabitatData.PressureState.AlwaysDepressurized
					|| habitatData.pressureState == HabitatData.PressureState.Breatheable;
			}

			if (!canBoard)
			{
				Message.Post($"Can't board {Lib.Bold(targetPart.partInfo.title)}", "Depressurize it first !");
				ignoreNextBoardAttemptDriveCheck = false;
				return canBoard;
			}

			if (!ignoreNextBoardAttemptDriveCheck)
			{
				double filesSize = 0.0;
				double fileCapacity = 0.0;
				int samplesSize = 0;
				int samplesCapacity = 0;

				foreach (DriveData drive in DriveData.GetDrives(instance.vessel))
				{
					filesSize += drive.FilesSize();
					samplesSize += drive.SamplesSize();
				}

				foreach (DriveData drive in DriveData.GetDrives(targetPart.vessel))
				{
					fileCapacity += drive.FileCapacityAvailable();
					samplesCapacity += (int)drive.SampleCapacityAvailable();
				}

				if (filesSize > fileCapacity || samplesSize > samplesCapacity)
				{
					DialogGUIButton cancel = new DialogGUIButton("#autoLOC_116009", delegate { }); // autoLOC_116009 : cancel
					Callback proceedCallback = delegate { ignoreNextBoardAttemptDriveCheck = true; instance.BoardPart(targetPart); }; // ignore this check on the method next call
					DialogGUIButton proceed = new DialogGUIButton("#autoLOC_116008", proceedCallback); // autoLOC_116008 : Board Anyway\n(Dump Experiments)

					string message = Lib.BuildString(
						string.Format("The vessel {0} doesn't have enough space to store all the experiments carried by {1}", targetPart.vessel.vesselName, instance.vessel.vesselName),
						"\n\n",
						"Files on EVA", " : ", Lib.HumanReadableDataSize(filesSize), " - ", "Storage capacity", " : ", Lib.HumanReadableDataSize(fileCapacity), "\n",
						"Samples on EVA", " : ", Lib.HumanReadableSampleSize(samplesSize), " - ", "Storage capacity", " : ", Lib.HumanReadableSampleSize(samplesCapacity), "\n\n",
						"If you proceed, some experiment results will be lost");

					PopupDialog.SpawnPopupDialog(
						new Vector2(0.5f, 0.5f),
						new Vector2(0.5f, 0.5f),
						new MultiOptionDialog("StoreExperimentsIssue", message, Localizer.Format("#autoLOC_116007"), HighLogic.UISkin, proceed, cancel), // autoLOC_116007 : Cannot store Experiments
						false,
						HighLogic.UISkin);

					return false;
				}
			}

			ignoreNextBoardAttemptDriveCheck = false;
			return true;
		}

		private void AttemptEVA(ProtoCrewMember crew, Part sourcePart, Transform hatchTransform)
		{
			FlightEVA.fetch.overrideEVA = true;
			if (sourcePart != null && sourcePart.TryGetModuleDataOfType(out HabitatData habitatData))
			{
					FlightEVA.fetch.overrideEVA =
						!(habitatData.pressureState == HabitatData.PressureState.Depressurized
						|| habitatData.pressureState == HabitatData.PressureState.AlwaysDepressurized
						|| habitatData.pressureState == HabitatData.PressureState.Breatheable);
			}

			if (FlightEVA.fetch.overrideEVA)
			{
				Message.Post($"Can't go on EVA from {Lib.Bold(sourcePart.partInfo.title)}", "Depressurize it first !");
			}
		}

		private void ToEVA(GameEvents.FromToAction<Part, Part> data)
		{
			// setup supply resources capacity in the eva kerbal
			// This has to be before the vesseldata creation, so the reshandler is 
			// initialized with the correct capacities
			Profile.SetupEva(data.to);

			// get vessel data
			if (!data.to.vessel.TryGetVesselDataNoError(out VesselData evaVD))
			{
				Lib.LogDebug($"Creating VesselData for EVA Kerbal : {data.to.vessel.vesselName}");
				evaVD = new VesselData(data.to.vessel);
				DB.AddNewVesselData(evaVD);
			}

			VesselData vesselVD = data.from.vessel.GetVesselData();

			// total crew of the origin vessel plus the EVAing kerbal
			double totalCrew = Lib.CrewCount(data.from.vessel) + 1.0;

			string evaPropellant = Lib.EvaPropellantName();

			// for each resource in the kerbal
			foreach (PartResource partRes in data.to.Resources)
			{
				// get the resource
				VesselResource evaRes = evaVD.ResHandler.GetResource(partRes.resourceName);
				VesselResource vesselRes = vesselVD.ResHandler.GetResource(partRes.resourceName);

				// clamp request by how much is available
				double amountTransferred = Math.Min(evaRes.Capacity, Math.Max(vesselRes.Amount + vesselRes.Deferred, 0.0));

				// special handling for EVA propellant
				if (evaRes.Name == evaPropellant)
				{
					if (amountTransferred <= 0.05 && !Lib.Landed(data.from.vessel))
					{
						Message.Post(Severity.danger,
							Local.CallBackMsg_EvaNoMP.Format("<b>" + evaPropellant + "</b>"), Local.CallBackMsg_EvaNoMP2);
						// "There isn't any <<1>> in the EVA suit", "Don't let the ladder go!"
					}
				}
				// for all ressources but propellant, only take this kerbal "share"
				else
				{
					amountTransferred /= totalCrew;
				}

				// remove resource from vessel
				vesselRes.Consume(amountTransferred);

				// add resource to eva kerbal
				evaRes.Produce(amountTransferred);
			}

			// turn off headlamp light, to avoid stock bug that show them for a split second when going on eva
			KerbalEVA kerbal = data.to.FindModuleImplementing<KerbalEVA>();
			EVA.HeadLamps(kerbal, false);

			// execute script
			evaVD.computer.Execute(data.from.vessel, ScriptType.eva_out);

			OnVesselModified(data.from.vessel);
		}


		private void FromEVA(GameEvents.FromToAction<Part, Part> data)
		{
			// contract configurator calls this event with both parts being the same when it adds a passenger
			if (data.from == data.to)
				return;

			VesselData vesselVD = data.to.vessel.GetVesselData();

			// for each resource in the eva kerbal, add leftovers to the vessel
			foreach (PartResource partRes in data.from.Resources)
			{
				vesselVD.ResHandler.GetResource(partRes.resourceName).Produce(partRes.amount);
			}

			// merge drives data
			DriveData.Transfer(data.from.vessel, data.to.vessel, true);

			// forget EVA vessel data
			Cache.PurgeVesselCaches(data.from.vessel);
			//Drive.Purge(data.from.vessel);

			// update boarded vessel
			this.OnVesselModified(data.to.vessel);

			// execute script
			vesselVD.computer.Execute(data.to.vessel, ScriptType.eva_in);
		}

		#endregion

		#region CREW TRANSFER

		public static bool disableCrewTransferFailMessage = false;
		private void CrewTransferSelected(CrewTransfer.CrewTransferData data)
		{
			bool sourceIsPressurized = false;
			if (data.sourcePart != null && data.sourcePart.TryGetModuleDataOfType(out HabitatData sourceHabitatData))
			{
				sourceIsPressurized = sourceHabitatData.pressureState == HabitatData.PressureState.Pressurized;
			}

			bool targetIsEnabled = false;
			bool targetIsPressurized = false;
			if (data.destPart != null && data.destPart.TryGetModuleDataOfType(out HabitatData destHabitatData))
			{
				// if hab isn't enabled, try to enable it. We do that because otherwise you can 
				// brick your vessel by not being able to transfer back people in control parts.
				if (destHabitatData.isEnabled || ModuleKsmHabitat.TryToggleHabitat(destHabitatData.loadedModule, destHabitatData, data.destPart.vessel.loaded))
					targetIsEnabled = true;

				targetIsPressurized = destHabitatData.pressureState == HabitatData.PressureState.Pressurized; 
			}

			if (!targetIsEnabled)
			{
				if (!disableCrewTransferFailMessage)
					Message.Post($"Can't transfer {Lib.Bold(data.crewMember.displayName)} to {Lib.Bold(data.destPart?.partInfo.title)}", "The habitat is disabled !");

				data.canTransfer = false;
			}
			else if ((sourceIsPressurized && !targetIsPressurized) || (!sourceIsPressurized && targetIsPressurized))
			{
				if (!disableCrewTransferFailMessage)
					Message.Post($"Can't transfer {Lib.Bold(data.crewMember.displayName)} from {Lib.Bold(data.sourcePart?.partInfo.title)}\nto {Lib.Bold(data.destPart?.partInfo.title)}", "One is pressurized and not the other !");

				data.canTransfer = false;
			}
			else
			{
				data.canTransfer = true;
			}

			disableCrewTransferFailMessage = false;
		}

		private void CrewTransferred(GameEvents.HostedFromToAction<ProtoCrewMember, Part> data)
		{
			if (data.from == data.to)
				return;

			double wasteTransferred = 0.0;

			if (data.from != null && data.from.TryGetModuleDataOfType(out HabitatData fromHabitatData))
			{
				
				int newCrewCount = Lib.CrewCount(data.from);
				if (fromHabitatData.crewCount - newCrewCount != 1)
				{
					Lib.LogStack($"From part {data.from.partInfo.title} : crew count old={fromHabitatData.crewCount}, new={newCrewCount}, HabitatData is desynchronized !", Lib.LogLevel.Error);
				}

				switch (fromHabitatData.pressureState)
				{
					case HabitatData.PressureState.AlwaysDepressurized:
					case HabitatData.PressureState.Depressurized:
					case HabitatData.PressureState.Pressurizing:
					case HabitatData.PressureState.DepressurizingBelowThreshold:

						PartResourceWrapper wasteRes = fromHabitatData.loadedModule.WasteRes;
						wasteTransferred = fromHabitatData.crewCount > 0 ? wasteRes.Amount / fromHabitatData.crewCount : 0.0;
						wasteRes.Amount = newCrewCount > 0 ? wasteRes.Amount - wasteTransferred : 0.0;
						wasteRes.Capacity = newCrewCount * Settings.PressureSuitVolume;
						break;
				}

				fromHabitatData.crewCount = newCrewCount;
			}


			if (data.to != null && data.to.TryGetModuleDataOfType(out HabitatData toHabitatData))
			{
				toHabitatData.crewCount = Lib.CrewCount(data.to);

				PartResource wasteRes;
				if (!data.to.Resources.Contains(Settings.HabitatWasteResource))
					wasteRes = Lib.AddResource(data.to, Settings.HabitatWasteResource, 0.0, HabitatLib.M3ToL(toHabitatData.loadedModule.volume));
				else
					wasteRes = data.to.Resources[Settings.HabitatWasteResource];

				// note : this is called when going from a vessel to EVA, but the EVA modules OnStart() isn't yet called.
				// So the waste resource capacity won't be set yet, and neither the wrapper. And we can't create it here because
				// that will be overriden anyway in the module 
				// So for now you magically get ride of some CO2 by going on EVA
				if (wasteRes != null)
				{
					switch (toHabitatData.pressureState)
					{
						case HabitatData.PressureState.AlwaysDepressurized:
						case HabitatData.PressureState.Depressurized:
						case HabitatData.PressureState.Pressurizing:
						case HabitatData.PressureState.DepressurizingBelowThreshold:

							wasteRes.maxAmount = toHabitatData.crewCount * Settings.PressureSuitVolume;
							break;
					}

					if (wasteTransferred > 0.0)
					{
						wasteRes.amount = Math.Min(wasteRes.amount + wasteTransferred, wasteRes.maxAmount);
					}
				}


			}
		}

		#endregion

		#region PART LIFECYCLE

		// Called by the OnPartCouple events, called for docking and KIS added parts
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
			if (Lib.IsEditor)
				return;

			// remove part from vesseldata
			VesselData.OnPartWillDie(p);

			// update vessel
			OnVesselModified(p.vessel);
		}

		#endregion

		#region VESSEL LIFECYCLE

		private void VesselCreated(Vessel v)
		{
			if (Serenity.GetModuleGroundExpControl(v) != null)
				v.vesselName = Lib.BuildString(v.mainBody.name, " Site ", Lib.Greek());
		}

		private void OnVesselModified(Vessel vessel)
		{
			// TODO : move emitter to KsmPartModule and get ride of this.
			foreach (var emitter in vessel.FindPartModulesImplementing<Emitter>())
				emitter.Recalculate();

			// Note : the anonymous cache is currently used for a bunch a bunch of things that could probably be either
			// refactored as KsmPartModules or stored elswhere in a more efficient way :
			// - warp drives : should definitiely be moved to CommHandler
			// - caching the modules that are getting background processing : this is made necessary because the current way
			//   of finding protomodules is terrible performance wise. Now that we basically have out own vessel/part/module
			//   data structure, maybe we should just store the prefabs and protomodule references there and get ride of the whole thing.
			//   I doubt the impact on scene change processing time would be noticeable, and that would reduce to zero the perf impact
			//   of the background processing loop. Plus that would allow to implement background processing for non-Kerbalism modules
			//   in a streamlined, unified way, and that simplify the background processing API implementation.
			// - caching the vessel computer devices
			// - caching Lib.HasPart() calls (experiment requirements)
			// - caching the scansat scanners (?)
			// - caching Lib.FindModules() (find protomodules), used in many places : Emitter, Greenhouse, Passive shield, Reliability, etc
			Cache.PurgeVesselCaches(vessel);
		}

		// Hack the stock recovery dialog to show our science results
		private void OnVesselRecoveryProcessingComplete(ProtoVessel pv, MissionRecoveryDialog dialog, float recoveryFactor)
		{
			VesselRecovery_OnVesselRecovered.OnVesselRecoveryProcessingComplete(dialog);
		}

		private void OnVesselStandardModification(Vessel vessel)
		{
			// avoid this being called on vessel launch, when vessel is not yet properly initialized
			if (!vessel.loaded && vessel.protoVessel == null) return;

			OnVesselModified(vessel);
		}

		// note: this is called multiple times when a vessel is recovered
		private void VesselRecovered(ProtoVessel pv, bool b)
		{
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
			Cache.PurgeVesselCaches(pv);
		}

		private void VesselTerminated(ProtoVessel pv)
		{
			// forget all kerbals data
			foreach (ProtoCrewMember c in pv.GetVesselCrew())
				DB.KillKerbal(c.name, true);

			// purge the caches
			Cache.PurgeVesselCaches(pv);

			// trigger die event on unloaded vessels only (this is handled trough OnPartWillDie for loaded vessels)
			if (pv.vesselRef != null && !pv.vesselRef.loaded && DB.TryGetVesselDataNoError(pv.vesselRef, out VesselData vd))
				vd.OnVesselWillDie();
		}

		private void VesselDestroyed(Vessel v)
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
			foreach (string key in DB.Kerbals.Keys)
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
			Cache.PurgeVesselCaches(v); // works with loaded and unloaded vessels

			// trigger die event on unloaded vessels only (this is handled trough OnPartWillDie for loaded vessels)
			if (!v.loaded && DB.TryGetVesselDataNoError(v, out VesselData vd))
				vd.OnVesselWillDie();
		}

		void VesselDock(GameEvents.FromToAction<Part, Part> e)
		{
			Cache.PurgeVesselCaches(e.from.vessel);
			// Update docked to vessel
			OnVesselModified(e.to.vessel);
		}

		#endregion

		#region OTHER

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

		#endregion

		
	}


} // KERBALISM
