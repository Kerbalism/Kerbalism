using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class StarFlux
	{
		/// <summary> reference to the sun/star</summary>
		public SimStar Star => star; SimStar star;

		/// <summary> normalized vector from vessel to sun</summary>
		public Vector3d direction;

		/// <summary> distance from vessel to sun center</summary>
		public double distance;

		/// <summary>
		/// return 1.0 when the vessel is in direct sunlight, 0.0 when in shadow
		/// <para/> in analytic evaluation, this is a scalar of representing the fraction of time spent in sunlight
		/// </summary>
		public double sunlightFactor;

		/// <summary> direct solar irradiance at vessel position in W/m², including atmospheric absorption </summary>
		public double directFlux;

		/// <summary> direct solar irradiance at vessel position in W/m², ignoring atmospheric absorption and occlusion</summary>
		public double directRawFlux;

		/// <summary> indirect irradiance from neighbouring bodies albedo at vessel position in W/m²</summary>
		public double bodiesAlbedoFlux;

		/// <summary> indirect irradiance from neighbouring bodies re-emissions in W/m²</summary>
		public double bodiesEmissiveFlux;

		/// <summary> proportion of this sun flux in the total flux at the vessel position (ignoring atmosphere and occlusion) </summary>
		public double directRawFluxProportion;

		public StarFlux(SimStar star)
		{
			this.star = star;
		}

		public static StarFlux[] StarArrayFactory()
		{
			StarFlux[] stars = new StarFlux[Sim.stars.Count];
			for (int i = 0; i < Sim.stars.Count; i++)
			{
				stars[i] = new StarFlux(Sim.stars[i]);
			}
			return stars;
		}

		public void Reset()
		{
			sunlightFactor = 0.0;
			directFlux = 0.0;
			directRawFlux = 0.0;
			bodiesAlbedoFlux = 0.0;
			bodiesEmissiveFlux = 0.0;
			directRawFluxProportion = 0.0;
		}
	}
}
