using System;
using System.Collections.Generic;
using System.Text;
using KERBALISM.Planner;


namespace KERBALISM
{


	public static class Modifiers
	{
		///<summary> Modifiers Evaluate method used for the Monitors background and current vessel simulation </summary>
		public static double Evaluate(Vessel v, VesselData vd, VesselResHandler resources, List<string> modifiers)
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
						k *= vd.EnvInBreathableAtmosphere ? 1.0 : 0.0;
						break;

					case "non_breathable":
						k *= vd.EnvInBreathableAtmosphere ? 0.0 : 1.0;
						break;

					case "oxygenAtmosphere":
						k *= vd.EnvInOxygenAtmosphere ? 1.0 : 0.0;
						break;

					case "staticPressure":
						k *= vd.EnvStaticPressure;
						break;

					case "vacuumLevel":
						k *= 1.0 - Math.Min(vd.EnvStaticPressure, 1.0);
						break;

					case "temperature":
						k *= vd.EnvTempDiff;
						break;

					case "radiation":
						k *= vd.EnvHabitatRadiation;
						break;

					case "shieldingSurface":
						k *= vd.HabitatInfo.shieldingSurface;
						break;

					case "shielding":
						k *= 1.0 - vd.HabitatInfo.shieldingModifier;
						break;

					case "volume":
						k *= vd.HabitatInfo.livingVolume;
						break;

					case "surface":
						k *= vd.HabitatInfo.pressurizedSurface;
						break;

					case "living_space":
						k /= vd.HabitatInfo.livingSpaceModifier;
						break;

					case "comfort":
						k /= vd.HabitatInfo.comfortModifier;
						break;

					case "pressure":
						k *= vd.HabitatInfo.pressureModifier;
						break;

					case "poisoning":
						k *= vd.HabitatInfo.poisoningLevel > Settings.PoisoningThreshold ? 1.0 : Settings.PoisoningFactor;
						break;

					case "per_capita":
						k /= (double)Math.Max(vd.CrewCount, 1);
						break;

					default:
						k *= resources.GetResource(mod).Amount;
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
						k /= va.comfortFactor;
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
						k *= sim.handler.GetResource(mod).Amount;
						break;
				}
			}
			return k;
		}
	}


} // KERBALISM
