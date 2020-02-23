using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public abstract class ResourceWrapper
	{
		public string name;

		public double amount;
		public double capacity;

		/// <summary> remember vessel-wide amount of previous step, to calculate rate and detect non-Kerbalism brokers </summary>
		public double oldAmount;

		/// <summary> remember vessel-wide capacity of previous step, to detect flow state changes </summary>
		public double oldCapacity;

		public ResourceWrapper(string name)
		{
			this.name = name;
			amount = 0.0;
			capacity = 0.0;
			oldAmount = 0.0;
			oldCapacity = 0.0;
		}

		public ResourceWrapper(ResourceWrapper previousWrapper)
		{
			name = previousWrapper.name;
			amount = 0.0;
			capacity = 0.0;
			oldAmount = previousWrapper.amount;
			oldCapacity = previousWrapper.capacity;
		}

		public abstract void ClearPartResources();

		public abstract void SyncToPartResources(double deferred, bool equalizeMode);
	}

	public abstract class ResourceWrapper<T> : ResourceWrapper
	{
		protected List<T> partResources = new List<T>();

		public ResourceWrapper(string name) : base(name) {}
		public ResourceWrapper(ResourceWrapper previousWrapper) : base(previousWrapper) {}

		public override void ClearPartResources()
		{
			oldAmount = amount;
			oldCapacity = capacity;
			amount = 0.0;
			capacity = 0.0;
			partResources.Clear();
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
}
