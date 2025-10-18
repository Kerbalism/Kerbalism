using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class ProcessController: PartModule, IModuleInfo, IAnimatedModule, ISpecifics, IConfigurable
	{
		// config
		[KSPField] public string resource = string.Empty; // pseudo-resource to control
		[KSPField] public string title = string.Empty;    // name to show on ui
		[KSPField] public string desc = string.Empty;     // description to show on tooltip
		[KSPField] public double capacity = 1.0;          // amount of associated pseudo-resource
		[KSPField] public bool toggle = true;             // show the enable/disable toggle button

		// persistence/config
		[KSPField(isPersistant = true)] public bool running;
		[KSPField(isPersistant = true)] public bool broken;

		// index of the default dump valve
		[KSPField] public int valve_i = 0;

		// caching of GetInfo() for automation tooltip
		public string ModuleInfo { get; private set; }

		protected int lastMultiplier = 0;

		private PartResource pseudoResource;
		private bool PseudoResourceFlowState
		{
			get => pseudoResource?.flowState ?? false;
			set
			{
				if (pseudoResource != null)
					pseudoResource.flowState = value;
			}
		}

		private DumpSpecs.ActiveValve dumpValve;
		private int persistentValveIndex = -1;
		
		private bool isConfigurable = false;

		public override void OnLoad(ConfigNode node)
		{
			// Set the process default valve according to the default valve defined in the module.
			// This is silly and can cause multiple module configs to fight over what the default
			// value should be. This config setting should really be in the process definition,
			// but it's in the module for backward compatibility reasons.
			if (valve_i > 0 && HighLogic.LoadedScene == GameScenes.LOADING)
			{
				Process process = Profile.processes.Find(x => x.modifiers.Contains(resource));
				process.defaultDumpValve.ValveIndex = valve_i;
				process.defaultDumpValveIndex = process.defaultDumpValve.ValveIndex;
			}
			
			ModuleInfo = GetInfo();
			node.TryGetValue(nameof(persistentValveIndex), ref persistentValveIndex);
		}

		public override void OnSave(ConfigNode node)
		{
			if (dumpValve != null)
				node.AddValue(nameof(persistentValveIndex), dumpValve.ValveIndex);
		}

		public void Start()
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			// configure on start, must be executed with enabled true on parts first load.
			Configure(true, 1);

			// The dump valve instance is acquired either from the game-wide instance in the editor
			// or from the persisted VesselData instance in flight.
			// Since we have no means to persist vessel-level dump settings in a ShipConstruct
			// (aka in the editor), we persist the dump valve index in the module and use it unless
			// the VesselData ActiveValve instance exists already.
			// Note that this system can lead to weirdness in the editor. Since dump spec is global
			// and never reset, new vessels will use whatever valve was last active. Also, loading
			// a subassembly or merging a craft will result in the current craft to be updated to
			// whatever the setting is on the loaded craft. This isn't really fixable without
			// implementing a vessel-level persistence mechanism for ShipConstructs.
			Process process = Profile.processes.Find(x => x.modifiers.Contains(resource));
			if (Lib.IsEditor() || vessel == null)
			{
				dumpValve = process.defaultDumpValve;
				if (persistentValveIndex > -1)
					dumpValve.ValveIndex = persistentValveIndex;
			}
			else
			{
				VesselData vd = vessel.GetVesselData();
				if (!vd.dumpValves.TryGetValue(process, out dumpValve))
				{
					dumpValve = new DumpSpecs.ActiveValve(process.dump);
					dumpValve.ValveIndex = persistentValveIndex > -1 ? persistentValveIndex : valve_i;
					vd.dumpValves.Add(process, dumpValve);
				}
			}

			// set dump valve ui button
			Events["DumpValve"].active = dumpValve.CanSwitchValves;

			// set action group ui
			Actions["Action"].guiName = Lib.BuildString(Local.ProcessController_Start_Stop, " ", title);//"Start/Stop

			// hide toggle if specified
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// deal with non-togglable processes
			if (!toggle)
				running = true;

			// set processes enabled state
			PseudoResourceFlowState = !broken && running;
		}

		///<summary> Called by Configure.cs. Configures the controller to settings passed from the configure module</summary>
		public virtual void Configure(bool enable, int multiplier)
		{
			lastMultiplier = multiplier;
			pseudoResource = part.Resources[resource];
			if (enable)
			{
				double configuredCapacity = capacity * multiplier;
				// if never set
				// - this is the case in the editor, the first time, or in flight
				//   in the case the module was added post-launch, or EVA kerbals
				// - check capacity so we apply apply multiplier changes.
				// - don't check amount as we don't want to alter it in flight (ie, self-consuming processes)
				if (pseudoResource == null || pseudoResource.maxAmount != configuredCapacity)
				{
					// add the resource
					// - always add the specified amount, even in flight
					pseudoResource = Lib.AddResource(part, resource, configuredCapacity, configuredCapacity);
				}

				// migrate craft files that previously (in 3.24 and below) relied on setting the amount to zero instead
				// of toggling the flow state. This should be safe to do since in the editor, a process controller
				// should always have amount = maxAmount.
				if (Lib.IsEditor() && pseudoResource.amount == 0.0 && pseudoResource.maxAmount > 0.0)
					pseudoResource.amount = pseudoResource.maxAmount;
			}
			else if (pseudoResource != null)
			{
				Lib.RemoveResource(part, resource);
				pseudoResource = null;
			}
		}

		public void ModuleIsConfigured() => isConfigurable = true;

		///<summary> Call this when process controller breaks down or is repaired </summary>
		public void ReliablityEvent(bool breakdown)
		{
			broken = breakdown;
			PseudoResourceFlowState = !broken && running;
		}

		public void Update()
		{
			if (!part.IsPAWVisible())
				return;

			// update rmb ui
			Events["Toggle"].guiName = Lib.StatusToggle(lastMultiplier + " " + title, broken ? Local.ProcessController_broken : running ? Local.ProcessController_running : Local.ProcessController_stopped);//"broken""running""stopped"
			Events["DumpValve"].guiName = Lib.StatusToggle(Local.ProcessController_Dump, dumpValve.ValveTitle);//"Dump"
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Processes", groupDisplayName = "#KERBALISM_Group_Processes")]//Processes
		public void Toggle()
		{
			SetRunning(!running);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#KERBALISM_ProcessController_Dump", active = true, groupName = "Processes", groupDisplayName = "#KERBALISM_Group_Processes")]//"Dump""Processes"
		public void DumpValve()
		{
			dumpValve.NextValve();

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		public void SetRunning(bool value)
		{
			// switch status
			running = !broken && value;
			PseudoResourceFlowState = running;

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// action groups
		[KSPAction("_")] public void Action(KSPActionParam param) { Toggle(); }

		// part tooltip
		public override string GetInfo()
		{
			return isConfigurable ? string.Empty : Specs().Info(desc);
		}

		public bool IsRunning() {
			return running && !broken;
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			Process process = Profile.processes.Find(k => k.modifiers.Contains(resource));
			if (process != null)
			{
				foreach (KeyValuePair<string, double> pair in process.inputs)
				{
					if (!process.modifiers.Contains(pair.Key))
						specs.Add(Lib.GetResourceDisplayName(pair.Key), Lib.BuildString(" <color=#ffaa00>", Lib.HumanOrSIRate(pair.Value * capacity, pair.Key.GetHashCode()), "</color>"));
					else
						specs.Add(Local.ProcessController_info1, Lib.HumanReadableDuration(0.5 / pair.Value));//"Half-life"
				}
				foreach (KeyValuePair<string, double> pair in process.outputs)
				{
					specs.Add(Lib.GetResourceDisplayName(pair.Key), Lib.BuildString(" <color=#00ff00>", Lib.HumanOrSIRate(pair.Value * capacity, pair.Key.GetHashCode()), "</color>"));
				}
			}
			return specs;
		}

		// module info support
		public string GetModuleTitle() { return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title); }  // Display after config widget
		public override string GetModuleDisplayName() { return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title); }  // Display after config widget
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// animation group support
		public void EnableModule() { }
		public void DisableModule() { }
		public bool ModuleIsActive() { return broken ? false : running; }
		public bool IsSituationValid() { return true; }

		/// <summary>
		/// Migrate saves from 3.24 and below.
		/// </summary>
		/// <remarks>
		/// In 3.25, we restored back the original implementation where process controllers are disabled by toggling their
		/// pseudoresource flowState instead of altering their amount (see issues #940, #642 and #941 to a lesser extent).
		/// This change require migrating the persisted state of process controllers, we have 4 possible cases :
		/// <para/> - User-enabled PC : state is consistent, no action required
		/// <para/> - User-disabled PC : isEnabled is true, running is false, resource amount is zero. We need to set the resource
		///           amount to maxAmount, and to set flowState = false
		/// <para/> - Configure-disabled PC : isEnabled is false, resource amount is zero. State is inconsistent, but we don't
		///           care since those modules will never become enabled again, and the resource will be removed by the Configure
		///           module on Start().
		/// <para/> - Reliability-disabled PC : isEnabled is false, resource amount is zero. We need to alter the state like in the
		///           user-disabled case, but we also need to set the "broken" flag on the PC.
		/// </remarks>
		public static void MigrateSaves(Version saveVersion)
		{
			if (saveVersion > saveNeedsMigrationMaxVersion)
				return;

			// will be null on new saves, shouldn't happen but better safe than sorry
			if (HighLogic.CurrentGame.flightState == null)
				return;

			foreach (Lib.ProtoPartData ppd in Lib.GetProtoVesselsPartData(HighLogic.CurrentGame.flightState.protoVessels))
			{
				foreach (Lib.ProtoModuleData pmd in ppd.modulesData)
				{
					if (pmd.modulePrefab is ProcessController pcPrefab)
					{
						bool isEnabled = Lib.Proto.GetBool(pmd.protoModule, "isEnabled");
						bool running = Lib.Proto.GetBool(pmd.protoModule, "running");

						if (!isEnabled || (isEnabled && !running))
						{
							List<ProtoPartResourceSnapshot> resources = pmd.protoPartData.protoPart.resources;
							for (int i = resources.Count; i-- > 0;)
							{
								if (resources[i].resourceName == pcPrefab.resource)
								{
									ProtoPartResourceSnapshot res = resources[i];
									if (res.amount == 0.0 && res.maxAmount > 0.0)
									{
										res.amount = res.maxAmount;
										res.flowState = false;

										if (!isEnabled)
											Lib.Proto.Set(pmd.protoModule, nameof(broken), true);
									}
									break;
								}
							}
						}
					}
				}
			}
		}

		private static Version saveNeedsMigrationMaxVersion = new Version(3, 24);
	}
} // KERBALISM

