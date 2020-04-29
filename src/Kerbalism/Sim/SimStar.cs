using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class SimStar
	{
		public CelestialBody body;
		private double solarFluxAtAU;
		private double solarFluxTotal;

		public SimStar(CelestialBody body, double solarFluxAtAU)
		{
			this.body = body;
			this.solarFluxAtAU = solarFluxAtAU;
		}

		// This must be called after the "stars" list is populated (because it use AU > GetParentStar)
		public void InitSolarFluxTotal()
		{
			solarFluxTotal = solarFluxAtAU * Sim.AU * Sim.AU * Math.PI * 4.0;
		}

		/// <summary>Luminosity in W/m² at the given distance from this sun/star</summary>
		/// <param name="distanceIsFromSunSurface">set to true if 'distance' is from the surface</param>
		public double SolarFlux(double distance, bool distanceIsFromStarSurface = false)
		{
			if (distanceIsFromStarSurface) distance += body.Radius;

			return solarFluxTotal / (Math.PI * 4 * distance * distance);
		}
	}
}
