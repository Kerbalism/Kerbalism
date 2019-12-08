// ====================================================================================================================
// Functions that other mods can call to interact with Kerbalism. If you don't build against it, call these using
// reflection. All functions work transparently with loaded and unloaded vessels, unless otherwise specified.
// ====================================================================================================================


using System;
using System.Reflection;
using System.Collections.Generic;

namespace KERBALISM
{
	public class AntennaInfo
	{
		// ====================================================================
		// VALUES SET BY KERBALISM
		// ====================================================================

		/// <summary>
		/// This will be set to true if the vessel currently is transmitting data.
		/// </summary>
		public bool transmitting = false;

		/// <summary>
		/// Set to true if the vessel is currently subjected to a CME storm
		/// </summary>
		public bool storm = false;

		/// <summary>
		/// Set to true if the vessel has enough EC to operate
		/// </summary>
		public bool powered = true;


		// ====================================================================
		// VALUES TO SET FOR KERBALISM
		// ====================================================================

		/// <summary>
		/// science data rate, in MB/s. note that internal transmitters can not transmit science data only telemetry data
		/// </summary>
		public double rate = 0.0;

		/// <summary> ec cost while transmitting at the above rate </summary>
		public double ec = 0.0;

		/// <summary> ec cost while not transmitting </summary>
		public double ec_idle = 0.0;

		/// <summary> link quality indicator for the UI, any value from 0-1.
		/// you MUST set this to >= 0 in your mod, otherwise the comm status
		/// will either be handled by an other mod or by the stock implementation.
		/// </summary>
		public double strength = -1;

		/// <summary>
		/// direct_link = 0, indirect_link = 1 (relayed signal), no_link = 2, plasma = 3 (plasma blackout on reentry), storm = 4 (cme storm blackout)
		/// </summary>
		public int status = 2;

		/// <summary>
		/// true if communication is established. if false, vessels can't transmit data and might be uncontrollable.
		/// </summary>
		public bool linked;

		/// <summary>
		/// The name of the thing at the other end of your radio beam (KSC, name of the relay, ...)
		/// </summary>
		public string target_name;

		/// <summary>
		/// Optional: communication path that will be displayed in the UI.
		/// Each entry in the List is one "hop" in your path.
		/// provide up to 3 values for each hop: string[] hop = { name, value, tooltip }
		/// - name: the name of the relay/station
		/// - value: link quality to that relay
		/// - tooltip: anything you want to display, maybe link distance, frequency band used, ...
		/// </summary>
		public List<string[]> control_path = null;
	}

	public static class API
	{
		// show a message in the ui
		public static void Message(string msg)
		{
			KERBALISM.Message.Post(msg);
		}

		// kill a kerbal, even an EVA one
		public static void Kill(Vessel v, ProtoCrewMember c)
		{
			if (!v.KerbalismData().IsSimulated) return;
			if (!DB.ContainsKerbal(c.name)) return;
			Misc.Kill(v, c);
		}

		// trigger an undesiderable event for the kerbal specified
		public static void Breakdown(Vessel v, ProtoCrewMember c)
		{
			if (!v.KerbalismData().IsSimulated) return;
			if (!DB.ContainsKerbal(c.name)) return;
			Misc.Breakdown(v, c);
		}

		// disable or re-enable all rules for the specified kerbal
		public static void DisableKerbal(string k_name, bool disabled)
		{
			if (!DB.ContainsKerbal(k_name)) return;
			DB.Kerbal(k_name).disabled = disabled;
		}

		// inject instant radiation dose to the specified kerbal (can use negative amounts)
		public static void InjectRadiation(string k_name, double amount)
		{
			if (!DB.ContainsKerbal(k_name)) return;
			KerbalData kd = DB.Kerbal(k_name);
			foreach (Rule rule in Profile.rules)
			{
				if (rule.modifiers.Contains("radiation"))
				{
					RuleData rd = kd.rules[rule.name];
					rd.problem = Math.Max(rd.problem + amount, 0.0);
				}
			}
		}


		// --- ENVIRONMENT ----------------------------------------------------------

		// return true if the vessel specified is in sunlight
		public static bool InSunlight(Vessel v)
		{
			return !v.KerbalismData().EnvInFullShadow;
		}

		// return true if the vessel specified is inside a breathable atmosphere
		public static bool Breathable(Vessel v)
		{
			return v.KerbalismData().EnvBreathable;
		}


