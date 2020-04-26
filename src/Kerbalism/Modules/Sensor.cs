using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	// Add a specific environment reading to a part ui, and to the telemetry panel.
	public class Sensor : PartModule, ISpecifics
	{
		// config
		[KSPField(isPersistant = true)] public string type;   // type of telemetry provided
		[KSPField] public string pin = string.Empty;        // pin animation

		// status
		[KSPField(guiActive = true, guiName = "_", groupName = "Sensors", groupDisplayName = "#KERBALISM_Group_Sensors", groupStartCollapsed = true)] public string Status;//Sensors

		// animations
		Animator pin_anim;

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// create animator
			pin_anim = new Animator(part, pin);

			// setup ui
			Fields["Status"].guiName = Lib.SpacesOnCaps(Lib.SpacesOnUnderscore(type));
		}


		public void Update()
		{
			// in flight
			if (Lib.IsFlight)
			{
				// get info from cache
				vessel.TryGetVesselData(out VesselData vd);

				// do nothing if vessel is invalid
				if (!vd.IsSimulated) return;

				// update status
				Status = Telemetry_content(vessel, vd, type);

				// if there is a pin animation
				if (pin.Length > 0)
				{
					// still-play pin animation
					pin_anim.Still((float)Telemetry_pin(vessel, vd, type));
				}
			}
		}


		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}

		// specifics support
		public Specifics Specs()
		{
			var specs = new Specifics();
			specs.Add(Local.Sensor_Type, type);//"Type"
			return specs;
		}


		// get readings value in [0,1] range, for pin animation
		public static double Telemetry_pin(Vessel v, VesselData vd, string type)
		{
			switch (type)
			{
				case "temperature": return Math.Min(vd.EnvTemperature / 11000.0, 1.0);
				case "radiation": return Math.Min(vd.EnvRadiation * 3600.0 / 11.0, 1.0);
				case "habitat_radiation": return Math.Min(HabitatRadiation(vd) * 3600.0 / 11.0, 1.0);
				case "pressure": return Math.Min(v.mainBody.GetPressure(v.altitude) / Sim.PressureAtSeaLevel / 11.0, 1.0);
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
				case "habitat_radiation": return HabitatRadiation(vd);
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
				case "habitat_radiation": return Lib.HumanReadableRadiation(HabitatRadiation(vd));
				case "pressure": return Lib.HumanReadablePressure(v.mainBody.GetPressure(v.altitude));
				case "gravioli": return vd.EnvGravioli < 0.33 ? Local.Sensor_shorttextinfo1 : vd.EnvGravioli < 0.66 ? Local.Sensor_shorttextinfo2 : Local.Sensor_shorttextinfo3;//"nothing here""almost one""WOW!"
			}
			return string.Empty;
		}

		private static double HabitatRadiation(VesselData vd)
		{
			return vd.Habitat.radiationRate;
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
						String.Format("{0,-14}\t<b>{1}</b>\n", Local.Sensor_solarflux, Lib.HumanReadableFlux(vd.EnvSolarFluxTotal)),//"solar flux"
						String.Format("{0,-14}\t<b>{1}</b>\n", Local.Sensor_albedoflux, Lib.HumanReadableFlux(vd.EnvAlbedoFlux)),//"albedo flux"
						String.Format("{0,-14}\t<b>{1}</b>", Local.Sensor_bodyflux, Lib.HumanReadableFlux(vd.EnvBodyFlux))//"body flux"
					);

				case "radiation":
					return string.Empty;

				case "habitat_radiation":
					return Lib.BuildString
					(
						"<align=left />",
						String.Format("{0,-14}\t<b>{1}</b>\n", Local.Sensor_environment, Lib.HumanReadableRadiation(vd.EnvRadiation, false)),//"environment"
						String.Format("{0,-14}\t<b>{1}</b>", Local.Sensor_habitats, Lib.HumanReadableRadiation(HabitatRadiation(vd), false))//"habitats"
					);

				case "pressure":
					return vd.EnvUnderwater
					  ? Local.Sensor_insideocean//"inside <b>ocean</b>"
					  : vd.EnvInAtmosphere
					  ? Local.Sensor_insideatmosphere.Format(vd.EnvInBreathableAtmosphere ? Local.Sensor_breathable : Local.Sensor_notbreathable)//"breathable""not breathable"                  //Lib.BuildString("inside <b>atmosphere</b> (", vd.EnvBreathable ? "breathable" : "not breathable", ")")
					  : Sim.InsideThermosphere(v)
					  ? Local.Sensor_insidethermosphere//"inside <b>thermosphere</b>""
					  : Sim.InsideExosphere(v)
					  ? Local.Sensor_insideexosphere//"inside <b>exosphere</b>"
					  : string.Empty;

				case "gravioli":
					return Lib.BuildString
					(
						Local.Sensor_Graviolidetection + " <b>" + vd.EnvGravioli.ToString("F2") + "</b>\n\n",//"Gravioli detection events per-year: 
						"<i>", Local.Sensor_info1, "\n",//The elusive negative gravioli particle\nseems to be much harder to detect than expected.
						Local.Sensor_info2, "</i>"//" On the other\nhand there seems to be plenty\nof useless positive graviolis around."
					);
			}
			return string.Empty;
		}
	}


} // KERBALISM



