using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class LaboratoryDevice : Device
	{
		public LaboratoryDevice(Laboratory lab)
		{
			this.lab = lab;
		}

		public override string Name()
		{
			return "laboratory";
		}

		public override uint Part()
		{
			return lab.part.flightID;
		}

		public override string Info()
		{
			return lab.running ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ACTIVE") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (lab.running != value) lab.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!lab.running);
		}

		Laboratory lab;
	}


	public sealed class ProtoLaboratoryDevice : Device
	{
		public ProtoLaboratoryDevice(ProtoPartModuleSnapshot lab, uint part_id)
		{
			this.lab = lab;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "laboratory";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			return Lib.Proto.GetBool(lab, "running") ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ACTIVE") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(lab, "running", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(lab, "running"));
		}

		ProtoPartModuleSnapshot lab;
		uint part_id;
	}


} // KERBALISM

