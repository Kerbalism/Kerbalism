using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class DrillDevice : Device
	{
		public DrillDevice(ModuleResourceHarvester drill)
		{
			this.drill = drill;
		}

		public override string Name()
		{
			return "drill";
		}

		public override uint Part()
		{
			return drill.part.flightID;
		}

		public override string Info()
		{
			if (drill.AlwaysActive) return Localizer.Format("#KERBALISM_Generic_ALWAYSON");
			return drill.IsActivated ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (drill.AlwaysActive) return;
			if (value) drill.StartResourceConverter();
			else drill.StopResourceConverter();
		}

		public override void Toggle()
		{
			Ctrl(!drill.IsActivated);
		}

		ModuleResourceHarvester drill;
	}


	public sealed class ProtoDrillDevice : Device
	{
		public ProtoDrillDevice(ProtoPartModuleSnapshot drill, ModuleResourceHarvester prefab, uint part_id)
		{
			this.drill = drill;
			this.prefab = prefab;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "drill";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			if (prefab.AlwaysActive) Localizer.Format("#KERBALISM_Generic_ALWAYSON");
			bool is_on = Lib.Proto.GetBool(drill, "IsActivated");
			return is_on ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (prefab.AlwaysActive) return;
			Lib.Proto.Set(drill, "IsActivated", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(drill, "IsActivated"));
		}

		private readonly ProtoPartModuleSnapshot drill;
		private ModuleResourceHarvester prefab;
		private readonly uint part_id;
	}


} // KERBALISM