using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{


	public static class Profile
	{
		public static List<string> modifiersCompilationErrors = new List<string>();

		// node parsing
		private static void Nodeparse(ConfigNode profile_node)
		{
			// parse all rules
			foreach (ConfigNode rule_node in profile_node.GetNodes("Rule"))
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
			foreach (ConfigNode supply_node in profile_node.GetNodes("Supply"))
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
			foreach (ConfigNode process_node in profile_node.GetNodes("Process"))
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

			if (modifiersCompilationErrors.Count > 0)
			{
				string errors = string.Empty;
				foreach (string error in modifiersCompilationErrors)
				{
					errors = errors + "\n\n" + error;
				}

				PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
					new Vector2(0.5f, 0.5f),
					new MultiOptionDialog(
						"Kerbalism profile modifiers compilation error",
						"Some profile modifiers could not be compiled.\n"
						 + "KSP will run but this can result in incorrect behaviour.\n\n"
						 + "Please check your profile configuration (the following errors have been logged in KSP.log)"
						 + errors,
						"Kerbalism profile modifiers compilation error",
						HighLogic.UISkin,
						new Rect(0.5f, 0.5f, 500f, 60f),
						new DialogGUIFlexibleSpace(),
						new DialogGUIHorizontalLayout(
							new DialogGUIFlexibleSpace(),
							new DialogGUIButton("OK", null, 140.0f, 30.0f, true),
							new DialogGUIFlexibleSpace()
						)
					),
					true,
					HighLogic.UISkin);
			}
		}

		// Support config file parsing
		private static void ParseSupport()
		{
			// for each profile
			foreach (ConfigNode profile_node in Lib.ParseConfigs("Profile"))
			{
				// get the name
				string name = Lib.ConfigValue(profile_node, "name", string.Empty);

				// if this is a Kerbalism Support profile
				if (name == "KerbalismSupport")
				{
					// get the mod name and directory
					string modname = Lib.ConfigValue(profile_node, "modname", string.Empty);
					string moddir = Lib.ConfigValue(profile_node, "moddir", string.Empty);

					// if the mods directory exists
					if (Lib.GameDirectoryExist(moddir))
					{
						// log profile and mod name
						Lib.Log(Lib.BuildString("importing Kerbalism Support profile for mod: ", modname));

						// parse nodes
						Nodeparse(profile_node);

						// done a Support profile now on to the next
					}
				}
			}
		}

		public static void Parse()
		{
			// initialize data
			rules = new List<Rule>();
			supplies = new List<Supply>();
			processes = new List<Process>();

			// if a profile is specified
			if (Settings.Profile.Length > 0)
			{
				// for each profile config
				foreach (ConfigNode profile_node in Lib.ParseConfigs("Profile"))
				{
					// get the name
					string name = Lib.ConfigValue(profile_node, "name", string.Empty);
					// if this is the one chosen in settings
					if (name == Settings.Profile)
					{
						// log profile name
						Lib.Log(Lib.BuildString("using profile: ", Settings.Profile));

						// parse nodes
						Nodeparse(profile_node);

						// Add support configs
						ParseSupport();

						// log info
						Lib.Log("supplies:");
						foreach (Supply supply in supplies) Lib.Log(Lib.BuildString("- ", supply.resource));
						if (supplies.Count == 0) Lib.Log("- none");
						Lib.Log("rules:");
						foreach (Rule rule in rules) Lib.Log(Lib.BuildString("- ", rule.name));
						if (rules.Count == 0) Lib.Log("- none");
						Lib.Log("processes:");
						foreach (Process process in processes) Lib.Log(Lib.BuildString("- ", process.name));
						if (processes.Count == 0) Lib.Log("- none");

						// we are done here
						return;
					}
				}
				// if we reach this point, the profile was not found
				Lib.Log(Lib.BuildString("profile '", Settings.Profile, "' was not found"), Lib.LogLevel.Warning);
			}
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
				process.Execute(vd, resources, elapsed_s);
			}
		}


		public static void SetupPods()
		{
			// add supply resources to all pods
			foreach (AvailablePart p in PartLoader.LoadedPartsList)
			{
				foreach (Supply supply in supplies)
				{
					supply.SetupPod(p);
				}
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


		public static List<Rule> rules;               // rules in the profile
		public static List<Supply> supplies;          // supplies in the profile
		public static List<Process> processes;        // processes in the profile
	}


} // KERBALISM
