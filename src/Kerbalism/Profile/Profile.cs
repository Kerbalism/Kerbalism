using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{


	public static class Profile
	{
		public const string NODENAME_PROFILE = "KERBALISM_PROFILE";
		public const string NODENAME_RULE = "RULE";
		public const string NODENAME_PROCESS = "PROCESS";
		public const string NODENAME_SUPPLY = "SUPPLY";

		public static List<Rule> rules;               // rules in the profile
		public static List<Supply> supplies;          // supplies in the profile
		public static List<Process> processes;        // processes in the profile

		// node parsing
		private static void Nodeparse(ConfigNode profile_node)
		{
			// parse all rules
			foreach (ConfigNode rule_node in profile_node.GetNodes(NODENAME_RULE))
			{
				try
				{
					// parse rule
					Rule rule = new Rule(rule_node);

					// ignore duplicates
					if (rules.Find(k => k.name == rule.name) == null)
					{
						// add the rule
						rules.Add(rule);
					}
				}
				catch (Exception e)
				{
					Lib.Log("failed to load rule\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}

			// parse all supplies
			foreach (ConfigNode supply_node in profile_node.GetNodes(NODENAME_SUPPLY))
			{
				try
				{
					// parse supply
					Supply supply = new Supply(supply_node);

					// ignore duplicates
					if (supplies.Find(k => k.resource == supply.resource) == null)
					{
						// add the supply
						supplies.Add(supply);
					}
				}
				catch (Exception e)
				{
					Lib.Log("failed to load supply\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}

			// parse all processes
			foreach (ConfigNode process_node in profile_node.GetNodes(NODENAME_PROCESS))
			{
				try
				{
					// parse process
					Process process = new Process(process_node);

					// ignore duplicates
					if (processes.Find(k => k.name == process.name) == null)
					{
						// add the process
						processes.Add(process);
					}
				}
				catch (Exception e)
				{
					Lib.Log("failed to load process\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}
		}

		public static void Parse()
		{
			// initialize data
			rules = new List<Rule>();
			supplies = new List<Supply>();
			processes = new List<Process>();

			// for each profile config
			ConfigNode[] profileNodes = Lib.ParseConfigs(NODENAME_PROFILE);
			ConfigNode profileNode;
			if (profileNodes.Length == 1)
			{
				profileNode = profileNodes[0];
			}
			else
			{
				profileNode = new ConfigNode();

				if (profileNodes.Length == 0)
				{
					ErrorManager.AddError(true, $"No profile found.",
					"You likely have forgotten to install KerbalismConfig or an alternative config pack in GameData.");
				}
				else if (profileNodes.Length > 1)
				{
					ErrorManager.AddError(true, $"Muliple profiles found.",
					"You likely have duplicates of KerbalismConfig or of an alternative config pack in GameData.");
				}
			}

			// parse nodes
			Nodeparse(profileNode);

			// log info
			Lib.Log($"{supplies.Count} {NODENAME_SUPPLY} definitions found :");
			foreach (Supply supply in supplies)
				Lib.Log($"- {supply.resource}");

			Lib.Log($"{rules.Count} {NODENAME_RULE} definitions found :");
			foreach (Rule rule in rules)
				Lib.Log($"- {rule.name}");

			Lib.Log($"{processes.Count} {NODENAME_PROCESS} definitions found :");
			foreach (Process process in processes)
				Lib.Log($"- {process.name}");

			
		}

		public static void Execute(Vessel v, VesselData vd, VesselResHandler resources, double elapsed_s)
		{
			if(vd.CrewCount > 0)
			{
				// execute all rules
				foreach (Rule rule in rules)
				{
					rule.Execute(v, vd, resources, elapsed_s);
				}
			}

			foreach (Process process in processes)
			{
				process.Execute(vd, elapsed_s);
			}
		}

		public static void SetupPod(AvailablePart ap)
		{
			// add supply resources to pods
			foreach (Supply supply in supplies)
			{
				supply.SetupPod(ap);
			}
		}

		public static void SetupEva(Part p)
		{
			foreach (Supply supply in supplies)
			{
				supply.SetupEva(p);
			}
		}

		public static void SetupRescue(VesselData vd)
		{
			foreach (Supply supply in supplies)
			{
				supply.SetupRescue(vd);
			}
		}
	}
} // KERBALISM
