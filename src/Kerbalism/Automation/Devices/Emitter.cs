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
			return Lib.Color(emitter.running, Localizer.Format("#KERBALISM_Generic_ON"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_OFF"), Lib.KColor.Yellow);
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
			return Lib.Color(Lib.Proto.GetBool(emitter, "running"), Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_DISABLED"), Lib.KColor.Yellow);
		}

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(emitter, "running", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(emitter, "running"));
		}

		private readonly ProtoPartModuleSnapshot emitter;
		private readonly uint part_id;
	}


} // KERBALISM
