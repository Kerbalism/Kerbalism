using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{



	public static class Storm
	{
		public static float sun_observation_quality = 1.0f;

		public static void Update(CelestialBody body, double elapsed_s)
		{
			// do nothing if storms are disabled
			if (!Features.SpaceWeather) return;

			// skip the sun
			if (Lib.IsSun(body)) return;

			// skip moons
			// note: referenceBody is never null here
			if (!Lib.IsSun(body.referenceBody)) return;

			// get body data
			BodyData bd = DB.Body(body.name);

			// generate storm time if necessary
			if (bd.storm_time <= double.Epsilon)
			{
				bd.storm_time = PreferencesStorm.Instance.StormMinTime + (PreferencesStorm.Instance.StormMaxTime - PreferencesStorm.Instance.StormMinTime) * Lib.RandomDouble();
			}

			// accumulate age
			bd.storm_age += elapsed_s * Storm_frequency(body.orbit.semiMajorAxis);

			// if storm is over
			if (bd.storm_age > bd.storm_time)
			{
				bd.storm_age = 0.0;
				bd.storm_time = 0.0;
				bd.storm_state = 0;
			}
			// if storm is in progress
			else if (bd.storm_age > bd.storm_time - PreferencesStorm.Instance.StormDuration)
			{
				bd.storm_state = 2;
			}
			// if storm is incoming
			else if (bd.storm_age > bd.storm_time - PreferencesStorm.Instance.StormDuration - Time_to_impact(body.orbit.semiMajorAxis))
			{
				bd.storm_state = 1;
			}

			// send messages
			// note: separed from state management to support the case when the user enter the SOI of a body under storm or about to be hit
			if (bd.msg_storm < 2 && bd.storm_state == 2)
			{
				if (Body_is_relevant(body))
				{
					Message.Post(Severity.danger, Lib.BuildString("The coronal mass ejection hit <b>", body.name, "</b> system"),
					  Lib.BuildString("Storm duration: ", Lib.HumanReadableDuration(TimeLeftCME(bd.storm_time, bd.storm_age))));
				}
				bd.msg_storm = 2;
			}
			else if (bd.msg_storm < 1 && bd.storm_state == 1)
			{
				// show warning message only if you're lucky...
				if(Lib.RandomFloat() < sun_observation_quality)
				{
					if (Body_is_relevant(body))
					{
						Message.Post(Severity.warning, Lib.BuildString("Our observatories report a coronal mass ejection directed toward <b>", body.name, "</b> system"),
						  Lib.BuildString("Time to impact: ", Lib.HumanReadableDuration(TimeBeforeCME(bd.storm_time, bd.storm_age))));
					}
				}
				bd.msg_storm = 1;
			}
			else if (bd.msg_storm > 1 && bd.storm_state == 0)
			{
				if (Body_is_relevant(body))
				{
					Message.Post(Severity.relax, Lib.BuildString("The solar storm at <b>", body.name, "</b> system is over"));
				}
				bd.msg_storm = 0;
			}
		}


		public static void Update(Vessel v, VesselData vd, double elapsed_s)
		{
			// do nothing if storms are disabled
			if (!Features.SpaceWeather) return;

			// only consider vessels in interplanetary space
			if (!Lib.IsSun(v.mainBody)) return;

			// skip unmanned vessels
			if (vd.CrewCount == 0) return;

			// generate storm time if necessary
			if (vd.storm_time <= double.Epsilon)
			{
				vd.storm_time = PreferencesStorm.Instance.StormMinTime + (PreferencesStorm.Instance.StormMaxTime - PreferencesStorm.Instance.StormMinTime) * Lib.RandomDouble();
			}

			// accumulate age
			vd.storm_age += elapsed_s * Storm_frequency(vd.EnvMainSun.Distance);

			// if storm is over
			if (vd.storm_age > vd.storm_time && vd.storm_state == 2)
			{
				vd.storm_age = 0.0;
				vd.storm_time = 0.0;
				vd.storm_state = 0;

				// send message
				Message.Post(Severity.relax, Lib.BuildString("The solar storm around <b>", v.vesselName, "</b> is over"));

				vd.msg_signal = false; // used to avoid sending 'signal is back' messages en-masse after the storm is over
			}
			// if storm is in progress
			else if (vd.storm_age > vd.storm_time - PreferencesStorm.Instance.StormDuration && vd.storm_state == 1)
			{
				vd.storm_state = 2;

				// send message
				Message.Post(Severity.danger, Lib.BuildString("The coronal mass ejection hit <b>", v.vesselName, "</b>"),
				Lib.BuildString("Storm duration: ", Lib.HumanReadableDuration(TimeLeftCME(vd.storm_time, vd.storm_age))));
			}
			// if storm is incoming
			else if (vd.storm_age > vd.storm_time - PreferencesStorm.Instance.StormDuration - Time_to_impact(vd.EnvMainSun.Distance) && vd.storm_state == 0)
			{
				vd.storm_state = 1;

				// send message
				Message.Post(Severity.warning, Lib.BuildString("Our observatories report a coronal mass ejection directed toward <b>", v.vesselName, "</b>"),
				Lib.BuildString("Time to impact: ", Lib.HumanReadableDuration(TimeBeforeCME(vd.storm_time, vd.storm_age))));
			}
		}


		// return storm frequency factor by distance from sun
		static double Storm_frequency(double dist)
		{
			double AU = Lib.GetParentPlanet(FlightGlobals.GetHomeBody()).orbit.semiMajorAxis;
			return AU / dist;
		}


		// return time to impact from CME event, in seconds
		static double Time_to_impact(double dist)
		{
			return dist / PreferencesStorm.Instance.StormEjectionSpeed;
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
				if (Lib.GetParentPlanet(v.mainBody) == body)
				{
					// get info from the cache
					VesselData vd = v.KerbalismData();

					// skip invalid vessels
					if (!vd.IsValid) continue;

					// obey message config
					if (!v.KerbalismData().cfg_storm) continue;

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
			if (!Features.SpaceWeather) return true;

			// skip the sun
			if (Lib.IsSun(body)) return true;

			// skip moons
			// note: referenceBody is never null here
			if (!Lib.IsSun(body.referenceBody)) return true;

			// do not skip the body
			return false;
		}


		// return true if a storm is incoming
		public static bool Incoming(Vessel v)
		{
			// if in interplanetary space
			if (Lib.IsSun(v.mainBody))
			{
				return v.KerbalismData().storm_state == 1;
			}
			// if inside a planetary system
			else
			{
				return DB.Body(Lib.GetParentPlanet(v.mainBody).name).storm_state == 1;
			}
		}


		// return true if a storm is in progress
		public static bool InProgress(Vessel v)
		{
			// if in interplanetary space
			if (Lib.IsSun(v.mainBody))
			{
				return v.KerbalismData().storm_state == 2;
			}
			// if inside a planetary system
			else
			{
				return DB.Body(Lib.GetParentPlanet(v.mainBody).name).storm_state == 2;
			}
		}


		// return true if a storm just ended
		// used to avoid sending 'signal is back' messages en-masse after the storm is over
		// - delta_time: time between calls to this function
		public static bool JustEnded(Vessel v, double delta_time)
		{
			// if in interplanetary space
			if (Lib.IsSun(v.mainBody))
			{
				return v.KerbalismData().storm_age < delta_time * 2.0;
			}
			// if inside a planetary system
			else
			{
				return DB.Body(Lib.GetParentPlanet(v.mainBody).name).storm_age < delta_time * 2.0;
			}
		}


		// return time left until CME impact
		static double TimeBeforeCME(double storm_time, double storm_age)
		{
			return Math.Max(0.0, storm_time - storm_age - PreferencesStorm.Instance.StormDuration);
		}


		// return time left until CME impact
		public static double TimeBeforeCME(Vessel v)
		{
			// if in interplanetary space
			if (Lib.IsSun(v.mainBody))
			{
				VesselData vd = v.KerbalismData();
				return TimeBeforeCME(vd.storm_time, vd.storm_age);
			}
			// if inside a planetary system
			else
			{
				BodyData bd = DB.Body(Lib.GetParentPlanet(v.mainBody).name);
				return TimeBeforeCME(bd.storm_time, bd.storm_age);
			}
		}


		// return time left until CME is over
		static double TimeLeftCME(double storm_time, double storm_age)
		{

			// get a pseudo-random number ranging from 0 to 1. pseudo-random because it needs
			// to be the same number every time we get a remaining time for the same storm
			double uncertainity = (storm_time % 100) / 100.0;

			// allow under-estimation of storm duration, but tend towards over-estimation
			uncertainity -= 0.3;

			// accurate sun observation -> less uncertainity
			uncertainity *= (1.0 - sun_observation_quality);

			double timeLeft = Math.Max(0.0, storm_time - storm_age);

			// add uncertainity to returned value
			timeLeft += uncertainity * PreferencesStorm.Instance.StormMaxTime;

			return timeLeft;
		}


		// return time left until CME is over
		public static double TimeLeftCME(Vessel v)
		{
			// if in interplanetary space
			if (Lib.IsSun(v.mainBody))
			{
				VesselData vd = v.KerbalismData();
				return TimeLeftCME(vd.storm_time, vd.storm_age);
			}
			// if inside a planetary system
			else
			{
				BodyData bd = DB.Body(Lib.GetParentPlanet(v.mainBody).name);
				return TimeLeftCME(bd.storm_time, bd.storm_age);
			}
		}
	}


} // KERBALISM
