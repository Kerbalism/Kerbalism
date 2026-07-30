using System;
using System.Collections.Generic;

namespace KERBALISM.EngineFailures
{
	/// <summary>
	/// Calculates automatic engine reliability ratings from config-defined
	/// propulsion families. This class only supplies ratings: EngineFailures
	/// continues to own wear, failures, persistence, UI and repairs.
	/// </summary>
	internal static class EngineReliabilityHeuristics
	{
		internal const double AutomaticDouble = -2.0;
		internal const int AutomaticInt = -2;

		sealed class FamilyDefinition
		{
			internal string Name;
			internal double IspMin;
			internal double IspMax;
			internal double BurnMin;
			internal double BurnMax;
			internal double BaseIgnitions;
			internal double ReferenceThrust;
			internal double ThrustExponent;
			internal double UpperStageBonus;
			internal int MinIgnitions;
			internal int MaxIgnitions;
			internal double TurnonFailureProbability;
		}

		struct EngineRatings
		{
			internal double BurnDuration;
			internal int Ignitions;
			internal double TurnonFailureProbability;
		}

		static readonly Dictionary<string, FamilyDefinition> families =
			new Dictionary<string, FamilyDefinition>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<string, HashSet<string>> resourceRoles =
			new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<string, string> moduleFamilies =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		static readonly HashSet<string> loggedUnknownParts =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		static bool loaded;

		internal static bool Apply(EngineFailures module)
		{
			if (module == null || !module.engine_reliability_auto)
				return true;
			if (module.part == null)
				return false;

			bool automaticBurn = IsAutomatic(module.rated_operation_duration);
			bool automaticIgnitions = module.rated_ignitions == AutomaticInt;
			bool automaticTurnon = IsAutomatic(module.turnon_failure_probability);
			if (!automaticBurn && !automaticIgnitions && !automaticTurnon)
				return true;

			if (!EnsureLoaded())
				return false;

			EngineRatings ratings;
			if (!TryCalculate(module, out ratings))
			{
				// Automatic values must fail open: an unsupported future engine
				// should not inherit an arbitrary chemical-engine lifetime.
				ratings = SafeFallback("unknownAdvanced");
				LogUnknown(module.part);
			}

			if (automaticBurn)
				module.rated_operation_duration = ratings.BurnDuration;
			if (automaticIgnitions)
				module.rated_ignitions = ratings.Ignitions;
			if (automaticTurnon)
				module.turnon_failure_probability = ratings.TurnonFailureProbability;
			return true;
		}

		static bool TryCalculate(EngineFailures module, out EngineRatings ratings)
		{
			ratings = default(EngineRatings);
			if (families.Count == 0)
				return false;

			string explicitFamily = module.engine_reliability_family;
			if (!string.IsNullOrEmpty(explicitFamily)
				&& !explicitFamily.Equals("auto", StringComparison.OrdinalIgnoreCase))
			{
				return TryCalculateForFamily(module.part.FindModulesImplementing<ModuleEngines>(), explicitFamily, out ratings);
			}

			string moduleFamily = FindModuleFamily(module.part);
			if (!string.IsNullOrEmpty(moduleFamily))
				return TryCalculateForFamily(module.part.FindModulesImplementing<ModuleEngines>(), moduleFamily, out ratings);

			List<ModuleEngines> engines = module.part.FindModulesImplementing<ModuleEngines>();
			if (engines == null || engines.Count == 0)
				return false;

			string commonFamily = null;
			double burnDuration = 0.0;
			int ignitionCount = 0;
			double turnonProbability = 0.0;

			foreach (ModuleEngines engine in engines)
			{
				string familyName = Classify(engine);
				if (string.IsNullOrEmpty(commonFamily))
					commonFamily = familyName;
				else if (!commonFamily.Equals(familyName, StringComparison.OrdinalIgnoreCase))
					return TryCalculateForFamily(engines, "unknownAdvanced", out ratings);

				FamilyDefinition family;
				if (!families.TryGetValue(familyName, out family))
					return false;

				double vacuumIsp = GetIsp(engine, 0.0f);
				double atmosphereIsp = GetIsp(engine, 1.0f);
				burnDuration = Math.Max(burnDuration, CalculateBurnDuration(family, vacuumIsp));
				ignitionCount = Math.Max(ignitionCount, CalculateIgnitions(family, engine.maxThrust, vacuumIsp, atmosphereIsp));
				turnonProbability = Math.Max(turnonProbability, family.TurnonFailureProbability);
			}

			ratings = new EngineRatings
			{
				BurnDuration = burnDuration,
				Ignitions = ignitionCount,
				TurnonFailureProbability = turnonProbability
			};
			return true;
		}

