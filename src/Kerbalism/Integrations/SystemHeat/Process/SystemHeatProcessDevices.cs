using KSP.Localization;

namespace KERBALISM
{
	public sealed class FissionReactorProcessDevice : LoadedDevice<ProcessControllerSystemHeat>
	{
		public FissionReactorProcessDevice(ProcessControllerSystemHeat module) : base(module) { }

		public override bool IsVisible => module.toggle;

		public override string DisplayName => Localizer.Format("#KERBALISM_Device_FissionReactor");

		public override string Tooltip => Lib.BuildString(base.Tooltip, "\n", Lib.Bold(Local.Process_capacity), "\n", module.ModuleInfo);

		public override string Status => Lib.Color(module.IsRunning(), Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value) => module.SetReactorPowerPercent(value ? 100f : 0f);

		public override void Toggle() => Ctrl(!module.IsRunning());
	}

	public sealed class ProtoFissionReactorProcessDevice : ProtoDevice<ProcessControllerSystemHeat>
	{
		public ProtoFissionReactorProcessDevice(ProcessControllerSystemHeat prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override bool IsVisible => prefab.toggle;

		public override string DisplayName => Localizer.Format("#KERBALISM_Device_FissionReactor");

		public override string Tooltip => Lib.BuildString(base.Tooltip, "\n", Lib.Bold(Local.Process_capacity), "\n", prefab.ModuleInfo);

		public override string Status => Lib.Color(IsProtoRunning(), Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Yellow);

		private bool IsProtoRunning()
		{
			if (Lib.Proto.GetBool(protoModule, nameof(ProcessController.broken)))
				return false;

			if (Lib.Proto.GetBool(protoModule, nameof(ProcessController.running)))
				return true;

			ProtoPartResourceSnapshot res = protoPart.resources.Find(k => k.resourceName == prefab.resource);
			if (res == null || !res.flowState || res.amount <= 0.0)
				return false;

			return Lib.Proto.GetFloat(protoModule, nameof(ProcessControllerSystemHeat.CurrentPowerPercent)) >= prefab.MinimumThrottle;
		}

		public override void Ctrl(bool value)
		{
			if (Lib.Proto.GetBool(protoModule, nameof(ProcessController.broken)))
				return;

			Lib.Proto.Set(protoModule, nameof(ProcessController.running), value);
			Lib.Proto.Set(protoModule, nameof(ProcessControllerSystemHeat.CurrentPowerPercent), value ? 100f : 0f);
			ProtoPartResourceSnapshot res = protoPart.resources.Find(k => k.resourceName == prefab.resource);
			if (res != null)
			{
				if (!value)
				{
					res.flowState = false;
					if (res.amount > 0.0)
						res.amount = 0.0;
				}
				else
					res.flowState = true;
			}
		}

		public override void Toggle() => Ctrl(!Lib.Proto.GetBool(protoModule, nameof(ProcessController.running)));
	}

	public sealed class SystemHeatProcessDevice : LoadedDevice<ProcessControllerSystemHeat>
	{
		private readonly ModuleAnimationGroup animator;

		public SystemHeatProcessDevice(ProcessControllerSystemHeat module) : base(module)
		{
			animator = module.part.FindModuleImplementing<ModuleAnimationGroup>();
		}

		public override bool IsVisible => module.toggle;

		public override string DisplayName => module.title;

		public override string Tooltip => Lib.BuildString(base.Tooltip, "\n", Lib.Bold(Local.Process_capacity), "\n", module.ModuleInfo);

		public override string Status => !module.IsDeployedForUse()
			? Local.Generic_notdeployed
			: Lib.Color(module.IsRunning(), Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (!module.IsDeployedForUse())
				return;

			module.SetRunning(value);
		}

		public override void Toggle() => Ctrl(!module.IsRunning());
	}

	public sealed class ProtoSystemHeatProcessDevice : ProtoDevice<ProcessControllerSystemHeat>
	{
		private readonly ProtoPartModuleSnapshot animator;

