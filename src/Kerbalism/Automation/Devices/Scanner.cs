using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	public sealed class ScannerDevice : LoadedDevice<PartModule>
	{
		public ScannerDevice(PartModule module) : base(module) { }

		public override string Status => Lib.Color(Lib.ReflectionValue<bool>(module, "scanning"), Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			bool scanning = Lib.ReflectionValue<bool>(module, "scanning");
			if (scanning && !value) module.Events["stopScan"].Invoke();
			else if (!scanning && value) module.Events["startScan"].Invoke();
		}

		public override void Toggle()
		{
			Ctrl(!Lib.ReflectionValue<bool>(module, "scanning"));
		}
	}

	public sealed class ProtoScannerDevice : ProtoDevice<PartModule>
	{
		private readonly Vessel vessel;

		public ProtoScannerDevice(PartModule prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, Vessel v)
			: base(prefab, protoPart, protoModule)
		{
			this.vessel = v;
		}

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "scanning"), Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			bool scanning = Lib.Proto.GetBool(protoModule, "scanning");
			if (scanning && !value) SCANsat.StopScanner(vessel, protoModule, prefab.part);
			else if (!scanning && value) SCANsat.ResumeScanner(vessel, protoModule, prefab.part);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "scanning"));
		}
	}


} // KERBALISM