		// --- RADIATION ------------------------------------------------------------

		/// <summary>return true if radiation is enabled</summary>
		public static bool RadiationEnabled()
		{
			return Features.Radiation;
		}

		/// <summary>return amount of environment radiation at the position of the specified vessel</summary>
		public static double Radiation(Vessel v)
		{
			if (!Features.Radiation) return 0.0;
			return v.KerbalismData().EnvRadiation;
		}

		/// <summary>return amount of environment effective in the habitats of the given vessel</summary>
		public static double HabitatRadiation(Vessel v)
		{
			if (!Features.Radiation) return 0.0;
			return v.KerbalismData().EnvHabitatRadiation;
		}

		/// <summary>return true if the vessel is inside the magnetopause of some body (except the sun)</summary>
		public static bool Magnetosphere(Vessel v)
		{
			if (!Features.Radiation) return false;
			return v.KerbalismData().EnvMagnetosphere;
		}

		/// <summary>return true if the vessel is inside the radiation belt of some body</summary>
		public static bool InnerBelt(Vessel v)
		{
			if (!Features.Radiation) return false;
			return v.KerbalismData().EnvInnerBelt;
		}

		/// <summary>return true if the vessel is inside the radiation belt of some body</summary>
		public static bool OuterBelt(Vessel v)
		{
			if (!Features.Radiation) return false;
			return v.KerbalismData().EnvOuterBelt;
		}

		/// <summary>return true if the given body has an inner radiation belt (doesn't matter if visible or not)</summary>
		public static bool HasInnerBelt(CelestialBody body)
		{
			if (!Features.Radiation) return false;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			return rb.model.has_inner;
		}

		/// <summary>return true if the given body has an inner radiation belt that is visible</summary>
		public static bool IsInnerBeltVisible(CelestialBody body)
		{
			if (!Features.Radiation) return false;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			return rb.model.has_inner && rb.inner_visible;
		}

		/// <summary>set visibility of the inner radiation belt</summary>
		public static void SetInnerBeltVisible(CelestialBody body, bool visible)
		{
			if (!Features.Radiation) return;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			rb.inner_visible = visible;
		}

		/// <summary>return true if the given body has an outer radiation belt (doesn't matter if visible or not)</summary>
		public static bool HasOuterBelt(CelestialBody body)
		{
			if (!Features.Radiation) return false;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			return rb.model.has_outer;
		}

		/// <summary>return true if the given body has an outer radiation belt that is visible</summary>
		public static bool IsOuterBeltVisible(CelestialBody body)
		{
			if (!Features.Radiation) return false;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			return rb.model.has_outer && rb.outer_visible;
		}

		/// <summary>set visibility of the inner radiation belt</summary>
		public static void SetOuterBeltVisible(CelestialBody body, bool visible)
		{
			if (!Features.Radiation) return;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			rb.outer_visible = visible;
		}

		/// <summary>return true if the given body has a magnetosphere (doesn't matter if visible or not)</summary>
		public static bool HasMagnetopause(CelestialBody body)
		{
			if (!Features.Radiation) return false;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			return rb.model.has_pause;
		}

		/// <summary>return true if the given body has a magnetopause that is visible</summary>
		public static bool IsMagnetopauseVisible(CelestialBody body)
		{
			if (!Features.Radiation) return false;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			return rb.model.has_pause && rb.pause_visible;
		}

		/// <summary>set visibility of the inner radiation belt</summary>
		public static void SetMagnetopauseVisible(CelestialBody body, bool visible)
		{
			if (!Features.Radiation) return;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			rb.pause_visible = visible;
		}

		/// <summary>return true if the given body has a belt or a magnetosphere (doesn't matter if visible or not)</summary>
		public static bool HasMagneticField(CelestialBody body)
		{
			if (!Features.Radiation) return false;
			RadiationBody rb = KERBALISM.Radiation.Info(body);
			return rb.model.Has_field();
		}

		/// <summary> Return the current solar activity for the given body. Normal activity ranges
		/// from 0..1, but be smaller than 0 or bigger than 1 during times of extreme low or high activity. </summary>
		public static double GetSolarActivity(CelestialBody body)
		{
			if (!Features.Radiation) return 0.0;
			var info = KERBALISM.Radiation.Info(body);
			return info.SolarActivity(false);
		}

