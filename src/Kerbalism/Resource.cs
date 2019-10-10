using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>Global cache for storing and accessing VesselResources (and ResourceInfo) handlers in all vessels, with shortcut for common methods</summary>
	public static class ResourceCache
	{
		// resource cache
		static Dictionary<Guid, VesselResources> entries;

		/// <summary> pseudo-ctor </summary>
		public static void Init()
		{
			entries = new Dictionary<Guid, VesselResources>();
		}

		/// <summary> clear all resource information for all vessels </summary>
		public static void Clear()
		{
			entries.Clear();
		}

		/// <summary> Reset the whole resource simulation for the vessel </summary>
		public static void Purge(Vessel v)
		{
			entries.Remove(v.id);
		}

		/// <summary> Reset the whole resource simulation for the vessel </summary>
		public static void Purge(ProtoVessel pv)
		{
			entries.Remove(pv.vesselID);
		}

		/// <summary> Return the VesselResources handler for this vessel </summary>
		public static VesselResources Get(Vessel v)
		{
			// try to get existing entry if any
			VesselResources entry;
			if (entries.TryGetValue(v.id, out entry)) return entry;

			// create new entry
			entry = new VesselResources();

			// remember new entry
			entries.Add(v.id, entry);

			// return new entry
			return entry;
		}

		/// <summary> return a resource handler (shortcut) </summary>
		public static ResourceInfo GetResource(Vessel v, string resource_name)
		{
			return Get(v).GetResource(v, resource_name);
		}

		/// <summary> record deferred production of a resource (shortcut) </summary>
		/// <param name="brokerName">short ui-friendly name for the producer</param>
		public static void Produce(Vessel v, string resource_name, double quantity, string brokerName)
		{
			GetResource(v, resource_name).Produce(quantity, brokerName);
		}

		/// <summary> record deferred consumption of a resource (shortcut) </summary>
		/// <param name="brokerName">short ui-friendly name for the consumer</param>
		public static void Consume(Vessel v, string resource_name, double quantity, string brokerName)
		{
			GetResource(v, resource_name).Consume(quantity, brokerName);
		}

		/// <summary> register deferred execution of a recipe (shortcut)</summary>
		public static void AddRecipe(Vessel v, ResourceRecipe recipe)
		{
			Get(v).AddRecipe(recipe);
		}
	}

	/// <summary>
	/// Handler for the vessel resources simulator.
	/// Allow access to the resource handler (ResourceInfo) for all resources on the vessel
	/// and also stores of all recorded recipes (ResourceRecipe)
	/// </summary>
	public sealed class VesselResources
	{
		private Dictionary<string, ResourceInfo> resources = new Dictionary<string, ResourceInfo>(32);
		private List<ResourceRecipe> recipes = new List<ResourceRecipe>(4);

		/// <summary> return a VesselResources handler </summary>
		public ResourceInfo GetResource(Vessel v, string resource_name)
		{
			// try to get existing entry if any
			ResourceInfo res;
			if (resources.TryGetValue(resource_name, out res)) return res;

			// create new entry
			res = new ResourceInfo(v, resource_name);

			// remember new entry
			resources.Add(resource_name, res);

			// return new entry
			return res;
		}

		/// <summary>
		/// Main vessel resource simulation update method.
		/// Execute all recipes to get final deferred amounts, then for each resource apply deferred requests, 
		/// synchronize the new amount in all parts and update ResourceInfo information properties (rates, brokers...)
		/// </summary>
		public void Sync(Vessel v, VesselData vd, double elapsed_s)
		{
			// execute all recorded recipes
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Resource.ExecuteRecipes");
			ResourceRecipe.ExecuteRecipes(v, this, recipes);
			UnityEngine.Profiling.Profiler.EndSample();

			// forget the recipes
			recipes.Clear();

			// apply all deferred requests and synchronize to vessel
			// PartResourceList is slow and VERY garbagey to iterate over (because it's a dictionary disguised as a list),
			// so acquiring a full list of all resources in a single loop is faster and less ram consuming than a
			// "n ResourceInfo" * "n parts" * "n PartResource" loop (can easily result in 1000+ calls to p.Resources.dict.Values)
			// It's also faster for unloaded vessels in the case of the ProtoPartResourceSnapshot lists, at the cost of a bit of garbage
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Resource.SyncAll");
			if (v.loaded)
			{
				Dictionary<string, List<PartResource>> resInfos = new Dictionary<string, List<PartResource>>(resources.Count);
				foreach (ResourceInfo resInfo in resources.Values)
					resInfos.Add(resInfo.ResourceName, new List<PartResource>());

				foreach (Part p in v.Parts)
				{
					foreach (PartResource r in p.Resources.dict.Values)
					{
						if (r.flowState && resInfos.ContainsKey(r.resourceName))
						{
							resInfos[r.resourceName].Add(r);
						}
					}
				}

				foreach (ResourceInfo resInfo in resources.Values)
					resInfo.Sync(v, vd, elapsed_s, resInfos[resInfo.ResourceName], null);
			}
			else
			{
				Dictionary<string, List<ProtoPartResourceSnapshot>> resInfos = new Dictionary<string, List<ProtoPartResourceSnapshot>>(resources.Count);
				foreach (ResourceInfo resInfo in resources.Values)
					resInfos.Add(resInfo.ResourceName, new List<ProtoPartResourceSnapshot>());

				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					foreach (ProtoPartResourceSnapshot r in p.resources)
					{
						if (r.flowState && resInfos.ContainsKey(r.resourceName))
						{
							resInfos[r.resourceName].Add(r);
						}
					}
				}

				foreach (ResourceInfo resInfo in resources.Values)
					resInfo.Sync(v, vd, elapsed_s, null, resInfos[resInfo.ResourceName]);
			}
			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary> record deferred production of a resource (shortcut) </summary>
		/// <param name="brokerName">short ui-friendly name for the producer</param>
		public void Produce(Vessel v, string resource_name, double quantity, string brokerName)
		{
			GetResource(v, resource_name).Produce(quantity, brokerName);
		}

		/// <summary> record deferred consumption of a resource (shortcut) </summary>
		/// <param name="tag">short ui-friendly name for the consumer</param>
		public void Consume(Vessel v, string resource_name, double quantity, string tag)
		{
			GetResource(v, resource_name).Consume(quantity, tag);
		}

		/// <summary> record deferred execution of a recipe (shortcut) </summary>
		public void AddRecipe(ResourceRecipe recipe)
		{
			recipes.Add(recipe);
		}
	}

	/// <summary>
	/// Handler for a single resource on a vessel. Expose vessel-wide information about amounts, rates and brokers (consumers/producers).
	/// Responsible for synchronization between the resource simulator and the actual resources present on each part. 
	/// </summary>
	public sealed class ResourceInfo
	{
		/// <summary> Associated resource name</summary>
		public string ResourceName { get; private set; }

		/// <summary> Rate of change in amount per-second, this is purely for visualization</summary>
		public double Rate { get; private set; }

		/// <summary> Rate of change in amount per-second, including average rate for interval-based rules</summary>
		public double AverageRate { get; private set; }

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public double Level { get; private set; }

		/// <summary> True if an interval-based rule consumption/production was processed in the last simulation step</summary>
		public bool IntervalRuleHappened { get; private set; }

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in Sync()</summary>
		public double Deferred { get; private set; }

		/// <summary> Amount of resource</summary>
		public double Amount { get; private set; }

		/// <summary> Storage capacity of resource</summary>
		public double Capacity { get; private set; }

		/// <summary> Simulated average rate of interval-based rules in amount per-second. This is for information only, the resource is not consumed</summary>
		private double intervalRulesRate;

		/// <summary> Amount consumed/produced by interval-based rules in this simulation step</summary>
		private double intervalRuleAmount;

		/// <summary>Dictionary of all consumers and producers (key) and how much amount they did add/remove (value).</summary>
		private Dictionary<string, double> brokersResourceAmounts;

		/// <summary>Dictionary of all interval-based rules (key) and their simulated average rate (value). This is for information only, the resource is not consumed</summary>
		private Dictionary<string, double> intervalRuleBrokersRates;

		/// <summary>Ctor</summary>
		public ResourceInfo(Vessel v, string res_name)
		{
			// remember resource name
			ResourceName = res_name;

			Deferred = 0;
			Amount = 0;
			Capacity = 0;

			brokersResourceAmounts = new Dictionary<string, double>();
			intervalRuleBrokersRates = new Dictionary<string, double>();

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

		/// <summary>Record a production, it will be stored in "Deferred" and later synchronized to the vessel in Sync()</summary>
		/// <param name="brokerName">origin of the production, will be available in the UI</param>
		public void Produce(double quantity, string brokerName)
		{
			Deferred += quantity;

			// keep track of every producer contribution for UI/debug purposes
			if (Math.Abs(quantity) < 1e-10) return;

			if (brokersResourceAmounts.ContainsKey(brokerName))
				brokersResourceAmounts[brokerName] += quantity;
			else
				brokersResourceAmounts.Add(brokerName, quantity);
		}

		/// <summary>Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in Sync()</summary>
		/// <param name="brokerName">origin of the consumption, will be available in the UI</param>
		public void Consume(double quantity, string brokerName)
		{
			Deferred -= quantity;

			// keep track of every consumer contribution for UI/debug purposes
			if (Math.Abs(quantity) < 1e-10) return;

			if (brokersResourceAmounts.ContainsKey(brokerName))
				brokersResourceAmounts[brokerName] -= quantity;
			else
				brokersResourceAmounts.Add(brokerName, -quantity);
		}

		/// <summary>synchronize resources from cache to vessel</summary>
		/// <remarks>
		/// this function will also sync from vessel to cache so you can always use the
		/// ResourceInfo interface to get information about resources
		/// </remarks>
		public void Sync(Vessel v, VesselData vd, double elapsed_s, List<PartResource> loadedResList, List<ProtoPartResourceSnapshot> unloadedResList)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Resource.Sync");
			// # OVERVIEW
			// - consumption/production is accumulated in "Deferred", then this function called
			// - save previous step amount/capacity
			// - part loop 1 : detect new amount/capacity
			// - if amount has changed, this mean there is non-Kerbalism producers/consumers on the vessel
			// - if non-Kerbalism producers are detected on a loaded vessel, prevent high timewarp rates
			// - clamp "Deferred" to amount/capacity
			// - part loop 2 : apply "Deferred" to all parts
			// - apply "Deferred" to amount
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
			// - we hacked the solar panels to use the resource cache (SolarPanelFixer)
			// - we detect incoherency on loaded vessels, and forbid the two highest warp speeds

			// remember vessel-wide amount currently known, to calculate rate and detect non-Kerbalism brokers
			double oldAmount = Amount;

			// remember vessel-wide capacity currently known, to detect flow state changes
			double oldCapacity = Capacity;

			// iterate over all enabled resource containers and detect amount/capacity again
			// - this detect production/consumption from stock and third-party mods
			//   that by-pass the resource cache, and flow state changes in general
			Amount = 0.0;
			Capacity = 0.0;

			if (v.loaded)
			{
				foreach (PartResource r in loadedResList)
				{
					Amount += r.amount;
					Capacity += r.maxAmount;
				}
			}
			else
			{
				foreach (ProtoPartResourceSnapshot r in unloadedResList)
				{
					Amount += r.amount;
					Capacity += r.maxAmount;
				}
			}

			// As we haven't yet synchronized anything, changes to amount can only come from non-Kerbalism producers or consumers
			double unsupportedBrokersRate = Amount - oldAmount;
			// Avoid false detection due to precision errors
			if (Math.Abs(unsupportedBrokersRate) < 1e-05) unsupportedBrokersRate = 0.0;
			// Calculate the resulting rate
			unsupportedBrokersRate /= elapsed_s;

			// Detect flow state changes
			bool flowStateChanged = Capacity - oldCapacity > 1e-05;

			// clamp consumption/production to vessel amount/capacity
			// - if deferred is negative, then amount is guaranteed to be greater than zero
			// - if deferred is positive, then capacity - amount is guaranteed to be greater than zero
			Deferred = Lib.Clamp(Deferred, -Amount, Capacity - Amount);

			// apply deferred consumption/production to all parts, simulating ALL_VESSEL_BALANCED
			// - iterating again is faster than using a temporary list of valid PartResources
			// - avoid very small values in deferred consumption/production
			if (Math.Abs(Deferred) > 1e-10)
			{
				if (v.loaded)
				{
					foreach (PartResource r in loadedResList)
					{
						// calculate consumption/production coefficient for the part
						double k = Deferred < 0.0
						  ? r.amount / Amount
						  : (r.maxAmount - r.amount) / (Capacity - Amount);

						// apply deferred consumption/production
						r.amount += Deferred * k;
					}
				}
				else
				{
					foreach (ProtoPartResourceSnapshot r in unloadedResList)
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

			// update amount, to get correct rate and levels at all times
			Amount += Deferred;

			// reset deferred production/consumption
			Deferred = 0.0;

			// recalculate level
			Level = Capacity > 0.0 ? Amount / Capacity : 0.0;

			// calculate rate of change per-second
			// - don't update rate during warp blending (stock modules have instabilities during warp blending) 
			// - ignore interval-based rules consumption/production
			if (!v.loaded || !Kerbalism.WarpBlending) Rate = (Amount - oldAmount - intervalRuleAmount) / elapsed_s;

			// calculate average rate of change per-second from interval-based rules
			intervalRulesRate = 0.0;
			foreach (var rb in intervalRuleBrokersRates)
			{
				intervalRulesRate += rb.Value;
			}

			// AverageRate is the exposed property that include simulated rate from interval-based rules.
			// For consistency with how "Rate" is calculated, we only add the simulated rate if there is some capacity or amount for it to have an effect
			AverageRate = Rate;
			if ((intervalRulesRate > 0.0 && Level < 1.0) || (intervalRulesRate < 0.0 && Level > 0.0)) AverageRate += intervalRulesRate;

			// For visualization purpose, update the VesselData.supplies brokers list, merging all detected sources :
			// - normal brokers that use Consume() or Produce()
			// - "virtual" brokers from interval-based rules
			// - non-Kerbalism brokers (aggregated rate)
			vd.Supply(ResourceName).UpdateResourceBrokers(brokersResourceAmounts, intervalRuleBrokersRates, unsupportedBrokersRate, elapsed_s);

			//Lib.Log("RESOURCE UPDATE : " + v);
			//foreach (var rb in vd.Supply(ResourceName).ResourceBrokers)
			//	Lib.Log(Lib.BuildString(ResourceName, " : ", rb.rate.ToString("+0.000000;-0.000000;+0.000000"), "/s (", rb.name, ")"));
			//Lib.Log("RESOURCE UPDATE END");

			// reset amount added/removed from interval-based rules
			IntervalRuleHappened = intervalRuleAmount > 0.0;
			intervalRuleAmount = 0.0;

			// if incoherent producers are detected, do not allow high timewarp speed
			// - can be disabled in settings
			// - unloaded vessels can't be incoherent, we are in full control there
			// - ignore incoherent consumers (no negative consequences for player)
			// - ignore flow state changes (avoid issue with process controllers and other things) 
			if (Settings.EnforceCoherency && v.loaded && TimeWarp.CurrentRate > 1000.0 && unsupportedBrokersRate > 0.0 && !flowStateChanged)
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
				Lib.StopWarp(1000.0);
			}

			// reset brokers
			brokersResourceAmounts.Clear();
			intervalRuleBrokersRates.Clear();

			// reset amount added/removed from interval-based rules
			intervalRuleAmount = 0.0;
			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>estimate time until depletion, including the simulated rate from interval-based rules</summary>
		public double DepletionTime()
		{
			// return depletion
			return Amount <= 1e-10 ? 0.0 : AverageRate >= -1e-10 ? double.NaN : Amount / -AverageRate;
		}

		/// <summary>Inform that meal has happened in this simulation step</summary>
		/// <remarks>A simulation step can cover many physics ticks, especially for unloaded vessels</remarks>
		public void UpdateIntervalRule(double amount, double averageRate, string ruleName)
		{
			intervalRuleAmount += amount;
			intervalRulesRate += averageRate;

			if (intervalRuleBrokersRates.ContainsKey(ruleName))
				intervalRuleBrokersRates[ruleName] += averageRate;
			else
				intervalRuleBrokersRates.Add(ruleName, averageRate);
		}
	}

	/// <summary>
	/// ResourceRecipe is a mean of converting inputs to outputs.
	/// It does so in relation with the rest of the resource simulation to detect available amounts for inputs and available capacity for outputs.
	/// Outputs can be defined a "dumpeable" to avoid this last limitation.
	/// </summary>

	// TODO : (GOTMACHINE) the "combined" feature (ability for an input to substitute another if not available) added a lot of complexity to the Recipe code,
	// all in the purpose of fixes for the habitat resource-based atmosphere system.
	// If at some point we rewrite habitat and get ride of said resources, "combined" will not be needed anymore,
	// so it would be a good idea to revert the changes made in this commit :
	// https://github.com/Kerbalism/Kerbalism/commit/91a154b0eeda8443d9dd888c2e40ca511c5adfa3#diff-ffbaadfd7e682c9dcb3912d5f8c5cabb

	// TODO : (GOTMACHINE) At some point, we want to use "virtual" resources in recipes.
	// Their purpose would be to give the ability to scale the non-resource output of a pure consumer.
	// Example : to scale antenna data rate by EC availability, define an "antennaOutput" virtual resource and a recipe that convert EC to antennaOutput
	// then check "antennaOutput" availability to scale the amount of data sent
	// This would also allow removing the "Cures" thing.

	public sealed class ResourceRecipe
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

		public List<Entry> inputs;   // set of input resources
		public List<Entry> outputs;  // set of output resources
		public List<Entry> cures;    // set of cures
		public double left;     // what proportion of the recipe is left to execute

		private string name;

		public ResourceRecipe(Part p, string name)
		{
			this.inputs = new List<Entry>();
			this.outputs = new List<Entry>();
			this.cures = new List<Entry>();
			this.left = 1.0;
			this.name = name;
		}

		public ResourceRecipe(ProtoPartSnapshot p, string name)
		{
			this.inputs = new List<Entry>();
			this.outputs = new List<Entry>();
			this.cures = new List<Entry>();
			this.left = 1.0;
			this.name = name;
		}

		/// <summary>add an input to the recipe</summary>
		public void AddInput(string resource_name, double quantity)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new Entry(resource_name, quantity));
			}
		}

		/// <summary>add a combined input to the recipe</summary>
		public void AddInput(string resource_name, double quantity, string combined)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new Entry(resource_name, quantity, true, combined));
			}
		}

		/// <summary>add an output to the recipe</summary>
		public void AddOutput(string resource_name, double quantity, bool dump)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				outputs.Add(new Entry(resource_name, quantity, dump));
			}
		}

		/// <summary>add a cure to the recipe</summary>
		public void AddCure(string cure, double quantity, string resource_name)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				cures.Add(new Entry(cure, quantity, true, resource_name));
			}
		}

		/// <summary>Execute all recipes and record deferred consumption/production for inputs/ouputs</summary>
		public static void ExecuteRecipes(Vessel v, VesselResources resources, List<ResourceRecipe> recipes)
		{
			bool executing = true;
			while (executing)
			{
				executing = false;
				for (int i = 0; i < recipes.Count; ++i)
				{
					ResourceRecipe recipe = recipes[i];
					if (recipe.left > double.Epsilon)
					{
						executing |= recipe.ExecuteRecipeStep(v, resources);
					}
				}
			}
		}

		/// <summary>
		/// Execute the recipe and record deferred consumption/production for inputs/ouputs.
		/// This need to be called multiple times until left &lt;= 0.0 for complete execution of the recipe.
		/// return true if recipe execution is completed, false otherwise
		/// </summary>
		private bool ExecuteRecipeStep(Vessel v, VesselResources resources)
		{
			// determine worst input ratio
			// - pure input recipes can just underflow
			double worst_input = left;
			if (outputs.Count > 0)
			{
				for (int i = 0; i < inputs.Count; ++i)
				{
					Entry e = inputs[i];
					ResourceInfo res = resources.GetResource(v, e.name);

					// handle combined inputs
					if (e.combined != null)
					{
						// is combined resource the primary
						if (e.combined != "")
						{
							Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
							ResourceInfo sec = resources.GetResource(v, sec_e.name);
							double pri_worst = Lib.Clamp((res.Amount + res.Deferred) * e.inv_quantity, 0.0, worst_input);
							if (pri_worst > 0.0)
							{
								worst_input = pri_worst;
							}
							else
							{
								worst_input = Lib.Clamp((sec.Amount + sec.Deferred) * sec_e.inv_quantity, 0.0, worst_input);
							}
						}
					}
					else
					{
						worst_input = Lib.Clamp((res.Amount + res.Deferred) * e.inv_quantity, 0.0, worst_input);
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
						ResourceInfo res = resources.GetResource(v, e.name);
						worst_output = Lib.Clamp((res.Capacity - (res.Amount + res.Deferred)) * e.inv_quantity, 0.0, worst_output);
					}
				}
			}

			// determine worst-io
			double worst_io = Math.Min(worst_input, worst_output);

			// consume inputs
			for (int i = 0; i < inputs.Count; ++i)
			{
				Entry e = inputs[i];
				ResourceInfo res = resources.GetResource(v, e.name);
				// handle combined inputs
				if (e.combined != null)
				{
					// is combined resource the primary
					if (e.combined != "")
					{
						Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
						ResourceInfo sec = resources.GetResource(v, sec_e.name);
						double need = (e.quantity * worst_io) + (sec_e.quantity * worst_io);
						// do we have enough primary to satisfy needs, if so don't consume secondary
						if (res.Amount + res.Deferred >= need) resources.Consume(v, e.name, need, name);
						// consume primary if any available and secondary
						else
						{
							need -= res.Amount + res.Deferred;
							res.Consume(res.Amount + res.Deferred, name);
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
				ResourceInfo res = resources.GetResource(v, e.name);
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
				totalAmount[i] = new ResourceInfo(v, resourceName[i]).Rate;        // Get generate rate for each resource
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

			if (!equalize) return;

			for (int i = 0; i < resourceName.Length; i++)
			{
				// resource level for Enabled habitats no Manned
				res_level[i] = totalE[i] / (maxAmount[i] - maxP[i]);

				// Manned is priority?
				// If resource amount is less then maxAmount in manned habitat and it's flagged to equalize, define as priority
				// Using Atmosphere, N2, O2 as Priority trigger (we don't want to use CO2 as a trigger)
				if (resourceName[i] != "WasteAtmosphere" && equalize)
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
		public static string[] resourceName = new string[2] { "Atmosphere", "WasteAtmosphere" };

		// Resources to equalize
		public static double precision = 0.00001;
	}
}
