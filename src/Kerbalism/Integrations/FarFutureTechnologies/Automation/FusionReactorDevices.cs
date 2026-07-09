using KERBALISM;
using KSP.Localization;

namespace KERBALISM
{
	internal static class FusionReactorControl
	{
		internal static void SetEnabled(PartModule reactor, bool value)
		{
			if (reactor == null || FarFutureTechnologies.Get(reactor, "Enabled", false) == value)
				return;

			if (value)
			{
				// FFT ReactorActivated() refuses to start without a charged capacitor.
				if (!FarFutureTechnologies.Get(reactor, "Charged", false))
					return;

				FarFutureTechnologies.EnableReactor(reactor);
				return;
			}

			FarFutureTechnologies.DisableReactor(reactor);
			if (!FarFutureTechnologies.Get(reactor, "Enabled", false))
				return;

			// Fallback only when native DisableReactor is unavailable.
			FarFutureTechnologies.Set(reactor, "Enabled", false);
			FarFutureTechnologies.Set(reactor, "Charging", false);
			FarFutureTechnologies.Set(reactor, "Charged", false);
			FusionReactorResourceSim.SetLoadedCharge(reactor, 0f);
			FusionReactorResourceSim.SyncLoadedChargeUI(reactor, false);
		}

		internal static string StatusText(PartModule reactor)
		{
			if (!FarFutureTechnologies.Get(reactor, "Enabled", false)
				&& FarFutureTechnologies.Get(reactor, "Charging", false)
				&& !FarFutureTechnologies.Get(reactor, "Charged", false))
				return Lib.Color(false, Localizer.Format("#KERBALISM_Device_FusionCharging"), Lib.Kolor.Yellow, Local.Generic_OFF, Lib.Kolor.Yellow);

			return Lib.Color(FarFutureTechnologies.Get(reactor, "Enabled", false), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);
		}

		internal static string ProtoStatusText(ProtoPartModuleSnapshot protoModule)
		{
			bool enabled = Lib.Proto.GetBool(protoModule, "Enabled");
			bool charging = Lib.Proto.GetBool(protoModule, "Charging");
			bool charged = Lib.Proto.GetBool(protoModule, "Charged");
			if (!enabled && charging && !charged)
				return Lib.Color(false, Localizer.Format("#KERBALISM_Device_FusionCharging"), Lib.Kolor.Yellow, Local.Generic_OFF, Lib.Kolor.Yellow);

			return Lib.Color(enabled, Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);
		}
	}

	public sealed class FusionReactorDevice : LoadedDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public FusionReactorDevice(PartModule module, string deviceName, string displayName) : base(module)
		{
			this.deviceName = deviceName;
			this.displayName = displayName;
		}

		public override string Name => deviceName;

		public override string DisplayName => displayName;

		public override string Status => FusionReactorControl.StatusText(module);

		public override void Ctrl(bool value)
		{
			FusionReactorControl.SetEnabled(module, value);
		}

		public override void Toggle()
		{
			Ctrl(!FarFutureTechnologies.Get(module, "Enabled", false));
		}
	}

	public sealed class ProtoFusionReactorDevice : ProtoDevice<PartModule>
	{
		private readonly string deviceName;
		private readonly string displayName;

		public ProtoFusionReactorDevice(PartModule prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, string deviceName, string displayName)
			: base(prefab, protoPart, protoModule)
		{
			this.deviceName = deviceName;
			this.displayName = displayName;
		}

		public override string Name => deviceName;

		public override string DisplayName => displayName;

		public override string Status => FusionReactorControl.ProtoStatusText(protoModule);

		public override void Ctrl(bool value)
		{
			if (value && !Lib.Proto.GetBool(protoModule, "Charged"))
				return;

			Lib.Proto.Set(protoModule, "Enabled", value);
			if (!value)
			{
				Lib.Proto.Set(protoModule, "Charging", false);
				Lib.Proto.Set(protoModule, "Charged", false);
				FusionReactorResourceSim.SetProtoCharge(protoModule, 0f);
			}
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "Enabled"));
		}
	}
}
