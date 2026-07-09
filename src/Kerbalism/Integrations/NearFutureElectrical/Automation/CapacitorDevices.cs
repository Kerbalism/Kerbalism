using KERBALISM;
using KSP.Localization;

namespace KERBALISM
{
	public sealed class CapacitorRechargeDevice : LoadedDevice<PartModule>
	{
		public CapacitorRechargeDevice(PartModule module) : base(module) { }

		public override string Name => "NFE capacitor recharge";

		public override string DisplayName => Localizer.Format("#KERBALISM_Device_CapacitorCharge");

		public override string Status => Lib.Color(NearFutureElectrical.Get(module, "Enabled", false), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (value)
				NearFutureElectrical.Enable(module);
			else
				NearFutureElectrical.Disable(module);
		}

		public override void Toggle()
		{
			Ctrl(!NearFutureElectrical.Get(module, "Enabled", false));
		}
	}

	public sealed class CapacitorDischargeDevice : LoadedDevice<PartModule>
	{
		public CapacitorDischargeDevice(PartModule module) : base(module) { }

		public override string Name => "NFE capacitor discharge";

		public override string DisplayName => Localizer.Format("#KERBALISM_Device_CapacitorDischarge");

		public override string Status => Lib.Color(NearFutureElectrical.Get(module, "Discharging", false), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (value)
				NearFutureElectrical.Discharge(module);
			else
				NearFutureElectrical.Set(module, "Discharging", false);
		}

		public override void Toggle()
		{
			Ctrl(!NearFutureElectrical.Get(module, "Discharging", false));
		}
	}

	public sealed class ProtoCapacitorRechargeDevice : ProtoDevice<PartModule>
	{
		public ProtoCapacitorRechargeDevice(PartModule prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name => "NFE capacitor recharge";

		public override string DisplayName => Localizer.Format("#KERBALISM_Device_CapacitorCharge");

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "Enabled"), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(protoModule, "Enabled", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "Enabled"));
		}
	}

	public sealed class ProtoCapacitorDischargeDevice : ProtoDevice<PartModule>
	{
		public ProtoCapacitorDischargeDevice(PartModule prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name => "NFE capacitor discharge";

		public override string DisplayName => Localizer.Format("#KERBALISM_Device_CapacitorDischarge");

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "Discharging"), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (value && GetStoredCharge(protoPart) <= 1e-6)
				return;

			Lib.Proto.Set(protoModule, "Discharging", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "Discharging"));
		}

		private static double GetStoredCharge(ProtoPartSnapshot partSnapshot)
		{
			for (int i = 0; i < partSnapshot.resources.Count; i++)
			{
				if (partSnapshot.resources[i].resourceName == "StoredCharge")
					return partSnapshot.resources[i].amount;
			}
			return 0.0;
		}
	}
}
