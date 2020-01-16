using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class SickbayDevice : LoadedDevice<Sickbay>
	{
		public SickbayDevice(Sickbay module) : base(module) { }

		public override string Status
			=> Lib.Color(module.running, Localizer.Format("#KERBALISM_Generic_RUNNING"), Lib.Kolor.Green, Localizer.Format("#KERBALISM_Generic_STOPPED"), Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			module.running = value;
		}

		public override void Toggle()
		{
			Ctrl(!module.running);
		}

		public override bool IsVisible => module.slots > 0;
	}

	public sealed class ProtoSickbayDevice : ProtoDevice<Sickbay>
	{
		public ProtoSickbayDevice(Sickbay prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Status
			=> Lib.Color(Lib.Proto.GetBool(protoModule, "running"), Localizer.Format("#KERBALISM_Generic_RUNNING"), Lib.Kolor.Green, Localizer.Format("#KERBALISM_Generic_STOPPED"), Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(protoModule, "running", value);
			protoPart.resources.Find(k => k.resourceName == prefab.resource).flowState = value;
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "running"));
		}

		public override bool IsVisible => Lib.Proto.GetUInt(protoModule, "slots", 0) > 0;
	}


} // KERBALISM



