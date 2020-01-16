using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class EmitterDevice : LoadedDevice<Emitter>
	{
		public EmitterDevice(Emitter module) : base(module) { }

		public override string Name => "emitter";

		public override string Status => Lib.Color(module.running, Localizer.Format("#KERBALISM_Generic_ON"), Lib.Kolor.Green, Localizer.Format("#KERBALISM_Generic_OFF"), Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (!module.toggle) return;
			if (module.running != value) module.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!module.running);
		}

		public override bool IsVisible => module.toggle;
	}

	public sealed class ProtoEmitterDevice : ProtoDevice<Emitter>
	{
		public ProtoEmitterDevice(Emitter prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name => "emitter";

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "running"), Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.Kolor.Green, Localizer.Format("#KERBALISM_Generic_DISABLED"), Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(protoModule, "running", value);
		}

		public override void Toggle()
		{
			if (!Lib.Proto.GetBool(protoModule, "toggle")) return;
			Ctrl(!Lib.Proto.GetBool(protoModule, "running"));
		}

		public override bool IsVisible => Lib.Proto.GetBool(protoModule, "toggle");
	}


} // KERBALISM
