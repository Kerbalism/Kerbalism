using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class GreenhouseDevice : Device
	{
		public GreenhouseDevice(Greenhouse greenhouse)
		{
			this.greenhouse = greenhouse;
		}

		public override string Name()
		{
			return "greenhouse";
		}

		public override uint Part()
		{
			return greenhouse.part.flightID;
		}

		public override string Info()
		{
			return greenhouse.active ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ENABLED") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (greenhouse.active != value) greenhouse.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!greenhouse.active);
		}

		Greenhouse greenhouse;
	}


	public sealed class ProtoGreenhouseDevice : Device
	{
		public ProtoGreenhouseDevice(ProtoPartModuleSnapshot greenhouse, uint part_id)
		{
			this.greenhouse = greenhouse;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "greenhouse";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			bool active = Lib.Proto.GetBool(greenhouse, "active");
			return active ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ENABLED") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(greenhouse, "active", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(greenhouse, "active"));
		}

		ProtoPartModuleSnapshot greenhouse;
		uint part_id;
	}


} // KERBALISM