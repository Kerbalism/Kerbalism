// ====================================================================================================================
// Functions that other mods can call to interact with Kerbalism. If you don't build against it, call these using
// reflection. All functions work transparently with loaded and unloaded vessels, unless otherwise specified.
// ====================================================================================================================


using System;
using System.Reflection;
using System.Collections.Generic;
using KERBALISM.Planner;

namespace KERBALISM
{
	public static class API
	{
		#region MISC
		// Post an on screen message
		public static void Message(string msg)
		{
			KERBALISM.Message.Post(msg);
		}

		#endregion

		#region KERBALS

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
				if (rule.name.Contains("radiation"))
				{
					RuleData rd = kd.rules[rule.name];
					rd.problem = Math.Max(rd.problem + amount, 0.0);
				}
			}
		}

		// inject instant radiation dose to all kerbals (can use negative amounts)
		public static void InjectRadiation(double amount)
		{
			foreach (Rule rule in Profile.rules)
			{
				if (rule.name.Contains("radiation"))
				{
					foreach (KerbalData kd in DB.Kerbals().Values)
					{
						RuleData rd = kd.rules[rule.name];
						rd.problem = Math.Max(rd.problem + amount, 0.0);
					}

				}
			}
		}

		#endregion

		#region ENVIRONMENT

		// return true if the vessel specified is in sunlight
		public static bool InSunlight(Vessel v)
		{
			return !v.KerbalismData().EnvInFullShadow;
		}

		// return true if the vessel specified is inside a breathable atmosphere
		public static bool Breathable(Vessel v)
		{
			return v.KerbalismData().EnvInBreathableAtmosphere;
		}

		#endregion

		#region RADIATION

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

		#endregion

		#region SOLAR STORMS

		// return true if a solar storm is incoming at the vessel position
		public static bool StormIncoming(Vessel v)
		{
			if (!Features.Radiation) return false;
			return v.KerbalismData().IsSimulated && Storm.Incoming(v);
		}

		// return true if a solar storm is in progress at the vessel position
		public static bool StormInProgress(Vessel v)
		{
			if (!Features.Radiation) return false;
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

		#endregion

		#region RELIABILITY

		// return true if at least a component has malfunctioned, or had a critical failure
		public static bool Malfunction(Vessel v)
		{
			if (!Features.Failures) return false;
			return v.KerbalismData().Malfunction;
		}

		// return true if at least a componet had a critical failure
		public static bool Critical(Vessel v)
		{
			if (!Features.Failures) return false;
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

		#endregion

		#region HABITAT

		// return volume of internal habitat in m^3
		public static double Volume(Vessel v)
		{
			if (!Features.LifeSupport) return 0.0;
			return v.KerbalismData().HabitatInfo.livingVolume;
		}

		// return surface of internal habitat in m^2
		public static double Surface(Vessel v)
		{
			if (!Features.LifeSupport) return 0.0;
			return v.KerbalismData().HabitatInfo.shieldingSurface;
		}

		// return normalized pressure of internal habitat
		public static double Pressure(Vessel v)
		{
			if (!Features.LifeSupport) return 0.0;
			return v.KerbalismData().HabitatInfo.pressureAtm;
		}

		// return level of co2 of internal habitat
		public static double Poisoning(Vessel v)
		{
			if (!Features.LifeSupport) return 0.0;
			return v.KerbalismData().HabitatInfo.poisoningLevel;
		}

		// return proportion of radiation blocked by shielding
		public static double Shielding(Vessel v)
		{
			return v.KerbalismData().HabitatInfo.shieldingModifier;
		}

		// return living space factor
		public static double LivingSpace(Vessel v)
		{
			return v.KerbalismData().HabitatInfo.livingSpaceFactor;
		}

		// return comfort factor
		public static double Comfort(Vessel v)
		{
			return v.KerbalismData().HabitatInfo.comfortFactor;
		}

		#endregion

		#region RESOURCE SIM

		/// <summary> Consume a resource through the kerbalism resource simulation, available for loaded and unloaded vessels </summary>
		/// <param name="v">the vessel to consume the resource on</param>
		/// <param name="resource_name">name of the resource to consume</param>
		/// <param name="quantity">amount of resource to consume.
		/// This should be scaled by the TimeWarp.fixedDeltaTime on loaded vessels or
		/// by the kerbalism provided elapsed time on unloaded vessels </param>
		/// <param name="title">origin of the resource consumer (shown in the UI)</param>
		public static void ConsumeResource(Vessel v, string resource_name, double quantity, string title)
		{
			v.KerbalismData().ResHandler.Consume(resource_name, quantity, ResourceBroker.GetOrCreate(title));
		}

		/// <summary> Produce a resource through the kerbalism resource simulation, available for loaded and unloaded vessels </summary>
		/// <param name="v">the vessel to produce the resource on</param>
		/// <param name="resource_name">name of the resource to produce</param>
		/// <param name="quantity">amount of resource to produce.
		/// This should be scaled by the TimeWarp.fixedDeltaTime on loaded vessels or
		/// by the kerbalism provided elapsed time on unloaded vessels </param>
		/// <param name="title">origin of the resource producer (shown in the UI)</param>
		public static void ProduceResource(Vessel v, string resource_name, double quantity, string title)
		{
			v.KerbalismData().ResHandler.Produce(resource_name, quantity, ResourceBroker.GetOrCreate(title));
		}

		/// <summary>
		/// Register a recipe to be processed by the kerbalism resource simulation
		/// A recipe is a combination of inputs and outputs, where the outputs are limited by the inputs availability
		/// The recipe will also be limited by the storage capacity of outputs, unless you set the "dump" boolean to true
		/// </summary>
		/// <param name="v">the vessel to process the recipe on</param>
		/// <param name="resources">array of inputs and outputs : resource names</param>
		/// <param name="rates">array of inputs and outputs : postive rates are outputs, negative rates are inputs.
		/// Rates must be scaled by the TimeWarp.fixedDeltaTime on loaded vessels or
		/// by the kerbalism provided elapsed time on unloaded vessels</param>
		/// <param name="dump">array of inputs and outputs : set to true to have an output not being limited by the storage capacity.
		/// This has no effect on inputs, the value is ignored</param>
		/// <param name="title">origin of the recipe (shown in the UI)</param>
		public static void AddResourceRecipe(Vessel v, string[] resources, double[] rates, bool[] dump, string title)
		{
			Recipe recipe = new Recipe(ResourceBroker.GetOrCreate(title));

			for (int i = 0; i < resources.Length; i++)
			{
				if (rates[i] < 0.0)
					recipe.AddInput(resources[i], -rates[i]);
				else
					recipe.AddOutput(resources[i], rates[i], dump[i]);
			}

			v.KerbalismData().ResHandler.AddRecipe(recipe);
		}

		public static double ResourceAmount(Vessel v, string resource_name)
		{
			return v.KerbalismData().ResHandler.GetResource(resource_name).Amount;
		}

		public static double ResourceCapacity(Vessel v, string resource_name)
		{
			return v.KerbalismData().ResHandler.GetResource(resource_name).Capacity;
		}

		public static double ResourceAvailability(Vessel v, string resource_name)
		{
			return v.KerbalismData().ResHandler.GetResource(resource_name).AvailabilityFactor;
		}

		/// <summary>
		/// Return a list of all consumers and producers for that resource.
		/// </summary>
		/*
		The double value is the positive or negative rate (in unit/s) for that broker
		The string[] array always contain 3 strings :
		- The first is the category (see the ResourceBroker.BrokerCategory enum)
		- The second is the broker id
		- The third is the broker localized title
		*/
		private static List<KeyValuePair<string[], double>> apiBrokers = new List<KeyValuePair<string[], double>>();
		public static List<KeyValuePair<string[], double>> ResourceBrokers(Vessel v, string resource_name)
		{
			List<ResourceBrokerRate> brokers = v.KerbalismData().ResHandler.GetResource(resource_name).ResourceBrokers;
			apiBrokers.Clear();
			foreach (ResourceBrokerRate rb in brokers)
			{
				apiBrokers.Add(new KeyValuePair<string[], double>(rb.broker.BrokerInfo, rb.rate));
			}
			return apiBrokers;
		}

		public static void PlannerConsumeResource(string resource_name, double quantity, string title)
		{
			PlannerResourceSimulator.Handler.Consume(resource_name, quantity, ResourceBroker.GetOrCreate(title));
		}

		public static void PlannerProduceResource(string resource_name, double quantity, string title)
		{
			PlannerResourceSimulator.Handler.Produce(resource_name, quantity, ResourceBroker.GetOrCreate(title));
		}

		public static void PlannerAddResourceRecipe(string[] resources, double[] rates, bool[] dump, string title)
		{
			Recipe recipe = new Recipe(ResourceBroker.GetOrCreate(title));

			for (int i = 0; i < resources.Length; i++)
			{
				if (rates[i] < 0.0)
					recipe.AddInput(resources[i], -rates[i]);
				else
					recipe.AddOutput(resources[i], rates[i], dump[i]);
			}

			PlannerResourceSimulator.Handler.AddRecipe(recipe);
		}

		public static double PlannerResourceAmount(string resource_name)
		{
			return PlannerResourceSimulator.Handler.GetResource(resource_name).Amount;
		}

		public static double PlannerResourceCapacity(string resource_name)
		{
			return PlannerResourceSimulator.Handler.GetResource(resource_name).Capacity;
		}

		public static double PlannerResourceAvailability(string resource_name)
		{
			return PlannerResourceSimulator.Handler.GetResource(resource_name).AvailabilityFactor;
		}

		#endregion

		#region SCIENCE

		// Science crediting override and event (implemented for Bureaucracy)
		/// <summary> set to true to prevent Kerbalism from doing any call to ResearchAndDevelopment.Instance.AddScience() </summary>
		public static bool preventScienceCrediting = false;

		/// <summary> set to true to have Kerbalism fire the onSubjectsReceived event</summary>
		public static bool subjectsReceivedEventEnabled = false;

		/// <summary> time intervals at which the onSubjectsReceived event will be fired, you can adjust these if needed</summary>
		public static double subjectsReceivedEventRealTimeInterval = 60.0; // in seconds
		public static double subjectsReceivedEventGameTimeInterval = 60.0 * 60.0 * 6.0 * 5.0; // in seconds

		/// <summary>
		/// Event fired when one of the above time interval is first reached, if there are some subjects that
		/// were transmitted or recovered since the last time the event has been fired.
		/// First arg is the list of subjects that has been added or updated in the stock ResearchAndDevelopment
		/// Second arg is the amount of science that was collected for each of these subjects (both list are always the same size)
		/// </summary>
		public static EventData<List<ScienceSubject>, List<double>> onSubjectsReceived
			= new EventData<List<ScienceSubject>, List<double>>("onSubjectsReceived");


		// KerbalismContracts event
		public static ExperimentStateChanged OnExperimentStateChanged = new ExperimentStateChanged();
		public class ExperimentStateChanged
		{
			internal List<Action<Vessel, string, bool>> receivers = new List<Action<Vessel, string, bool>>();
			public void Add(Action<Vessel, string, bool> receiver) { if (!receivers.Contains(receiver)) receivers.Add(receiver); }
			public void Remove(Action<Vessel, string, bool> receiver) { if (receivers.Contains(receiver)) receivers.Remove(receiver); }

			public void Notify(Vessel vessel, string experiment_id, ModuleKsmExperiment.ExpStatus oldStatus, ModuleKsmExperiment.ExpStatus newStatus)
			{
				bool wasRunning = oldStatus == ModuleKsmExperiment.ExpStatus.Forced || oldStatus == ModuleKsmExperiment.ExpStatus.Running;
				bool isRunning = newStatus == ModuleKsmExperiment.ExpStatus.Forced || newStatus == ModuleKsmExperiment.ExpStatus.Running;
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
				foreach (ModuleKsmExperiment e in vessel.FindPartModulesImplementing<ModuleKsmExperiment>())
				{
					if (e.enabled && e.experiment_id == experiment_id &&
						(e.State == ModuleKsmExperiment.RunningState.Running || e.State == ModuleKsmExperiment.RunningState.Forced))
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

						if (m.moduleName == "ModuleKsmExperiment"
							&& ((ModuleKsmExperiment)module_prefab).experiment_id == experiment_id)
						{
							var state = Lib.Proto.GetEnum(m, "expState", ModuleKsmExperiment.RunningState.Stopped);
							if (state == ModuleKsmExperiment.RunningState.Running || state == ModuleKsmExperiment.RunningState.Forced)
								return true;
						}
					}
				}
			}

			return false;
		}

		#endregion

		#region COMMUNICATIONS

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

		/// <summary> Call this to register an editor comms handler. Please call this as soon as possible (ex : in a static class constructor) </summary>
		public static void EnableEditorCommHandler()
		{
			CommHandlerEditor.APIHandlerEnabled = true;
		}

		/// <summary>
		/// register the API comm update method used in the editor. Method signature must match the following :
		/// <para/> public static void MyHandlerUpdate(ConnectionInfoEditor connection)
		/// <para/> See Comms/ConnectionInfoEditor.cs for details
		/// </summary>
		public static void RegisterEditorCommHandler(MethodInfo updateMethod)
		{
			try
			{
				CommHandlerEditor.APIHandlerUpdate = (Action<ConnectionInfoEditor>)Delegate.CreateDelegate(typeof(Action<ConnectionInfoEditor>), updateMethod);
				CommHandlerEditor.APIHandlerEnabled = true;
			}
			catch (Exception e)
			{
				CommHandlerEditor.APIHandlerEnabled = false;
				Lib.Log($"Failed to register editor API comm handler\n {e.ToString()}", Lib.LogLevel.Error);
			}

		}

		public static CommInfo Comm = new CommInfo();
		public class CommInfo
		{
			//This is the list of methods that should be activated when the event fires
			internal List<MethodInfo> handlers = new List<MethodInfo>();

			//This adds a connection info handler
			public void Add(MethodInfo handler)
			{
				if (handler == null)
				{
					Lib.Log("Kerbalism CommInfo.Add called with null handler", Lib.LogLevel.Error);
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
		}

		#endregion
	}
} // KERBALISM