		public static RadiationFieldChanged OnRadiationFieldChanged = new RadiationFieldChanged();
		public class RadiationFieldChanged
		{
			internal List<Action<Vessel, bool, bool, bool>> receivers = new List<Action<Vessel, bool, bool, bool>>();
			public void Add(Action<Vessel, bool, bool, bool> receiver) { if (!receivers.Contains(receiver)) receivers.Add(receiver); }
			public void Remove(Action<Vessel, bool, bool, bool> receiver) { if (receivers.Contains(receiver)) receivers.Remove(receiver); }

			public void Notify(Vessel vessel, bool innerBelt, bool outerBelt, bool magnetosphere)
			{
				foreach (Action<Vessel, bool, bool, bool> receiver in receivers)
				{
					try
					{
						receiver.Invoke(vessel, innerBelt, outerBelt, magnetosphere);
					}
					catch (Exception e)
					{
						Lib.Log("RadiationFieldChanged: Exception in event receiver " + e.Message + "\n" + e.ToString());
					}
				}
			}
		}

		// --- SPACE WEATHER --------------------------------------------------------

		// return true if a solar storm is incoming at the vessel position
		public static bool StormIncoming(Vessel v)
		{
			if (!Features.SpaceWeather) return false;
			return v.KerbalismData().IsSimulated && Storm.Incoming(v);
		}

		// return true if a solar storm is in progress at the vessel position
		public static bool StormInProgress(Vessel v)
		{
			if (!Features.SpaceWeather) return false;
			VesselData vd = v.KerbalismData();
			return vd.IsSimulated && vd.EnvStorm;
		}

		// return true if the vessel is subject to a signal blackout
		public static bool Blackout(Vessel v)
		{
			if (!RemoteTech.Enabled) return false;
			return v.KerbalismData().EnvBlackout;
		}

		/// <summary>
		/// Returns the current sun observation quality (ranges from 0 to 1). this is
		/// the probability that the player will get a warning for an incoming CME
		/// </summary>
		/// <returns>The observation quality.</returns>
		public static float StormObservationQuality()
		{
			return Storm.sun_observation_quality;
		}

		/// <summary>
		/// Set the current sun observation quality (ranges from 0 to 1). this is
		/// the probability that the player will get a warning for an incoming CME
		/// </summary>
		/// <param name="quality">Quality.</param>
		public static void SetStormObservationQuality(float quality)
		{
			Storm.sun_observation_quality = Lib.Clamp(quality, 0.0f, 1.0f);
		}

		// --- RELIABILITY ----------------------------------------------------------

		// return true if at least a component has malfunctioned, or had a critical failure
		public static bool Malfunction(Vessel v)
		{
			if (!Features.Reliability) return false;
			return v.KerbalismData().Malfunction;
		}

		// return true if at least a componet had a critical failure
		public static bool Critical(Vessel v)
		{
			if (!Features.Reliability) return false;
			return v.KerbalismData().Critical;
		}

		// return true if the part specified has a malfunction or critical failure
		public static bool Broken(Part part)
		{
			return part.FindModulesImplementing<Reliability>().FindAll(k => k.isEnabled && k.broken) != null;
		}

		// repair a specified part
		public static void Repair(Part part)
		{
			part.FindModulesImplementing<Reliability>().FindAll(k => k.isEnabled && k.broken).ForEach(k => k.Repair());
		}


		// --- HABITAT --------------------------------------------------------------

		// return volume of internal habitat in m^3
		public static double Volume(Vessel v)
		{
			if (!Features.Habitat) return 0.0;
			return v.KerbalismData().Volume;
		}

		// return surface of internal habitat in m^2
		public static double Surface(Vessel v)
		{
			if (!Features.Habitat) return 0.0;
			return v.KerbalismData().Surface;
		}

		// return normalized pressure of internal habitat
		public static double Pressure(Vessel v)
		{
			if (!Features.Pressure) return 0.0;
			return v.KerbalismData().Pressure;
		}

		// return level of co2 of internal habitat
		public static double Poisoning(Vessel v)
		{
			if (!Features.Poisoning) return 0.0;
			return v.KerbalismData().Poisoning;
		}

		// return proportion of radiation blocked by shielding
		public static double Shielding(Vessel v)
		{
			return v.KerbalismData().Shielding;
		}

