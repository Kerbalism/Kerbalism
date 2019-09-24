using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class GeneratorDevice : LoadedDevice<ModuleGenerator>
	{
		public GeneratorDevice(ModuleGenerator module) : base(module) { }

		public override string Name => "generator";

		public override string Status
			=> module.isAlwaysActive ? Localizer.Format("#KERBALISM_Generic_ALWAYSON") : Lib.Color(module.generatorIsActive, Localizer.Format("#KERBALISM_Generic_ON"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_OFF"),  Lib.KColor.Yellow);

		public override void Ctrl(bool value)
		{
			if (module.isAlwaysActive) return;
			if (value) module.Activate();
			else module.Shutdown();
		}

		public override void Toggle()
		{
			Ctrl(!module.generatorIsActive);
		}

		public override bool IsVisible => !module.isAlwaysActive;
	}


	public sealed class ProtoGeneratorDevice : ProtoDevice<ModuleGenerator>
	{
		public ProtoGeneratorDevice(ModuleGenerator prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name => "generator";

		public override string Status
		{
			get
			{
				if (prefab.isAlwaysActive) return Localizer.Format("#KERBALISM_Generic_ALWAYSON");
				bool is_on = Lib.Proto.GetBool(protoModule, "generatorIsActive");
				return Lib.Color(is_on, Localizer.Format("#KERBALISM_Generic_ON"), Lib.KColor.Green, Localizer.Format("#KERBALISM_Generic_OFF"), Lib.KColor.Yellow);
			}
		}

		public override void Ctrl(bool value)
		{
			if (prefab.isAlwaysActive) return;
			Lib.Proto.Set(protoModule, "generatorIsActive", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "generatorIsActive"));
		}

		public override bool IsVisible => !prefab.isAlwaysActive;
	}


} // KERBALISM
