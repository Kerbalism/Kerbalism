using KSP.Localization;

namespace KERBALISM
{
	internal static class SystemHeatActivatedControl
	{
		internal static void SetActivated(PartModule module, bool value)
		{
			if (module == null || SystemHeat.IsActivated(module) == value)
				return;

			string methodName = value ? "Activate" : "Deactivate";
			SystemHeat.Call(module, methodName);
			if (SystemHeat.IsActivated(module) == value)
				return;

			SystemHeat.Set(module, "IsActivated", value);
		}

		internal static string StatusText(PartModule module) =>
			Lib.Color(SystemHeat.IsActivated(module), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		internal static string ProtoStatusText(ProtoPartModuleSnapshot protoModule) =>
			Lib.Color(
				Lib.Proto.GetBool(protoModule, "IsActivated"),
				Local.Generic_ON,
				Lib.Kolor.Green,
				Local.Generic_OFF,
				Lib.Kolor.Yellow);
	}

	public sealed class SystemHeatNativeConverterDevice : LoadedDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public SystemHeatNativeConverterDevice(PartModule module, string deviceName, string displayName) : base(module)
		{
			this.deviceName = deviceName;
			this.displayName = displayName;
		}

		public override string Name => deviceName;

		public override string DisplayName => displayName;

		public override string Status => SystemHeatActivatedControl.StatusText(module);

		public override void Ctrl(bool value) => SystemHeatActivatedControl.SetActivated(module, value);

		public override void Toggle() => Ctrl(!SystemHeat.IsActivated(module));
	}

	public sealed class ProtoSystemHeatNativeConverterDevice : ProtoDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public ProtoSystemHeatNativeConverterDevice(
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

		public override string Status => SystemHeatActivatedControl.ProtoStatusText(protoModule);

		public override void Ctrl(bool value) => Lib.Proto.Set(protoModule, "IsActivated", value);

		public override void Toggle() => Ctrl(!Lib.Proto.GetBool(protoModule, "IsActivated"));
	}

	public sealed class SystemHeatNativeHarvesterDevice : LoadedDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public SystemHeatNativeHarvesterDevice(PartModule module, string deviceName, string displayName) : base(module)
		{
			this.deviceName = deviceName;
			this.displayName = displayName;
		}

		public override string Name => deviceName;

		public override string DisplayName => displayName;

		public override string Status => SystemHeatActivatedControl.StatusText(module);

		public override void Ctrl(bool value) => SystemHeatActivatedControl.SetActivated(module, value);

		public override void Toggle() => Ctrl(!SystemHeat.IsActivated(module));
	}

	public sealed class ProtoSystemHeatNativeHarvesterDevice : ProtoDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public ProtoSystemHeatNativeHarvesterDevice(
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

		public override string Status => SystemHeatActivatedControl.ProtoStatusText(protoModule);

		public override void Ctrl(bool value) => Lib.Proto.Set(protoModule, "IsActivated", value);

		public override void Toggle() => Ctrl(!Lib.Proto.GetBool(protoModule, "IsActivated"));
	}
}
