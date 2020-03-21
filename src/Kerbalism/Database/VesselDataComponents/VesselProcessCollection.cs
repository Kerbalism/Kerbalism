using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class VesselProcessCollection : IEnumerable<VesselProcess>
	{
		private const string NODENAME_PROCESSES = "PROCESSES";

		private static List<string> processesToRemove = new List<string>();

		private Dictionary<string, VesselProcess> processes = new Dictionary<string, VesselProcess>();

		public IEnumerator<VesselProcess> GetEnumerator() => processes.Values.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => processes.Values.GetEnumerator();

		public bool TryGetProcessData(string processName, out VesselProcess vesselProcess)
		{
			return processes.TryGetValue(processName, out vesselProcess);
		}

		public VesselProcess GetOrCreateProcessData(Process process)
		{
			if (!processes.TryGetValue(process.name, out VesselProcess vesselProcessData))
			{
				vesselProcessData = new VesselProcess(process);
				processes.Add(process.name, vesselProcessData);
			}

			return vesselProcessData;
		}

		public void EvaluateAfterModuleUpdate(VesselDataBase vd)
		{
			processesToRemove.Clear();

			foreach (VesselProcess vesselProcess in processes.Values)
			{
				vesselProcess.Evaluate(vd);

				if (vesselProcess.MaxCapacity == 0.0)
				{
					processesToRemove.Add(vesselProcess.ProcessName);
				}
			}

			foreach (string removedProcess in processesToRemove)
			{
				processes.Remove(removedProcess);
			}
		}


		public void Load(ConfigNode vesselDataNode)
		{
			ConfigNode processesNode = vesselDataNode.GetNode(NODENAME_PROCESSES);
			if (processesNode == null)
				return;

			foreach (ConfigNode processNode in processesNode.GetNodes())
			{
				VesselProcess vesselProcess = new VesselProcess(processNode.name, processNode);
				if (vesselProcess.process == null)
					continue;

				processes.Add(vesselProcess.ProcessName, vesselProcess);
			}
		}

		public void Save(ConfigNode vesselDataNode)
		{
			ConfigNode processNode = new ConfigNode(NODENAME_PROCESSES);

			foreach (VesselProcess process in processes.Values)
			{
				process.Save(processNode.AddNode(process.ProcessName));
			}

			if (processNode.CountNodes > 0)
			{
				vesselDataNode.AddNode(processNode);
			}
		}
	}
}
