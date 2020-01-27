using KSP.Localization;
using System.Collections.Generic;

namespace KERBALISM
{
	// note : theoretically ModuleDataTransmitter can handle multiple animation modules, we don't support it.

	public sealed class AntennaDevice : LoadedDevice<ModuleDataTransmitter>
	{
		private readonly IScalarModule deployFxModule;

		public AntennaDevice(ModuleDataTransmitter module) : base(module)
		{
			List<IScalarModule> deployFxModules = Lib.ReflectionValue<List<IScalarModule>>(module, "deployFxModules");
			if (deployFxModules != null && deployFxModules.Count > 0)
			{
				deployFxModule = deployFxModules[0];
			}
		}

		public override bool IsVisible => deployFxModule != null;

		public override string Name
		{
			get
			{
				switch (module.antennaType)
				{
					case AntennaType.INTERNAL: return Lib.BuildString(Local.AntennaUI_type1, ", ", module.powerText);//internal antenna
					case AntennaType.DIRECT: return Lib.BuildString(Local.AntennaUI_type2,", ", module.powerText);//direct antenna
					case AntennaType.RELAY: return Lib.BuildString(Local.AntennaUI_type3, ", ", module.powerText);//relay antenna
					default: return string.Empty;
				}
			}
		}

		public override string Status
		{
			get
			{
				if (!deployFxModule.CanMove)
					return Lib.Color(Local.AntennaUI_unavailable, Lib.Kolor.Orange);//"unavailable"
				else if (deployFxModule.IsMoving())
					return Local.AntennaUI_deploying;//"deploying"
				else if (deployFxModule.GetScalar == 1f)
					return Lib.Color(Local.Generic_EXTENDED, Lib.Kolor.Green);
				else if (deployFxModule.GetScalar < 1f)
					return Lib.Color(Local.Generic_RETRACTED, Lib.Kolor.Yellow);

				return Local.Antenna_statu_unknown;
			}
		}

		public override void Ctrl(bool value)
		{
			if (deployFxModule.CanMove && !deployFxModule.IsMoving())
			{
				// ModuleAnimateGeneric.SetScalar() is borked
				if (deployFxModule is ModuleAnimateGeneric mac && mac.animSwitch != value)
					mac.Toggle();
				else
					deployFxModule.SetScalar(value ? 1f : 0f);
			}
		}

		public override void Toggle()
		{
			if (deployFxModule.CanMove && !deployFxModule.IsMoving())
			{
				// ModuleAnimateGeneric.SetScalar() is borked
				if (deployFxModule is ModuleAnimateGeneric mac)
					mac.Toggle();
				else
					deployFxModule.SetScalar(deployFxModule.GetScalar == 0f ? 1f : 0f);
			}
		}
	}

	public sealed class ProtoAntennaDevice : ProtoDevice<ModuleDataTransmitter>
	{
		private readonly ProtoPartModuleSnapshot scalarModuleSnapshot;

		public ProtoAntennaDevice(ModuleDataTransmitter prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			if (prefab.DeployFxModuleIndices != null && prefab.DeployFxModuleIndices.Length > 0 && prefab.part.Modules[prefab.DeployFxModuleIndices[0]] is IScalarModule)
			{
				scalarModuleSnapshot = protoPart.modules[prefab.DeployFxModuleIndices[0]];
			}
		}

		public override bool IsVisible => scalarModuleSnapshot != null;

		public override string Name
		{
			get
			{
				switch (prefab.antennaType)
				{
					case AntennaType.INTERNAL: return Lib.BuildString(Local.AntennaUI_type1, ", ", prefab.powerText);//internal antenna
					case AntennaType.DIRECT: return Lib.BuildString(Local.AntennaUI_type2, ", ", prefab.powerText);//direct antenna
					case AntennaType.RELAY: return Lib.BuildString(Local.AntennaUI_type3, ", ", prefab.powerText);//relay antenna
					default: return string.Empty;
				}
			}
		}

		public override string Status
		{
			get
			{
				if (protoPart.shielded)
					return Lib.Color(Local.AntennaUI_unavailable, Lib.Kolor.Orange);//"unavailable"

				switch (scalarModuleSnapshot.moduleName)
				{
					case "ModuleDeployableAntenna":
					case "ModuleDeployablePart":
						switch (Lib.Proto.GetString(scalarModuleSnapshot, "deployState"))
						{
							case "EXTENDED": return Lib.Color(Local.Generic_EXTENDED, Lib.Kolor.Green);
							case "RETRACTED": return Lib.Color(Local.Generic_RETRACTED, Lib.Kolor.Yellow);
							case "BROKEN": return Lib.Color(Local.Generic_BROKEN, Lib.Kolor.Red);
						}
						break;
					case "ModuleAnimateGeneric":
						return Lib.Proto.GetFloat(scalarModuleSnapshot, "animTime") > 0f ?
							Lib.Color(Local.Generic_EXTENDED, Lib.Kolor.Green) :
							Lib.Color(Local.Generic_RETRACTED, Lib.Kolor.Yellow);
				}
				return Local.Antenna_statu_unknown;//"unknown"
			}
		}

		public override void Ctrl(bool value)
		{
			if (protoPart.shielded)
				return;

			switch (scalarModuleSnapshot.moduleName)
			{
				case "ModuleDeployableAntenna":
				case "ModuleDeployablePart":
					if (Lib.Proto.GetString(scalarModuleSnapshot, "deployState") == "BROKEN")
						return;

					Lib.Proto.Set(scalarModuleSnapshot, "deployState", value ? "EXTENDED" : "RETRACTED");
					break;

				case "ModuleAnimateGeneric":
					Lib.Proto.Set(scalarModuleSnapshot, "animTime", value ? 1f : 0f);
					Lib.Proto.Set(scalarModuleSnapshot, "animSwitch", !value); // animSwitch is true when retracted
					break;
			}

			Lib.Proto.Set(protoModule, "canComm", value);
		}

		public override void Toggle()
		{
			switch (scalarModuleSnapshot.moduleName)
			{
				case "ModuleDeployableAntenna":
				case "ModuleDeployablePart":
					Ctrl(Lib.Proto.GetString(scalarModuleSnapshot, "deployState") == "RETRACTED");
					break;
				case "ModuleAnimateGeneric":
					// animSwitch is true when retracted
					Ctrl(Lib.Proto.GetBool(scalarModuleSnapshot, "animSwitch"));
					break;
			}
		}
	}
}
