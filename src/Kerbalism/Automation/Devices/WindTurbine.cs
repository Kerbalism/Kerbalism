using System;

namespace KERBALISM
{
	public sealed class WindTurbineDevice : LoadedDevice<PETTurbineFixer>
	{
		public WindTurbineDevice(PETTurbineFixer module) : base(module) { }

		public override string Name => "wind turbine";

		public override string DisplayName => Local.Brokers_WindTurbine;

		public override string Status
		{
			get
			{
				if (module.IsBroken)
					return Lib.Color(Local.Generic_BROKEN, Lib.Kolor.Red);

				if (!module.IsDeployable)
					return Lib.Color(module.IsActive, Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

				switch (module.DeployState)
				{
					case "RETRACTED":
						return Lib.Color(Local.Generic_RETRACTED, Lib.Kolor.Yellow);
					case "EXTENDING":
						return Local.Generic_EXTENDING;
					case "RETRACTING":
						return Local.Generic_RETRACTING;
					case "EXTENDED":
						return Lib.Color(Local.Generic_EXTENDED, Lib.Kolor.Green);
					default:
						return Local.Statu_unknown;
				}
			}
		}

		public override bool IsVisible => module.TurbineModule != null && !module.IsBroken;

		public override void Ctrl(bool value) => module.Ctrl(value);

		public override void Toggle() => module.Toggle();
	}

	public sealed class ProtoWindTurbineDevice : ProtoDevice<PETTurbineFixer>
	{
		private readonly ProtoPartModuleSnapshot turbineProto;
		private readonly PartModule turbinePrefab;

		public ProtoWindTurbineDevice(PETTurbineFixer prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			turbineProto = PETTurbineResourceSim.FindTurbineProto(protoPart);
			turbinePrefab = null;
			if (protoPart != null)
			{
				AvailablePart ap = PartLoader.getPartInfoByName(protoPart.partName);
				if (ap != null)
					turbinePrefab = PETTurbineResourceSim.FindTurbinePrefab(ap.partPrefab);
			}
		}

		public override string Name => "wind turbine";

		public override string DisplayName => Local.Brokers_WindTurbine;

		public override string Status
		{
			get
			{
				if (turbineProto == null)
					return Local.Statu_unknown;

				if (PETTurbineResourceSim.IsBroken(turbineProto))
					return Lib.Color(Local.Generic_BROKEN, Lib.Kolor.Red);

				bool deployable = PETTurbineResourceSim.IsDeployable(turbinePrefab);
				if (!deployable)
				{
					bool active = Lib.Proto.GetBool(turbineProto, "isActive");
					return Lib.Color(active, Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);
				}

				switch (PETTurbineResourceSim.GetDeployState(turbineProto))
				{
					case "RETRACTED":
						return Lib.Color(Local.Generic_RETRACTED, Lib.Kolor.Yellow);
					case "EXTENDING":
						return Local.Generic_EXTENDING;
					case "RETRACTING":
						return Local.Generic_RETRACTING;
					case "EXTENDED":
						return Lib.Color(Local.Generic_EXTENDED, Lib.Kolor.Green);
					default:
						return Local.Statu_unknown;
				}
			}
		}

		public override bool IsVisible => turbineProto != null && !PETTurbineResourceSim.IsBroken(turbineProto);

		public override void Ctrl(bool value)
		{
			if (turbineProto == null || PETTurbineResourceSim.IsBroken(turbineProto))
				return;

			bool deployable = PETTurbineResourceSim.IsDeployable(turbinePrefab);
			if (deployable)
			{
				string state = PETTurbineResourceSim.GetDeployState(turbineProto);
				if (value && state == "RETRACTED")
					PETTurbineResourceSim.ProtoSetDeployed(turbineProto, true);
				else if (!value && state == "EXTENDED")
					PETTurbineResourceSim.ProtoSetDeployed(turbineProto, false);
				return;
			}

			PETTurbineResourceSim.ProtoSetActive(turbineProto, value);
		}

		public override void Toggle()
		{
			if (turbineProto == null || PETTurbineResourceSim.IsBroken(turbineProto))
				return;

			bool deployable = PETTurbineResourceSim.IsDeployable(turbinePrefab);
			if (deployable)
			{
				string state = PETTurbineResourceSim.GetDeployState(turbineProto);
				if (state == "RETRACTED")
					PETTurbineResourceSim.ProtoSetDeployed(turbineProto, true);
				else if (state == "EXTENDED")
					PETTurbineResourceSim.ProtoSetDeployed(turbineProto, false);
				return;
			}

			bool active = Lib.Proto.GetBool(turbineProto, "isActive");
			PETTurbineResourceSim.ProtoSetActive(turbineProto, !active);
		}
	}
}
