using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class GreenhouseDevice : LoadedDevice<Greenhouse>
	{
		public GreenhouseDevice(Greenhouse module) : base(module) { }

		public override string Status => Lib.Color(module.active, Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (module.active != value) module.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!module.active);
		}
	}

	public sealed class ProtoGreenhouseDevice : ProtoDevice<Greenhouse>
	{
		public ProtoGreenhouseDevice(Greenhouse prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "active"), Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(protoModule, "active", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "active"));
		}
	}


} // KERBALISM
