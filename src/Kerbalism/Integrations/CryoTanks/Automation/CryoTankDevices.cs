using KSP.Localization;

namespace KERBALISM
{
	public sealed class CryoTankCoolingDevice : LoadedDevice<PartModule>
	{
		public CryoTankCoolingDevice(PartModule module) : base(module) { }

		// Keep Name English for stable device Id hashing across languages.
		public override string Name
		{
			get
			{
				string moduleId = CryoTanks.Get(module, "moduleID", string.Empty);
				return string.IsNullOrEmpty(moduleId) ? "cryo tank cooling" : "cryo tank cooling " + moduleId;
			}
		}

		public override string DisplayName => Localizer.Format("#KERBALISM_Device_CryoTankCooling");

		public override string Status => Lib.Color(
			CryoTanks.GetCoolingEnabled(module),
			Local.Generic_ON, Lib.Kolor.Green,
			Local.Generic_OFF, Lib.Kolor.Yellow);

		public override bool IsVisible => CryoTankDeviceCollector.IsCoolable(module, module.part);

		public override void Ctrl(bool value)
		{
			if (!IsVisible)
				return;
			CryoTanks.SetCoolingEnabled(module, value);
		}

		public override void Toggle()
		{
			Ctrl(!CryoTanks.GetCoolingEnabled(module));
		}
	}

	public sealed class ProtoCryoTankCoolingDevice : ProtoDevice<PartModule>
	{
		public ProtoCryoTankCoolingDevice(PartModule prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name
		{
			get
			{
				string moduleId = CryoTanks.Get(prefab, "moduleID", string.Empty);
				return string.IsNullOrEmpty(moduleId) ? "cryo tank cooling" : "cryo tank cooling " + moduleId;
			}
		}

		public override string DisplayName => Localizer.Format("#KERBALISM_Device_CryoTankCooling");

		public override string Status => Lib.Color(
			Lib.Proto.GetBool(protoModule, "CoolingEnabled"),
			Local.Generic_ON, Lib.Kolor.Green,
			Local.Generic_OFF, Lib.Kolor.Yellow);

		public override bool IsVisible => CryoTankDeviceCollector.IsCoolable(prefab, protoPart);

		public override void Ctrl(bool value)
		{
			if (!IsVisible)
				return;
			Lib.Proto.Set(protoModule, "CoolingEnabled", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "CoolingEnabled"));
		}
	}
}
