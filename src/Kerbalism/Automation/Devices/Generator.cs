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
			return generator.isAlwaysActive ? Localizer.Format("#KERBALISM_Generic_ALWAYSON") : Lib.Color(generator.generatorIsActive, Localizer.Format("#KERBALISM_Generic_ON"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_OFF"),  Lib.KColor.Yellow);
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

		public override bool IsVisible()
		{
			return !generator.isAlwaysActive;
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
			return Lib.Color(is_on, Localizer.Format("#KERBALISM_Generic_ON"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_OFF"), Lib.KColor.Yellow);
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

		public override bool IsVisible()
		{
			return !prefab.isAlwaysActive;
		}

		private readonly ProtoPartModuleSnapshot generator;
		private ModuleGenerator prefab;
		private readonly uint part_id;
	}


} // KERBALISM
