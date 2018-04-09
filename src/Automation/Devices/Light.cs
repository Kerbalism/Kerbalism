using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class LightDevice : Device
	{
		public LightDevice(ModuleLight light)
		{
			this.light = light;
		}

		public override string name()
		{
			return "light";
		}

		public override uint part()
		{
			return light.part.flightID;
		}

		public override string info()
		{
			return light.isOn ? "<color=cyan>on</color>" : "<color=red>off</color>";
		}

		public override void ctrl(bool value)
		{
			if (value) light.LightsOn();
			else light.LightsOff();
		}

		public override void toggle()
		{
			ctrl(!light.isOn);
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

		public override string name()
		{
			return "light";
		}

		public override uint part()
		{
			return part_id;
		}

		public override string info()
		{
			bool is_on = Lib.Proto.GetBool(light, "isOn");
			return is_on ? "<color=cyan>on</color>" : "<color=red>off</color>";
		}

		public override void ctrl(bool value)
		{
			Lib.Proto.Set(light, "isOn", value);
		}

		public override void toggle()
		{
			ctrl(!Lib.Proto.GetBool(light, "isOn"));
		}

		ProtoPartModuleSnapshot light;
		uint part_id;
	}


} // KERBALISM