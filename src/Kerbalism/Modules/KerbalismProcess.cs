using System;
using System.Collections.Generic;
using UnityEngine;

// EXPERIMENTAL
namespace KERBALISM
{
	public class KerbalismProcess : PartModule
	{
		[KSPField] public double ec_produced = 0;    // EC produced by this part
		[KSPField] public double ec_consumed = 0;    // EC consumed by this part
		[KSPField] public string resources_produced = string.Empty;    // resources produced by this part
		[KSPField] public string resources_consumed = string.Empty;    // resources consumed by this part
		[KSPField] public bool loaded = false;  // true if resource consumption/production should happen when loaded
		[KSPField] public bool unloaded = true; // true if resource consumption/production should happen when unloaded

		private List<KeyValuePair<string, double>> resourcesProduced;
		private List<KeyValuePair<string, double>> resourcesConsumed;
		private static readonly List<KeyValuePair<string, double>> noResources = new List<KeyValuePair<string, double>>();

		internal static List<KeyValuePair<string, double>> ParseResources(string resources, bool logErros = false)
		{
			if (string.IsNullOrEmpty(resources)) return noResources;

			List<KeyValuePair<string, double>> defs = new List<KeyValuePair<string, double>>();
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;

			foreach (string s in Lib.Tokenize(resources, ','))
			{
				// definitions are Resource@rate
				var p = Lib.Tokenize(s, '@');
				if (p.Count != 2) continue;             // malformed definition
				string res = p[0];
				if (!reslib.Contains(res)) continue;    // unknown resource
				double rate = double.Parse(p[1]);
				if (res.Length < 1 || rate < double.Epsilon) continue;  // rate <= 0
				defs.Add(new KeyValuePair<string, double>(res, rate));
			}
			return defs;
		}

		public override void OnStart(StartState state)
		{
			if (Lib.DisableScenario(this)) return;
			if (Lib.IsEditor()) return;

			resourcesProduced = ParseResources(resources_produced, false);
			resourcesConsumed = ParseResources(resources_produced, false);
		}

		public void FixedUpdate()
		{
			if (!loaded) return;
			Resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

			RunProcessTick(vessel, Kerbalism.elapsed_s, ec_produced, resourcesProduced, ec_consumed, resourcesConsumed, ec, ResourceCache.Get(vessel));
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, KerbalismProcess process, Resource_info ec, Vessel_resources resources, double elapsed_s)
		{
			if (!process.unloaded) return;

			List<KeyValuePair<string, double>> resourcesProduced;
			List<KeyValuePair<string, double>> resourcesConsumed;

			if (!Cache.HasVesselObjectsCache(v, "kerbalism_process_produced_" + process.part.flightID))
			{
				resourcesProduced = ParseResources(process.resources_produced, false);
				Cache.SetVesselObjectsCache<List<KeyValuePair<string, double>>>(v, "kerbalism_process_produced_" + process.part.flightID, resourcesProduced);
			}
			else
			{
				resourcesProduced = Cache.VesselObjectsCache<List<KeyValuePair<string, double>>>(v, "kerbalism_process_produced_" + process.part.flightID);
			}

			if (!Cache.HasVesselObjectsCache(v, "kerbalism_process_consumed_" + process.part.flightID))
			{
				resourcesConsumed = ParseResources(process.resources_produced, false);
				Cache.SetVesselObjectsCache<List<KeyValuePair<string, double>>>(v, "kerbalism_process_consumed_" + process.part.flightID, resourcesConsumed);
			}
			else
			{
				resourcesConsumed = Cache.VesselObjectsCache<List<KeyValuePair<string, double>>>(v, "kerbalism_process_consumed_" + process.part.flightID);
			}

			RunProcessTick(v, elapsed_s, process.ec_produced, resourcesProduced, process.ec_consumed, resourcesConsumed, ec, resources);
		}

		private static void RunProcessTick(Vessel v, double elapsed_s,
			double ec_produced, List<KeyValuePair<string, double>> resourcesProduced,
			double ec_consumed, List<KeyValuePair<string, double>> resourcesConsumed,
			Resource_info ec, Vessel_resources resources)
		{
			// evaluate process rate
			double rate = 1;
			if (ec_consumed < ec.amount) rate = ec.amount / ec_consumed;

			foreach (var consumed in resourcesConsumed)
			{
				var ri = resources.Info(v, consumed.Key);
				rate = Math.Min(rate, Lib.Clamp(ri.amount / (consumed.Value * elapsed_s), 0, 1));
			}

			foreach (var produced in resourcesProduced)
			{
				var ri = resources.Info(v, produced.Key);
				var capacityAvailable = ri.capacity - ri.amount;
				var amountProduced = produced.Value * elapsed_s;
				if (capacityAvailable < amountProduced)
					rate = Math.Min(rate, Lib.Clamp(capacityAvailable / amountProduced, 0, 1));
			}

			// produce/consume according to rate
			if (rate < double.Epsilon) return;

			ec.Consume(ec_consumed * elapsed_s * rate, "module process");
			ec.Produce(ec_produced * elapsed_s * rate, "module process");

			foreach (var consumed in resourcesConsumed)
				resources.Info(v, consumed.Key).Consume(consumed.Value * elapsed_s * rate, "module process");
			foreach (var produced in resourcesProduced)
				resources.Info(v, produced.Key).Produce(produced.Value * elapsed_s * rate, "module process");
		}
	}
}
