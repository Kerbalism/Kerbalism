using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;


namespace KERBALISM
{

	// Add a specific environment reading to a part ui, and to the telemetry panel.
	public class Sensor : PartModule, ISpecifics
	{
		// config
		[KSPField(isPersistant = true)] public string type;   // type of telemetry provided
		[KSPField] public string pin = string.Empty;        // pin animation

		// status
		[KSPField(guiActive = true, guiName = "_")] public string Status;

		// animations
		Animator pin_anim;


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// create animator
			pin_anim = new Animator(part, pin);

			// setup ui
			Fields["Status"].guiName = Lib.UppercaseFirst(type);
		}


		public void Update()
		{
			// in flight
			if (Lib.IsFlight())
			{
				// get info from cache
				VesselData vd = vessel.KerbalismData();

				// do nothing if vessel is invalid
				if (!vd.IsValid) return;

				// update status
				Status = Telemetry_content(vessel, vd, type);

				// if there is a pin animation
				if (pin.Length > 0)
				{
					// still-play pin animation
					pin_anim.Still(Telemetry_pin(vessel, vd, type));
				}
			}
		}


		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info("Add telemetry readings to the part ui, and to the telemetry panel");
		}


		// specifics support
		public Specifics Specs()
		{
			var specs = new Specifics();
			specs.Add("Type", type);
			return specs;
		}


		// get readings value in [0,1] range, for pin animation
		public static double Telemetry_pin(Vessel v, VesselData vd, string type)
		{
			switch (type)
			{
				case "temperature": return Math.Min(vd.EnvTemperature / 11000.0, 1.0);
				case "radiation": return Math.Min(vd.EnvRadiation * 3600.0 / 11.0, 1.0);
				case "habitat_radiation": return Math.Min(vd.EnvHabitatRadiation * 3600.0 / 11.0, 1.0);
				case "pressure": return Math.Min(v.mainBody.GetPressure(v.altitude) / Sim.PressureAtSeaLevel() / 11.0, 1.0);
				case "gravioli": return Math.Min(vd.EnvGravioli, 1.0);
			}
			return 0.0;
		}

		// get readings value
		public static double Telemetry_value(Vessel v, VesselData vd, string type)
		{
			switch (type)
			{
				case "temperature": return vd.EnvTemperature;
				case "radiation": return vd.EnvRadiation;
				case "habitat_radiation": return vd.EnvHabitatRadiation;
				case "pressure": return v.mainBody.GetPressure(v.altitude);
				case "gravioli": return vd.EnvGravioli;
			}
			return 0.0;
		}

		// get readings short text info
		public static string Telemetry_content(Vessel v, VesselData vd, string type)
		{
			switch (type)
			{
				case "temperature": return Lib.HumanReadableTemp(vd.EnvTemperature);
				case "radiation": return Lib.HumanReadableRadiation(vd.EnvRadiation);
				case "habitat_radiation": return Lib.HumanReadableRadiation(vd.EnvHabitatRadiation);
				case "pressure": return Lib.HumanReadablePressure(v.mainBody.GetPressure(v.altitude));
				case "gravioli": return vd.EnvGravioli < 0.33 ? "nothing here" : vd.EnvGravioli < 0.66 ? "almost one" : "WOW!";
			}
			return string.Empty;
		}

		// get readings tooltip
		public static string Telemetry_tooltip(Vessel v, VesselData vd, string type)
		{
			switch (type)
			{
				case "temperature":
					return Lib.BuildString
					(
						"<align=left />",
						String.Format("{0,-14}\t<b>{1}</b>\n", "solar flux", Lib.HumanReadableFlux(vd.EnvSolarFluxTotal)),
						String.Format("{0,-14}\t<b>{1}</b>\n", "albedo flux", Lib.HumanReadableFlux(vd.EnvAlbedoFlux)),
						String.Format("{0,-14}\t<b>{1}</b>", "body flux", Lib.HumanReadableFlux(vd.EnvBodyFlux))
					);

				case "radiation":
					return string.Empty;

				case "habitat_radiation":
					return Lib.BuildString
					(
						"<align=left />",
						String.Format("{0,-14}\t<b>{1}</b>\n", "environment", Lib.HumanReadableRadiation(vd.EnvRadiation)),
						String.Format("{0,-14}\t<b>{1}</b>", "habitats", Lib.HumanReadableRadiation(vd.EnvHabitatRadiation))
					);

				case "pressure":
					return vd.EnvUnderwater
					  ? "inside <b>ocean</b>"
					  : vd.EnvInAtmosphere
					  ? Lib.BuildString("inside <b>atmosphere</b> (", vd.EnvBreathable ? "breathable" : "not breathable", ")")
					  : Sim.InsideThermosphere(v)
					  ? "inside <b>thermosphere</b>"
					  : Sim.InsideExosphere(v)
					  ? "inside <b>exosphere</b>"
					  : string.Empty;

				case "gravioli":
					return Lib.BuildString
					(
						"Gravioli detection events per-year: <b>", vd.EnvGravioli.ToString("F2"), "</b>\n\n",
						"<i>The elusive negative gravioli particle\nseems to be much harder to detect\n",
						"than expected. On the other\nhand there seems to be plenty\nof useless positive graviolis around.</i>"
					);
			}
			return string.Empty;
		}
	}


} // KERBALISM



