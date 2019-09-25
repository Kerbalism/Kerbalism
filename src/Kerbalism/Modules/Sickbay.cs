using System.Collections.Generic;
using UnityEngine;
using System;

namespace KERBALISM
{

	public class Sickbay : PartModule, IModuleInfo, ISpecifics
	{
		private static int MAX_SLOTS = 5;

		// config
		[KSPField] public string resource = string.Empty; // pseudo-resource to control
		[KSPField] public double capacity = 1.0;          // amount of associated pseudo-resource
		[KSPField] public double rate = 0.0;              // healing rate
		[KSPField] public string title = string.Empty;    // name to show on ui
		[KSPField] public string desc = string.Empty;     // description to show on tooltip
		[KSPField] public int slots = 1;                  // how many kerbals can be healed at once
		[KSPField] public bool cureEverybody = false;     // cure everyone in the part, ignore slots

		[KSPField(isPersistant = true)] public string patients = "";
		private List<string> patientList = new List<string>();

		[KSPField(isPersistant = true)] public bool running;


#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Habitat", groupDisplayName = "Habitat")]
#endif
		public void Toggle()
		{
			// switch status
			running = !running;
			if (slots == 0 && !cureEverybody)
			{
				// can't run when not enabled
				running = false;
			}
			if(cureEverybody)
			{
				foreach (ProtoCrewMember c in part.protoModuleCrew)
				{
					if (running) AddPatient(c.name);
					else RemovePatient(c.name);
				}
			}

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		[KSPAction("_")] public void Action(KSPActionParam param) { Toggle(); }

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false)]
		public void Toggle1() { Toggle(1); }
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false)]
		public void Toggle2() { Toggle(2); }
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false)]
		public void Toggle3() { Toggle(3); }
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false)]
		public void Toggle4() { Toggle(4); }
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false)]
		public void Toggle5() { Toggle(5); }
#else
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false, groupName = "Habitat", groupDisplayName = "Habitat")]
		public void Toggle1() { Toggle(1); }
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false, groupName = "Habitat", groupDisplayName = "Habitat")]
		public void Toggle2() { Toggle(2); }
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false, groupName = "Habitat", groupDisplayName = "Habitat")]
		public void Toggle3() { Toggle(3); }
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false, groupName = "Habitat", groupDisplayName = "Habitat")]
		public void Toggle4() { Toggle(4); }
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "cure", active = false, groupName = "Habitat", groupDisplayName = "Habitat")]
		public void Toggle5() { Toggle(5); }
#endif

		public void Start()
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			if (slots > MAX_SLOTS)
				slots = MAX_SLOTS;

			Actions["Action"].guiName = Lib.BuildString("Start/Stop ", title);

			foreach (string s in patients.Split(','))
			{
				if (s.Length > 0) patientList.Add(s);
			}

			// configure on start
			Configure(true, slots, cureEverybody);

			UpdateActions();
		}

		public void Configure(bool enable, int slots, bool cureEverybody)
		{
			if (cureEverybody) Lib.SetProcessEnabledDisabled(part, resource, enable, capacity);
			else Lib.SetProcessEnabledDisabled(part, resource, enable, capacity * slots);
		}

		public void Update()
		{
			// remove all patients that are not in this part
			List<string> removeList = new List<string>();
			foreach (string patientName in patientList)
			{
				bool inPart = false;
				foreach (ProtoCrewMember crew in part.protoModuleCrew)
				{
					if (crew.name == patientName)
					{
						inPart = true;
						break;
					}
				}
				if (!inPart)
					removeList.Add(patientName);
			}

			if (!cureEverybody)
			{
				// make sure we don't heal more patients than we have slots
				int remainingSlots = slots;
				foreach (ProtoCrewMember crew in part.protoModuleCrew)
				{
					if (remainingSlots <= 0)
						removeList.Add(crew.name);

					if (patientList.Contains(crew.name))
						remainingSlots--;
				}
			}
			RemovePatients(removeList);

			Configure(running, slots, cureEverybody);
			UpdateActions();
		}

		private void RemovePatients(List<string> patientNames)
		{
			foreach (string patientName in patientNames)
				RemovePatient(patientName);
		}

		internal void RemovePatient(string patientName)
		{
			if (!patientList.Contains(patientName))
				return;

			patientList.Remove(patientName);
			KerbalData kd = DB.Kerbal(patientName);
			string key = resource + ",";
			int p = kd.sickbay.IndexOf(key, 0, StringComparison.Ordinal);
			if (p >= 0)
			{
				kd.sickbay = kd.sickbay.Remove(p, key.Length);
			}
			patients = string.Join(",", patientList.ToArray());
			if (running)
				running = patientList.Count > 0 && (slots > 0 || cureEverybody);
		}

		private void AddPatient(string patientName)
		{
			if (patientList.Contains(patientName))
				return;

			patientList.Add(patientName);
			KerbalData kd = DB.Kerbal(patientName);
			kd.sickbay += resource + ",";
			patients = string.Join(",", patientList.ToArray());
			running = true;
		}

		private bool IsPatient(string patientName)
		{
			return patientList.Contains(patientName);
		}

		private void UpdateActions()
		{
			Events["Toggle"].active = slots > 0 || cureEverybody;
			Events["Toggle"].guiName = Lib.StatusToggle(title, running ? "running" : "stopped");

			if (!Lib.IsFlight())
				return;

			int i;
			for (i = 1; i < MAX_SLOTS; i++)
				Events["Toggle" + i].active = false;

			if (!cureEverybody)
			{
				i = 1;
				int slotsAvailable = slots;
				foreach (string patientName in patientList)
				{
					BaseEvent e = Events["Toggle" + i++];
					e.active = true;
					e.guiName = Lib.BuildString(title, ": dismiss ", patientName);
					slotsAvailable--;
					if (slotsAvailable == 0)
						break;
				}

				if (slotsAvailable > 0)
				{
					foreach (ProtoCrewMember crew in part.protoModuleCrew)
					{
						if (IsPatient(crew.name))
							continue;

						BaseEvent e = Events["Toggle" + i++];
						e.active = true;
						e.guiName = Lib.BuildString(title, ": cure ", crew.name);
						if (i > MAX_SLOTS)
							break;
					}
				}
			}
		}

		private void Toggle(int i)
		{
			if (patientList.Count >= i)
			{
				string patientName = patientList[i - 1];
				RemovePatient(patientName);
				return;
			}
			i -= patientList.Count;
			if (part.protoModuleCrew.Count >= i)
			{
				ProtoCrewMember crewMember = part.protoModuleCrew[i - 1];
				AddPatient(crewMember.name);
				return;
			}
		}

		// part tooltip
		public override string GetInfo()
		{
			if (slots == 0 && !cureEverybody) return string.Empty;
			return Specs().Info(desc);
		}

		// specifics support
		public Specifics Specs()
		{
			if (slots == 0 && !cureEverybody) return null;
			Specifics specs = new Specifics();
			if(cureEverybody) specs.Add("Cures", "All kerbals in part");
			else if(slots > 0) specs.Add("Capacity", slots + " Kerbals");
			return specs;
		}

		// module info support
		public string GetModuleTitle()
		{
			if (slots == 0 && !cureEverybody) return String.Empty;
			return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title);
		}

		public override string GetModuleDisplayName()
		{
			if(slots == 0 && !cureEverybody) return String.Empty;
			return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title);
		}

		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
	}


} // KERBALISM

