using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
    public static class Storm
    {
        public readonly static Dictionary<int, float> sunObservationQuality = new Dictionary<int, float>();

        internal static void CreateStorm(StormData bd, CelestialBody body, double distanceToSun)
        {
            // do nothing if storms are disabled
            if (!Features.Radiation) return;

            var now = Planetarium.GetUniversalTime();

            if (bd.storm_generation < now)
            {
                var sun = Sim.GetParentStar(body);
                var avgDuration = PreferencesRadiation.Instance.AvgStormDuration;

                // retry after 5 * average storm duration + jitter (to avoid recalc spikes)
                bd.storm_generation = now + avgDuration * 5 + avgDuration * Lib.RandomDouble();

                var rb = Radiation.Info(sun);
                var activity = rb.solar_cycle > 0 ? rb.SolarActivity() : 1.0;

                if (Lib.RandomDouble() < activity * PreferencesRadiation.Instance.stormFrequency)
                {
                    // storm duration depends on current solar activity
                    bd.storm_duration = avgDuration / 2.0 + avgDuration * activity * 2;

                    // if further out, the storm lasts longer (but is weaker)
                    bd.storm_duration /= Storm_frequency(distanceToSun);

                    // set a start time to give enough time for warning
                    bd.storm_time = now + Time_to_impact(distanceToSun);

                    // delay next storm generation by duration of this one
                    bd.storm_generation += bd.storm_duration;

                    // add a random error to the estimated storm duration if we don't observe the sun too well
                    var error = bd.storm_duration * 3 * Lib.RandomDouble() * (1 - SunObservationQuality(sun));
                    bd.displayed_duration = bd.storm_duration + error;

                    // show warning message only if you're lucky...
                    bd.display_warning = Lib.RandomFloat() < SunObservationQuality(sun);


#if DEBUG_RADIATION
					Lib.Log("Storm on " + body + " will start in " + Lib.HumanReadableDuration(bd.storm_time - now) + " and last for " + Lib.HumanReadableDuration(bd.storm_duration));
				}
				else
				{
					Lib.Log("No storm on " + body + ", will retry in " + Lib.HumanReadableDuration(bd.storm_generation - now));
#endif
                }
            }

            if (bd.storm_time + bd.storm_duration < now)
            {
                // storm is over
                bd.Reset();
            }
            else if (bd.storm_time < now && bd.storm_time + bd.storm_duration > now)
            {
                // storm in progress
                bd.storm_state = 2;
            }
            else if (bd.storm_time > now)
            {
                // storm incoming
                bd.storm_state = 1;
            }
        }

        public static void Update(CelestialBody body, double elapsed_s)
        {
			// do nothing if storms are disabled
			if (!Features.Radiation) return;

            StormData bd = DB.Storm(body.name);
            CreateStorm(bd, body, body.orbit.semiMajorAxis);

            // send messages

            if (Body_is_relevant(body))
            {
                switch (bd.storm_state)
                {
                    case 2:
                        if (bd.msg_storm < 2)
                        {
							Message.Post(Severity.danger, Local.Storm_msg1.Format("<b>" + body.displayName + "</b>"),//"The coronal mass ejection hit <<1>> system  //Lib.BuildString( )
								Lib.BuildString(Local.Storm_msg1text, " ", Lib.HumanReadableDuration(bd.displayed_duration)));//"Storm duration:"
                        }
                        break;

                    case 1:
                        if (bd.msg_storm < 1 && bd.display_warning)
                        {
                            var tti = bd.storm_time - Planetarium.GetUniversalTime();
							Message.Post(Severity.warning, Local.Storm_msg2.Format("<b>" + body.displayName + "</b>"),//Lib.BuildString("Our observatories report a coronal mass ejection directed toward <<1>> system")
								Lib.BuildString(Local.Storm_msg2text," ", Lib.HumanReadableDuration(tti)));//"Time to impact:"
                        }
                        break;

                    case 0:
                        if (bd.msg_storm == 2)
                        {
                            Message.Post(Severity.relax, Local.Storm_msg3.Format("<b>" + body.displayName + "</b>"));//Lib.BuildString("The solar storm at <<1>> system is over")
                        }
                        break;
                }
            }

            bd.msg_storm = bd.storm_state;
        }

        public static void Update(Vessel v, VesselData vd, double elapsed_s)
        {
            // do nothing if storms are disabled
            if (!Features.Radiation) return;

            // only consider vessels in interplanetary space
            if (!Sim.IsStar(v.mainBody)) return;

			// disregard EVAs
			if (v.isEVA) return;

            var bd = vd.stormData;
            CreateStorm(bd, v.mainBody, vd.MainStar.distance);

            if (vd.cfg_storm)
            {
                switch (bd.storm_state)
                {
                    case 0: // no storm
                        if (bd.msg_storm == 2)
                        {
							// send message
							Message.Post(Severity.relax, Local.Storm_msg4.Format("<b>" + v.vesselName + "</b>"));//Lib.BuildString("The solar storm around <<1>> is over")
                            vd.msg_signal = false; // used to avoid sending 'signal is back' messages en-masse after the storm is over
                        }
                        break;

                    case 2: // storm in progress
                        if (bd.msg_storm < 2)
                        {
							Message.Post(Severity.danger, Local.Storm_msg5.Format("<b>" + v.vesselName + "</b>"),//Lib.BuildString("The coronal mass ejection hit <<1>>)
							  Lib.BuildString(Local.Storm_msg1text, " ", Lib.HumanReadableDuration(bd.displayed_duration)));//"Storm duration:"
                        }
                        break;

                    case 1: // storm incoming
                        if (bd.msg_storm < 1 && bd.display_warning)
                        {
                            var tti = bd.storm_time - Planetarium.GetUniversalTime();
							Message.Post(Severity.warning, Local.Storm_msg6.Format("<b>" + v.vesselName + "</b>"),//Lib.BuildString("Our observatories report a coronal mass ejection directed toward <<1>>)
								Lib.BuildString(Local.Storm_msg2text, " ", Lib.HumanReadableDuration(tti)));//"Time to impact:
                        }
						break;
                }
            }
            bd.msg_storm = bd.storm_state;
        }


        // return storm frequency factor by distance from sun
        static double Storm_frequency(double dist)
        {
            return Sim.AU / dist;
        }


        // return time to impact from CME event, in seconds
        static double Time_to_impact(double dist)
        {
            return dist / PreferencesRadiation.Instance.StormEjectionSpeed;
        }


        // return true if body is relevant to the player
        // - body: reference body of the planetary system
        static bool Body_is_relevant(CelestialBody body)
        {
            // [disabled]
            // special case: home system is always relevant
            // note: we deal with the case of a planet mod setting homebody as a moon
            //if (body == Lib.PlanetarySystem(FlightGlobals.GetHomeBody())) return true;

            // for each vessel
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                // if inside the system
                if (Sim.GetParentPlanet(v.mainBody) == body)
                {
					// get info from the cache
					if (!v.TryGetVesselData(out VesselData vd))
						continue;

                    // skip invalid vessels
                    if (!vd.IsSimulated) continue;

                    // obey message config
                    if (!vd.cfg_storm) continue;

                    // body is relevant
                    return true;
                }
            }
            return false;
        }


        // used by the engine to update one body per-step
        public static bool Skip_body(CelestialBody body)
        {
            // skip all bodies if storms are disabled
            if (!Features.Radiation) return true;

            // skip the sun
            if (Sim.IsStar(body)) return true;

            // skip moons
            // note: referenceBody is never null here
            if (!Sim.IsStar(body.referenceBody)) return true;

            // do not skip the body
            return false;
        }

        /// <summary>return true if a storm is incoming</summary>
        public static bool Incoming(Vessel v)
        {
			v.TryGetVesselDataTemp(out VesselData vd);
			var bd = Sim.IsStar(v.mainBody) ? vd.stormData : DB.Storm(Sim.GetParentPlanet(v.mainBody).name);
            return bd.storm_state == 1 && bd.display_warning;
        }

        /// <summary>return true if a storm is in progress</summary>
        public static bool InProgress(Vessel v)
        {
			v.TryGetVesselDataTemp(out VesselData vd);
			var bd = Sim.IsStar(v.mainBody) ? vd.stormData : DB.Storm(Sim.GetParentPlanet(v.mainBody).name);
            return bd.storm_state == 2;
        }

		internal static float SunObservationQuality(CelestialBody sun)
		{
            if (!sunObservationQuality.ContainsKey(sun.flightGlobalsIndex))
                sunObservationQuality[sun.flightGlobalsIndex] = 1;
            return sunObservationQuality[sun.flightGlobalsIndex];
		}

		internal static void SetSunObservationQuality(CelestialBody sun, float quality)
		{
            sunObservationQuality[sun.flightGlobalsIndex] = quality;
		}
	}

} // KERBALISM
