using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// Interface for common interactions with VesselResource and VirtualResource.
	/// You can cast this to VesselResource to get the extra information it contains (rates...)
	/// </summary>
	public interface IResource
	{
		/// <summary> Associated resource name</summary>
		string Name { get; }

		/// <summary> Amount of resource</summary>
		double Amount { get; }

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in Sync()</summary>
		double Deferred { get; }

		// Note : Having Capacity and Level here isn't necessary since they aren't used in VirtualResource
		// but it's convenient to have them directly accessible instead of having to cast to VesselResource.

		/// <summary> Storage capacity of resource</summary>
		double Capacity { get; }

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		double Level { get; }

		bool NeedUpdate { get; }

		/// <summary> Called at the VesselResHandler instantiation, after the ResourceWrapper amount and capacity has been evaluated </summary>
		void Init();

		/// <summary> Called by the VesselResHandler, every update :
		/// <para/> - After Recipes have been processed
		/// <para/> - After the VesselResHandler has been updated with all part resources references, and after amount/oldAmount and capacity/oldCapacity have been set
		/// </summary>
		bool ExecuteAndSyncToParts(double elapsed_s);

		/// <summary>Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()</summary>
		/// <param name="broker">origin of the consumption, will be available in the UI</param>
		void Consume(double quantity, ResourceBroker broker = null, bool isCritical = false);

		/// <summary>Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()</summary>
		/// <param name="broker">origin of the consumption, will be available in the UI</param>
		void RecipeConsume(double quantity, ResourceBroker broker);

		/// <summary>Record a production, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()</summary>
		/// <param name="broker">origin of the production, will be available in the UI</param>
		void Produce(double quantity, ResourceBroker broker = null);
	}
}
