using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	// Data structure holding the vessel wide process state, evaluated from VesselData
	public class VesselProcess
	{
		/// <summary> Vessel-wide switch for this process </summary>
		public bool enabled = true;

		/// <summary> Vessel-wide max running power [0;1], applied to the capacity of all enabled ProcessControllers</summary>
		public double enabledFactor = 1.0;   // max. desired process rate 

		/// <summary> currently dumped resources </summary>
		public List<string> dumpedOutputs { get; private set; }

		/// <summary> reference to the process definition </summary>
		public Process process { get; private set; }

		/// <summary> capacity from all enabled ProcessControllers </summary>
		private double enabledCapacity;

		private string cachedDescription;
		private double cachedDescriptionCapacity;

		// temporary values updated from ProcessControllerData
		private double newTotalCapacity;
		private double newEnabledCapacity;

		/// <summary> total vessel capacity, including disabled ProcessControllers </summary>
		public double MaxCapacity { get; private set; } = 0.0;

		/// <summary> final current capacity with enabled/enabledFactor applied</summary>
		public double AvailableCapacity => enabled ? enabledCapacity * enabledFactor : 0.0;

		public bool CanToggle => process.canToggle;
		public string ProcessName => process.name;
		public string ProcessTitle => process.title;

		public VesselProcess(Process process)
		{
			this.process = process;
			this.dumpedOutputs = new List<string>(process.dumpedOutputsDefault);
		}

		public VesselProcess(string processName, ConfigNode node)
		{
			enabled = Lib.ConfigValue(node, "enabled", true);
			enabledFactor = Lib.ConfigValue(node, "enabledFactor", 1.0);
			dumpedOutputs = Lib.Tokenize(Lib.ConfigValue(node, "dumpedOutputs", ""), ',');
			process = Profile.processes.Find(p => p.name == processName);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("enabled", enabled);
			node.AddValue("enabledFactor", enabledFactor);
			node.AddValue("dumpedOutputs", string.Join(",", dumpedOutputs.ToArray()));
		}

		public void Evaluate(VesselDataBase vd)
		{
			MaxCapacity = newTotalCapacity;
			newTotalCapacity = 0.0;

			enabledCapacity = newEnabledCapacity;
			newEnabledCapacity = 0.0;

			VesselVirtualResource processRes = (VesselVirtualResource)vd.ResHandler.GetResource(process.resourceName);
			processRes.SetCapacity(enabledCapacity);
			processRes.SetAmount(AvailableCapacity);
		}

		public void RegisterProcessControllerCapacity(bool enabled, double maxCapacity)
		{
			newTotalCapacity += maxCapacity;

			if (enabled)
				newEnabledCapacity += maxCapacity;
		}

		public string Description()
		{
			if (cachedDescriptionCapacity == AvailableCapacity && !string.IsNullOrEmpty(cachedDescription))
				return cachedDescription;

			cachedDescriptionCapacity = AvailableCapacity;
			cachedDescription = process.Specifics(cachedDescriptionCapacity).Info();
			return cachedDescription;
		}
	}
}
