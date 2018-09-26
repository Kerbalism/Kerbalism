using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Sickbay : PartModule, IModuleInfo, ISpecifics
	{
		private static int MAX_SLOTS = 5;

		// config
		[KSPField] public string resource = string.Empty; // pseudo-resource to control
		[KSPField] public double capacity = 1.0;          // amount of associated pseudo-resource
		[KSPField] public string rule = string.Empty;     // which rule to affect
		[KSPField] public double rate = 0.0;              // healing rate
		[KSPField] public string title = string.Empty;    // name to show on ui
		[KSPField] public string desc = string.Empty;     // description to show on tooltip
		[KSPField] public int slots = 1;                  // how many kerbals can be healed at once

		[KSPField(isPersistant = true)] public string patients = "";
		private List<string> patientList = new List<string>();

		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "heal", active = false)]
		public void Toggle1()
		{
			Toggle(1);
		}
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "heal", active = false)]
		public void Toggle2()
		{
			Toggle(2);
		}
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "heal", active = false)]
		public void Toggle3()
		{
			Toggle(3);
		}
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "heal", active = false)]
		public void Toggle4()
		{
			Toggle(4);
		}
		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "heal", active = false)]
		public void Toggle5()
		{
			Toggle(5);
		}


		public void Start()
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			if (slots > MAX_SLOTS)
				slots = MAX_SLOTS;

			foreach (string s in patients.Split(','))
			{
				if (s.Length > 0) patientList.Add(s);
			}

			// configure on start
			Configure(true, slots);

			UpdateActions();
		}

		public void Configure(bool enable, int slots)
		{
			if (enable)
			{
				// if never set
				// - this is the case in the editor, the first time, or in flight
				//   in the case the module was added post-launch, or EVA kerbals
				if (!part.Resources.Contains(resource))
				{
					// add the resource
					// - always add the specified amount, even in flight
					Lib.AddResource(part, resource, capacity, capacity);
				}
				// has slots changed
				else if (this.slots != slots)
				{
					// slots has increased
					if (this.slots < slots)
					{
						Lib.AddResource(part, resource, capacity * (slots - this.slots), capacity * (slots - this.slots));
					}
					// slots has decreased
					else
					{
						Lib.RemoveResource(part, resource, 0.0, capacity * (this.slots - slots));
					}
				}
				this.slots = slots;
			}
			else
			{
				Lib.RemoveResource(part, resource, 0.0, capacity * this.slots);
				this.slots = 1;
			}
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

			// make sure we don't heal more patients than we have slots
			int remainingSlots = slots;
			foreach (ProtoCrewMember crew in part.protoModuleCrew)
			{
				if (remainingSlots <= 0)
					removeList.Add(crew.name);

				if (patientList.Contains(crew.name))
					remainingSlots--;
			}
			RemovePatients(removeList);

			Lib.SetResourceFlow(part, resource, patientList.Count > 0);
			UpdateActions();
		}

		private void RemovePatients(List<string> patientNames)
		{
			foreach (string patientName in patientNames)
				RemovePatient(patientName);
		}

		private void RemovePatient(string patientName)
		{
			if (!patientList.Contains(patientName))
				return;
			
			patientList.Remove(patientName);
			KerbalData kd = DB.Kerbal(patientName);
			kd.Rule(rule).offset = 0;
			patients = string.Join(",", patientList.ToArray());
		}

		private void AddPatient(string patientName)
		{
			if (patientList.Contains(patientName))
				return;

			patientList.Add(patientName);
			KerbalData kd = DB.Kerbal(patientName);
			kd.Rule(rule).offset = -rate;
			patients = string.Join(",", patientList.ToArray());
		}

		private bool IsPatient(string patientName)
		{
			return patientList.Contains(patientName);
		}

		private void UpdateActions()
		{
			if (!Lib.IsFlight())
			{
				return;
			}

			int i;
			for (i = 1; i < MAX_SLOTS; i++)
				Events["Toggle" + i].active = false;

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
			return Specs().Info(desc);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add("Capacity", slots + " Kerbals");
			return specs;
		}

		// module info support
		public string GetModuleTitle() { return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title); }  // Display after config widget
		public override string GetModuleDisplayName() { return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title); }  // Display after config widget
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
	}


} // KERBALISM