		// return living space factor
		public static double LivingSpace(Vessel v)
		{
			return v.KerbalismData().LivingSpace;
		}

		// return comfort factor
		public static double Comfort(Vessel v)
		{
			return v.KerbalismData().Comforts.factor;
		}


		// --- RESOURCES ------------------------------------------------------------

		public static void ConsumeResource(Vessel v, string resource_name, double quantity, string title)
		{
			ResourceCache.Consume(v, resource_name, quantity, title);
		}

		public static void ProduceResource(Vessel v, string resource_name, double quantity, string title)
		{
			ResourceCache.Produce(v, resource_name, quantity, title);
		}

		public static void ProcessResources(Vessel v, List<KeyValuePair<string, double>> resources, string title)
		{
			Lib.Log("ProcessResources called for vessel " + v);
			foreach(var p in resources)
			{
				if (p.Value < 0)
				{
					Lib.Log("Consuming " + p.Key + " amount " + p.Value + " for " + title);
					ResourceCache.Consume(v, p.Key, -p.Value, title);
				}
				else
				{
					Lib.Log("Producing " + p.Key + " amount " + p.Value + " for " + title);
					ResourceCache.Produce(v, p.Key, p.Value, title);
				}
			}
		}

		public static double ResourceAmount(Vessel v, string resource_name)
		{
			return ResourceCache.GetResource(v, resource_name).Amount;
		}

		public static List<double> ResourceAmounts(Vessel v, List<string> resource_names)
		{
			List<double> result = new List<double>(resource_names.Count);
			foreach (var name in resource_names)
				result.Add(ResourceCache.GetResource(v, name).Amount);
			return result;
		}

		public static double ResourceCapacity(Vessel v, string resource_name)
		{
			return ResourceCache.GetResource(v, resource_name).Capacity;
		}

		public static List<double> ResourceCapacities(Vessel v, List<string> resource_names)
		{
			List<double> result = new List<double>(resource_names.Count);
			foreach (var name in resource_names)
				result.Add(ResourceCache.GetResource(v, name).Capacity);
			return result;
		}

		public static double ResourceLevel(Vessel v, string resource_name)
		{
			return ResourceCache.GetResource(v, resource_name).Level;
		}

		public static List<double> ResourceLevels(Vessel v, List<string> resource_names)
		{
			List<double> result = new List<double>(resource_names.Count);
			foreach (var name in resource_names)
				result.Add(ResourceCache.GetResource(v, name).Level);
			return result;
		}

		// --- SCIENCE --------------------------------------------------------------

		public static ExperimentStateChanged OnExperimentStateChanged = new ExperimentStateChanged();
		public class ExperimentStateChanged
		{
			internal List<Action<Vessel, string, bool>> receivers = new List<Action<Vessel, string, bool>>();
			public void Add(Action<Vessel, string, bool> receiver) { if (!receivers.Contains(receiver)) receivers.Add(receiver); }
			public void Remove(Action<Vessel, string, bool> receiver) { if (receivers.Contains(receiver)) receivers.Remove(receiver); }

			public void Notify(Vessel vessel, string experiment_id, Experiment.ExpStatus oldStatus, Experiment.ExpStatus newStatus)
			{
				bool wasRunning = oldStatus == Experiment.ExpStatus.Forced || oldStatus == Experiment.ExpStatus.Running;
				bool isRunning = newStatus == Experiment.ExpStatus.Forced || newStatus == Experiment.ExpStatus.Running;
				if (wasRunning == isRunning) return;
				foreach (Action<Vessel, string, bool> receiver in receivers)
				{
					try
					{
						receiver.Invoke(vessel, experiment_id, isRunning);
					}
					catch (Exception e)
					{
						Lib.Log("ExperimentStateChanged: Exception in event receiver " + e.Message + "\n" + e.ToString());
					}
				}
			}
		}