		public ProtoSystemHeatProcessDevice(ProcessControllerSystemHeat prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			animator = protoPart.FindModule("ModuleAnimationGroup");
		}

		public override bool IsVisible => prefab.toggle;

		public override string DisplayName => prefab.title;

		public override string Tooltip => Lib.BuildString(base.Tooltip, "\n", Lib.Bold(Local.Process_capacity), "\n", prefab.ModuleInfo);

		public override string Status
		{
			get
			{
				bool running = Lib.Proto.GetBool(protoModule, nameof(ProcessController.running));
				return !IsProtoDeployed()
					? Local.Generic_notdeployed
					: Lib.Color(running, Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Yellow);
			}
		}

		private bool IsProtoDeployed()
		{
			if (!prefab.requireDeploy)
				return true;

			if (animator != null)
				return Lib.Proto.GetBool(animator, "isDeployed");

			return Lib.Proto.GetBool(protoModule, "deployed");
		}

		public override void Ctrl(bool value)
		{
			if (Lib.Proto.GetBool(protoModule, nameof(ProcessController.broken)) || !IsProtoDeployed())
				return;

			Lib.Proto.Set(protoModule, nameof(ProcessController.running), value);
			ProtoPartResourceSnapshot res = protoPart.resources.Find(k => k.resourceName == prefab.resource);
			if (res != null)
				res.flowState = value;
		}

		public override void Toggle() => Ctrl(!Lib.Proto.GetBool(protoModule, nameof(ProcessController.running)));
	}

	public sealed class SystemHeatHarvesterDevice : LoadedDevice<HarvesterSystemHeat>
	{
		private readonly ModuleAnimationGroup animator;

		public SystemHeatHarvesterDevice(HarvesterSystemHeat module) : base(module)
		{
			animator = module.part.FindModuleImplementing<ModuleAnimationGroup>();
		}

		// keep Name English for stable device Id hashing across languages
		public override string Name => Lib.BuildString(module.resource, " harvester").ToLower();
		public override string DisplayName => Local.Automation_harvester.Format(module.resource);

		public override string Status => animator != null && !module.deployed
			? Local.Generic_notdeployed
			: !module.running
				? Lib.Color(Local.Generic_STOPPED, Lib.Kolor.Yellow)
				: module.issue.Length == 0
					? Lib.Color(Local.Generic_RUNNING, Lib.Kolor.Green)
					: Lib.Color(module.issue, Lib.Kolor.Red);

		public override void Ctrl(bool value)
		{
			if (module.deployed)
				module.running = value;
		}

		public override void Toggle() => Ctrl(!module.running);
	}

	public sealed class ProtoSystemHeatHarvesterDevice : ProtoDevice<HarvesterSystemHeat>
	{
		private readonly ProtoPartModuleSnapshot animator;

		public ProtoSystemHeatHarvesterDevice(HarvesterSystemHeat prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			animator = protoPart.FindModule("ModuleAnimationGroup");
		}

		// keep Name English for stable device Id hashing across languages
		public override string Name => Lib.BuildString(prefab.resource, " harvester").ToLower();
		public override string DisplayName => Local.Automation_harvester.Format(prefab.resource);

		public override string Status
		{
			get
			{
				bool deployed = Lib.Proto.GetBool(protoModule, "deployed");
				bool running = Lib.Proto.GetBool(protoModule, "running");
				string issue = Lib.Proto.GetString(protoModule, "issue");

				return animator != null && !deployed
					? Local.Generic_notdeployed
					: !running
						? Lib.Color(Local.Generic_STOPPED, Lib.Kolor.Yellow)
						: issue.Length == 0
							? Lib.Color(Local.Generic_RUNNING, Lib.Kolor.Green)
							: Lib.Color(issue, Lib.Kolor.Red);
			}
		}

		public override void Ctrl(bool value)
		{
			if (Lib.Proto.GetBool(protoModule, "deployed"))
				Lib.Proto.Set(protoModule, "running", value);
		}

		public override void Toggle() => Ctrl(!Lib.Proto.GetBool(protoModule, "running"));
	}
}
