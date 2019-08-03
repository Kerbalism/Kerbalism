using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// this class gives a view on resources, either per part or vessel wide
	/// the caller can use this in much the same way as Resource_info class
	/// </summary>
	/// <remarks>
	/// typically the resource simulator works on the sum of all resources in the vessel
	/// sometimes resources cannot flow between parts, and this class hides the difference between the two cases
	/// </remarks>
	public abstract class Resource_info_view
	{
		protected Resource_info_view() { }
		public abstract double deferred { get; }
		public abstract double amount { get; }
		public abstract double capacity { get; }

		/// <summary>record a deferred production</summary>
		public abstract void Produce(double quantity, string tag);
		/// <summary>record a deferred consumption</summary>
		public abstract void Consume(double quantity, string tag);
	}

	/// <summary>
	/// Class that contains:
	/// * For a single vessel
	/// * For a single resource
	/// all the information the simulator needs
	/// </summary>
	/// <remarks>
	/// It also contains all needed functionality to synchronize resource information between the
	/// kerbalism simulator and the kerbal space per part information
	/// </remarks>
	public sealed class Resource_info
	{
		public Resource_info(Vessel v, string res_name)
		{
			// remember resource name
			ResourceName = res_name;

			Deferred = 0;
			Amount = 0;
			Capacity = 0;

			brokers = new Dictionary<string, double>();
			ruleBrokers = new Dictionary<string, double>();
			_vessel_wide_view = new Resource_info_view_impl(ResourceName, this);

			// get amount & capacity
			if (v.loaded)
			{
				foreach (Part p in v.Parts)
				{
					foreach (PartResource r in p.Resources)
					{
						if (r.resourceName == ResourceName)
						{
							if (r.flowState) // has the user chosen to make a flowable resource flow
							{
								Amount += r.amount;
								Capacity += r.maxAmount;
							}
						}
#if DEBUG_RESOURCES
						// Force view all resource in Debug Mode
						r.isVisible = true;
#endif
					}
				}
			}
			else
			{
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					foreach (ProtoPartResourceSnapshot r in p.resources)
					{
						if (r.flowState && r.resourceName == ResourceName)
						{
							if (r.flowState) // has the user chosen to make a flowable resource flow
							{
								Amount += r.amount;
								Capacity += r.maxAmount;
							}
						}
					}
				}
			}

			// calculate level
			Level = Capacity > double.Epsilon ? Amount / Capacity : 0.0;
		}

		/// <summary>Implementation of Resource_info_view</summary>
		/// <remarks>Only constructed by Resource_info class to hide the dependencies between the two classes</remarks>
		private class Resource_info_view_impl : Resource_info_view
		{
			public Resource_info_view_impl(string resource_name, Resource_info i)
			{
				info = i;
			}

			private Resource_info info;

			public override double deferred
			{
				get => info.Deferred;
			}
			public override double amount
			{
				get => info.Amount;
			}
			public override double capacity
			{
				get => info.Capacity;
			}

			public override void Produce(double quantity, string tag)
			{
				info.Produce(quantity, tag);
			}
			public override void Consume(double quantity, string tag)
			{
				info.Consume(quantity, tag);
			}
		}

		public Resource_info_view GetResourceInfoView()
		{
			return _vessel_wide_view;
		}

		/// <summary>record a deferred production for the vessel wide bookkeeping</summary>
		public void Produce(double quantity, string tag)
		{
			Deferred += quantity;

			// keep track of every producer contribution for UI/debug purposes
			if (Math.Abs(quantity) < 1e-06) return;

			if (brokers.ContainsKey(tag))
				brokers[tag] += quantity;
			else
				brokers.Add(tag, quantity);
		}

		/// <summary>record a deferred consumption for the vessel wide bookkeeping</summary>
		public void Consume(double quantity, string tag)
		{
			Deferred -= quantity;

			// keep track of every consumer contribution for UI/debug purposes
			if (Math.Abs(quantity) < 1e-06) return;

			if (brokers.ContainsKey(tag))
				brokers[tag] -= quantity;
			else
				brokers.Add(tag, -quantity);
		}

		/// <summary>synchronize resources from cache to vessel</summary>
		/// <remarks>
		/// this function will also sync from vessel to cache so you can always use the
		/// Resource_info interface to get information about resources
		/// </remarks>
		public void Sync(Vessel v, VesselData vd, double elapsed_s)
		{
			// # OVERVIEW
			// - deferred consumption/production is accumulated, then this function called
			// - detect amount/capacity in vessel
			// - clamp deferred to amount/capacity
			// - apply deferred
			// - update cached amount [disabled, see comments]
			// - calculate change rate per-second
			// - calculate resource level
			// - reset deferred

			// # NOTE
			// It is impossible to guarantee coherency in resource simulation of loaded vessels,
			// if consumers/producers external to the resource cache exist in the vessel (#96).
			// Such is the case for example on loaded vessels with stock solar panels.
			// The effect is that the whole resource simulation become dependent on timestep again.
			// From the user point-of-view, there are two cases:
			// - (A) the timestep-dependent error is smaller than capacity
			// - (B) the timestep-dependent error is bigger than capacity
			// In case [A], there are no consequences except a slightly wrong computed level and rate.
			// In case [B], the simulation became incoherent and from that point anything can happen,
			// like for example insta-death by co2 poisoning or climatization.
			// To avoid the consequences of [B]:
			// - we hacked the stock solar panel to use the resource cache
			// - we detect incoherency on loaded vessels, and forbid the two highest warp speeds

			// remember amount currently known, to calculate rate later on
			// this is explicitly vessel wide amount (even for non-flowing resources), because it is used
			// to visualize rate and restrict timewarp, which is only done per vessel
			double oldAmount = Amount;

			// remember capacity currently known, to detect flow state changes
			// this is explicitly vessel wide amount (even for non-flowing resources), because it is used
			// to restrict timewarp
			double oldCapacity = Capacity;

			// iterate over all enabled resource containers and detect amount/capacity again
			// - this detect production/consumption from stock and third-party mods
			//   that by-pass the resource cache, and flow state changes in general
			Amount = 0.0;
			Capacity = 0.0;

			// PLEASE READ
			// because only the first loop is garuanteed to run, we sync back non-flowing part
			// specific resources right here, rather than in the second loop
			// this is to avoid performance impact of introducing a third loop
			// the part specific amount or capacity does not count towards the global capacity
			// because these resources cannot flow freely
			// an example of a non-flowing resource would be enriched uranium
			if (v.loaded)
			{
				foreach (Part p in v.Parts)
				{
					foreach (PartResource r in p.Resources)
					{
						if (r.resourceName == ResourceName)
						{
							// a resource is either tracked vessel wide, or per part, but not both
							// the sum should always be realistic
							if (r.flowState)
							{
								Amount += r.amount;
								Capacity += r.maxAmount;
							}
						}
					}
				}
			}
			else
			{
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					foreach (ProtoPartResourceSnapshot r in p.resources)
					{
						if (r.resourceName == ResourceName)
						{
							// a resource is either tracked vessel wide, or per part, but not both
							// the sum should always be realistic
							if (r.flowState)
							{
								Amount += r.amount;
								Capacity += r.maxAmount;
							}
						}
					}
				}
			}

			// Calculate rate from non-Kerbalism producers/consumers
			double unsupportedBrokersRate = Amount - oldAmount;
			// avoid false detection due to precision errors in stock amounts
			if (Math.Abs(unsupportedBrokersRate) < 1e-05) unsupportedBrokersRate = 0.0;
			// Calculate rate
			unsupportedBrokersRate /= elapsed_s;

			// if incoherent producers are detected, do not allow high timewarp speed
			// - ignore incoherent consumers (no negative consequences for player)
			// - ignore flow state changes (avoid issue with process controllers)
			// - unloaded vessels can't be incoherent, we are in full control there
			// - can be disabled in settings
			// - avoid false detection due to precision errors in stock amounts
			// note that this is applied to all resources including non-flowing resources restricted to parts
			if (Settings.EnforceCoherency && v.loaded && TimeWarp.CurrentRateIndex >= 6 && unsupportedBrokersRate > 0.0 && Capacity - oldCapacity < 1e-05)
			{
				Message.Post
				(
				  Severity.warning,
				  Lib.BuildString
				  (
					!v.isActiveVessel ? Lib.BuildString("On <b>", v.vesselName, "</b>\na ") : "A ",
					"producer of <b>", ResourceName, "</b> has\n",
					"incoherent behavior at high warp speed.\n",
					"<i>Unload the vessel before warping</i>"
				  )
				);
				Lib.StopWarp(5);
			}

			// clamp consumption/production to vessel amount/capacity
			// - if deferred is negative, then amount is guaranteed to be greater than zero
			// - if deferred is positive, then capacity - amount is guaranteed to be greater than zero
			Deferred = Lib.Clamp(Deferred, -Amount, Capacity - Amount);

			// apply deferred consumption/production, simulating ALL_VESSEL_BALANCED
			// - iterating again is faster than using a temporary list of valid PartResources
			// - avoid very small values in deferred consumption/production
			if (Math.Abs(Deferred) > 1e-10)
			{
				if (v.loaded)
				{
					foreach (Part p in v.parts)
					{
						foreach (PartResource r in p.Resources)
						{
							if (r.flowState && r.resourceName == ResourceName)
							{
								// calculate consumption/production coefficient for the part
								double k = Deferred < 0.0
								  ? r.amount / Amount
								  : (r.maxAmount - r.amount) / (Capacity - Amount);

								// apply deferred consumption/production
								r.amount += Deferred * k;
							}
						}
					}
				}
				else
				{
					foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
					{
						foreach (ProtoPartResourceSnapshot r in p.resources)
						{
							if (r.flowState && r.resourceName == ResourceName)
							{
								// calculate consumption/production coefficient for the part
								double k = Deferred < 0.0
								  ? r.amount / Amount
								  : (r.maxAmount - r.amount) / (Capacity - Amount);

								// apply deferred consumption/production
								r.amount += Deferred * k;
							}
						}
					}
				}
			}

			// update amount, to get correct rate and levels at all times
			Amount += Deferred;

			// calculate rate of change per-second
			// - don't update rate during and immediately after warp blending (stock modules have instabilities during warp blending)
			// - ignore interval based rules changes
			// note that we explicitly read vessel average resources here, even for non-flowing resources, because this is
			// for visualization which is per vessel
			if (!v.loaded || Kerbalism.warp_blending > 50) Rate = (Amount - oldAmount - intervalRuleAmount) / elapsed_s;

			// recalculate level
			Level = Capacity > double.Epsilon ? Amount / Capacity : 0.0;

			// reset deferred production/consumption
			Deferred = 0.0;

			vd.Supply(ResourceName).UpdateResourceBrokers(brokers, ruleBrokers, unsupportedBrokersRate, elapsed_s);

			if (PreferencesLifeSupport.Instance.resourceLogging)
			{
				Lib.Log("RESOURCE UPDATE : " + v);
				foreach (var rb in vd.Supply(ResourceName).ResourceBrokers)
					Lib.Log(Lib.BuildString(ResourceName, " : ", rb.rate.ToString("+0.000000;-0.000000;+0.000000"), "/s (", rb.name, ")"));
				Lib.Log("RESOURCE UPDATE END");
			}


			intervalRulesRate = 0.0;
			foreach (var rb in ruleBrokers)
			{
				intervalRulesRate += rb.Value;
			}

			brokers.Clear();
			ruleBrokers.Clear();
			intervalRuleAmount = 0.0;
		}

		/// <summary>estimate time until depletion, including the simulated rate from interval-based rules</summary>
		public double DepletionTime()
		{
			// calculate total rate of change
			double delta = AverageRate;

			// return depletion
			return Amount <= 1e-10 ? 0.0 : AverageRate >= -1e-10 ? double.NaN : Amount / -AverageRate;
		}

		/// <summary>Inform that meal has happened in this simulation step</summary>
		/// <remarks>A simulation step can cover many physics ticks, especially for unloaded vessels</remarks>
		public void UpdateIntervalRule(double amount, double averageRate, string ruleName)
		{
			intervalRuleAmount += amount;
			intervalRulesRate += averageRate;

			if (ruleBrokers.ContainsKey(ruleName))
				ruleBrokers[ruleName] += averageRate;
			else
				ruleBrokers.Add(ruleName, averageRate);
		}

		// associated resource name
		/// <summary> rate of change in amount per-second, this is purely for visualization</summary>
		public string ResourceName { get; private set; }

		/// <summary> rate of change in amount per-second, this is purely for visualization</summary>
		public double Rate { get; private set; }

		/// <summary> rate of change in amount per-second, including average rate for interval-based rules</summary>
		public double AverageRate => Rate + intervalRulesRate;

		/// <summary> amount vs capacity, or 0 if there is no capacity</summary>
		public double Level { get; private set; }

		/// <summary> true if a meal-like consumption/production was processed in the last simulation step</summary>
		private bool IntervalRuleHappened => intervalRuleAmount > 0.0;



		// the getters provide the total value for the vessel, this is typically either:
		// * vessel wide value if resource_name can flow between parts
		// * sum of all part specific values if resource_name cannot flow between parts
		// this means the getters here are meant for visualization purposes, not for the actual simulation

		/// <summary> not yet consumed or produced amount that will be synchronized to the vessel in Sync()</summary>
		public double Deferred { get; private set; }

		/// <summary> amount of resource</summary>
		public double Amount { get; private set; }

		/// <summary> storage capacity of resource</summary>
		public double Capacity { get; private set; }

		/// <summary> simulated average rate for interval-based rules in amount per-second </summary>
		private double intervalRulesRate;

		/// <summary> Amount consumed/produced by interval-based rules in this simulation step</summary>
		private double intervalRuleAmount;

		/// <summary>Dictionary of all consumers and producers (key) and how much amount they did add/remove (value).</summary>
		private Dictionary<string, double> brokers;

		/// <summary>Dictionary of all interval-based rules (key) and their average rate (value).</summary>
		private Dictionary<string, double> ruleBrokers;

		private Resource_info_view _vessel_wide_view;
	}

	/// <summary>destription of how to convert inputs to outputs</summary>
	/// <remarks>
	/// this class is also responsible for executing the recipe, such that it is actualized in the Resource_info
	/// </remarks>
	public sealed class Resource_recipe
	{
		public struct Entry
		{
			public Entry(string name, double quantity, bool dump = true, string combined = null)
			{
				this.name = name;
				this.combined = combined;
				this.quantity = quantity;
				this.inv_quantity = 1.0 / quantity;
				this.dump = dump;
			}
			public string name;
			public string combined;    // if entry is the primary to be combined, then the secondary resource is named here. secondary entry has its combined set to "" not null
			public double quantity;
			public double inv_quantity;
			public bool dump;
		}

		public Resource_recipe(Part p, string name)
		{
			this.inputs = new List<Entry>();
			this.outputs = new List<Entry>();
			this.cures = new List<Entry>();
			this.left = 1.0;
			this.loaded_part = p;
			this.name = name;
		}

		public Resource_recipe(ProtoPartSnapshot p, string name)
		{
			this.inputs = new List<Entry>();
			this.outputs = new List<Entry>();
			this.cures = new List<Entry>();
			this.left = 1.0;
			this.unloaded_part = p;
			this.name = name;
		}

		private Resource_info_view GetResourceInfoView(Vessel v, Vessel_resources resources, string resource_name)
		{
			return resources.Info(v, resource_name).GetResourceInfoView();
		}

		/// <summary>
		/// add an input to the recipe
		/// </summary>
		public void Input(string resource_name, double quantity)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new Entry(resource_name, quantity));
			}
		}

		/// <summary>
		/// add a combined input to the recipe
		/// /// </summary>
		public void Input(string resource_name, double quantity, string combined)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new Entry(resource_name, quantity, true, combined));
			}
		}

		// add an output to the recipe
		public void Output(string resource_name, double quantity, bool dump)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				outputs.Add(new Entry(resource_name, quantity, dump));
			}
		}

		// add a cure to the recipe
		public void Cure(string cure, double quantity, string resource_name)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				cures.Add(new Entry(cure, quantity, true, resource_name));
			}
		}

		// execute the recipe
		public bool Execute(Vessel v, Vessel_resources resources)
		{
			// determine worst input ratio
			// - pure input recipes can just underflow
			double worst_input = left;
			if (outputs.Count > 0)
			{
				for (int i = 0; i < inputs.Count; ++i)
				{
					Entry e = inputs[i];
					Resource_info_view res = GetResourceInfoView(v, resources, e.name);
					// handle combined inputs
					if (e.combined != null)
					{
						// is combined resource the primary
						if (e.combined != "")
						{
							Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
							Resource_info_view sec = GetResourceInfoView(v, resources, sec_e.name);
							double pri_worst = Lib.Clamp((res.amount + res.deferred) * e.inv_quantity, 0.0, worst_input);
							if (pri_worst > 0.0)
							{
								worst_input = pri_worst;
							}
							else
							{
								worst_input = Lib.Clamp((sec.amount + sec.deferred) * sec_e.inv_quantity, 0.0, worst_input);
							}
						}
					}
					else
					{
						worst_input = Lib.Clamp((res.amount + res.deferred) * e.inv_quantity, 0.0, worst_input);
					}
				}
			}

			// determine worst output ratio
			// - pure output recipes can just overflow
			double worst_output = left;
			if (inputs.Count > 0)
			{
				for (int i = 0; i < outputs.Count; ++i)
				{
					Entry e = outputs[i];
					if (!e.dump) // ignore outputs that can dump overboard
					{
						Resource_info_view res = GetResourceInfoView(v, resources, e.name);
						worst_output = Lib.Clamp((res.capacity - (res.amount + res.deferred)) * e.inv_quantity, 0.0, worst_output);
					}
				}
			}

			// determine worst-io
			double worst_io = Math.Min(worst_input, worst_output);

			// consume inputs
			for (int i = 0; i < inputs.Count; ++i)
			{
				Entry e = inputs[i];
				Resource_info_view res = GetResourceInfoView(v, resources, e.name);
				// handle combined inputs
				if (e.combined != null)
				{
					// is combined resource the primary
					if (e.combined != "")
					{
						Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
						Resource_info_view sec = GetResourceInfoView(v, resources, sec_e.name);
						double need = (e.quantity * worst_io) + (sec_e.quantity * worst_io);
						// do we have enough primary to satisfy needs, if so don't consume secondary
						if (res.amount + res.deferred >= need) resources.Consume(v, e.name, need, name);
						// consume primary if any available and secondary
						else
						{
							need -= res.amount + res.deferred;
							res.Consume(res.amount + res.deferred, name);
							sec.Consume(need, name);
						}
					}
				}
				else 
				{
					res.Consume(e.quantity * worst_io, name);
				}
			}

			// produce outputs
			for (int i = 0; i < outputs.Count; ++i)
			{
				Entry e = outputs[i];
				Resource_info_view res = GetResourceInfoView(v, resources, e.name);
				res.Produce(e.quantity * worst_io, name);
			}

			// produce cures
			for (int i = 0; i < cures.Count; ++i)
			{
				Entry entry = cures[i];
				List<RuleData> curingRules = new List<RuleData>();
				foreach(ProtoCrewMember crew in v.GetVesselCrew()) {
					KerbalData kd = DB.Kerbal(crew.name);
					if(kd.sickbay.IndexOf(entry.combined + ",", StringComparison.Ordinal) >= 0) {
						curingRules.Add(kd.Rule(entry.name));
					}
				}

				foreach(RuleData rd in curingRules)
				{
					rd.problem -= entry.quantity * worst_io / curingRules.Count;
					rd.problem = Math.Max(rd.problem, 0);
				}
			}

			// update amount left to execute
			left -= worst_io;

			// the recipe was executed, at least partially
			return worst_io > double.Epsilon;
		}

		public List<Entry> inputs;   // set of input resources
		public List<Entry> outputs;  // set of output resources
		public List<Entry> cures;    // set of cures
		public double left;     // what proportion of the recipe is left to execute

		private string name;
		private Part loaded_part = null; // one of these is null
		private ProtoPartSnapshot unloaded_part = null;
	}

	/// <summary>
	/// contains all resource recipes that are pending or being executed on this vessel
	/// it also contains the information for all resources contained within this vessel
	/// </summary>
	/// <remarks>
	/// processes use psuedo-resources as a multiplier for their recipes, these
	/// pseudo resources are also contained within
	/// </remarks>
	public sealed class Vessel_resources
	{
		// return a resource handler
		public Resource_info Info(Vessel v, string resource_name)
		{
			// try to get existing entry if any
			Resource_info res;
			if (resources.TryGetValue(resource_name, out res)) return res;

			// create new entry
			res = new Resource_info(v, resource_name);

			// remember new entry
			resources.Add(resource_name, res);

			// return new entry
			return res;
		}

		/// <summary> apply consumers/producers deferred requests for a vessel and synchronize the new amount in the vessel</summary>
		public void Sync(Vessel v, VesselData vd, double elapsed_s)
		{
			// execute all possible recipes
			bool executing = true;
			while (executing)
			{
				executing = false;
				for (int i = 0; i < recipes.Count; ++i)
				{
					Resource_recipe recipe = recipes[i];
					if (recipe.left > double.Epsilon)
					{
						executing |= recipe.Execute(v, this);
					}
				}
			}

			// forget the recipes
			recipes.Clear();

			// apply all deferred requests and synchronize to vessel
			foreach (var pair in resources) pair.Value.Sync(v, vd, elapsed_s);
		}

		// record deferred production of a resource (shortcut)
		public void Produce(Vessel v, string resource_name, double quantity, string tag)
		{
			Info(v, resource_name).Produce(quantity, tag);
		}

		// record deferred consumption of a resource (shortcut)
		public void Consume(Vessel v, string resource_name, double quantity, string tag)
		{
			Info(v, resource_name).Consume(quantity, tag);
		}

		// record deferred execution of a recipe
		public void Transform(Resource_recipe recipe)
		{
			recipes.Add(recipe);
		}

		public Dictionary<string, Resource_info> resources = new Dictionary<string, Resource_info>(32);
		public List<Resource_recipe> recipes = new List<Resource_recipe>(4);
	}

	/// <summary>cache for the resources of all vessels</summary>
	public static class ResourceCache
	{
		public static void Init()
		{
			entries = new Dictionary<Guid, Vessel_resources>();
		}

		public static void Clear()
		{
			entries.Clear();
		}

		public static void Purge(Vessel v)
		{
			entries.Remove(v.id);
		}

		public static void Purge(ProtoVessel pv)
		{
			entries.Remove(pv.vesselID);
		}

		// return resource cache for a vessel
		public static Vessel_resources Get(Vessel v)
		{
			// try to get existing entry if any
			Vessel_resources entry;
			if (entries.TryGetValue(v.id, out entry)) return entry;

			// create new entry
			entry = new Vessel_resources();

			// remember new entry
			entries.Add(v.id, entry);

			// return new entry
			return entry;
		}

		// return a resource handler (shortcut)
		public static Resource_info Info(Vessel v, string resource_name)
		{
			return Get(v).Info(v, resource_name);
		}

		// register deferred production of a resource (shortcut)
		public static void Produce(Vessel v, string resource_name, double quantity, string tag)
		{
			Info(v, resource_name).Produce(quantity, tag);
		}

		// register deferred consumption of a resource (shortcut)
		public static void Consume(Vessel v, string resource_name, double quantity, string tag)
		{
			Info(v, resource_name).Consume(quantity, tag);
		}

		// register deferred execution of a recipe (shortcut)
		public static void Transform(Vessel v, Resource_recipe recipe)
		{
			Get(v).Transform(recipe);
		}

		// resource cache
		static Dictionary<Guid, Vessel_resources> entries;
	}

	// equalize/vent a vessel
	public static class ResourceBalance
	{
		// This Method has a lot of "For\Foreach" because it was design for multi resources
		// Method don't count disabled habitats
		public static void Equalizer(Vessel v)
		{
			// get resource level in habitats
			double[] res_level = new double[resourceName.Length];                   // Don't count Manned or Depressiong habitats

			// Total resource in parts not disabled
			double[] totalAmount = new double[resourceName.Length];
			double[] maxAmount = new double[resourceName.Length];

			// Total resource in Enabled parts (No crew)
			double[] totalE = new double[resourceName.Length];
			double[] maxE = new double[resourceName.Length];

			// Total resource in Manned parts (Priority!)
			double[] totalP = new double[resourceName.Length];
			double[] maxP = new double[resourceName.Length];

			// Total resource in Depressurizing
			double[] totalD = new double[resourceName.Length];
			double[] maxD = new double[resourceName.Length];

			// amount to equalize speed
			double[] amount = new double[resourceName.Length];

			// Can be positive or negative, controlling the resource flow
			double flowController;

			bool[] mannedisPriority = new bool[resourceName.Length];                // The resource is priority
			bool equalize = false;                                                  // Has any resource that needs to be equalized

			// intial value
			for (int i = 0; i < resourceName.Length; i++)
			{
				totalAmount[i] = new Resource_info(v, resourceName[i]).Rate;        // Get generate rate for each resource
				maxAmount[i] = 0;

				totalE[i] = 0;
				maxE[i] = 0;

				totalP[i] = 0;
				maxP[i] = 0;

				totalD[i] = 0;
				maxD[i] = 0;

				mannedisPriority[i] = false;
			}

			double max_pressure = 1.0;
			foreach (Habitat partHabitat in v.FindPartModulesImplementing<Habitat>())
			{
				// Skip disabled habitats
				if (partHabitat.state != Habitat.State.disabled)
				{
					max_pressure = Math.Min(max_pressure, partHabitat.max_pressure);

					// Has flag to be Equalized?
					equalize |= partHabitat.needEqualize;

					PartResource[] resources = new PartResource[resourceName.Length];
					for (int i = 0; i < resourceName.Length; i++)
					{
						if (partHabitat.part.Resources.Contains(resourceName[i]))
						{
							PartResource t = partHabitat.part.Resources[resourceName[i]];

							// Manned Amounts
							if (Lib.IsCrewed(partHabitat.part))
							{
								totalP[i] += t.amount;
								maxP[i] += t.maxAmount;
							}
							// Amount for Depressurizing
							else if (partHabitat.state == Habitat.State.depressurizing)
							{
								totalD[i] += t.amount;
								maxD[i] += t.maxAmount;
							}
							else
							{
								totalE[i] += t.amount;
								maxE[i] += t.maxAmount;
							}
							totalAmount[i] += t.amount;
							maxAmount[i] += t.maxAmount;
						}
					}
				}
			}

			v.KerbalismData().maxPressure = max_pressure; // TODO: (GOT) max_pressure should be either evaluated in VesselData or set here, but it can't be both, there is something deeply wrong here !
			Cache.SetVesselObjectsCache<double>(v, "max_pressure", max_pressure);

			if (!equalize) return;

			for (int i = 0; i < resourceName.Length; i++)
			{
				// resource level for Enabled habitats no Manned
				res_level[i] = totalE[i] / (maxAmount[i] - maxP[i]);

				// Manned is priority?
				// If resource amount is less then maxAmount in manned habitat and it's flagged to equalize, define as priority
				// Using Atmosphere, N2, O2 as Priority trigger (we don't want to use CO2 or Humidity as a trigger)
				if (resourceName[i] != "WasteAtmosphere" && resourceName[i] != "MoistAtmosphere" && equalize)
				{
					mannedisPriority[i] = maxP[i] - totalP[i] > 0;
				}

				// determine generic equalization speed	per resource
				if (mannedisPriority[i])
					amount[i] = maxAmount[i] * equalize_speed * Kerbalism.elapsed_s;
				else
					amount[i] = (maxE[i] + maxD[i]) * equalize_speed * Kerbalism.elapsed_s;
			}

			if (equalize)
			{
				foreach (Habitat partHabitat in v.FindPartModulesImplementing<Habitat>())
				{
					bool stillNeed = false;
					if (partHabitat.state != Habitat.State.disabled)
					{
						for (int i = 0; i < resourceName.Length; i++)
						{
							if (partHabitat.part.Resources.Contains(resourceName[i]))
							{
								PartResource t = partHabitat.part.Resources[resourceName[i]];
								flowController = 0;

								// Conditions in order
								// If perctToMax = 0 (means Habitat will have 0% of amount:
								//	1 case: modules still needs to be equalized
								//	2 case: has depressurizing habitat
								//	3 case: dropping everything into the priority habitats

								if ((Math.Abs(res_level[i] - (t.amount / t.maxAmount)) > precision && !Lib.IsCrewed(partHabitat.part))
									|| ((partHabitat.state == Habitat.State.depressurizing
									|| mannedisPriority[i]) && t.amount > double.Epsilon))
								{
									double perctToAll;              // Percent of resource for this habitat related
									double perctRest;               // Percent to fill priority

									perctToAll = t.amount / maxAmount[i];

									double perctToType;
									double perctToMaxType;

									// Percts per Types
									if (Lib.IsCrewed(partHabitat.part))
									{
										perctToType = t.amount / totalP[i];
										perctToMaxType = t.maxAmount / maxP[i];
									}
									else if (partHabitat.state == Habitat.State.depressurizing)
									{
										perctToType = t.amount / totalD[i];
										perctToMaxType = t.maxAmount / maxD[i];
									}
									else
									{
										perctToType = t.amount / totalE[i];
										perctToMaxType = t.maxAmount / maxE[i];
									}

									// Perct from the left resource
									if (totalAmount[i] - maxP[i] <= 0 || partHabitat.state == Habitat.State.depressurizing)
									{
										perctRest = 0;
									}
									else
									{
										perctRest = (((totalAmount[i] - maxP[i]) * perctToMaxType) - t.amount) / totalE[i];
									}

									// perctToMax < perctToAll ? habitat will send resource : otherwise will receive, flowController == 0 means no flow
									if ((partHabitat.state == Habitat.State.depressurizing || totalAmount[i] - maxP[i] <= 0) && !Lib.IsCrewed(partHabitat.part))
									{
										flowController = 0 - perctToType;
									}
									else if (mannedisPriority[i] && !Lib.IsCrewed(partHabitat.part))
									{
										flowController = Math.Min(perctToMaxType - perctToAll, (t.maxAmount - t.amount) / totalAmount[i]);
									}
									else
									{
										flowController = perctRest;
									}

									// clamp amount to what's available in the hab and what can fit in the part
									double amountAffected;
									if (partHabitat.state == Habitat.State.depressurizing)
									{
										amountAffected = flowController * totalD[i];
									}
									else if (!mannedisPriority[i] || !Lib.IsCrewed(partHabitat.part))
									{
										amountAffected = flowController * totalE[i];
									}
									else
									{
										amountAffected = flowController * totalP[i];
									}

									amountAffected *= equalize_speed;

									amountAffected = Math.Sign(amountAffected) >= 0 ? Math.Max(Math.Sign(amountAffected) * precision, amountAffected) : Math.Min(Math.Sign(amountAffected) * precision, amountAffected);

									double va = amountAffected < 0.0
										? Math.Abs(amountAffected) > t.amount                // If negative, habitat can't send more than it has
										? t.amount * (-1)
										: amountAffected
										: Math.Min(amountAffected, t.maxAmount - t.amount);  // if positive, habitat can't receive more than max

									va = Double.IsNaN(va) ? 0.0 : va;

									// consume relative percent of this part
									t.amount += va;

									if (va < double.Epsilon) stillNeed = false;
									else stillNeed = true;
								}
							}
						}
					}

					partHabitat.needEqualize = stillNeed;
				}


			}
		}

		// constants
		const double equalize_speed = 0.01;  // equalization/venting mutiple speed per-second, in proportion to amount

		// Resources to equalize
		public static string[] resourceName = new string[3] { "Atmosphere", "WasteAtmosphere", "MoistAtmosphere" };

		// Resources to equalize
		public static double precision = 0.00001;
	}
}
