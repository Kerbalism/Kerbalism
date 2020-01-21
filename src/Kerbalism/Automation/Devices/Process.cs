using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class ProcessDevice : LoadedDevice<ProcessController>
	{
		public ProcessDevice(ProcessController module) : base(module) { }

		public override bool IsVisible => module.toggle;

		public override string DisplayName => module.title;

		public override string Tooltip => Lib.BuildString(base.Tooltip, "\n", Lib.Bold("Process capacity :"),"\n", module.ModuleInfo);

		public override string Status => Lib.Color(module.IsRunning(), Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
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

		public override bool IsVisible => prefab.toggle;

		public override string DisplayName => prefab.title;

		public override string Tooltip => Lib.BuildString(base.Tooltip, "\n", Lib.Bold("Process capacity :"), "\n", prefab.ModuleInfo);

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "running"), Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
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



