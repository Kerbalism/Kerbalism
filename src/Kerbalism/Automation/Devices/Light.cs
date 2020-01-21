using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class LightDevice : LoadedDevice<ModuleLight>
	{
		public LightDevice(ModuleLight module) : base(module) { }

		public override string Name => "light";

		public override string Status => Lib.Color(module.isOn, Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (value) module.LightsOn();
			else module.LightsOff();
		}

		public override void Toggle()
		{
			Ctrl(!module.isOn);
		}
	}


	public sealed class ProtoLightDevice : ProtoDevice<ModuleLight>
	{
		public ProtoLightDevice(ModuleLight prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name => "light";

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "isOn"), Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(protoModule, "isOn", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "isOn"));
		}
	}


} // KERBALISM
