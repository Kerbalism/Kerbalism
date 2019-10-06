using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class ConverterDevice : LoadedDevice<ModuleResourceConverter>
	{
		public ConverterDevice(ModuleResourceConverter module) : base(module) { }

		public override string Status => module.AlwaysActive ? Localizer.Format("#KERBALISM_Generic_ALWAYSON") : Lib.Color(module.IsActivated, Localizer.Format("#KERBALISM_Generic_ON"), Lib.Kolor.Green, Localizer.Format("#KERBALISM_Generic_OFF"), Lib.Kolor.Yellow);

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


	public sealed class ProtoConverterDevice : ProtoDevice<ModuleResourceConverter>
	{
		public ProtoConverterDevice(ModuleResourceConverter prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Status
		{
			get
			{
				if (prefab.AlwaysActive) return Localizer.Format("#KERBALISM_Generic_ALWAYSON");
				bool is_on = Lib.Proto.GetBool(protoModule, "IsActivated");
				return Lib.Color(is_on, Localizer.Format("#KERBALISM_Generic_ON"), Lib.Kolor.Green, Localizer.Format("#KERBALISM_Generic_OFF"), Lib.Kolor.Yellow);
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
