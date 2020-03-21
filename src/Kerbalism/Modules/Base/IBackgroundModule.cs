
namespace KERBALISM
{
	public interface IBackgroundModule
	{
		/// <summary>
		/// When the vessel is unloaded, for every persisted ProtoPartModuleSnapshot, Kerbalism will call this method on the module prefab.
		/// <para/>When using the module/part fields/properties, you are using the default values on the prefab, so :
		/// <para/>- never modify a prefab value, only read them
		/// <para/>- be aware that the prefab fields won't be affected by upgrades (if you want to use upgrades, you need to use a persistent field)
		/// <para/>- you can read/write persisted fields by using the Lib.Proto.* methods using the protoModule reference
		/// <para/>- any resource consumption/production must be scaled by the provided elapsed_s
		/// </summary>
		/// <param name="vd">reference to the VesselData, allow accessing the Vessel, ResHandler, etc</param>
		/// <param name="protoPart">the ProtoPartSnapshot the protomodule is on</param>
		/// <param name="protoModule">the ProtoPartModuleSnapshot reference</param>
		/// <param name="elapsed_s">seconds elapsed since last simulation step</param>
		void BackgroundUpdate(VesselData vd, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, double elapsed_s);
	}
}