		/// <summary> Returns true if the experiment is currently active and collecting data </summary>
		public static bool ExperimentIsRunning(Vessel vessel, string experiment_id)
		{
			if (!Features.Science) return false;

			if (vessel.loaded)
			{
				foreach (Experiment e in vessel.FindPartModulesImplementing<Experiment>())
				{
					if (e.enabled && e.experiment_id == experiment_id &&
						(e.State == Experiment.RunningState.Running || e.State == Experiment.RunningState.Forced))
						return true;
				}
			}
			else
			{
				var PD = new Dictionary<string, Lib.Module_prefab_data>();
				foreach (ProtoPartSnapshot p in vessel.protoVessel.protoPartSnapshots)
				{
					// get part prefab (required for module properties)
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;
					// get all module prefabs
					var module_prefabs = part_prefab.FindModulesImplementing<PartModule>();
					// clear module indexes
					PD.Clear();
					foreach (ProtoPartModuleSnapshot m in p.modules)
					{
						// get the module prefab
						// if the prefab doesn't contain this module, skip it
						PartModule module_prefab = Lib.ModulePrefab(module_prefabs, m.moduleName, PD);
						if (!module_prefab) continue;
						// if the module is disabled, skip it
						// note: this must be done after ModulePrefab is called, so that indexes are right
						if (!Lib.Proto.GetBool(m, "isEnabled")) continue;

						if (m.moduleName == "Experiment"
							&& ((Experiment)module_prefab).experiment_id == experiment_id)
						{
							var state = Lib.Proto.GetEnum(m, "expState", Experiment.RunningState.Stopped);
							if (state == Experiment.RunningState.Running || state == Experiment.RunningState.Forced)
								return true;
						}
					}
				}
			}

			return false;
		}

		// --- FAILURES --------------------------------------------------------------

		public static FailureInfo Failure = new FailureInfo();

		public class FailureInfo
		{
			//This is the list of methods that should be activated when the event fires
			internal List<Action<Part, string, bool>> receivers = new List<Action<Part, string, bool>>();

			//This adds a connection info handler
			public void Add(Action<Part, string, bool> receiver)
			{
				//We only add it if it isn't already added. Just in case.
				if (!receivers.Contains(receiver))
				{
					receivers.Add(receiver);
				}
			}

			//This removes a connection info handler
			public void Remove(Action<Part, string, bool> receiver)
			{
				//We also only remove it if it's actually in the list.
				if (receivers.Contains(receiver))
				{
					receivers.Remove(receiver);
				}
			}

			public void Notify(Part part, string type, bool failure)
			{
				//Loop through the list of listening methods and Invoke them.
				foreach (Action<Part, string, bool> receiver in receivers)
				{
					receiver.Invoke(part, type, failure);
				}
			}
		}

		// --- COMMUNICATION --------------------------------------------------------------

		public static double VesselConnectionRate(Vessel v)
		{
			var vi = v.KerbalismData();
			if (!vi.IsSimulated) return 0.0;
			return vi.Connection.rate;
		}

		public static bool VesselConnectionLinked(Vessel v)
		{
			var vi = v.KerbalismData();
			if (!vi.IsSimulated) return false;
			return vi.Connection.linked;
		}

		public static int VesselConnectionTransmitting(Vessel v)
		{
			var vi = v.KerbalismData();
			if (!vi.IsSimulated) return 0;
			return vi.filesTransmitted.Count;
		}

		public static CommInfo Comm = new CommInfo();
		public class CommInfo
		{
			//This is the list of methods that should be activated when the event fires
			internal List<MethodInfo> handlers = new List<MethodInfo>();

			//This adds a connection info handler
			public void Add(MethodInfo handler)
			{
				if(handler == null)
				{
					Lib.Log("Error: Kerbalism CommInfo.Add called with null handler");
					return;
				}
				//We only add it if it isn't already added. Just in case.
				if (!handlers.Contains(handler))
				{
					handlers.Add(handler);
				}
			}

			//This removes a connection info handler
			public void Remove(MethodInfo handler)
			{
				//We also only remove it if it's actually in the list.
				if (handlers.Contains(handler))
				{
					handlers.Remove(handler);
				}
			}

			//This initializes an antennaInfo object. Connection info handlers must
			//set antennaInfo.strength to a value >= 0, otherwise the antennaInfo will
			//be passed to the next handler.
			public void Init(AntennaInfo antennaInfo, Vessel pv)
			{
				//Loop through the list of listening methods and Invoke them.
				foreach (MethodInfo handler in handlers)
				{
					try {
						handler.Invoke(null, new object[] { antennaInfo, pv });
						if (antennaInfo.strength > -1) return;
					} catch(Exception e) {
						Lib.Log("CommInfo handler threw exception " + e.Message + "\n" + e.ToString());
					}
				}
			}
		}
	}
} // KERBALISM

