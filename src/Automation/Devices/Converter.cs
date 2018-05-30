using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class ConverterDevice : Device
	{
		public ConverterDevice(ModuleResourceConverter converter)
		{
			this.converter = converter;
		}

		public override string Name()
		{
			return "converter";
		}

		public override uint Part()
		{
			return converter.part.flightID;
		}

		public override string Info()
		{
			return converter.AlwaysActive ? Localizer.Format("#KERBALISM_Generic_ALWAYSON") : converter.IsActivated ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (converter.AlwaysActive) return;
			if (value) converter.StartResourceConverter();
			else converter.StopResourceConverter();
		}

		public override void Toggle()
		{
			Ctrl(!converter.IsActivated);
		}

		ModuleResourceConverter converter;
	}


	public sealed class ProtoConverterDevice : Device
	{
		public ProtoConverterDevice(ProtoPartModuleSnapshot converter, ModuleResourceConverter prefab, uint part_id)
		{
			this.converter = converter;
			this.prefab = prefab;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "converter";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			if (prefab.AlwaysActive) return Localizer.Format("#KERBALISM_Generic_ALWAYSON");
			bool is_on = Lib.Proto.GetBool(converter, "IsActivated");
			return is_on ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (prefab.AlwaysActive) return;
			Lib.Proto.Set(converter, "IsActivated", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(converter, "IsActivated"));
		}

		private readonly ProtoPartModuleSnapshot converter;
		private ModuleResourceConverter prefab;
		private readonly uint part_id;
	}


} // KERBALISM