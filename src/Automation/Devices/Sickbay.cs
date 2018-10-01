using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class SickbayDevice : Device
	{
		public SickbayDevice(Sickbay sickbay_ctrl)
		{
			this.sickbay_ctrl = sickbay_ctrl;
		}

		public override string Name()
		{
			return sickbay_ctrl.title.ToLower();
		}

		public override uint Part()
		{
			return sickbay_ctrl.part.flightID;
		}

		public override string Info()
		{
			return sickbay_ctrl.running
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_RUNNING") + "</color>"
			  : "<color=red>" + Localizer.Format("#KERBALISM_Generic_STOPPED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			sickbay_ctrl.running = value;
		}

		public override void Toggle()
		{
			Ctrl(!sickbay_ctrl.running);
		}

		Sickbay sickbay_ctrl;
	}


	public sealed class ProtoSickbayDevice : Device
	{
		public ProtoSickbayDevice(ProtoPartModuleSnapshot process_ctrl, Sickbay prefab, uint part_id)
		{
			this.sickbay_ctrl = process_ctrl;
			this.prefab = prefab;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return prefab.title.ToLower();
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			return Lib.Proto.GetBool(sickbay_ctrl, "running")
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_RUNNING") + "</color>"
			  : "<color=red>" + Localizer.Format("#KERBALISM_Generic_STOPPED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(sickbay_ctrl, "running", value);
			ProtoPartSnapshot part_prefab = FlightGlobals.FindProtoPartByID(part_id);
			part_prefab.resources.Find(k => k.resourceName == prefab.resource).flowState = value;
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(sickbay_ctrl, "running"));
		}

		private readonly ProtoPartModuleSnapshot sickbay_ctrl;
		private Sickbay prefab;
		private readonly uint part_id;
	}


} // KERBALISM



