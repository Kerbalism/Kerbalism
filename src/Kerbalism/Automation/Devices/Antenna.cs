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
					case AntennaType.INTERNAL: return Lib.BuildString("internal antenna, ", module.powerText);
					case AntennaType.DIRECT: return Lib.BuildString("direct antenna, ", module.powerText);
					case AntennaType.RELAY: return Lib.BuildString("relay antenna, ", module.powerText);
					default: return string.Empty;
				}
			}
		}

		public override string Status
		{
			get
			{
				if (!deployFxModule.CanMove)
					return Lib.Color("unavailable", Lib.Kolor.Orange);
				else if (deployFxModule.IsMoving())
					return Localizer.Format("deploying");
				else if (deployFxModule.GetScalar == 1f)
					return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.Kolor.Green);
				else if (deployFxModule.GetScalar == 0f)
					return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.Kolor.Yellow);

				return "error";
			}
		}

		public override void Ctrl(bool value)
		{
			if (deployFxModule.CanMove && !deployFxModule.IsMoving())
				deployFxModule.SetScalar(value ? 1f : 0f);
		}

		public override void Toggle()
		{
			if (deployFxModule.CanMove && !deployFxModule.IsMoving())
				deployFxModule.SetScalar(deployFxModule.GetScalar == 0f ? 1f : 0f);
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
					case AntennaType.INTERNAL: return Lib.BuildString("internal antenna, ", prefab.powerText);
					case AntennaType.DIRECT: return Lib.BuildString("direct antenna, ", prefab.powerText);
					case AntennaType.RELAY: return Lib.BuildString("relay antenna, ", prefab.powerText);
					default: return string.Empty;
				}
			}
		}

		public override string Status
		{
			get
			{
				if (protoPart.shielded)
					return Lib.Color("unavailable", Lib.Kolor.Orange);

				switch (scalarModuleSnapshot.moduleName)
				{
					case "ModuleDeployableAntenna":
					case "ModuleDeployablePart":
						switch (Lib.Proto.GetString(scalarModuleSnapshot, "deployState"))
						{
							case "EXTENDED": return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.Kolor.Green);
							case "RETRACTED": return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.Kolor.Yellow);
							case "BROKEN": return Lib.Color(Localizer.Format("#KERBALISM_Generic_BROKEN"), Lib.Kolor.Red);
						}
						break;
					case "ModuleAnimateGeneric":
						return Lib.Proto.GetFloat(scalarModuleSnapshot, "animTime") > 0f ?
							Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.Kolor.Green) :
							Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.Kolor.Yellow);
				}
				return Localizer.Format("#KERBALISM_Antenna_statu_unknown");//"unknown"
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
					Ctrl(Lib.Proto.GetFloat(scalarModuleSnapshot, "animTime") == 0f);
					break;
			}
		}
	}
}
