using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class LightDevice : Device
	{
		public LightDevice(ModuleLight light)
		{
			this.light = light;
		}

		public override string Name()
		{
			return "light";
		}

		public override uint Part()
		{
			return light.part.flightID;
		}

		public override string Info()
		{
			return light.isOn ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (value) light.LightsOn();
			else light.LightsOff();
		}

		public override void Toggle()
		{
			Ctrl(!light.isOn);
		}

		ModuleLight light;
	}


	public sealed class ProtoLightDevice : Device
	{
		public ProtoLightDevice(ProtoPartModuleSnapshot light, uint part_id)
		{
			this.light = light;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "light";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			bool is_on = Lib.Proto.GetBool(light, "isOn");
			return is_on ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(light, "isOn", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(light, "isOn"));
		}

		private readonly ProtoPartModuleSnapshot light;
		private readonly uint part_id;
	}


} // KERBALISM