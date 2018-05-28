using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class GeneratorDevice : Device
	{
		public GeneratorDevice(ModuleGenerator generator)
		{
			this.generator = generator;
		}

		public override string Name()
		{
			return "generator";
		}

		public override uint Part()
		{
			return generator.part.flightID;
		}

		public override string Info()
		{
			return generator.isAlwaysActive ? Localizer.Format("#KERBALISM_Generic_ALWAYSON") : generator.generatorIsActive ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (generator.isAlwaysActive) return;
			if (value) generator.Activate();
			else generator.Shutdown();
		}

		public override void Toggle()
		{
			Ctrl(!generator.generatorIsActive);
		}

		ModuleGenerator generator;
	}


	public sealed class ProtoGeneratorDevice : Device
	{
		public ProtoGeneratorDevice(ProtoPartModuleSnapshot generator, ModuleGenerator prefab, uint part_id)
		{
			this.generator = generator;
			this.prefab = prefab;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "generator";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			if (prefab.isAlwaysActive) return Localizer.Format("#KERBALISM_Generic_ALWAYSON");
			bool is_on = Lib.Proto.GetBool(generator, "generatorIsActive");
			return is_on ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>" : "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (prefab.isAlwaysActive) return;
			Lib.Proto.Set(generator, "generatorIsActive", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(generator, "generatorIsActive"));
		}

		ProtoPartModuleSnapshot generator;
		ModuleGenerator prefab;
		uint part_id;
	}


} // KERBALISM