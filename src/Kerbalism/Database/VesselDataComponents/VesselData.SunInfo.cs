using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public partial class VesselData : VesselDataBase
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
			public static void UpdateSunsInfo(VesselData vd, Vector3d vesselPosition, double elapsedSeconds)
			{
				Vessel v = vd.Vessel;
				double lastSolarFlux = 0.0;

				vd.sunsInfo = new List<SunInfo>(Sim.suns.Count);
				vd.solarFluxTotal = 0.0;
				vd.rawSolarFluxTotal = 0.0;

				foreach (Sim.SunData sunData in Sim.suns)
				{
					SunInfo sunInfo = new SunInfo(sunData);

					if (vd.isAnalytic)
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
						if (vd.inAtmosphere)
							sunInfo.atmoFactor = Sim.AtmosphereFactorAnalytic(v.mainBody, vesselPosition, sunInfo.direction);
						else
							sunInfo.atmoFactor = 1.0;
					}
					else
					{
						// determine if in sunlight, calculate sun direction and distance
						sunInfo.sunlightFactor = Sim.IsBodyVisible(v, vesselPosition, sunData.body, vd.visibleBodies, out sunInfo.direction, out sunInfo.distance) ? 1.0 : 0.0;
						// get atmospheric absorbtion
						sunInfo.atmoFactor = Sim.AtmosphereFactor(v.mainBody, vesselPosition, sunInfo.direction);
					}

					// get resulting solar flux in W/m²
					sunInfo.rawSolarFlux = sunInfo.sunData.SolarFlux(sunInfo.distance);
					sunInfo.solarFlux = sunInfo.rawSolarFlux * sunInfo.sunlightFactor * sunInfo.atmoFactor;
					// increment total flux from all stars
					vd.rawSolarFluxTotal += sunInfo.rawSolarFlux;
					vd.solarFluxTotal += sunInfo.solarFlux;
					// add the star to the list
					vd.sunsInfo.Add(sunInfo);
					// the most powerful star will be our "default" sun. Uses raw flux before atmo / sunlight factor
					if (sunInfo.rawSolarFlux > lastSolarFlux)
					{
						lastSolarFlux = sunInfo.rawSolarFlux;
						vd.mainSun = sunInfo;
					}
				}

				vd.sunlightFactor = 0.0;
				foreach (SunInfo sunInfo in vd.sunsInfo)
				{
					sunInfo.fluxProportion = sunInfo.rawSolarFlux / vd.rawSolarFluxTotal;
					vd.sunlightFactor += sunInfo.SunlightFactor * sunInfo.fluxProportion;
				}
				// avoid rounding errors
				if (vd.sunlightFactor > 0.99) vd.sunlightFactor = 1.0;
			}
		}
	}


}
