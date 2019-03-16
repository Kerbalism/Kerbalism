using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class EmitterDevice : Device
	{
		public EmitterDevice(Emitter emitter)
		{
			this.emitter = emitter;
		}

		public override string Name()
		{
			return "emitter";
		}

		public override uint Part()
		{
			return emitter.part.flightID;
		}

		public override string Info()
		{
			return emitter.running ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + " </color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (!emitter.toggle) return;
			if (emitter.running != value) emitter.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!emitter.running);
		}

		public override bool IsVisible()
		{
			return emitter.toggle;
		}

		Emitter emitter;
	}


	public sealed class ProtoEmitterDevice : Device
	{
		public ProtoEmitterDevice(ProtoPartModuleSnapshot emitter, uint part_id)
		{
			this.emitter = emitter;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "emitter";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			return Lib.Proto.GetBool(emitter, "running") ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ACTIVE") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (!Lib.Proto.GetBool(emitter, "toggle")) return;
			Lib.Proto.Set(emitter, "running", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(emitter, "running"));
		}

		public override bool IsVisible()
		{
			return Lib.Proto.GetBool(emitter, "toggle");
		}

		private readonly ProtoPartModuleSnapshot emitter;
		private readonly uint part_id;
	}


} // KERBALISM