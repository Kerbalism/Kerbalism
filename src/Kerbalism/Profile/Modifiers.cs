using System;
using System.Collections.Generic;
using KERBALISM.Planner;


namespace KERBALISM
{


	public static class Modifiers
	{
		/// <summary>Prefix for multiplicative inverse of a resource amount: inv:Water → 1/Water.Amount</summary>
		public const string InversePrefix = "inv:";

		///<summary> Modifiers Evaluate method used for the Monitors background and current vessel simulation </summary>
		public static double Evaluate(Vessel v, VesselData vd, VesselResources resources, List<string> modifiers)
		{
			double k = 1.0;
			foreach (string mod in modifiers)
			{
				switch (mod)
				{
					case "zerog":
						k *= vd.EnvZeroG ? 1.0 : 0.0;
						break;

					case "landed":
						k *= vd.EnvLanded ? 1.0 : 0.0;
						break;

					case "breathable":
						k *= vd.EnvBreathable ? 1.0 : 0.0;
						break;

					case "non_breathable":
						k *= vd.EnvBreathable ? 0.0 : 1.0;
						break;

					case "temperature":
						k *= vd.VesselSurvivalTempDiff;
						break;

					case "radiation":
						k *= vd.EnvHabitatRadiation;
						break;

					case "shielding":
						k *= 1.0 - vd.Shielding;
						break;

					case "volume":
						k *= vd.Volume;
						break;

					case "surface":
						k *= vd.Surface;
						break;

					case "living_space":
						k /= vd.LivingSpace;
						break;

					case "comfort":
						k /= vd.Comforts.factor;
						break;

					case "pressure":
						k *= vd.Pressure > Settings.PressureThreshold ? 1.0 : Settings.PressureFactor;
						break;

					case "poisoning":
						k *= vd.Poisoning > Settings.PoisoningThreshold ? 1.0 : Settings.PoisoningFactor;
						break;

					case "per_capita":
						k /= (double)Math.Max(vd.CrewCount, 1);
						break;

					default:
						k *= ResourceAmountFactor(mod, v, resources);
						break;
				}
			}
			return k;
		}


		///<summary> Modifiers Evaluate method used for the Planners vessel simulation in the VAB/SPH </summary>
		public static double Evaluate(EnvironmentAnalyzer env, VesselAnalyzer va, ResourceSimulator sim, List<string> modifiers)
		{
			double k = 1.0;
			foreach (string mod in modifiers)
			{
				switch (mod)
				{
					case "zerog":
						k *= env.zerog ? 1.0 : 0.0;
						break;

					case "landed":
						k *= env.landed ? 1.0 : 0.0;
						break;

					case "breathable":
						k *= env.breathable ? 1.0 : 0.0;
						break;

					case "non_breathable":
						k *= env.breathable ? 0.0 : 1.0;
						break;

					case "temperature":
						k *= env.temp_diff;
						break;

					case "radiation":
						k *= Math.Max(Radiation.Nominal, (env.landed ? env.surface_rad : env.magnetopause_rad) + va.emitted);
						break;

					case "shielding":
						k *= 1.0 - va.shielding;
						break;

					case "volume":
						k *= va.volume;
						break;

					case "surface":
						k *= va.surface;
						break;

					case "living_space":
						k /= va.living_space;
						break;

					case "comfort":
						k /= va.comforts.factor;
						break;

					case "pressure":
						k *= va.pressurized ? 1.0 : Settings.PressureFactor;
						break;

					case "poisoning":
						k *= !va.scrubbed ? 1.0 : Settings.PoisoningFactor;
						break;

					case "per_capita":
						k /= (double)Math.Max(va.crew_count, 1);
						break;

					default:
						k *= ResourceAmountFactor(mod, sim);
						break;
				}
			}
			return k;
		}

		/// <summary>
		/// Resource amount, or 1/amount when prefixed with <see cref="InversePrefix"/>.
		/// Zero (or near-zero) amount yields 0 for the inverse, so rates stay finite.
		/// </summary>
		static double ResourceAmountFactor(string mod, Vessel v, VesselResources resources)
		{
			if (!mod.StartsWith(InversePrefix, StringComparison.Ordinal))
				return resources.GetResource(v, mod).Amount;

			string resource = mod.Substring(InversePrefix.Length);
			if (resource.Length == 0)
				return 0.0;

			double amount = resources.GetResource(v, resource).Amount;
			return amount > double.Epsilon ? 1.0 / amount : 0.0;
		}

		/// <summary> planner variant of <see cref="ResourceAmountFactor(string, Vessel, VesselResources)"/> </summary>
		static double ResourceAmountFactor(string mod, ResourceSimulator sim)
		{
			if (!mod.StartsWith(InversePrefix, StringComparison.Ordinal))
				return sim.Resource(mod).amount;

			string resource = mod.Substring(InversePrefix.Length);
			if (resource.Length == 0)
				return 0.0;

			double amount = sim.Resource(resource).amount;
			return amount > double.Epsilon ? 1.0 / amount : 0.0;
		}
	}


} // KERBALISM
