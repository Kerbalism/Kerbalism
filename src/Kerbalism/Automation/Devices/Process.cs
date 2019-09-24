using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class ProcessDevice : LoadedDevice<ProcessController>
	{
		public ProcessDevice(ProcessController module) : base(module) { }

		public override string Status => Lib.Color(module.IsRunning(), Localizer.Format("#KERBALISM_Generic_RUNNING"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_STOPPED"), Lib.KColor.Yellow);

		public override void Ctrl(bool value)
		{
			if (!module.toggle) return;
			module.SetRunning(value);
		}

		public override void Toggle()
		{
			Ctrl(!module.IsRunning());
		}
	}

	public sealed class ProtoProcessDevice : ProtoDevice<ProcessController>
	{
		public ProtoProcessDevice(ProcessController prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "running"), Localizer.Format("#KERBALISM_Generic_RUNNING"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_STOPPED"), Lib.KColor.Yellow);

		public override void Ctrl(bool value)
		{
			if (!prefab.toggle) return;
			Lib.Proto.Set(protoModule, "running", value);

			double capacity = prefab.capacity;
			var res = protoPart.resources.Find(k => k.resourceName == prefab.resource);
			res.amount = value ? capacity : 0.0;
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "running"));
		}
	}


} // KERBALISM



