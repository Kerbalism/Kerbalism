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

		public override string name()
		{
			return "emitter";
		}

		public override uint part()
		{
			return emitter.part.flightID;
		}

		public override string info()
		{
			return emitter.running ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + " </color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void ctrl(bool value)
		{
			if (emitter.running != value) emitter.Toggle();
		}

		public override void toggle()
		{
			ctrl(!emitter.running);
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

		public override string name()
		{
			return "emitter";
		}

		public override uint part()
		{
			return part_id;
		}

		public override string info()
		{
			return Lib.Proto.GetBool(emitter, "running") ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ACTIVE") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + "</color>";
		}

		public override void ctrl(bool value)
		{
			Lib.Proto.Set(emitter, "running", value);
		}

		public override void toggle()
		{
			ctrl(!Lib.Proto.GetBool(emitter, "running"));
		}

		ProtoPartModuleSnapshot emitter;
		uint part_id;
	}


} // KERBALISM