		static bool TryCalculateForFamily(List<ModuleEngines> engines, string familyName, out EngineRatings ratings)
		{
			ratings = default(EngineRatings);
			FamilyDefinition family;
			if (!families.TryGetValue(familyName, out family))
				return false;

			double burnDuration = 0.0;
			int ignitionCount = 0;
			if (engines != null)
			{
				foreach (ModuleEngines engine in engines)
				{
					double vacuumIsp = GetIsp(engine, 0.0f);
					double atmosphereIsp = GetIsp(engine, 1.0f);
					burnDuration = Math.Max(burnDuration, CalculateBurnDuration(family, vacuumIsp));
					ignitionCount = Math.Max(ignitionCount, CalculateIgnitions(family, engine.maxThrust, vacuumIsp, atmosphereIsp));
				}
			}

			// A special module may not expose a stock ModuleEngines. Its family
			// still supplies safe family defaults.
			if (engines == null || engines.Count == 0)
				burnDuration = family.BurnMax;

			ratings = new EngineRatings
			{
				BurnDuration = burnDuration,
				Ignitions = ignitionCount,
				TurnonFailureProbability = family.TurnonFailureProbability
			};
			return true;
		}

		static string Classify(ModuleEngines engine)
		{
			var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (engine.propellants != null)
			{
				foreach (Propellant propellant in engine.propellants)
				{
					HashSet<string> mappedRoles;
					if (!resourceRoles.TryGetValue(propellant.name, out mappedRoles))
						continue;
					roles.UnionWith(mappedRoles);
				}
			}

			if (roles.Contains("solid")) return "solid";
			if (roles.Contains("nuclearSaltWater")) return "nuclearSaltWater";
			if (roles.Contains("fissionFragment")) return "fissionFragment";
			if (roles.Contains("antimatter")) return "antimatter";
			if (roles.Contains("pulse")) return "pulse";
			if (roles.Contains("fusion")) return "fusion";
			if (roles.Contains("intake")) return "airbreathing";
			if (roles.Contains("electricPropellant")) return "electric";

			bool hasOxygen = roles.Contains("oxygen") || roles.Contains("oxidizer");
			if (roles.Contains("storableFuel") && roles.Contains("storableOxidizer")) return "storable";
			if (roles.Contains("monoprop") && !roles.Contains("storableOxidizer")) return "monoprop";
			if (roles.Contains("hydrogen") && hasOxygen) return "hydrolox";
			if (roles.Contains("methane") && hasOxygen) return "methalox";
			if (roles.Contains("kerosene") && hasOxygen) return "kerolox";
			if (roles.Contains("stockFuel") && hasOxygen) return "stockChemical";
			if ((roles.Contains("hydrogen") || roles.Contains("stockFuel")) && !hasOxygen) return "nuclearThermal";
			if (hasOxygen) return "stockChemical";

			return "unknownAdvanced";
		}

		static string FindModuleFamily(Part part)
		{
			foreach (PartModule module in part.Modules)
			{
				string family;
				if (moduleFamilies.TryGetValue(module.moduleName, out family))
					return family;
			}
			return null;
		}

		static double CalculateBurnDuration(FamilyDefinition family, double vacuumIsp)
		{
			if (family.BurnMax <= 0.0)
				return 0.0;
			if (family.BurnMax <= family.BurnMin)
				return family.BurnMax;
			if (vacuumIsp <= 0.0 || family.IspMax <= family.IspMin)
				return family.BurnMax;

			double x = Clamp((vacuumIsp - family.IspMin) / (family.IspMax - family.IspMin), 0.0, 1.0);
			double smooth = x * x * (3.0 - 2.0 * x);
			return family.BurnMin + (family.BurnMax - family.BurnMin) * smooth;
		}

		static int CalculateIgnitions(FamilyDefinition family, double maxThrust, double vacuumIsp, double atmosphereIsp)
		{
			if (family.BaseIgnitions <= 0.0 || family.MaxIgnitions <= 0)
				return 0;

			double sizeFactor = 1.0;
			if (maxThrust > 0.0 && family.ReferenceThrust > 0.0)
			{
				sizeFactor = Math.Pow(family.ReferenceThrust / Math.Max(maxThrust, 1.0), family.ThrustExponent);
				sizeFactor = Clamp(sizeFactor, 0.5, 4.0);
			}

			double upperStage = 0.0;
			if (vacuumIsp > 0.0 && atmosphereIsp > 0.0)
				upperStage = SmoothStep(1.4, 2.2, vacuumIsp / atmosphereIsp);

			double calculated = family.BaseIgnitions
				* sizeFactor
				* (1.0 + family.UpperStageBonus * upperStage);
			int rounded = (int)Math.Round(calculated, MidpointRounding.AwayFromZero);
			return Math.Max(family.MinIgnitions, Math.Min(family.MaxIgnitions, rounded));
		}

