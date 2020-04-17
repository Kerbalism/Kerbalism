using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public static class Kopernicus
	{
		private class BodyInfo
		{
			public string displayName;
			public string refBody;
			public double mass;
			public bool isSun = false;
			public bool isHomeworld = false;
			public double solarDayLength = 0.0;
			public double radius = 0.0;
			public double sma = 0.0;
			public double gravParameter = 0.0;
			public bool dayLengthNeedConversion = false;

			public BodyInfo() { }

			public BodyInfo(string displayName, string refBody, double mass, double solarDayLength, double sma, double radius, double gravParameter, bool isSun, bool isHomeworld)
			{
				this.displayName = displayName;
				this.mass = mass;
				this.refBody = refBody;
				this.isSun = isSun;
				this.isHomeworld = isHomeworld;
				this.solarDayLength = solarDayLength;
				this.radius = radius;
				this.sma = sma;
				this.gravParameter = gravParameter;
			}
		}

		private static readonly Dictionary<string, BodyInfo> bodyTemplates = new Dictionary<string, BodyInfo>()
		{
			{ "Sun", new BodyInfo("Sun", null, 1.75654591319326E+28, 0.0, 0.0, 261600000, 1.17233279483249E+18, true, false)},
			{ "Kerbin", new BodyInfo("Kerbin", "Sun", 5.29151583439215E+22, 21600, 13599840256, 600000, 3531600000000.0, false, true)},
			{ "Mun", new BodyInfo("Mun", "Kerbin", 9.7599066119646E+20, 141115.385296176, 12000000, 200000, 65138397520.7807, false, false)},
			{ "Minmus", new BodyInfo( "Minmus", "Kerbin", 2.64575795662095E+19, 40578.1222569469, 47000000, 60000, 1765800026.31247, false, false)},
			{ "Moho", new BodyInfo("Moho", "Sun", 2.52633139930162E+21, 2665723.44748356, 5263138304, 250000, 168609378654.509, false, false)},
			{ "Eve", new BodyInfo("Eve", "Sun", 1.2243980038014E+23, 81661.8566811459, 9832684544, 700000, 8171730229210.87, false, false)},
			{ "Duna", new BodyInfo("Duna", "Sun", 4.51542702477492E+21, 65766.7068643109, 20726155264, 320000, 301363211975.098, false, false)},
			{ "Ike", new BodyInfo("Ike", "Duna", 2.78216152235874E+20, 65766.7096451232, 3200000, 130000, 18568368573.144, false, false)},
			{ "Jool", new BodyInfo("Jool", "Sun", 4.23321273059351E+24, 36012.3870456149, 68773560320, 6000000, 282528004209995, false, false)},
			{ "Laythe", new BodyInfo("Laythe", "Jool", 2.93973106291216E+22, 53007.7122025236, 27184000, 500000, 1962000029236.08, false, false)},
			{ "Vall", new BodyInfo("Vall", "Jool", 3.10876554482042E+21, 106069.476525395, 43152000, 300000 , 207481499473.751, false, false)},
			{ "Bop", new BodyInfo("Bop", "Jool", 3.72610898343278E+19, 547355.076395446, 128500000, 65000, 2486834944.41491, false, false)},
			{ "Tylo", new BodyInfo("Tylo", "Jool", 4.23321273059351E+22, 212356.353174406, 68500000, 600000, 2825280042099.95, false, false)},
			{ "Gilly", new BodyInfo("Gilly", "Eve", 1.24203632781093E+17, 28396.8085034526, 31500000, 13000, 8289449.81471635, false, false)},
			{ "Pol", new BodyInfo( "Pol", "Jool", 1.08135065806823E+19, 909742.17664378, 179890000, 44000,721702080, false, false)},
			{ "Dres", new BodyInfo("Dres", "Sun", 3.21909365785247E+20, 34825.304721078, 40839348203, 138000, 21484488600, false, false)},
			{ "Eeloo", new BodyInfo("Eeloo", "Sun", 1.11492242417007E+21, 19462.4124696157, 90118820000, 210000, 74410814527.0496, false, false)}
		};

		/// <summary>
		/// Find and parse Kopernicus configs to determine the calendar that should be used for the home world
		/// This is needed so we can use the proper calendar for parsing our configs and compiling parts
		/// If no Kopernicus modified home body is found, return the standard 6 hours / 426 days kerbin calendar
		/// Notes :
		/// - This result in the "24h/365d" KSP setting to always be ignored.
		/// - Doesn't account for Kopernicus "orbitPatches"
		/// - If the home body is a moon, the days in year value is derived from the parent planet orbit
		/// - Doesn't support a tidally locked home body (it should be doable but I'm lazy)
		/// - It use the solar day duration (not the sidereal day duration)
		/// </summary>
		public static bool GetHomeWorldCalendar(out double hoursInDayExact, out ulong hoursInDayL, out double daysInYearExact, out ulong daysInYearL, out string homeBodyName)
		{
			hoursInDayExact = 0.0;
			daysInYearExact = 0.0;
			hoursInDayL = 0;
			daysInYearL = 0;
			homeBodyName = string.Empty;

			var kopernicusNodes = GameDatabase.Instance.GetConfigs("Kopernicus");
			if (kopernicusNodes.Length != 1)
				return false;

			ConfigNode kopernicusCfg = kopernicusNodes[0].config;

			Dictionary<string, BodyInfo> bodies = new Dictionary<string, BodyInfo>();
			BodyInfo homeBody = null;

			foreach (ConfigNode bodyNode in kopernicusCfg.GetNodes("Body"))
			{
				BodyInfo bodyInfo = new BodyInfo();
				
				string name = Lib.ConfigValue(bodyNode, "name", string.Empty);
				if (name == string.Empty)
					continue;

				bodyInfo.displayName = name;

				string identifier = Lib.ConfigValue(bodyNode, "identifier", string.Empty);
				if (identifier != string.Empty)
					name = identifier;



				ConfigNode bodyTemplate = bodyNode.GetNode("Template");
				bool isUsingKerbinTemplate = false;
				if (bodyTemplate != null)
				{
					string templateName = Lib.ConfigValue(bodyTemplate, "name", string.Empty);
					if (templateName != string.Empty && bodyTemplates.TryGetValue(templateName, out BodyInfo templateInfo))
					{
						bodyInfo.mass = templateInfo.mass;
						bodyInfo.isSun = templateInfo.isSun;
						bodyInfo.isHomeworld = templateInfo.isHomeworld;
						bodyInfo.solarDayLength = templateInfo.solarDayLength;
						bodyInfo.sma = templateInfo.sma;
						bodyInfo.radius = templateInfo.radius;
						bodyInfo.gravParameter = templateInfo.gravParameter;
					}

					if (templateName == "Kerbin")
					{
						isUsingKerbinTemplate = true;
					}
				}

				ConfigNode orbitNode = bodyNode.GetNode("Orbit");
				if (orbitNode != null)
				{
					string refBodyName = Lib.ConfigValue(orbitNode, "referenceBody", string.Empty);
					if (refBodyName != string.Empty)
					{
						bodyInfo.refBody = refBodyName;
					}

					double semiMajorAxis = Lib.ConfigValue(orbitNode, "semiMajorAxis", -1.0);
					if (semiMajorAxis >= 0.0)
						bodyInfo.sma = semiMajorAxis;
				}

				ConfigNode bodyProperties = bodyNode.GetNode("Properties");
				if (bodyProperties == null)
					continue;

				bodyInfo.displayName = Lib.ConfigValue(bodyProperties, "displayName", bodyInfo.displayName).Replace("^N", "");

				if (bodyProperties.HasValue("isHomeWorld"))
				{
					bodyInfo.isHomeworld = Lib.ConfigValue(bodyProperties, "isHomeWorld", false);
					if (bodyInfo.isHomeworld)
					{
						homeBody = bodyInfo;
						homeBodyName = bodyInfo.displayName;
					}
				}

				double rotationPeriod = Lib.ConfigValue(bodyProperties, "rotationPeriod", -1.0);
				if (rotationPeriod >= 0.0)
				{
					bodyInfo.solarDayLength = rotationPeriod;
					bodyInfo.dayLengthNeedConversion = true;
				}

				if (bodyProperties.HasValue("solarRotationPeriod"))
				{
					bodyInfo.dayLengthNeedConversion = !Lib.ConfigValue(bodyProperties, "solarRotationPeriod", false);
				}
				else if (isUsingKerbinTemplate)
				{
					bodyInfo.dayLengthNeedConversion = false;
				}

				double radius = Lib.ConfigValue(bodyProperties, "radius", -1.0);
				if (radius >= 0.0)
					bodyInfo.radius = radius;

				if (bodyProperties.HasValue("geeASL"))
				{
					double geeASL = Lib.ConfigValue(bodyProperties, "geeASL", 0.0);
					bodyInfo.gravParameter = GravParameterFromGeeASL(bodyInfo.radius, geeASL);

				}
				else if (bodyProperties.HasValue("mass"))
				{
					bodyInfo.mass = Lib.ConfigValue(bodyProperties, "mass", 0.0);
					bodyInfo.gravParameter = GravParameterFromMass(bodyInfo.radius, bodyInfo.mass);
				}
				else if (bodyProperties.HasValue("gravParameter"))
				{
					bodyInfo.gravParameter = Lib.ConfigValue(bodyProperties, "gravParameter", 0.0);
				}

				bodies.Add(name, bodyInfo);
			}

			foreach (BodyInfo bodyInfo in bodies.Values)
			{
				if (homeBody == null && bodyInfo.isHomeworld)
				{
					homeBody = bodyInfo;
					homeBodyName = bodyInfo.displayName;
				}

				if (bodyInfo.dayLengthNeedConversion && !string.IsNullOrEmpty(bodyInfo.refBody))
				{
					if (!bodies.TryGetValue(bodyInfo.refBody, out BodyInfo refBody))
						continue;

					double bodyOrbitPeriod = GetOrbitPeriod(bodyInfo.sma, refBody.gravParameter);
					bodyInfo.solarDayLength = SolarDayLengthFromSiderealDayLength(bodyInfo.solarDayLength, bodyOrbitPeriod);
				}
			}


			if (homeBody == null)
				return false;

			double homeSolarDayLength = homeBody.solarDayLength;

			// in case the home body is a moon, we use it's parent planet to determine the days in year
			BodyInfo sun = homeBody;
			while (!sun.isSun)
			{
				homeBody = sun;
				if (!bodies.TryGetValue(sun.refBody, out sun))
				{
					return false;
				}
			}

			double orbitPeriod = GetOrbitPeriod(homeBody.sma, sun.gravParameter);

			daysInYearExact = orbitPeriod / homeSolarDayLength;
			hoursInDayExact = homeSolarDayLength / 3600.0;

			// In case the body/orbit parameters are defined to result in exact "integer" daysInYear / hoursInDay values,
			// due to FP errors and the use of Math.Floor, there is a risk we end up with a missing hour or day.
			// So we add an extra second to be safe. Example : with JNSQ our calculations would result in
			// daysInYear = 363.999999999999, adding the extra second gives 364.000023148147
			daysInYearL = (ulong)Math.Floor((orbitPeriod + 1.0) / homeSolarDayLength);
			hoursInDayL = (ulong)Math.Floor((homeSolarDayLength + 1.0) / 3600.0);

			return true;
		}

		// these two methods are derived from the Kopernicus source :
		// https://github.com/Kopernicus/Kopernicus/blob/2bbd5b5a230d07163d5927ff85bba1e3f341f25c/src/Kopernicus/Configuration/PropertiesLoader.cs#L485
		private static double GravParameterFromGeeASL(double radius, double geeAsl)
		{
			radius *= radius;
			return geeAsl * 9.80665 * radius;
		}

		private static double GravParameterFromMass(double radius, double mass)
		{
			radius *= radius;
			double geeAsl = mass * (6.67408E-11 / 9.80665) / radius;
			return geeAsl * 9.80665 * radius;
		}

		// this method is derived from the stock CelestialBody.CBUpdate() method
		private static double GetOrbitPeriod(double semiMajorAxis, double parentGravParameter)
		{
			double smaAbs = Math.Abs(semiMajorAxis);
			double meanMotion = Math.Sqrt(parentGravParameter / (smaAbs * smaAbs * smaAbs));
			return Math.PI * 2.0 / meanMotion;
		}

		public static double SolarDayLengthFromSiderealDayLength(double siderealDayLength, double orbitPeriod)
		{
			return siderealDayLength * orbitPeriod / (orbitPeriod - siderealDayLength);
		}
	}
}
