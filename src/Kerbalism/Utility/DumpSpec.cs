using System;
using System.Collections.Generic;


namespace KERBALISM
{
	/// <summary>
	/// Contains a list of resources that can be dumped overboard
	/// </summary>
	public sealed class DumpSpecs
	{
		// constructor
		/// <summary> Configures the always dump resources and the dump valves, dump valves are only used if always_dump is empty or contains "false" </summary>
		public DumpSpecs(string always_dump, string dump_valves)
		{
			// if always_dump is empty or false: configure dump valves if any requested
			if (always_dump.Length == 0 || string.Equals(always_dump, "false", System.StringComparison.OrdinalIgnoreCase))
			{
				// if dump_valves is empty or false then don't do anything
				if (dump_valves.Length > 0 && !string.Equals(dump_valves, "false", System.StringComparison.OrdinalIgnoreCase))
				{
					dumpType = DumpType.DumpValve;
					dumpValves.Add(new List<string>());
					dumpValvesNames.Add("Nothing");

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
				dumpType = DumpType.AlwaysDump;
			}
			// all other cases: dump only specified resources in always_dump
			else
			{
				dumpType = DumpType.DumpValve;
				dumpValves.Add(Lib.Tokenize(always_dump, ','));
				dumpValvesNames.Add(always_dump);
			}
		}

		private enum DumpType
		{
			NeverDump,
			AlwaysDump,
			DumpValve
		}

		private DumpType dumpType = DumpType.NeverDump;
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

			public bool CanSwitchValves => _dumpSpecs.dumpValves.Count > 1;

			public string ValveTitle => current < _dumpSpecs.dumpValvesNames.Count ? _dumpSpecs.dumpValvesNames[current] : string.Empty;

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
				if (_dumpSpecs.dumpValves.Count == 0)
					current = 0;
				else
					current = ++current % _dumpSpecs.dumpValves.Count;

				return current;
			}

			/// <summary> returns true if the specified resource should be dumped </summary>
			public bool Check(string res_name)
			{
				switch (_dumpSpecs.dumpType)
				{
					case DumpType.NeverDump:
						return false;
					case DumpType.AlwaysDump:
						return true;
					case DumpType.DumpValve:
						List<string> valve = _dumpSpecs.dumpValves[current];
						int i = valve.Count;
						while (i-- > 0)
							if (valve[i] == res_name)
								return true;
						break;
				}

				return false;
			}
		}
	}
} // KERBALISM



