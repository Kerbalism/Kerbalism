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
				any = false;
				list = new List<string>();

				// if dump_valves is empty or false then don't do anything
				if (dump_valves.Length == 0 || string.Equals(dump_valves, "false", System.StringComparison.OrdinalIgnoreCase))
				{
					AnyValves = false;
					valves = new List<string>();
					valves.Insert(0, "None");
				}
				// create list of dump valves
				else
				{
					AnyValves = true;
					valves = Lib.Tokenize(dump_valves, ',');
					valves.Insert(0, "None");
					for (int i = 0; i < valves.Count; i++)
						valves[i] = valves[i].Replace("&", ", ");
				}
			}
			// if true: dump everything
			else if (string.Equals(always_dump, "true", System.StringComparison.OrdinalIgnoreCase))
			{
				any = true;
				list = new List<string>();
				AnyValves = false;
				valves = new List<string>();
				valves.Insert(0, "None");
			}
			// all other cases: dump only specified resources in always_dump
			else
			{
				any = false;
				list = Lib.Tokenize(always_dump, ',');
				AnyValves = false;
				valves = new List<string>();
				valves.Insert(0, "None");
			}
		}

		// methods

		/// <summary> returns true if any dump valves exist for the process </summary>
		public bool AnyValves { get; private set; } = false;

		/// <summary> activates or returns the current dump valve index </summary>
		public int ValveIndex
		{
			get { return AnyValves ? valve_i : 0; }
			set
			{
				valve_i = (!AnyValves || value > valves.Count - 1 || value < 0) ? 0 : value;
				if (AnyValves) DeployValve();
			}
		}

		/// <summary> activates the next dump valve and returns its index </summary>
		public int NextValve
		{
			get
			{
				valve_i = valve_i >= valves.Count - 1 ? 0 : ++valve_i;
				if (AnyValves) DeployValve();
				return valve_i;
			}
			private set { }
		}

		/// <summary> deploys the current dump valve to the dump list </summary>
		private void DeployValve()
		{
			if (!AnyValves || valve_i <= 0)
				list.Clear();
			else
				list = Lib.Tokenize(valves[valve_i], ',');
		}

		/// <summary> returns true if the specified resource should be dumped </summary>
		public bool Check(string res_name)
		{
			return any || list.Contains(res_name);
		}

		/// <summary> true if any resources should be dumped </summary>
		private readonly bool any = false;
		/// <summary> list of resources to dump overboard </summary>
		private List<string> list;
		/// <summary> index of currently active dump valve </summary>
		private int valve_i = 0;
		/// <summary> list of dump valves the user can choose from </summary>
		public readonly List<string> valves;
	}


} // KERBALISM



