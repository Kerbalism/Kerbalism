using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class EmitterDevice : LoadedDevice<ModuleKsmRadiationEmitter>
	{
		public EmitterDevice(ModuleKsmRadiationEmitter module) : base(module) { }

		public override string Name => "emitter";

		public override string Status => Lib.Color(module.moduleData.running, Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (!module.canToggle) return;
			if (module.moduleData.running != value) module.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!module.moduleData.running);
		}

		public override bool IsVisible => module.canToggle;
	}

	public sealed class ProtoEmitterDevice : ProtoDevice<ModuleKsmRadiationEmitter>
	{
		public ProtoEmitterDevice(ModuleKsmRadiationEmitter prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			protoPart.TryGetModuleDataOfType(out moduleData);
		}

		private RadiationEmitterData moduleData;

		public override string Name => "emitter";

		public override string Status => Lib.Color(moduleData.running, Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (!prefab.canToggle) return;
			if (moduleData.running != value) moduleData.running = value;
		}

		public override void Toggle()
		{
			Ctrl(!moduleData.running);
		}

		public override bool IsVisible => prefab.canToggle;
	}


} // KERBALISM
