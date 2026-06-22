using KSP.Localization;

namespace KERBALISM
{
	internal static class SpaceDustHarvesterControl
	{
		internal static void SetEnabled(PartModule harvester, bool value)
		{
			if (harvester == null || SpaceDust.Get(harvester, "Enabled", false) == value)
				return;

			SpaceDust.Set(harvester, "Enabled", value);
		}

		internal static string StatusText(PartModule harvester) =>
			Lib.Color(SpaceDust.Get(harvester, "Enabled", false), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		internal static string ProtoStatusText(ProtoPartModuleSnapshot protoModule) =>
			Lib.Color(
				Lib.Proto.GetBool(protoModule, "Enabled"),
				Local.Generic_ON,
				Lib.Kolor.Green,
				Local.Generic_OFF,
				Lib.Kolor.Yellow);
	}

	public sealed class SpaceDustHarvesterDevice : LoadedDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public SpaceDustHarvesterDevice(PartModule module, string deviceName, string displayName) : base(module)
		{
			this.deviceName = deviceName;
			this.displayName = displayName;
		}

		public override string Name => deviceName;

		public override string DisplayName => displayName;

		public override string Status => SpaceDustHarvesterControl.StatusText(module);

		public override void Ctrl(bool value) => SpaceDustHarvesterControl.SetEnabled(module, value);

		public override void Toggle() => Ctrl(!SpaceDust.Get(module, "Enabled", false));
	}

	public sealed class ProtoSpaceDustHarvesterDevice : ProtoDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public ProtoSpaceDustHarvesterDevice(
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

		public override string Status => SpaceDustHarvesterControl.ProtoStatusText(protoModule);

		public override void Ctrl(bool value) => Lib.Proto.Set(protoModule, "Enabled", value);

		public override void Toggle() => Ctrl(!Lib.Proto.GetBool(protoModule, "Enabled"));
	}
}
