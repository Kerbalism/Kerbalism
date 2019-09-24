using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class DrillDevice : LoadedDevice<ModuleResourceHarvester>
	{
		public DrillDevice(ModuleResourceHarvester module) : base(module) { }

		public override string Name => "drill";

		public override string Status
		{
			get
			{
				if (module.AlwaysActive) return Localizer.Format("#KERBALISM_Generic_ALWAYSON");
				return Lib.Color(module.IsActivated, Localizer.Format("#KERBALISM_Generic_ON"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_OFF"), Lib.KColor.Yellow);
			}
		}

		public override void Ctrl(bool value)
		{
			if (module.AlwaysActive) return;
			if (value) module.StartResourceConverter();
			else module.StopResourceConverter();
		}

		public override void Toggle()
		{
			Ctrl(!module.IsActivated);
		}
	}


	public sealed class ProtoDrillDevice : ProtoDevice<ModuleResourceHarvester>
	{
		public ProtoDrillDevice(ModuleResourceHarvester prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name => "drill";

		public override string Status
		{
			get
			{
				if (prefab.AlwaysActive) Localizer.Format("#KERBALISM_Generic_ALWAYSON");
				bool is_on = Lib.Proto.GetBool(protoModule, "IsActivated");
				return Lib.Color(is_on, Localizer.Format("#KERBALISM_Generic_ON"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_OFF"), Lib.KColor.Yellow);
			}
		}

		public override void Ctrl(bool value)
		{
			if (prefab.AlwaysActive) return;
			Lib.Proto.Set(protoModule, "IsActivated", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "IsActivated"));
		}
	}


} // KERBALISM
