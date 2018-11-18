using System;
using System.Collections.Generic;
using System.Text;


namespace KERBALISM
{


	public static class Modifiers
	{
		public static double Evaluate(Vessel v, Vessel_info vi, Vessel_resources resources, List<string> modifiers, Part p = null, ProtoPartSnapshot pp = null)
		{
			double k = 1.0;
			foreach (string mod in modifiers)
			{
				switch (mod)
				{
					case "zerog":
						k *= vi.zerog ? 1.0 : 0.0;
						break;

					case "landed":
						k *= vi.landed ? 1.0 : 0.0;
						break;

					case "breathable":
						k *= vi.breathable ? 1.0 : 0.0;
						break;

					case "non_breathable":
						k *= vi.breathable ? 0.0 : 1.0;
						break;

					case "temperature":
						k *= vi.temp_diff;
						break;

					case "radiation":
						k *= vi.radiation;
						break;

					case "shielding":
						k *= 1.0 - vi.shielding;
						break;

					case "volume":
						k *= vi.volume;
						break;

					case "surface":
						k *= vi.surface;
						break;

					case "living_space":
						k /= vi.living_space;
						break;

					case "comfort":
						k /= vi.comforts.factor;
						break;

					case "pressure":
						k *= vi.pressure > Settings.PressureThreshold ? 1.0 : Settings.PressureFactor;
						break;

					case "poisoning":
						k *= vi.poisoning > Settings.PoisoningThreshold ? 1.0 : Settings.PoisoningFactor;
						break;

					case "humidity":
						k *= vi.humidity > Settings.HumidityThreshold ? 1.0 : Settings.HumidityFactor;
						break;

					case "per_capita":
						k /= (double)Math.Max(vi.crew_count, 1);
						break;

					default:
						// If a psuedo resource is flowable, per part processing would result in every part producing the entire capacity of the vessel
						if (!Lib.IsResourceImpossibleToFlow(mod)) throw new Exception("psuedo-resource " + mod + " must be NO_FLOW");
						if (p != null) 			k *= resources.Info(v, mod).GetResourceInfoView(p).amount;  // loaded part
						else if (pp != null)	k *= resources.Info(v, mod).GetResourceInfoView(pp).amount; // unloaded part
						else					k *= resources.Info(v, mod).amount;                         // vessel-wide
						break;
				}
			}
			return k;
		}


		public static double Evaluate(Environment_analyzer env, Vessel_analyzer va, Resource_simulator sim, List<string> modifiers, Part p = null)
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

					case "humidity":
						k *= !va.humid ? 1.0 : Settings.HumidityFactor;
						break;

					case "per_capita":
						k /= (double)Math.Max(va.crew_count, 1);
						break;

					default:
						// If a psuedo resource is flowable, per part processing would result in every part producing the entire capacity of the vessel
						if (!Lib.IsResourceImpossibleToFlow(mod)) throw new Exception("psuedo-resource " + mod + "must be NO_FLOW");
						if (p != null)			k *= sim.Resource(mod).GetSimulatedResourceView(p).amount; // loaded part
						else					k *= sim.Resource(mod).amount;                             // vessel-wide
						break;
				}
			}
			return k;
		}
	}


} // KERBALISM