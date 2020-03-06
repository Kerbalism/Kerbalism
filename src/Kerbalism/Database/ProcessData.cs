using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	/// <summary>
	/// vessel wide process configuration
	/// </summary>
	public class ProcessData
	{
		public string name;        // process name as defined in profile
		public double desiredRate;  // value from 0..1 determines the desired running capacity in %
		public List<string> dump;  // vessel wide dump settings for this process

		private Process process;
		private string cachedDescription;

		public ProcessData(string name, double capacity, double desiredRate)
		{
			this.name = name;
			this.desiredRate = Lib.Clamp(desiredRate, 0.0, 1.0);
			process = Profile.processes.Find(p => p.name == name);
			dump = new List<string>(process.defaultDumped);
		}

		public ProcessData(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", "");
			desiredRate = Lib.ConfigValue(node, "desiredRate", 1.0);
			desiredRate = Lib.Clamp(desiredRate, 0.0, 1.0);
			dump = Lib.Tokenize(Lib.ConfigValue(node, "dump", ""), ',');
			process = Profile.processes.Find(p => p.name == name);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("name", name);
			node.AddValue("desiredRate", desiredRate);
			node.AddValue("dump", string.Join(",", dump));
		}

		internal void CycleDesiredRate()
		{
			double step = 0.1;

			desiredRate += step;
			if (desiredRate > 1.0)
				desiredRate = 0.0;
		}

		public void Execute(Vessel v, VesselData vd, VesselResHandler resources, double elapsed_s)
		{
			if (process == null)
				return;

			process.Execute(v, vd, resources, elapsed_s, desiredRate, dump);
		}

		public string Description()
		{
			if (!string.IsNullOrEmpty(cachedDescription))
				return cachedDescription;

			cachedDescription = process.Specifics(1.0 * desiredRate).Info();
			return cachedDescription;
		}
        internal ProcessData GetFlightReferenceFromPart(Part part)
        {
            throw new NotImplementedException();
        }
   	}


} // KERBALISM
