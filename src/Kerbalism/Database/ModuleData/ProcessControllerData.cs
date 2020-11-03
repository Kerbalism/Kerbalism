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

		private PartResourceData capacityResource;

		public override void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule, ProtoPartSnapshot protoPart)
		{
			processName = modulePrefab.processName;
			processCapacity = modulePrefab.capacity;
			isRunning = modulePrefab.running;
			isBroken = modulePrefab.broken;

			Process = Profile.processes.Find(p => p.name == processName);

			if (partData != null)
			{
				SetupCapacityResource();
			}
		}

		public void Setup(string processName, double processCapacity)
		{
			if (capacityResource != null)
			{
				partData.virtualResources.RemoveResource(capacityResource);
				capacityResource = null;
			}

			this.processName = processName;
			this.processCapacity = processCapacity;
			Process = Profile.processes.Find(p => p.name == processName);

			SetupCapacityResource();
		}

		public override void OnLoad(ConfigNode node)
		{
			processName = Lib.ConfigValue(node, "processName", "");
			processCapacity = Lib.ConfigValue(node, "processCapacity", 0.0);
			isRunning = Lib.ConfigValue(node, "isRunning", true);
			isBroken = Lib.ConfigValue(node, "isBroken", false);

			Process = Profile.processes.Find(p => p.name == processName);

			if (Process == null)
			{
				moduleIsEnabled = false;
				return;
			}

			if (Process.UseCapacityResource)
			{
				capacityResource = partData.virtualResources.GetResource(Process.CapacityResourceName, Lib.ConfigValue(node, "capacityIndex", -1));
				if (capacityResource == null)
				{
					SetupCapacityResource();
				}
			}

		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("processName", processName);
			node.AddValue("processCapacity", processCapacity);
			node.AddValue("isRunning", isRunning);
			node.AddValue("isBroken", isBroken);
			if (Process != null && Process.UseCapacityResource)
			{
				node.AddValue("capacityIndex", capacityResource.ContainerIndex);
			}
				
		}

		public override void OnVesselDataUpdate()
		{
			if (!moduleIsEnabled || isBroken)
				return;

			double availableCapacity;
			if (capacityResource != null)
			{
				availableCapacity = processCapacity * capacityResource.Level;
				capacityResource.FlowState = isRunning;
			}
			else
			{
				availableCapacity = processCapacity;
			}

			VesselData.VesselProcesses.GetOrCreateProcessData(Process).RegisterProcessControllerCapacity(isRunning, availableCapacity);
		}

		private void SetupCapacityResource()
		{
			if (Process != null && Process.UseCapacityResource)
			{
				capacityResource = partData.virtualResources.AddResource(Process.CapacityResourceName, processCapacity, processCapacity, true);
			}
		}
	}
}
