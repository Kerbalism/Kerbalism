using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class VesselProcesses
	{
		public List<VesselProcessData> Processes => processes;
		List<VesselProcessData> processes = new List<VesselProcessData>();

		public VesselProcesses() { }

		public VesselProcesses(ConfigNode node)
		{
			if (node == null)
				return;

			foreach (var process_node in node.GetNodes())
			{
				var pd = new VesselProcessData(process_node);
				if (pd.process != null)
					processes.Add(pd);
			}
		}

		public void Save(ConfigNode node)
		{
			foreach (var pd in processes)
			{
				pd.Save(node.AddNode(DB.To_safe_key(pd.processName)));
			}
		}

		/// <summary>
		/// condense all ProcessData (processes running on individual parts) into one vessel-global process info, aggregating the total capacity
		/// </summary>
		internal void Evaluate(List<PartProcessData> partProcessDatas, VesselResHandler resHandler)
		{
			// reset all newTotalCapacities so we know which processes were removed or changed capacities
			foreach (VesselProcessData vesselProcess in processes)
				vesselProcess.newTotalCapacity = -1; // will delete all processes with negative capacity later

			// sum up the toal capacities from all process part data objects to their corresponding vessel process entries
			foreach (PartProcessData partProcessData in partProcessDatas)
			{
				if (partProcessData.process == null)
				{
					Lib.LogDebug("Found part process data without a process named " + partProcessData.processName);
					continue;
				}

				VesselProcessData vesselProcess = processes.Find(d => d.processName == partProcessData.processName);
				if (vesselProcess == null)
				{
					// found a part process data node without a corresponding vessel process.
					// add the vessel process
					Lib.LogDebug($"adding new vessel process for {partProcessData.processName}");
					vesselProcess = new VesselProcessData(partProcessData.processName, partProcessData.process);
					processes.Add(vesselProcess);
				}

				// if we have a process part data entry (irrelevant if disabled or broken),
				// we want to retain this vessel process. remove the deletion marker
				if (vesselProcess.newTotalCapacity < 0)
					vesselProcess.newTotalCapacity = 0;

				// if the process on the part is turned on and not broken, add the capacity
				if (partProcessData.isRunning && !partProcessData.isBroken)
					vesselProcess.newTotalCapacity += partProcessData.processCapacity;
			}

			// remove all processes that no longer have a corresponding part data
			for (int i = processes.Count; i > 0; i--)
			{
				if (processes[i - 1].newTotalCapacity < 0)
				{
					Lib.LogDebug($"process {processes[i - 1].processName} has nil capacity, removing");
					processes.RemoveAt(i - 1);
				}
			}

			// handle process capacity changes
			foreach(VesselProcessData vesselProcess in processes)
			{
				if (vesselProcess.newTotalCapacity != vesselProcess.totalCapacity)
					vesselProcess.UpdateCapacity(vesselProcess.newTotalCapacity, resHandler);
			}
		}

		internal void Execute(Vessel v, VesselData vd, VesselResHandler resources, double elapsed_s)
		{
			// execute all processes on vessel
			foreach (var p in processes)
			{
				if (p.enabled)
					p.process.Execute(v, vd, resources, elapsed_s, p.maxSetting, p.dumpedOutputs);
			}
		}
	}

	// Data structure holding the vessel wide process state, evaluated from VesselData
	public class VesselProcessData
	{
		public bool enabled { get; private set; } = true;
		public double maxSetting { get; private set; } = 1.0;	// max. desired process rate [0..1]
		public double totalCapacity { get; private set; } = 0.0;
		public string processName { get; private set; }
		public List<string> dumpedOutputs { get; private set; }
		public Process process { get; private set; }
		public bool visible { get; private set; }

		private string cachedDescription;
		internal double newTotalCapacity;

		public VesselProcessData(string name, Process process)
		{
			this.processName = name;
			this.process = process;
			this.dumpedOutputs = new List<string>(process.defaultDumped);
			this.visible = process.canToggle;
		}

		public VesselProcessData(ConfigNode node)
		{
			enabled = Lib.ConfigValue(node, "enabled", true);
			maxSetting = Lib.ConfigValue(node, "maxSetting", 1.0);
			totalCapacity = Lib.ConfigValue(node, "totalCapacity", 0.0);
			processName = Lib.ConfigValue(node, "processName", "");
			dumpedOutputs = Lib.Tokenize(Lib.ConfigValue(node, "dumpedOutputs", ""), ',');
			process = Profile.processes.Find(p => p.name == processName);
			if (process != null)
				visible = process.canToggle;
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("enabled", enabled);
			node.AddValue("maxSetting", maxSetting);
			node.AddValue("totalCapacity", totalCapacity);
			node.AddValue("processName", processName);
			node.AddValue("dumpedOutputs", string.Join(",", dumpedOutputs.ToArray()));
		}

		public string Description()
		{
			if (!string.IsNullOrEmpty(cachedDescription))
				return cachedDescription;

			cachedDescription = process.Specifics(maxSetting * totalCapacity).Info();
			return cachedDescription;
		}

		internal void UpdateCapacity(double capacity, VesselResHandler resHandler)
		{
			totalCapacity = capacity;

			foreach(var res in process.scalars)
			{
				if(resHandler.GetResource(res) is VirtualResource vr)
				{
					vr.Capacity = capacity;
					vr.Amount = capacity;
				}
			}
		}

		public void SetMaxSetting(double setting, VesselResHandler resHandler)
		{
			maxSetting = setting;
			cachedDescription = null;
		}

		public void SetEnabled(bool setting, VesselResHandler resHandler)
		{
			enabled = setting;
			cachedDescription = null;
		}
	}

	public class PartProcessData
	{
		public string processName;		// internal name of the process (i.e. "scrubber" or "sabatier")
		public double processCapacity;	// this part modules capacity to run this process
		public string processId;		// id of the process controller part module
		public ModuleKsmProcessController module = null;
		public bool isRunning;  // true/false, if process controller is turned on or not
		public bool isBroken;   // true if process controller is broken
		public Process process { get; private set; } // the process associated with the process name, for convenience

		public PartProcessData(string name, double capacity, string id, bool running, bool broken)
		{
			processName = name;
			processCapacity = capacity;
			processId = id;
			isRunning = running;
			isBroken = broken;

			process = Profile.processes.Find(p => p.name == processName);
		}

		public PartProcessData(ConfigNode node)
		{
			processName = Lib.ConfigValue(node, "processName", "");
			processCapacity = Lib.ConfigValue(node, "processCapacity", 0.0);
			processId = Lib.ConfigValue(node, "processId", "");
			isRunning = Lib.ConfigValue(node, "isRunning", true);
			isBroken = Lib.ConfigValue(node, "isBroken", false);

			process = Profile.processes.Find(p => p.name == processName);
		}

		internal void Save(ConfigNode node)
		{
			node.AddValue("processName", processName);
			node.AddValue("processCapacity", processCapacity);
			node.AddValue("processId", processId);
			node.AddValue("isRunning", isRunning);
			node.AddValue("isBroken", isBroken);
		}

		public static void SetFlightReferenceFromPart(Part part, PartProcessData data) => part.vessel.KerbalismData().Parts.Get(part.flightID).Add(data);

		public static PartProcessData GetFlightReferenceFromPart(Part part, string id) => part.vessel.KerbalismData().Parts.Get(part.flightID).Processes?.Find(d => d.processId == id);

		public static PartProcessData GetFlightReferenceFromProtoPart(Vessel vessel, ProtoPartSnapshot part, string id) => vessel.KerbalismData().Parts.Get(part.flightID).Processes?.Find(d => d.processId == id);
	}
}
