using System;

namespace KERBALISM.VesselLogic
{
	/*
	public class VesselEnvironment
	{
		public class SunInfo
		{
			/// <summary> reference to the sun/star</summary>
			public Sim.SunData SunData => sunData; Sim.SunData sunData;

			/// <summary> normalized vector from vessel to sun</summary>
			public Vector3d Direction => direction; Vector3d direction;

			/// <summary> distance from vessel to sun surface</summary>
			public double Distance => distance; double distance;

			/// <summary>
			/// return 1.0 when the vessel is in direct sunlight, 0.0 when in shadow
			/// <para/> in analytic evaluation, this is a scalar of representing the fraction of time spent in sunlight
			/// </summary>
			// current limitations :
			// - the result is dependant on the vessel altitude at the time of evaluation, 
			//   consequently it gives inconsistent behavior with highly eccentric orbits
			// - this totally ignore the orbit inclinaison, polar orbits will be treated as equatorial orbits
			public double SunlightFactor => sunlightFactor; double sunlightFactor;

			/// <summary>
			/// solar flux at vessel position in W/m², include atmospheric absorption if inside an atmosphere (atmo_factor)
			/// <para/> zero when the vessel is in shadow while evaluation is non-analytic (low timewarp rates)
			/// <para/> in analytic evaluation, this include fractional sunlight / atmo absorbtion
			/// </summary>
			public double SolarFlux => solarFlux; double solarFlux;

			/// <summary>
			/// scalar for solar flux absorbtion by atmosphere at vessel position, not meant to be used directly (use solar_flux instead)
			/// <para/> if integrated over orbit (analytic evaluation), average atmospheric absorption factor over the daylight period (not the whole day)
			/// </summary>
			public double AtmoFactor => atmoFactor; double atmoFactor;

			/// <summary> proportion of this sun flux in the total flux at the vessel position (ignoring atmoshere and occlusion) </summary>
			public double FluxProportion => fluxProportion; double fluxProportion;

			/// <summary> similar to solar flux but doesn't account for atmo absorbtion nor occlusion</summary>
			private double rawSolarFlux;

			public SunInfo(Sim.SunData sunData)
			{
				this.sunData = sunData;
			}

			/// <summary>
			/// Update the 'sunsInfo' list and the 'mainSun', 'solarFluxTotal' variables.
			/// Uses discrete or analytic (for high timewarp speeds) evaluation methods based on the isAnalytic bool.
			/// Require the 'visibleBodies' variable to be set.
			/// </summary>
			// at the two highest timewarp speed, the number of sun visibility samples drop to the point that
			// the quantization error first became noticeable, and then exceed 100%, to solve this:
			// - we switch to an analytical estimation of the sunlight/shadow period
			// - atmo_factor become an average atmospheric absorption factor over the daylight period (not the whole day)
			public static void UpdateSunsInfo(Vessel v, VesselEnvironment ve, Vector3d vesselPosition, double elapsedSeconds)
			{
				double lastSolarFlux = 0.0;

				ve.sunsInfo = new List<SunInfo>(Sim.suns.Count);
				ve.solarFluxTotal = 0.0;
				ve.rawSolarFluxTotal = 0.0;

				foreach (Sim.SunData sunData in Sim.suns)
				{
					SunInfo sunInfo = new SunInfo(sunData);

					if (ve.isAnalytic)
					{
						// get sun direction and distance
						Lib.DirectionAndDistance(vesselPosition, sunInfo.sunData.body, out sunInfo.direction, out sunInfo.distance);
						// analytical estimation of the portion of orbit that was in sunlight.
						// it has some limitations, see the comments on Sim.ShadowPeriod

						if (Settings.UseSamplingSunFactor)
							// sampling estimation of the portion of orbit that is in sunlight
							// until we will calculate again
							sunInfo.sunlightFactor = Sim.SampleSunFactor(v, elapsedSeconds);

						else
							// analytical estimation of the portion of orbit that was in sunlight.
							// it has some limitations, see the comments on Sim.ShadowPeriod
							sunInfo.sunlightFactor = 1.0 - Sim.ShadowPeriod(v) / Sim.OrbitalPeriod(v);


						// get atmospheric absorbtion
						// for atmospheric bodies whose rotation period is less than 120 hours,
						// determine analytic atmospheric absorption over a single body revolution instead
						// of using a discrete value that would be unreliable at large timesteps :
						if (ve.inAtmosphere)
							sunInfo.atmoFactor = Sim.AtmosphereFactorAnalytic(v.mainBody, vesselPosition, sunInfo.direction);
						else
							sunInfo.atmoFactor = 1.0;
					}
					else
					{
						// determine if in sunlight, calculate sun direction and distance
						sunInfo.sunlightFactor = Sim.IsBodyVisible(v, vesselPosition, sunData.body, ve.visibleBodies, out sunInfo.direction, out sunInfo.distance) ? 1.0 : 0.0;
						// get atmospheric absorbtion
						sunInfo.atmoFactor = Sim.AtmosphereFactor(v.mainBody, vesselPosition, sunInfo.direction);
					}

					// get resulting solar flux in W/m²
					sunInfo.rawSolarFlux = sunInfo.sunData.SolarFlux(sunInfo.distance);
					sunInfo.solarFlux = sunInfo.rawSolarFlux * sunInfo.sunlightFactor * sunInfo.atmoFactor;
					// increment total flux from all stars
					ve.rawSolarFluxTotal += sunInfo.rawSolarFlux;
					ve.solarFluxTotal += sunInfo.solarFlux;
					// add the star to the list
					ve.sunsInfo.Add(sunInfo);
					// the most powerful star will be our "default" sun. Uses raw flux before atmo / sunlight factor
					if (sunInfo.rawSolarFlux > lastSolarFlux)
					{
						lastSolarFlux = sunInfo.rawSolarFlux;
						ve.mainSun = sunInfo;
					}
				}

				ve.sunlightFactor = 0.0;
				foreach (SunInfo sunInfo in ve.sunsInfo)
				{
					sunInfo.fluxProportion = sunInfo.rawSolarFlux / ve.rawSolarFluxTotal;
					ve.sunlightFactor += sunInfo.SunlightFactor * sunInfo.fluxProportion;
				}
				// avoid rounding errors
				if (ve.sunlightFactor > 0.99) ve.sunlightFactor = 1.0;
			}
		}

		/// <summary>
		/// [environment] true when timewarping faster at 10000x or faster. When true, some fields are updated more frequently
		/// and their evaluation is changed to an analytic, timestep-independant and vessel-position-independant mode.
		/// </summary>
		public bool IsAnalytic => isAnalytic; bool isAnalytic;

		/// <summary> [environment] true if inside ocean</summary>
		public bool Underwater => underwater; bool underwater;

		/// <summary> [environment] true if on the surface of a body</summary>
		public bool Landed => landed; bool landed;

		/// <summary> current atmospheric pressure in atm</summary>
		public double StaticPressure => envStaticPressure; double envStaticPressure;

		/// <summary> Is the vessel inside an atmosphere ?</summary>
		public bool InAtmosphere => inAtmosphere; bool inAtmosphere;

		/// <summary> Is the vessel inside a breatheable atmosphere ?</summary>
		public bool InOxygenAtmosphere => inOxygenAtmosphere; bool inOxygenAtmosphere;

		/// <summary> Is the vessel inside a breatheable atmosphere and at acceptable pressure conditions ?</summary>
		public bool InBreathableAtmosphere => inBreathableAtmosphere; bool inBreathableAtmosphere;

		/// <summary> [environment] true if in zero g</summary>
		public bool ZeroG => zeroG; bool zeroG;

		/// <summary> [environment] solar flux reflected from the nearest body</summary>
		public double AlbedoFlux => albedoFlux; double albedoFlux;

		/// <summary> [environment] infrared radiative flux from the nearest body</summary>
		public double BodyFlux => bodyFlux; double bodyFlux;

		/// <summary> [environment] total flux at vessel position</summary>
		public double TotalFlux => totalFlux; double totalFlux;

		/// <summary> [environment] temperature ar vessel position</summary>
		public double Temperature => temperature; double temperature;

		/// <summary> [environment] difference between environment temperature and survival temperature</summary>// 
		public double TempDiff => tempDiff; double tempDiff;

		/// <summary> [environment] radiation at vessel position</summary>
		public double Radiation => radiation; double radiation;

		/// <summary> [environment] radiation effective for habitats/EVAs</summary>
		public double HabitatRadiation => shieldedRadiation; double shieldedRadiation;

		/// <summary> [environment] true if vessel is inside a magnetopause (except the heliosphere)</summary>
		public bool Magnetosphere => magnetosphere; bool magnetosphere;

		/// <summary> [environment] true if vessel is inside a radiation belt</summary>
		public bool InnerBelt => innerBelt; bool innerBelt;

		/// <summary> [environment] true if vessel is inside a radiation belt</summary>
		public bool OuterBelt => outerBelt; bool outerBelt;

		/// <summary> [environment] true if vessel is outside sun magnetopause</summary>
		public bool Interstellar => interstellar; bool interstellar;

		/// <summary> [environment] true if the vessel is inside a magnetopause (except the sun) and under storm</summary>
		public bool Blackout => blackout; bool blackout;

		/// <summary> [environment] true if vessel is inside thermosphere</summary>
		public bool Thermosphere => thermosphere; bool thermosphere;

		/// <summary> [environment] true if vessel is inside exosphere</summary>
		public bool Exosphere => exosphere; bool exosphere;

		/// <summary> [environment] true if vessel currently experienced a solar storm</summary>
		public bool Storm => inStorm; bool inStorm;

		/// <summary> [environment] proportion of ionizing radiation not blocked by atmosphere</summary>
		public double GammaTransparency => gammaTransparency; double gammaTransparency;

		/// <summary> [environment] gravitation gauge particles detected (joke)</summary>
		public double Gravioli => gravioli; double gravioli;

		/// <summary> [environment] Bodies whose apparent diameter from the vessel POV is greater than ~10 arcmin (~0.003 radians)</summary>
		// real apparent diameters at earth : sun/moon =~ 30 arcmin, Venus =~ 1 arcmin
		public List<CelestialBody> VisibleBodies => visibleBodies; List<CelestialBody> visibleBodies;

		/// <summary> [environment] Sun that send the highest nominal solar flux (in W/m²) at vessel position</summary>
		public SunInfo MainSun => mainSun; SunInfo mainSun;

		/// <summary> [environment] Angle of the main sun on the body surface at vessel position</summary>
		public double SunBodyAngle => sunBodyAngle; double sunBodyAngle;

		/// <summary>
		///  [environment] total solar flux from all stars at vessel position in W/m², include atmospheric absorption if inside an atmosphere (atmo_factor)
		/// <para/> zero when the vessel is in shadow while evaluation is non-analytic (low timewarp rates)
		/// <para/> in analytic evaluation, this include fractional sunlight factor
		/// </summary>
		public double SolarFluxTotal => solarFluxTotal; double solarFluxTotal;

		/// <summary> similar to solar flux total but doesn't account for atmo absorbtion nor occlusion</summary>
		private double rawSolarFluxTotal;

		/// <summary> [environment] Average time spend in sunlight, including sunlight from all suns/stars. Each sun/star influence is pondered by its flux intensity</summary>
		public double SunlightFactor => sunlightFactor; double sunlightFactor;

		/// <summary> [environment] true if the vessel is currently in sunlight, or at least half the time when in analytic mode</summary>
		public bool InSunlight => sunlightFactor > 0.49;

		/// <summary> [environment] true if the vessel is currently in shadow, or least 90% of the time when in analytic mode</summary>
		// this threshold is also used to ignore light coming from distant/weak stars 
		public bool InFullShadow => sunlightFactor < 0.1;

		/// <summary> [environment] List of all stars/suns and the related data/calculations for the current vessel</summary>
		public List<SunInfo> SunsInfo => sunsInfo; List<SunInfo> sunsInfo;
	}
	*/
}
