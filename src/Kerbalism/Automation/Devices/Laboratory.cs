using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class LaboratoryDevice : LoadedDevice<Laboratory>
	{
		public LaboratoryDevice(Laboratory module) : base(module) { }

		public override string Status => Lib.Color(module.running, Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.Kolor.Green, Localizer.Format("#KERBALISM_Generic_DISABLED"), Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (module.running != value) module.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!module.running);
		}
	}


	public sealed class ProtoLaboratoryDevice : ProtoDevice<Laboratory>
	{
		public ProtoLaboratoryDevice(Laboratory prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "running"), Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.Kolor.Green, Localizer.Format("#KERBALISM_Generic_DISABLED"), Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(protoModule, "running", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "running"));
		}
	}


} // KERBALISM