		static double GetIsp(ModuleEngines engine, float pressure)
		{
			return engine != null && engine.atmosphereCurve != null
				? engine.atmosphereCurve.Evaluate(pressure)
				: 0.0;
		}

		static EngineRatings SafeFallback(string familyName)
		{
			FamilyDefinition family;
			if (families.TryGetValue(familyName, out family))
			{
				return new EngineRatings
				{
					BurnDuration = 0.0,
					Ignitions = 0,
					TurnonFailureProbability = family.TurnonFailureProbability
				};
			}

			return new EngineRatings
			{
				BurnDuration = 0.0,
				Ignitions = 0,
				TurnonFailureProbability = 0.001
			};
		}

		static bool IsAutomatic(double value)
		{
			return Math.Abs(value - AutomaticDouble) < 1e-9;
		}

		static double SmoothStep(double edge0, double edge1, double value)
		{
			if (edge1 <= edge0)
				return value >= edge1 ? 1.0 : 0.0;
			double x = Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
			return x * x * (3.0 - 2.0 * x);
		}

		static double Clamp(double value, double min, double max)
		{
			return Math.Max(min, Math.Min(max, value));
		}

		static bool EnsureLoaded()
		{
			if (loaded)
				return true;

			ConfigNode[] roots = GameDatabase.Instance == null
				? null
				: GameDatabase.Instance.GetConfigNodes("KERBALISM_ENGINE_RELIABILITY");
			if (roots == null || roots.Length == 0)
				return false;

			foreach (ConfigNode root in roots)
			{
				foreach (ConfigNode node in root.GetNodes("RESOURCE"))
				{
					string name = Lib.ConfigValue(node, "name", string.Empty);
					if (string.IsNullOrEmpty(name))
						continue;
					resourceRoles[name] = ParseRoles(Lib.ConfigValue(node, "roles", string.Empty));
				}

				foreach (ConfigNode node in root.GetNodes("MODULE"))
				{
					string name = Lib.ConfigValue(node, "name", string.Empty);
					string family = Lib.ConfigValue(node, "family", string.Empty);
					if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(family))
						moduleFamilies[name] = family;
				}

				foreach (ConfigNode node in root.GetNodes("FAMILY"))
				{
					FamilyDefinition family = ParseFamily(node);
					if (!string.IsNullOrEmpty(family.Name))
						families[family.Name] = family;
				}
			}
			loaded = true;
			return true;
		}

		static HashSet<string> ParseRoles(string value)
		{
			var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrEmpty(value))
				return result;

			string[] tokens = value.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string token in tokens)
				result.Add(token.Trim());
			return result;
		}

		static FamilyDefinition ParseFamily(ConfigNode node)
		{
			return new FamilyDefinition
			{
				Name = Lib.ConfigValue(node, "name", string.Empty),
				IspMin = Lib.ConfigValue(node, "isp_min", 0.0),
				IspMax = Lib.ConfigValue(node, "isp_max", 0.0),
				BurnMin = Lib.ConfigValue(node, "burn_min", 0.0),
				BurnMax = Lib.ConfigValue(node, "burn_max", 0.0),
				BaseIgnitions = Lib.ConfigValue(node, "base_ignitions", 0.0),
				ReferenceThrust = Lib.ConfigValue(node, "reference_thrust", 100.0),
				ThrustExponent = Lib.ConfigValue(node, "thrust_exponent", 0.3),
				UpperStageBonus = Lib.ConfigValue(node, "upper_stage_bonus", 0.0),
				MinIgnitions = Lib.ConfigValue(node, "min_ignitions", 0),
				MaxIgnitions = Lib.ConfigValue(node, "max_ignitions", 0),
				TurnonFailureProbability = Lib.ConfigValue(node, "turnon_failure_probability", 0.0)
			};
		}

		static void LogUnknown(Part part)
		{
			string partName = part.partInfo != null ? part.partInfo.name : part.name;
			if (!loggedUnknownParts.Add(partName))
				return;
			Lib.Log("Engine reliability: no automatic family for " + partName
				+ "; burn and ignition limits disabled", Lib.LogLevel.Warning);
		}
	}
}
