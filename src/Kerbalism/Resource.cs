using System;
using System.Collections.Generic;
using System.Linq;

namespace KERBALISM
{
	// this class serves as a convient way of accessing resource levels
	// in exactly the same way as Resource_info (i.e. more readable than getter funtions)
	// without most of the code being aware if this is vessel wide
	// or part specific resources
	public abstract class Resource_info_view
	{
		protected Resource_info_view() {}
		public abstract double deferred { get; }
		public abstract double amount { get; }
		public abstract double capacity { get; }

		// record a deferred production
		public abstract void Produce(double quantity);
		// record a deferred consumption
		public abstract void Consume(double quantity);
	}

	// store info about a resource in a vessel
	public sealed class Resource_info
	{
		public Resource_info(Vessel v, string res_name)
		{
			// remember resource name
			resource_name = res_name;

			// vessel-wide resources
			InitDicts(new Resource_location());

			// get amount & capacity
			if (v.loaded)
			{
				foreach (Part p in v.Parts)
				{
					foreach (PartResource r in p.Resources)
					{
						if (r.resourceName == resource_name)
						{
							if (r.info.resourceFlowMode == ResourceFlowMode.NO_FLOW) // resource can never flow, e.g. enriched uranium
							{
								// per part resources
								Resource_location location = new Resource_location(p);
								InitDicts(location);
								_amount[location] = r.amount;
								_capacity[location] = r.maxAmount;
							}
							else if (r.flowState) // has the user chosen to make a flowable resource flow
							{
								_amount[vessel_wide_location] += r.amount;
								_capacity[vessel_wide_location] += r.maxAmount;
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
						if (r.flowState && r.resourceName == resource_name)
						{
							if (r.definition.resourceFlowMode == ResourceFlowMode.NO_FLOW) // resource can never flow, e.g. enriched uranium
							{
								// per part resources
								Resource_location location = new Resource_location(p);
								InitDicts(location);
								_amount[location] = r.amount;
								_capacity[location] = r.maxAmount;
							}
							else if (r.flowState) // has the user chosen to make a flowable resource flow
							{
								_amount[vessel_wide_location] += r.amount;
								_capacity[vessel_wide_location] += r.maxAmount;
							}
						}
					}
				}
			}

			// calculate level
			level = capacity > double.Epsilon ? amount / capacity : 0.0;
		}

		// Identifier for either:
		// Vessel-wide resources
		// Per-part resources
		// Used to identify loaded and unloaded parts in the same way
		private class Resource_location
		{
			public Resource_location(Part p)
			{
#if !KSP13 // for KSP13 everything will go into vessel wide storage due to lack of persistentId
				vessel_wide = false;
				persistent_identifier = p.persistentId;
#endif
			}
			public Resource_location(ProtoPartSnapshot p)
			{
#if !KSP13
				vessel_wide = false;
				persistent_identifier = p.persistentId;
#endif
			}
			public Resource_location() {}

			public bool IsVesselWide() { return vessel_wide; }
			public uint GetPersistentPartId() { return persistent_identifier; }


			private bool vessel_wide = true;
			private uint persistent_identifier = 0;
		}

		// see comments of Resource_info_view
		private class Resource_info_view_impl : Resource_info_view
		{
			// null parts go to vessel-wide and are intended for kerbal (e.g. eating) related rules
			public Resource_info_view_impl(Part p, string resource_name, Resource_info i)
			{
				info = i;
				if (p != null && new PartResourceDefinition(resource_name).resourceFlowMode == ResourceFlowMode.NO_FLOW)
				{
					location = new Resource_location(p);
					if (!info._deferred.ContainsKey(location)) info.InitDicts(location);
				}
				else
				{
					location = info.vessel_wide_location;
				}
			}
			public Resource_info_view_impl(ProtoPartSnapshot p, string resource_name, Resource_info i)
			{
				info = i;
				if (p != null && new PartResourceDefinition(resource_name).resourceFlowMode == ResourceFlowMode.NO_FLOW)
				{
					location = new Resource_location(p);
				}
				else
				{
					location = info.vessel_wide_location;
				}
			}

			private Resource_location location;
			private Resource_info info;

			public override double deferred
			{
				get => info._deferred[location];
			}
			public override double amount
			{
				get => info._amount[location];
			}
			public override double capacity
			{
				get => info._capacity[location];
			}

			public override void Produce(double quantity)
			{
				info.Produce(location, quantity);
			}
			public override void Consume(double quantity)
			{
				info.Consume(location, quantity);
			}
		}

		// usually invoked if a per-part non-flowing resource appears
		private void InitDicts(Resource_location location)
		{
			_deferred[location] = 0.0;
			_amount[location] = 0.0;
			_capacity[location] = 0.0;
		}

		public Resource_info_view GetResourceInfoView(Part p)
		{
			if (!_cached_part_views.ContainsKey(p))
			{
				_cached_part_views[p] = new Resource_info_view_impl(p, resource_name, this);
			}
			return _cached_part_views[p];
		}

		public Resource_info_view GetResourceInfoView(ProtoPartSnapshot p)
		{
			if (!_cached_proto_part_views.ContainsKey(p))
			{
				_cached_proto_part_views[p] = new Resource_info_view_impl(p, resource_name, this);
			}
			return _cached_proto_part_views[p];
		}

		// record a deferred production
		public void Produce(double quantity)
		{
			_deferred[vessel_wide_location] += quantity;
		}
		private void Produce(Resource_location location, double quantity)
		{
			_deferred[location] += quantity;
		}

		// record a deferred consumption
		public void Consume(double quantity)
		{
			_deferred[vessel_wide_location] -= quantity;
		}
		private void Consume(Resource_location location, double quantity)
		{
			_deferred[location] -= quantity;
		}

		// synchronize amount from cache to vessel
		public void Sync(Vessel v, double elapsed_s)
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
			double old_amount = amount;

			// remember capacity currently known, to detect flow state changes
			// this is explicitly vessel wide amount (even for non-flowing resources), because it is used
			// to restrict timewarp
			double old_capacity = capacity;

			// iterate over all enabled resource containers and detect amount/capacity again
			// - this detect production/consumption from stock and third-party mods
			//   that by-pass the resource cache, and flow state changes in general
			_amount[vessel_wide_location] = 0.0;
			_capacity[vessel_wide_location] = 0.0;

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
						if (r.flowState && r.resourceName == resource_name)
						{
							_amount[vessel_wide_location] += r.amount;
							_capacity[vessel_wide_location] += r.maxAmount;

							// sync back part specific non-flowing resources
							Resource_location location = new Resource_location(p);
							if (_amount.ContainsKey(location))
							{
								// see notes with the vessel_wide_location equivalent
								_amount[location] = r.amount;
								_capacity[location] = r.maxAmount;

								// actual sync-ing back
								r.amount = Lib.Clamp(r.amount + _deferred[location], 0, r.maxAmount);
								_amount[location] = r.amount;
								_deferred[location] = 0.0;
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
						if (r.flowState && r.resourceName == resource_name)
						{
							_amount[vessel_wide_location] += r.amount;
							_capacity[vessel_wide_location] += r.maxAmount;

							// sync back part specific non-flowing resources
							Resource_location location = new Resource_location(p);
							if (_amount.ContainsKey(location))
							{
								// see notes with the vessel_wide_location equivalent
								_amount[location] = r.amount;
								_capacity[location] = r.maxAmount;

								// actual sync-ing back
								r.amount = Lib.Clamp(r.amount + _deferred[location], 0, r.maxAmount);
								_amount[location] = r.amount;
								_deferred[location] = 0.0;
							}
						}
					}
				}
			}

			// if incoherent producers are detected, do not allow high timewarp speed
			// - ignore incoherent consumers (no negative consequences for player)
			// - ignore flow state changes (avoid issue with process controllers)
			// - unloaded vessels can't be incoherent, we are in full control there
			// - can be disabled in settings
			// - avoid false detection due to precision errors in stock amounts
			// note that this is applied to all resources including non-flowing resources restricted to parts
			if (Settings.EnforceCoherency && v.loaded && TimeWarp.CurrentRateIndex >= 6 && amount - old_amount > 1e-05 && capacity - old_capacity < 1e-05)
			{
				Message.Post
				(
				  Severity.warning,
				  Lib.BuildString
				  (
					!v.isActiveVessel ? Lib.BuildString("On <b>", v.vesselName, "</b>\na ") : "A ",
					"producer of <b>", resource_name, "</b> has\n",
					"incoherent behavior at high warp speed.\n",
					"<i>Unload the vessel before warping</i>"
				  )
				);
				Lib.StopWarp(5);
			}

			// clamp consumption/production to vessel amount/capacity
			// - if deferred is negative, then amount is guaranteed to be greater than zero
			// - if deferred is positive, then capacity - amount is guaranteed to be greater than zero
			_deferred[vessel_wide_location] = Lib.Clamp(deferred, -amount, capacity - amount);

			// apply deferred consumption/production, simulating ALL_VESSEL_BALANCED
			// - iterating again is faster than using a temporary list of valid PartResources
			// - avoid very small values in deferred consumption/production
			if (Math.Abs(deferred) > 1e-10)
			{
				if (v.loaded)
				{
					foreach (Part p in v.parts)
					{
						foreach (PartResource r in p.Resources)
						{
							if (r.flowState && r.resourceName == resource_name)
							{
								// calculate consumption/production coefficient for the part
								double k = _deferred[vessel_wide_location] < 0.0
								  ? r.amount / _amount[vessel_wide_location]
								  : (r.maxAmount - r.amount) / (_capacity[vessel_wide_location] - _amount[vessel_wide_location]);

								// apply deferred consumption/production
								r.amount += _deferred[vessel_wide_location] * k;
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
							if (r.flowState && r.resourceName == resource_name)
							{
								// calculate consumption/production coefficient for the part
								double k = _deferred[vessel_wide_location] < 0.0
								  ? r.amount / _amount[vessel_wide_location]
								  : (r.maxAmount - r.amount) / (_capacity[vessel_wide_location] - _amount[vessel_wide_location]);

								// apply deferred consumption/production
								r.amount += _deferred[vessel_wide_location] * k;
							}
						}
					}
				}
			}

			// update amount, to get correct rate and levels at all times
			_amount[vessel_wide_location] += _deferred[vessel_wide_location];

			// calculate rate of change per-second
			// - don't update rate during and immediately after warp blending (stock modules have instabilities during warp blending)
			// - don't update rate during the simulation steps where meal is consumed, to avoid counting it twice
			// note that we explicitly read vessel average resources here, even for non-flowing resources, because this is
			// for visualization which is per vessel
			if ((!v.loaded || Kerbalism.warp_blending > 50) && !meal_happened) rate = (amount - old_amount) / elapsed_s;

			// recalculate level
			level = capacity > double.Epsilon ? amount / capacity : 0.0;

			// reset deferred production/consumption
			_deferred[vessel_wide_location] = 0.0;

			// reset meal flag
			meal_happened = false;
		}

		// estimate time until depletion
		public double Depletion(int crew_count)
		{
			// calculate all interval-normalized rates from related rules
			double meal_rate = 0.0;
			if (crew_count > 0)
			{
				foreach (Rule rule in Profile.rules)
				{
					if (rule.interval > 0)
					{
						if (rule.input == resource_name) meal_rate -= rule.rate / rule.interval;
						if (rule.output == resource_name) meal_rate += rule.rate / rule.interval;
					}
				}
				meal_rate *= (double)crew_count;
			}

			// calculate total rate of change
			double delta = rate + meal_rate;

			// return depletion
			return amount <= double.Epsilon ? 0.0 : delta >= -1e-10 ? double.NaN : amount / -delta;
		}

		// Inform that meal has happened in this simulation step (which potentially covers many physics ticks)
		public void SetMealHappened()
		{
			meal_happened = true;
		}

		// Enforce that modification happens through official accessor functions
		// Many external classes need to read these values, and they want convenient access
		// However direct modification of these members from outside would make the coupling with this class far too high
		public string resource_name
		{
			get {
				return _resource_name;
			}
			private set {
				_resource_name = value;
			}
		}
		public double rate
		{
			get {
				return _rate;
			}
			private set {
				_rate = value;
			}
		}
		public double level
		{
			get {
				return _level;
			}
			private set {
				_level = value;
			}
		}
		public bool meal_happened
		{
			get {
				return _meal_happened;
			}
			private set {
				_meal_happened = value;
			}
		}

		// the setters don't exist because they cannot be implemented
		// the getters provide the total value for the vessel, this is typically either:
		// * vessel wide value if resource_name can flow between parts
		// * sum of all part specific values if resource_name cannot flow between parts
		// this means the getters here are meant for visualization purposes, not for the actual simulation
		public double deferred
		{
			get {
				return _deferred.Values.Sum();
			}
		}
		public double amount
		{
			get {
				return _amount.Values.Sum();
			}
		}
		public double capacity
		{
			get {
				return _capacity.Values.Sum();
			}
		}

		private string _resource_name; // associated resource name
		private double _rate;          // rate of change in amount per-second, this is purely for visualization so don't track per part
		private double _level;         // amount vs capacity, or 0 if there is no capacity
		private bool _meal_happened;   // true if a meal-like consumption/production was processed in the last simulation step

		private IDictionary<Resource_location, double> _deferred = new Dictionary<Resource_location, double>(); // accumulate deferred requests
		private IDictionary<Resource_location, double> _amount = new Dictionary<Resource_location, double>();   // amount of resource
		private IDictionary<Resource_location, double> _capacity = new Dictionary<Resource_location, double>(); // storage capacity of resource

		private Resource_location vessel_wide_location = new Resource_location(); // to avoid constructing this object many times
		private IDictionary<Part, Resource_info_view> _cached_part_views = new Dictionary<Part, Resource_info_view>();
		private IDictionary<ProtoPartSnapshot, Resource_info_view> _cached_proto_part_views = new Dictionary<ProtoPartSnapshot, Resource_info_view>();
	}

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

