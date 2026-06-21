using KSP.Localization;

namespace KERBALISM
{
	internal static class FissionReactorControl
	{
		internal static void SetEnabled(PartModule reactor, bool value)
		{
			if (reactor == null || SystemHeat.Get(reactor, "Enabled", false) == value)
				return;

			string methodName = value ? "EnableReactor" : "DisableReactor";
			SystemHeat.Call(reactor, methodName);
			if (SystemHeat.Get(reactor, "Enabled", false) == value)
				return;

			SystemHeat.Set(reactor, "Enabled", value);
		}

		internal static string StatusText(PartModule reactor)
		{
			return Lib.Color(SystemHeat.Get(reactor, "Enabled", false), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);
		}

		internal static string ProtoStatusText(ProtoPartModuleSnapshot protoModule)
		{
			return Lib.Color(
				Lib.Proto.GetBool(protoModule, "Enabled"),
				Local.Generic_ON,
				Lib.Kolor.Green,
				Local.Generic_OFF,
				Lib.Kolor.Yellow);
		}
	}

	public sealed class FissionReactorDevice : LoadedDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public FissionReactorDevice(PartModule module, string deviceName, string displayName) : base(module)
		{
			this.deviceName = deviceName;
			this.displayName = displayName;
		}

		public override string Name => deviceName;

		public override string DisplayName => displayName;

		public override string Status => FissionReactorControl.StatusText(module);

		public override void Ctrl(bool value)
		{
			FissionReactorControl.SetEnabled(module, value);
		}

		public override void Toggle()
		{
			Ctrl(!SystemHeat.Get(module, "Enabled", false));
		}
	}

	public sealed class ProtoFissionReactorDevice : ProtoDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public ProtoFissionReactorDevice(
			PartModule prefab,
			ProtoPartSnapshot protoPart,
			ProtoPartModuleSnapshot protoModule,
			string deviceName,
			string displayName)
			: base(prefab, protoPart, protoModule)
		{
			this.deviceName = deviceName;
			this.displayName = displayName;
		}

		public override string Name => deviceName;

		public override string DisplayName => displayName;

		public override string Status => FissionReactorControl.ProtoStatusText(protoModule);

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(protoModule, "Enabled", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "Enabled"));
		}
	}
}
