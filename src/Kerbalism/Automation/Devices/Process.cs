using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class ProcessDevice : Device
	{
		public ProcessDevice(ProcessController process_ctrl)
		{
			this.process_ctrl = process_ctrl;
		}

		public override string Name()
		{
			return process_ctrl.title.ToLower();
		}

		public override uint Part()
		{
			return process_ctrl.part.flightID;
		}

		public override string Info()
		{
			return process_ctrl.IsRunning()
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_RUNNING") + "</color>"
			  : "<color=red>" + Localizer.Format("#KERBALISM_Generic_STOPPED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (!process_ctrl.toggle) return;
			process_ctrl.SetRunning(value);
		}

		public override void Toggle()
		{
			Ctrl(!process_ctrl.IsRunning());
		}

		ProcessController process_ctrl;
	}


	public sealed class ProtoProcessDevice : Device
	{
		public ProtoProcessDevice(ProtoPartModuleSnapshot process_ctrl, ProcessController prefab, uint part_id)
		{
			this.process_ctrl = process_ctrl;
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
			return Lib.Proto.GetBool(process_ctrl, "running")
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_RUNNING") + "</color>"
			  : "<color=red>" + Localizer.Format("#KERBALISM_Generic_STOPPED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (!prefab.toggle) return;
			Lib.Proto.Set(process_ctrl, "running", value);
			ProtoPartSnapshot part_prefab = FlightGlobals.FindProtoPartByID(part_id);
			part_prefab.resources.Find(k => k.resourceName == prefab.resource).flowState = value;

			/*
			prefab.resource;
			prefab.multiple;
			prefab.capacity;

			Lib.SetProcessEnabledDisabled(part_prefab.resources, resource, running, capacity * multiple);

			part_prefab
			// vvv--- this is the code run in active parts. effect is not the same as above
			Lib.SetProcessEnabledDisabled(part, resource, running, capacity * multiple);


			if (!p.Resources.Contains(res_name))
			{
				Lib.AddResource(p, res_name, 0.0, process_capacity);
			}

			if (enable)
			{
				SetResource(p, res_name, process_capacity, process_capacity);
			}
			else
			{
				// Never remove the resource capacity, otherwise checks against
				// the pseudo resource might fail
				SetResource(p, res_name, 0.0, process_capacity);
			}
*/

		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(process_ctrl, "running"));
		}

		private readonly ProtoPartModuleSnapshot process_ctrl;
		private ProcessController prefab;
		private readonly uint part_id;
	}


} // KERBALISM