		public Resource_recipe(Part p)
		{
			this.inputs = new List<Entry>();
			this.outputs = new List<Entry>();
			this.cures = new List<Entry>();
			this.left = 1.0;
			this.loaded_part = p;
		}

		public Resource_recipe(ProtoPartSnapshot p)
		{
			this.inputs = new List<Entry>();
			this.outputs = new List<Entry>();
			this.cures = new List<Entry>();
			this.left = 1.0;
			this.unloaded_part = p;
		}

		private Resource_info_view GetResourceInfoView(Vessel v, Vessel_resources resources, string resource_name)
		{
			if (loaded_part)
			{
				return resources.Info(v, resource_name).GetResourceInfoView(loaded_part);
			}
			else // note unloaded _part can also be null
			{
				return resources.Info(v, resource_name).GetResourceInfoView(unloaded_part);
			}
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
							if (pri_worst > 0.0) worst_input = pri_worst;
							else worst_input = Lib.Clamp((sec.amount + sec.deferred) * sec_e.inv_quantity, 0.0, worst_input);
						}
					}
					else worst_input = Lib.Clamp((res.amount + res.deferred) * e.inv_quantity, 0.0, worst_input);
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
				// handle combined inputs
				if (e.combined != null)
				{
					// is combined resource the primary
					if (e.combined != "")
					{
						Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
						Resource_info_view sec = GetResourceInfoView(v, resources, sec_e.name);
						Resource_info_view res = GetResourceInfoView(v, resources, e.name);
						double need = (e.quantity * worst_io) + (sec_e.quantity * worst_io);
						// do we have enough primary to satisfy needs, if so don't consume secondary
						if (res.amount + res.deferred >= need) resources.Consume(v, e.name, need);
						// consume primary if any available and secondary
						else
						{
							need -= res.amount + res.deferred;
							resources.Consume(v, e.name, res.amount + res.deferred);
							resources.Consume(v, sec_e.name, need);
						}
					}
				}
				else resources.Consume(v, e.name, e.quantity * worst_io);
			}

			// produce outputs
			for (int i = 0; i < outputs.Count; ++i)
			{
				Entry e = outputs[i];
				resources.Produce(v, e.name, e.quantity * worst_io);
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

		private Part loaded_part = null; // one of these is null
		private ProtoPartSnapshot unloaded_part = null;
	}

	// the resource cache of a vessel
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

		// apply deferred requests for a vessel and synchronize the new amount in the vessel
		public void Sync(Vessel v, double elapsed_s)
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
			foreach (var pair in resources) pair.Value.Sync(v, elapsed_s);
		}

		// record deferred production of a resource (shortcut)
		public void Produce(Vessel v, string resource_name, double quantity)
		{
			Info(v, resource_name).Produce(quantity);
		}

		// record deferred consumption of a resource (shortcut)
		public void Consume(Vessel v, string resource_name, double quantity)
		{
			Info(v, resource_name).Consume(quantity);
		}

		// record deferred execution of a recipe
		public void Transform(Resource_recipe recipe)
		{
			recipes.Add(recipe);
		}

		public Dictionary<string, Resource_info> resources = new Dictionary<string, Resource_info>(32);
		public List<Resource_recipe> recipes = new List<Resource_recipe>(4);
	}

	// manage per-vessel resource caches
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
		public static void Produce(Vessel v, string resource_name, double quantity)
		{
			Info(v, resource_name).Produce(quantity);
		}

		// register deferred consumption of a resource (shortcut)
		public static void Consume(Vessel v, string resource_name, double quantity)
		{
			Info(v, resource_name).Consume(quantity);
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
				totalAmount[i] = new Resource_info(v, resourceName[i]).rate;        // Get generate rate for each resource
				maxAmount[i] = 0;

				totalE[i] = 0;
				maxE[i] = 0;

				totalP[i] = 0;
				maxP[i] = 0;

				totalD[i] = 0;
				maxD[i] = 0;

				mannedisPriority[i] = false;
			}

			foreach (Habitat partHabitat in v.FindPartModulesImplementing<Habitat>())
			{
				// Skip disabled habitats
				if (partHabitat.state != Habitat.State.disabled)
				{
					// Has flag to be Equalized?
					equalize |= partHabitat.needEqualize;

					PartResource[] resources = new PartResource[resourceName.Length];
					for (int i = 0; i < resourceName.Length; i++)
					{
						if (partHabitat.part.Resources.Contains(resourceName[i]))
						{
							PartResource t = partHabitat.part.Resources[resourceName[i]];

							// Manned Amounts
							if (Lib.IsManned(partHabitat.part))
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

								if ((Math.Abs(res_level[i] - (t.amount / t.maxAmount)) > precision && !Lib.IsManned(partHabitat.part))
									|| ((partHabitat.state == Habitat.State.depressurizing
									|| mannedisPriority[i]) && t.amount > double.Epsilon))
								{
									double perctToAll;              // Percent of resource for this habitat related
									double perctRest;               // Percent to fill priority

									perctToAll = t.amount / maxAmount[i];

									double perctToType;
									double perctToMaxType;

									// Percts per Types
									if (Lib.IsManned(partHabitat.part))
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
									if ((partHabitat.state == Habitat.State.depressurizing || totalAmount[i] - maxP[i] <= 0) && !Lib.IsManned(partHabitat.part))
									{
										flowController = 0 - perctToType;
									}
									else if (mannedisPriority[i] && !Lib.IsManned(partHabitat.part))
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
									else if (!mannedisPriority[i] || !Lib.IsManned(partHabitat.part))
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

									if (va == 0) stillNeed = false;
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
