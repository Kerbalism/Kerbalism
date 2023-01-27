using System;
using System.Collections.Generic;


namespace KERBALISM
{


	/// <summary>
	/// Contains a list of resources that can be dumped overboard
	/// </summary>
	public sealed class DumpSpecs
	{
		private const string All = "All";

		// constructor
		/// <summary> Configures the always dump resources and the dump valves, dump valves are only used if always_dump is empty or contains "false" </summary>
		public DumpSpecs(string always_dump, string dump_valves)
		{
			dumpValves.Add(new List<string> { "Nothing" });
			dumpValvesNames.Add("Nothing");
			canSwitchValves = false;

			// if always_dump is empty or false: configure dump valves if any requested
			if (always_dump.Length == 0 || string.Equals(always_dump, "false", System.StringComparison.OrdinalIgnoreCase))
			{
				// if dump_valves is empty or false then don't do anything
				if (dump_valves.Length > 0 && !string.Equals(dump_valves, "false", System.StringComparison.OrdinalIgnoreCase))
				{
					canSwitchValves = true;
					foreach (string dumpValve in Lib.Tokenize(dump_valves, ','))
					{
						List<string> resources = Lib.Tokenize(dumpValve, '&');
						dumpValves.Add(resources);
						dumpValvesNames.Add(string.Join(", ", resources));
					}
				}
			}
			// if true: dump everything
			else if (string.Equals(always_dump, "true", System.StringComparison.OrdinalIgnoreCase))
			{
				dumpValves.Add(new List<string>() { All });
				dumpValvesNames.Add("All outputs");
			}
			// all other cases: dump only specified resources in always_dump
			else
			{
				dumpValves.Add(Lib.Tokenize(always_dump, ','));
				dumpValvesNames.Add(always_dump);
			}
		}

		private bool canSwitchValves;
		private List<List<string>> dumpValves = new List<List<string>>();
		private List<string> dumpValvesNames = new List<string>();

		public sealed class ActiveValve
		{
			private DumpSpecs _dumpSpecs;
			public DumpSpecs DumpSpecs => _dumpSpecs;
			private int current;

			public ActiveValve(DumpSpecs dumpSpecs)
			{
				_dumpSpecs = dumpSpecs;
			}

			public bool CanSwitchValves => _dumpSpecs.canSwitchValves;

			public string ValveTitle => _dumpSpecs.dumpValvesNames[current];

			/// <summary> activates or returns the current dump valve index </summary>
			public int ValveIndex
			{
				get => current;
				set
				{
					if (value < 0 || value >= _dumpSpecs.dumpValves.Count)
						value = 0;

					current = value;
				}
			}

			/// <summary> activates the next dump valve and returns its index </summary>
			public int NextValve()
			{
				current = ++current % _dumpSpecs.dumpValves.Count;
				return current;
			}

			/// <summary> returns true if the specified resource should be dumped </summary>
			public bool Check(string res_name)
			{
				List<string> valve = _dumpSpecs.dumpValves[current];

				if (valve[0] == All)
					return true;

				int i = valve.Count;
				while (i-- > 0)
					if (valve[i] == res_name)
						return true;

				return false;
			}
		}
	}
} // KERBALISM



