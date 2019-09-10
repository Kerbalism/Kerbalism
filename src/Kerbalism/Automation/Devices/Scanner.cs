using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class ScannerDevice : Device
	{
		public ScannerDevice(PartModule scanner)
		{
			this.scanner = scanner;
		}

		public override string Name()
		{
			return "scanner";
		}

		public override uint Part()
		{
			return scanner.part.flightID;
		}

		public override string Info()
		{
			return Lib.Color(Lib.ReflectionValue<bool>(scanner, "scanning"), Localizer.Format("#KERBALISM_Generic_ENABLED"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_DISABLED"), Lib.KColor.Yellow);
		}

		public override void Ctrl(bool value)
		{
			bool scanning = Lib.ReflectionValue<bool>(scanner, "scanning");
			if (scanning && !value) scanner.Events["stopScan"].Invoke();
			else if (!scanning && value) scanner.Events["startScan"].Invoke();
		}

		public override void Toggle()
		{
			Ctrl(!Lib.ReflectionValue<bool>(scanner, "scanning"));
		}

		PartModule scanner;
	}


	public sealed class ProtoScannerDevice : Device
	{
		public ProtoScannerDevice(ProtoPartModuleSnapshot scanner, Part part_prefab, Vessel v, uint part_id)
		{
			this.scanner = scanner;
			this.part_prefab = part_prefab;
			this.vessel = v;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "scanner";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			return Lib.Color(Lib.Proto.GetBool(scanner, "scanning"), Localizer.Format("#KERBALISM_Generic_ENABLED"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_DISABLED"), Lib.KColor.Yellow);
		}

		public override void Ctrl(bool value)
		{
			bool scanning = Lib.Proto.GetBool(scanner, "scanning");
			if (scanning && !value) SCANsat.StopScanner(vessel, scanner, part_prefab);
			else if (!scanning && value) SCANsat.ResumeScanner(vessel, scanner, part_prefab);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(scanner, "scanning"));
		}

		private readonly ProtoPartModuleSnapshot scanner;
		private readonly Part part_prefab;
		private readonly Vessel vessel;
		private readonly uint part_id;
	}


} // KERBALISM

