using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public sealed class HarvesterDevice : LoadedDevice<Harvester>
	{
		private readonly ModuleAnimationGroup animator;

		public HarvesterDevice(Harvester module) : base(module)
		{
			animator = module.part.FindModuleImplementing<ModuleAnimationGroup>();
		}

		public override string Name => Lib.BuildString(module.resource, " harvester").ToLower();

		public override string Status
		{
			get
			{
			return animator != null && !module.deployed
			  ? "not deployed"
			  : !module.running
			  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_STOPPED"), Lib.Kolor.Yellow)
			  : module.issue.Length == 0
			  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_RUNNING"), Lib.Kolor.Green)
			  : Lib.Color(module.issue, Lib.Kolor.Red);
			}
		}

		public override void Ctrl(bool value)
		{
			if (module.deployed)
			{
				module.running = value;
			}
		}

		public override void Toggle()
		{
			Ctrl(!module.running);
		}
	}

	public sealed class ProtoHarvesterDevice : ProtoDevice<Harvester>
	{
		private readonly ProtoPartModuleSnapshot animator;

		public ProtoHarvesterDevice(Harvester prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			this.animator = protoPart.FindModule("ModuleAnimationGroup");
		}

		public override string Name => Lib.BuildString(prefab.resource, " harvester").ToLower();

		public override string Status
		{
			get
			{
				bool deployed = Lib.Proto.GetBool(protoModule, "deployed");
				bool running = Lib.Proto.GetBool(protoModule, "running");
				string issue = Lib.Proto.GetString(protoModule, "issue");

				return animator != null && !deployed
				  ? "not deployed"
				  : !running
				  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_STOPPED"), Lib.Kolor.Yellow)
				  : issue.Length == 0
				  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_RUNNING"), Lib.Kolor.Green)
				  : Lib.Color(issue, Lib.Kolor.Red);
			}
		}

		public override void Ctrl(bool value)
		{
			if (Lib.Proto.GetBool(protoModule, "deployed"))
			{
				Lib.Proto.Set(protoModule, "running", value);
			}
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "running"));
		}
	}


} // KERBALISM
