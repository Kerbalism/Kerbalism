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

		public override string name()
		{
			return "scanner";
		}

		public override uint part()
		{
			return scanner.part.flightID;
		}

		public override string info()
		{
			return Lib.ReflectionValue<bool>(scanner, "scanning")
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ENABLED") + "</color>"
			  : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void ctrl(bool value)
		{
			bool scanning = Lib.ReflectionValue<bool>(scanner, "scanning");
			if (scanning && !value) scanner.Events["stopScan"].Invoke();
			else if (!scanning && value) scanner.Events["startScan"].Invoke();
		}

		public override void toggle()
		{
			ctrl(!Lib.ReflectionValue<bool>(scanner, "scanning"));
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

		public override string name()
		{
			return "scanner";
		}

		public override uint part()
		{
			return part_id;
		}

		public override string info()
		{
			return Lib.Proto.GetBool(scanner, "scanning")
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ENABLED") + "</color>"
			  : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void ctrl(bool value)
		{
			bool scanning = Lib.Proto.GetBool(scanner, "scanning");
			if (scanning && !value) SCANsat.stopScanner(vessel, scanner, part_prefab);
			else if (!scanning && value) SCANsat.resumeScanner(vessel, scanner, part_prefab);
		}

		public override void toggle()
		{
			ctrl(!Lib.Proto.GetBool(scanner, "scanning"));
		}

		ProtoPartModuleSnapshot scanner;
		Part part_prefab;
		Vessel vessel;
		uint part_id;
	}


} // KERBALISM

