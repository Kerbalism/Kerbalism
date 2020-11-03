using Flee.PublicTypes;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ProcessControllerData : ModuleData<ModuleKsmProcessController, ProcessControllerData>
	{
		public string processName;      // internal name of the process (i.e. "scrubber" or "sabatier")
		public double processCapacity;  // this part modules capacity to run this process
		public bool isRunning;  // true/false, if process controller is turned on or not
		public bool isBroken;   // true if process controller is broken
		public Process Process { get; private set; } // the process associated with the process name, for convenience
		public VesselProcess VesselProcess { get; private set; }

		private double availableCapacity;
		private double consumedCapacity;

		public override void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule, ProtoPartSnapshot protoPart)
		{
			processName = modulePrefab.processName;
			processCapacity = modulePrefab.capacity;
			isRunning = modulePrefab.running;
			isBroken = modulePrefab.broken;

			Process = Profile.processes.Find(p => p.name == processName);
		}

		public void Setup(string processName, double processCapacity)
		{
			this.processName = processName;
			this.processCapacity = processCapacity;
			Process = Profile.processes.Find(p => p.name == processName);
		}

		public override void OnLoad(ConfigNode node)
		{
			processName = Lib.ConfigValue(node, "processName", "");
			processCapacity = Lib.ConfigValue(node, "processCapacity", 0.0);
			isRunning = Lib.ConfigValue(node, "isRunning", true);
			isBroken = Lib.ConfigValue(node, "isBroken", false);
			consumedCapacity = Lib.ConfigValue(node, "consumedCapacity", 0.0);

			Process = Profile.processes.Find(p => p.name == processName);

			if (Process == null)
				moduleIsEnabled = false;
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("processName", processName);
			node.AddValue("processCapacity", processCapacity);
			node.AddValue("isRunning", isRunning);
			node.AddValue("isBroken", isBroken);
			node.AddValue("consumedCapacity", consumedCapacity);
		}

		public override void OnStart()
		{
			if (!moduleIsEnabled || isBroken)
				return;

			VesselProcess = VesselData.VesselProcesses.GetOrCreateProcessData(Process);
		}

		public override void OnFixedUpdate(double elapsedSec)
		{
			if (!moduleIsEnabled || isBroken)
				return;

			consumedCapacity += Process.selfConsumptionRate * elapsedSec;
			availableCapacity = Math.Max(processCapacity - consumedCapacity, 0.0);
		}

		public override void OnVesselDataUpdate()
		{
			if (!moduleIsEnabled || isBroken)
				return;

			VesselProcess.RegisterProcessControllerCapacity(isRunning, availableCapacity);
		}
	}
}
