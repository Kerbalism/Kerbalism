using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// For a single resource, keep track of amount, capacity and references to the stock PartResource / ProtoPartResourceSnapshot objects
	/// Used to abstract away the differences when working with loaded / unloaded / editor vessels
	/// </summary>
	public abstract class ResourceWrapper
	{
		/// <summary> resource name </summary>
		public string name;

		/// <summary> current amount </summary>
		public double amount;

		/// <summary> current capacity </summary>
		public double capacity;

		/// <summary> remember vessel-wide amount of previous step, to calculate rate and detect non-Kerbalism brokers </summary>
		public double oldAmount;

		/// <summary> remember vessel-wide capacity of previous step, to detect flow state changes </summary>
		public double oldCapacity;

		/// <summary> default ctor, to be used when the VesselReshandler is instantiated</summary>
		public ResourceWrapper(string name)
		{
			this.name = name;
			amount = 0.0;
			capacity = 0.0;
			oldAmount = 0.0;
			oldCapacity = 0.0;
		}

		/// <summary> ctor for switching the wrapper between the loaded and unloaded types without loosing the amount/capacity </summary>
		public ResourceWrapper(ResourceWrapper previousWrapper)
		{
			name = previousWrapper.name;
			amount = 0.0;
			capacity = 0.0;
			oldAmount = previousWrapper.amount;
			oldCapacity = previousWrapper.capacity;
		}

		/// <summary> To be called at the beginning of a simulation step :
		/// <para/>- save current amount/capacity in oldAmount/oldCapacity
		/// <para/>- reset amount/capacity
		/// <para/>- clear the stock PartResource / ProtoPartResourceSnapshot references</summary>
		/// <param name="doReset">if false, don't reset amount/capacity and don't clear references. For processing editor simulation steps</param>
		public abstract void ClearPartResources(bool doReset = true);

		/// <summary> synchronize deferred to the PartResource / ProtoPartResourceSnapshot references</summary>
		/// <param name="deferred">amount to add or remove</param>
		/// <param name="equalizeMode">if true, the total amount (current + deffered) will redistributed equally amongst all parts </param>
		public abstract void SyncToPartResources(double deferred, bool equalizeMode);

		public override string ToString()
		{
			return $"{name} : {amount.ToString("F1")} / {capacity.ToString("F1")}";
		}
	}

	public abstract class ResourceWrapper<T> : ResourceWrapper
	{
		protected List<T> partResources = new List<T>();

		public ResourceWrapper(string name) : base(name) {}
		public ResourceWrapper(ResourceWrapper previousWrapper) : base(previousWrapper) {}

		public override void ClearPartResources(bool doReset = true)
		{
			oldAmount = amount;
			oldCapacity = capacity;
			if (doReset)
			{
				amount = 0.0;
				capacity = 0.0;
				partResources.Clear();
			}
		}

		public abstract void AddPartresources(T partResource);
	}

	public class LoadedResourceWrapper : ResourceWrapper<PartResource>
	{
		public LoadedResourceWrapper(string name) : base(name) { }
		public LoadedResourceWrapper(ResourceWrapper previousWrapper) : base(previousWrapper) { }

		public override void AddPartresources(PartResource partResource)
		{
			partResources.Add(partResource);
			amount += partResource.amount;
			capacity += partResource.maxAmount;
		}

		public override void SyncToPartResources(double deferred, bool equalizeMode)
		{
			if (equalizeMode)
			{
				// apply deferred consumption/production to all parts,
				// equally balancing the total amount amongst all parts
				foreach (PartResource partResource in partResources)
				{
					partResource.amount = (amount + deferred) * (partResource.maxAmount / capacity);
				}
			}
			else
			{
				// apply deferred consumption/production to all parts, simulating ALL_VESSEL_BALANCED
				// avoid very small values in deferred consumption/production
				if (Math.Abs(deferred) > 1e-16)
				{
					foreach (PartResource partResource in partResources)
					{
						// calculate consumption/production coefficient for the part
						double k;
						if (deferred < 0.0)
							k = partResource.amount / amount;
						else
							k = (partResource.maxAmount - partResource.amount) / (capacity - amount);

						// apply deferred consumption/production
						partResource.amount += deferred * k;
					}
				}
			}
		}
	}

	public class ProtoResourceWrapper : ResourceWrapper<ProtoPartResourceSnapshot>
	{
		public ProtoResourceWrapper(string name) : base(name) { }
		public ProtoResourceWrapper(ResourceWrapper previousWrapper) : base(previousWrapper) { }

		public override void AddPartresources(ProtoPartResourceSnapshot partResource)
		{
			partResources.Add(partResource);
			amount += partResource.amount;
			capacity += partResource.maxAmount;
		}

		public override void SyncToPartResources(double deferred, bool equalizeMode)
		{
			if (equalizeMode)
			{
				// apply deferred consumption/production to all parts,
				// equally balancing the total amount amongst all parts
				foreach (ProtoPartResourceSnapshot partResource in partResources)
				{
					partResource.amount = (amount + deferred) * (partResource.maxAmount / capacity);
				}
			}
			else
			{
				// apply deferred consumption/production to all parts, simulating ALL_VESSEL_BALANCED
				// avoid very small values in deferred consumption/production
				if (Math.Abs(deferred) > 1e-16)
				{
					foreach (ProtoPartResourceSnapshot partResource in partResources)
					{
						// calculate consumption/production coefficient for the part
						double k;
						if (deferred < 0.0)
							k = partResource.amount / amount;
						else
							k = (partResource.maxAmount - partResource.amount) / (capacity - amount);

						// apply deferred consumption/production
						partResource.amount += deferred * k;
					}
				}
			}
		}
	}

	/// <summary>
	/// EditorResourceWrapper doesn't keep the PartResource references and doesn't synchronize anything, it only
	/// </summary>
	public class EditorResourceWrapper : ResourceWrapper<PartResource>
	{
		public EditorResourceWrapper(string name) : base(name) { }

		public override void AddPartresources(PartResource partResource)
		{
			amount += partResource.amount;
			capacity += partResource.maxAmount;
		}

		public override void SyncToPartResources(double deferred, bool equalizeMode) { }
	}
}
