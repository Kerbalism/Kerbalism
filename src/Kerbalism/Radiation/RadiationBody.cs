using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	// store data about radiation for a body
	public class RadiationBody
	{
		// ctor: default
		public RadiationBody(CelestialBody body)
		{
			this.model = RadiationModel.none;
			this.body = body;
			this.reference = 0;
			this.geomagnetic_pole = new Vector3(0.0f, 1.0f, 0.0f);
		}

		// ctor: deserialize
		public RadiationBody(ConfigNode node, Dictionary<string, RadiationModel> models, CelestialBody body)
		{
			name = Lib.ConfigValue(node, "name", "");
			radiation_inner = Lib.ConfigValue(node, "radiation_inner", 0.0) / 3600.0;
			radiation_inner_gradient = Lib.ConfigValue(node, "radiation_inner_gradient", 3.3);
			radiation_outer = Lib.ConfigValue(node, "radiation_outer", 0.0) / 3600.0;
			radiation_outer_gradient = Lib.ConfigValue(node, "radiation_outer_gradient", 2.2);
			radiation_pause = Lib.ConfigValue(node, "radiation_pause", 0.0) / 3600.0;
			radiation_surface = Lib.ConfigValue(node, "radiation_surface", -1.0) / 3600.0;
			solar_cycle = Lib.ConfigValue(node, "solar_cycle", -1.0);
			solar_cycle_offset = Lib.ConfigValue(node, "solar_cycle_offset", 0.0);
			geomagnetic_pole_lat = Lib.ConfigValue(node, "geomagnetic_pole_lat", 90.0f);
			geomagnetic_pole_lon = Lib.ConfigValue(node, "geomagnetic_pole_lon", 0.0f);
			geomagnetic_offset = Lib.ConfigValue(node, "geomagnetic_offset", 0.0f);
			reference = Lib.ConfigValue(node, "reference", Lib.GetParentSun(body).flightGlobalsIndex);

			// get the radiation environment
			if (!models.TryGetValue(Lib.ConfigValue(node, "radiation_model", ""), out model)) model = RadiationModel.none;

			// get the body
			this.body = body;

			float lat = (float)(geomagnetic_pole_lat * Math.PI / 180.0);
			float lon = (float)(geomagnetic_pole_lon * Math.PI / 180.0);

			float x = Mathf.Cos(lat) * Mathf.Cos(lon);
			float y = Mathf.Sin(lat);
			float z = Mathf.Cos(lat) * Mathf.Sin(lon);
			geomagnetic_pole = new Vector3(x, y, z).normalized;

			if (Lib.IsSun(body))
			{
				// suns without a solar cycle configuration default to a cycle of 6 years
				// (set to 0 if you really want none)
				if (solar_cycle < 0)
					solar_cycle = Lib.SecondsInYearExact * 6;

				// add a rather nominal surface radiation for suns that have no config
				// for comparison: the stock kerbin sun has a surface radiation of 47 rad/h, which gives 0.01 rad/h near Kerbin
				// (set to 0 if you really want none)
				if (radiation_surface < 0)
					radiation_surface = 10.0 * 3600.0;
			}

			// calculate point emitter strength r0 at center of body
			if (radiation_surface > 0)
				radiation_r0 = radiation_surface * 4 * Math.PI * body.Radius * body.Radius;
		}

		public string name;            // name of the body
		public double radiation_inner; // rad/h inside inner belt
		public double radiation_inner_gradient; // how quickly the radiation rises as you go deeper into the belt
		public double radiation_outer; // rad/h inside outer belt
		public double radiation_outer_gradient; // how quickly the radiation rises as you go deeper into the belt
		public double radiation_pause; // rad/h inside magnetopause
		public double radiation_surface; // rad/h of gamma radiation on the surface
		public double radiation_r0 = 0.0; // rad/h of gamma radiation at the center of the body (calculated from radiation_surface)
		public double solar_cycle;     // interval time of solar activity (11 years for sun)
		public double solar_cycle_offset; // time to add to the universal time when calculating the cycle, used to have cycles that don't start at 0
		public int reference;          // index of the body that determine x-axis of the gsm-space
		public float geomagnetic_pole_lat = 90.0f;
		public float geomagnetic_pole_lon = 0.0f;
		public float geomagnetic_offset = 0.0f;
		public Vector3 geomagnetic_pole;

		public bool inner_visible = true;
		public bool outer_visible = true;
		public bool pause_visible = true;

		// shortcut to the radiation environment
		public RadiationModel model;

		// shortcut to the body
		public CelestialBody body;

		/// <summary> Returns the magnetopause radiation level, accounting for solar activity cycles </summary>
		public double RadiationPause()
		{
			if (solar_cycle > 0)
			{
				return radiation_pause + radiation_pause * 0.2 * SolarActivity();
			}
			return radiation_pause;
		}

		/// <summary> Return a number [-0.15 .. 1.05] that represents current solar activity, clamped to [0 .. 1] if clamp is true </summary>
		public double SolarActivity(bool clamp = true)
		{
			if (solar_cycle <= 0) return 0;

			var t = (solar_cycle_offset + Planetarium.GetUniversalTime()) / solar_cycle * 2 * Math.PI; // Math.Sin/Cos works with radians

			// this gives a pseudo-erratic curve, see https://www.desmos.com/calculator/q5flvzvxia
			// in range -0.15 .. 1.05
			var r = (-Math.Cos(t) + Math.Sin(t * 75) / 5 + 0.9) / 2.0;
			if (clamp) r = Lib.Clamp(r, 0.0, 1.0);
			return r;
		}
	}

}
