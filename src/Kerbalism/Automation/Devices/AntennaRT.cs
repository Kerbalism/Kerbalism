using KSP.Localization;
using static ModuleDeployablePart;

namespace KERBALISM
{
	public sealed class AntennaRTDevice : LoadedDevice<PartModule>
	{
		public AntennaRTDevice(PartModule module) : base(module) { }

		public override string Name => "antenna";

		public override string Status
		{
			get
			{
				return Lib.ReflectionValue<bool>(module, "IsRTActive")
					? Lib.Color(Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.Kolor.Green)
					: Lib.Color(Localizer.Format("#KERBALISM_Generic_INACTIVE"), Lib.Kolor.Yellow);
			}
		}

		public override void Ctrl(bool value) => Lib.ReflectionValue(module, "IsRTActive", value);

		public override void Toggle() => Ctrl(!Lib.ReflectionValue<bool>(module, "IsRTActive"));
	}

	public sealed class ProtoAntennaRTDevice : ProtoDevice<PartModule>
	{
		public ProtoAntennaRTDevice(PartModule prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name => "antenna";

		public override string Status
		{
			get
			{
				return Lib.Proto.GetBool(protoModule, "IsRTActive")
						  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.Kolor.Green)
						  : Lib.Color(Localizer.Format("#KERBALISM_Generic_INACTIVE"), Lib.Kolor.Yellow);
			}
		}

		public override void Ctrl(bool value) => Lib.Proto.Set(protoModule, "IsRTActive", value);

		public override void Toggle() => Ctrl(!Lib.Proto.GetBool(protoModule, "IsRTActive"));
	}
}
