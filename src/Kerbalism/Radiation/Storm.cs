using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public static class Storm
	{
		/// <summary>Global observatory quality [0..1]. Affects CME warning chance and displayed duration error.</summary>
		public static float sun_observation_quality = 1.0f;

		/// <summary>
		/// Companion stars whose flux at the target is at least this fraction of the primary are also
		/// allowed to generate CMEs (binary / multi-star packs).
		/// </summary>
		const double MinCompanionFluxFraction = 0.05;

		/// <summary>
		/// Generate / advance a storm delivered from <paramref name="sourceStar"/> over <paramref name="distanceToSun"/>.
		/// </summary>
		internal static void CreateStorm(StormData bd, CelestialBody sourceStar, double distanceToSun)
		{
			// do nothing if storms are disabled
			if (!Features.SpaceWeather) return;

			var now = Planetarium.GetUniversalTime();

			if (bd.storm_generation < now)
			{
				var avgDuration = PreferencesRadiation.Instance.AvgStormDuration;

				// retry after 5 * average storm duration + jitter (to avoid recalc spikes)
				bd.storm_generation = now + avgDuration * 5 + avgDuration * Lib.RandomDouble();

				var rb = Radiation.Info(sourceStar);
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
					var error = bd.storm_duration * 3 * Lib.RandomDouble() * (1 - sun_observation_quality);
					bd.displayed_duration = bd.storm_duration + error;

					// show warning message only if you're lucky...
					bd.display_warning = Lib.RandomFloat() < sun_observation_quality;


#if DEBUG_RADIATION
					Lib.Log("Storm from " + sourceStar + " will start in " + Lib.HumanReadableDuration(bd.storm_time - now) + " and last for " + Lib.HumanReadableDuration(bd.storm_duration));
				}
				else
				{
					Lib.Log("No storm from " + sourceStar + ", will retry in " + Lib.HumanReadableDuration(bd.storm_generation - now));
#endif
				}
			}

			AdvanceStormState(bd, now);
		}

		static void AdvanceStormState(StormData bd, double now)
		{
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
			if (!Features.SpaceWeather) return;

			foreach (CelestialBody star in RelevantStarsAt(body.position, Lib.GetParentSun(body)))
			{
				double dist = Vector3d.Distance(body.position, star.position);
				StormData bd = DB.Storm(StormKey(body, star));
				CreateStorm(bd, star, dist);

				// send messages
				if (Body_is_relevant(body))
					PostBodyMessages(body, bd, star);

				bd.msg_storm = bd.storm_state;
			}
		}

		public static void Update(Vessel v, VesselData vd, double elapsed_s)
		{
			try
			{
				// do nothing if storms are disabled
				if (!Features.SpaceWeather) return;

				// only consider vessels in interplanetary space (star or multi-star barycenter SOI)
				if (!IsInterplanetaryBody(v.mainBody)) return;

				// disregard EVAs
				if (v.isEVA) return;

				if (vd.EnvSunsInfo == null || vd.EnvSunsInfo.Count == 0)
				{
					// Suns not evaluated yet this tick — fall back to the SOI star or brightest local star
					CelestialBody star = Lib.IsSun(v.mainBody) ? v.mainBody : Sim.BrightestSunAt(Lib.VesselPosition(v));
					if (star == null) return;
					StormData bd = vd.GetStormDataForStar(star);
					double dist = vd.EnvMainSun != null && vd.EnvMainSun.SunData.body == star
						? vd.EnvMainSun.Distance
						: Sim.SunDistance(Lib.VesselPosition(v), star);
					CreateStorm(bd, star, dist);
					if (vd.cfg_storm)
						PostVesselMessages(v, bd, star);
					bd.msg_storm = bd.storm_state;
					return;
				}

				var relevantKeys = new HashSet<string>();
				foreach (VesselData.SunInfo sunInfo in vd.EnvSunsInfo)
				{
					if (!IsVesselStormSun(v, vd, sunInfo))
						continue;

					CelestialBody star = sunInfo.SunData.body;
					relevantKeys.Add(star.bodyName);
					StormData bd = vd.GetStormDataForStar(star);
					CreateStorm(bd, star, sunInfo.Distance);

					if (vd.cfg_storm)
						PostVesselMessages(v, bd, star);

					bd.msg_storm = bd.storm_state;
				}

				// Historical slots must still expire, but must not affect the vessel or generate new CMEs
				// while their source star is no longer locally relevant.
				if (vd.stormDataByStar != null)
				{
					double now = Planetarium.GetUniversalTime();
					foreach (KeyValuePair<string, StormData> kv in vd.stormDataByStar)
					{
						if (relevantKeys.Contains(kv.Key))
							continue;
						AdvanceStormState(kv.Value, now);
					}
				}
			}
			catch
			{
				vd.IsSimulated = vd.CheckIfSimulated();
				return;
			}
		}

		static void PostBodyMessages(CelestialBody body, StormData bd, CelestialBody star)
		{
			string systemLabel = StormTargetLabel(body.name, star);

			switch (bd.storm_state)
			{
				case 2:
					if (bd.msg_storm < 2)
					{
						Message.Post(Severity.danger, Local.Storm_msg1.Format("<b>" + systemLabel + "</b>"),
							Lib.BuildString(Local.Storm_msg1text, " ", Lib.HumanReadableDuration(bd.displayed_duration)));
					}
					break;

				case 1:
					if (bd.msg_storm < 1 && bd.display_warning)
					{
						var tti = bd.storm_time - Planetarium.GetUniversalTime();
						Message.Post(Severity.warning, Local.Storm_msg2.Format("<b>" + systemLabel + "</b>"),
							Lib.BuildString(Local.Storm_msg2text, " ", Lib.HumanReadableDuration(tti)));
					}
					break;

				case 0:
					if (bd.msg_storm == 2)
					{
						Message.Post(Severity.relax, Local.Storm_msg3.Format("<b>" + systemLabel + "</b>"));
					}
					break;
			}
		}

		static void PostVesselMessages(Vessel v, StormData bd, CelestialBody star)
		{
			string vesselLabel = StormTargetLabel(v.vesselName, star);

			switch (bd.storm_state)
			{
				case 0: // no storm
					if (bd.msg_storm == 2)
					{
						Message.Post(Severity.relax, Local.Storm_msg4.Format("<b>" + vesselLabel + "</b>"));
						v.KerbalismData().msg_signal = false; // avoid mass 'signal is back' messages after the storm
					}
					break;

				case 2: // storm in progress
					if (bd.msg_storm < 2)
					{
						Message.Post(Severity.danger, Local.Storm_msg5.Format("<b>" + vesselLabel + "</b>"),
							Lib.BuildString(Local.Storm_msg1text, " ", Lib.HumanReadableDuration(bd.displayed_duration)));
					}
					break;

				case 1: // storm incoming
					if (bd.msg_storm < 1 && bd.display_warning)
					{
						var tti = bd.storm_time - Planetarium.GetUniversalTime();
						Message.Post(Severity.warning, Local.Storm_msg6.Format("<b>" + vesselLabel + "</b>"),
							Lib.BuildString(Local.Storm_msg2text, " ", Lib.HumanReadableDuration(tti)));
					}
					break;
			}
		}

		static string StormTargetLabel(string name, CelestialBody star)
		{
			if (Sim.suns.Count <= 1 || star == null)
				return name;
			return name + " / " + star.bodyName;
		}

		/// <summary>DB key for a planetary-system storm from a given source star (save-compatible for the parent sun).</summary>
		public static string StormKey(CelestialBody planet, CelestialBody star)
		{
			CelestialBody parent = Lib.GetParentSun(planet);
			if (parent == null || star == null || parent == star)
				return planet.name;
			return planet.name + "@" + star.bodyName;
		}

		/// <summary>Stars that can deliver a CME to a world position (primary + bright companions).</summary>
		public static List<CelestialBody> RelevantStarsAt(Vector3d worldPos, CelestialBody preferredPrimary = null)
		{
			var result = new List<CelestialBody>();
			if (Sim.suns.Count == 0)
				return result;

			CelestialBody primary = preferredPrimary;
			if (primary == null || !Lib.IsSun(primary))
				primary = Sim.BrightestSunAt(worldPos);
			if (primary == null)
				primary = Sim.suns[0].body;

			result.Add(primary);
			if (Sim.suns.Count == 1)
				return result;

			Sim.SunData primaryData = null;
			foreach (Sim.SunData sd in Sim.suns)
			{
				if (sd.body == primary)
				{
					primaryData = sd;
					break;
				}
			}

			double primaryDist = Vector3d.Distance(worldPos, primary.position);
			double primaryFlux = primaryData != null ? primaryData.FluxProxyAtDistance(primaryDist) : 0.0;

			foreach (Sim.SunData sd in Sim.suns)
			{
				if (sd.body == primary)
					continue;
				double flux = sd.FluxProxyAtDistance(Vector3d.Distance(worldPos, sd.body.position));
				if (primaryFlux <= double.Epsilon || flux / primaryFlux >= MinCompanionFluxFraction)
					result.Add(sd.body);
			}

			return result;
		}

		static bool IsInterplanetaryBody(CelestialBody body)
		{
			return Lib.IsSun(body) || Lib.IsBarycenter(body);
		}

		static bool IsVesselStormSun(Vessel v, VesselData vd, VesselData.SunInfo sunInfo)
		{
			if (sunInfo == null || sunInfo.SunData == null)
				return false;
			if (Sim.suns.Count <= 1)
				return true;

			double primaryFluxProportion = vd.EnvMainSun?.FluxProportion ?? 0.0;
			// Always track the local brightest / SOI star and any companion contributing meaningful flux
			return sunInfo == vd.EnvMainSun
				|| sunInfo.SunData.body == v.mainBody
				// Both proportions have the same total-flux denominator, so this compares
				// companion flux against primary flux rather than against total system flux.
				|| sunInfo.FluxProportion >= primaryFluxProportion * MinCompanionFluxFraction;
		}

		/// <summary>Enumerate storm slots that can affect this vessel.</summary>
		public static IEnumerable<KeyValuePair<CelestialBody, StormData>> GetAffectingStorms(Vessel v)
		{
			if (v == null)
				yield break;

			if (IsInterplanetaryBody(v.mainBody))
			{
				VesselData vd = v.KerbalismData();
				if (vd?.stormDataByStar == null)
					yield break;

				if (vd.EnvSunsInfo != null && vd.EnvSunsInfo.Count > 0)
				{
					foreach (VesselData.SunInfo sunInfo in vd.EnvSunsInfo)
					{
						if (!IsVesselStormSun(v, vd, sunInfo))
							continue;
						CelestialBody star = sunInfo.SunData.body;
						if (vd.stormDataByStar.TryGetValue(star.bodyName, out StormData data))
							yield return new KeyValuePair<CelestialBody, StormData>(star, data);
					}
				}
				else
				{
					CelestialBody star = Lib.IsSun(v.mainBody) ? v.mainBody : Sim.BrightestSunAt(Lib.VesselPosition(v));
					if (star != null && vd.stormDataByStar.TryGetValue(star.bodyName, out StormData data))
						yield return new KeyValuePair<CelestialBody, StormData>(star, data);
				}
				yield break;
			}

			CelestialBody planet = Lib.GetParentPlanet(v.mainBody);
			if (planet == null)
				yield break;

			foreach (CelestialBody star in RelevantStarsAt(planet.position, Lib.GetParentSun(planet)))
			{
				yield return new KeyValuePair<CelestialBody, StormData>(star, DB.Storm(StormKey(planet, star)));
			}
		}

		/// <summary>
		/// Best storm for UI. When <paramref name="requiredState"/> is 0: prefer in-progress, else soonest warned inbound.
		/// Pass 1 or 2 to restrict to incoming / in-progress only.
		/// </summary>
		public static bool TryGetPrimaryStorm(Vessel v, out StormData bd, out CelestialBody star, uint requiredState = 0)
		{
			bd = null;
			star = null;
			if (v == null)
				return false;

			StormData bestInProgress = null;
			StormData bestIncoming = null;
			CelestialBody bestInProgressStar = null;
			CelestialBody bestIncomingStar = null;
			double bestRemaining = double.MaxValue;
			double bestTTI = double.MaxValue;
			double now = Planetarium.GetUniversalTime();

			foreach (KeyValuePair<CelestialBody, StormData> kv in GetAffectingStorms(v))
			{
				StormData data = kv.Value;
				if (data == null)
					continue;

				if (data.storm_state == 2 && (requiredState == 0 || requiredState == 2))
				{
					double remaining = data.storm_time + data.displayed_duration - now;
					if (remaining < bestRemaining)
					{
						bestRemaining = remaining;
						bestInProgress = data;
						bestInProgressStar = kv.Key;
					}
				}
				else if (data.storm_state == 1 && data.display_warning && (requiredState == 0 || requiredState == 1))
				{
					double tti = data.storm_time - now;
					if (tti < bestTTI)
					{
						bestTTI = tti;
						bestIncoming = data;
						bestIncomingStar = kv.Key;
					}
				}
			}

			if (requiredState == 1)
			{
				bd = bestIncoming;
				star = bestIncomingStar;
				return bd != null;
			}

			if (requiredState == 2)
			{
				bd = bestInProgress;
				star = bestInProgressStar;
				return bd != null;
			}

			if (bestInProgress != null)
			{
				bd = bestInProgress;
				star = bestInProgressStar;
				return true;
			}

			if (bestIncoming != null)
			{
				bd = bestIncoming;
				star = bestIncomingStar;
				return true;
			}

			return false;
		}

		/// <summary>Extra storm radiation rate (rad/s) from all active CMEs affecting the vessel.</summary>
		public static double RadiationStrength(Vessel v, VesselData vd)
		{
			if (v == null || vd == null || !Features.SpaceWeather)
				return 0.0;

			double total = 0.0;
			foreach (KeyValuePair<CelestialBody, StormData> kv in GetAffectingStorms(v))
			{
				if (kv.Value == null || kv.Value.storm_state != 2)
					continue;

				CelestialBody star = kv.Key;
				double sunlight = 1.0;
				double fluxWeight = 1.0;
				if (vd.EnvSunsInfo != null)
				{
					fluxWeight = 0.0;
					foreach (VesselData.SunInfo sunInfo in vd.EnvSunsInfo)
					{
						if (sunInfo.SunData.body == star)
						{
							sunlight = sunInfo.SunlightFactor;
							fluxWeight = sunInfo.FluxProportion;
							break;
						}
					}
				}
				else if (vd.EnvMainSun != null && vd.EnvMainSun.SunData.body == star)
				{
					sunlight = vd.EnvMainSun.SunlightFactor;
				}

				double activity = Radiation.Info(star).SolarActivity(false) / 2.0;
				total += PreferencesRadiation.Instance.StormRadiation * sunlight * fluxWeight * (activity + 0.5);
			}

			return total;
		}

		// return storm frequency factor by distance from sun
		static double Storm_frequency(double dist)
		{
			if (dist <= 0.0)
				return 1.0;
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
			// for each vessel
			foreach (Vessel v in FlightGlobals.Vessels)
			{
				// if inside the system
				if (Lib.GetParentPlanet(v.mainBody) == body)
				{
					// get info from the cache
					VesselData vd = v.KerbalismData();

					// skip invalid vessels
					if (!vd.IsSimulated) continue;

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

			if (body == null)
				return true;

			// skip the suns themselves
			if (Lib.IsSun(body)) return true;
			if (Lib.IsBarycenter(body)) return true;

			// only planetary-system roots (not moons); works with barycenter hierarchies
			return body != Lib.GetParentPlanet(body);
		}

		/// <summary>return true if a storm is incoming</summary>
		public static bool Incoming(Vessel v)
		{
			foreach (KeyValuePair<CelestialBody, StormData> kv in GetAffectingStorms(v))
			{
				if (kv.Value != null && kv.Value.storm_state == 1 && kv.Value.display_warning)
					return true;
			}
			return false;
		}

		/// <summary>return true if a storm is in progress</summary>
		public static bool InProgress(Vessel v)
		{
			foreach (KeyValuePair<CelestialBody, StormData> kv in GetAffectingStorms(v))
			{
				if (kv.Value != null && kv.Value.storm_state == 2)
					return true;
			}
			return false;
		}
	}

} // KERBALISM
