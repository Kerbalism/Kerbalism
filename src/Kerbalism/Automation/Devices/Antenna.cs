using KSP.Localization;
using static ModuleDeployablePart;

namespace KERBALISM
{
	public sealed class AntennaDevice : LoadedDevice<PartModule>
	{
		private readonly ModuleAnimateGeneric specialCase;
		private readonly ModuleDeployableAntenna animDefault;
		private readonly string ModuleName;

		public AntennaDevice(PartModule module, string ModuleName) : base(module)
		{
			this.ModuleName = ModuleName;
			if (ModuleName == "ModuleDataTransmitter")
			{
				// do we have an animation
				animDefault = module.part.FindModuleImplementing<ModuleDeployableAntenna>();
				specialCase = module.part.FindModuleImplementing<ModuleAnimateGeneric>();
				if (animDefault != null) this.ModuleName = "ModuleDeployableAntenna";
				else if (specialCase != null && !module.part.name.Contains("Lander"))
				{
					// the Mk-2 lander can has doors that can be opened via a ModuleAnimateGeneric
					// and would show up as "antenna" in automation.
					this.ModuleName = "ModuleAnimateGeneric";
				}
			}
		}

		public override string Name => "antenna";

		public override string Status
		{
			get
			{
				switch (ModuleName)
				{
					case "ModuleDeployableAntenna":
						switch (animDefault.deployState)
						{
							case DeployState.EXTENDED: return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.Kolor.Green);
							case DeployState.RETRACTED: return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.Kolor.Yellow);
							case DeployState.BROKEN: return Lib.Color(Localizer.Format("#KERBALISM_Generic_BROKEN"), Lib.Kolor.Red);
							case DeployState.EXTENDING: return Localizer.Format("#KERBALISM_Generic_EXTENDING");
							case DeployState.RETRACTING: return Localizer.Format("#KERBALISM_Generic_RETRACTING");
						}
						break;
					case "ModuleAnimateGeneric":
						if (specialCase.aniState == ModuleAnimateGeneric.animationStates.MOVING)
						{
							return specialCase.animSpeed > 0 ? Localizer.Format("#KERBALISM_Generic_EXTENDING")
															 : Localizer.Format("#KERBALISM_Generic_RETRACTING");
						}
						return specialCase.animSpeed > 0 ? Lib.Color(Localizer.Format("#KERBALISM_Antenna_statu_deployed"), Lib.Kolor.Green) : Lib.Color(Localizer.Format("#KERBALISM_Antenna_statu_retracted"), Lib.Kolor.Yellow);//"deployed""retracted"
					case "ModuleDataTransmitter":
						return Localizer.Format("#KERBALISM_Antenna_statu_fixed");//"fixed"
					case "ModuleRTAntenna":
						return Lib.ReflectionValue<bool>(module, "IsRTActive")
						  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.Kolor.Green)
						  : Lib.Color(Localizer.Format("#KERBALISM_Generic_INACTIVE"), Lib.Kolor.Yellow);
				}
				return Localizer.Format("#KERBALISM_Antenna_statu_unknown");//"unknown"
			}
		}

		public override void Ctrl(bool value)
		{
			switch (ModuleName)
			{
				case "ModuleDeployableAntenna":
					if (animDefault.deployState == DeployState.BROKEN) return;
					if (value) animDefault.Extend(); else animDefault.Retract(); break;
				case "ModuleAnimateGeneric": specialCase.Toggle(); break;
				case "ModuleRTAntenna": Lib.ReflectionValue(module, "IsRTActive", value); break;
			}
		}

		public override void Toggle()
		{
			switch (ModuleName)
			{
				case "ModuleDeployableAntenna":
					Ctrl(!(animDefault.deployState == DeployState.EXTENDED));
					break;
				case "ModuleAnimateGeneric":
					Ctrl(true);
					break;
				case "ModuleRTAntenna":
					Ctrl(!Lib.ReflectionValue<bool>(module, "IsRTActive"));
					break;
			}
		}

		public override bool IsVisible
		{
			get
			{
				switch (ModuleName)
				{
					case "ModuleDataTransmitter":
						return false;
					default:
						return true;
				}
			}
		}
	}

	public sealed class ProtoAntennaDevice : ProtoDevice<PartModule>
	{
		private readonly string ModuleName;
		private readonly ProtoPartModuleSnapshot antenna;

		public ProtoAntennaDevice(PartModule prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, string ModuleName)
			: base(prefab, protoPart, protoModule)
		{
			if (ModuleName == "ModuleDataTransmitter")
			{
				this.ModuleName = "ModuleDataTransmitter";
				this.antenna = protoModule;

				if (protoPart.FindModule("ModuleDeployableAntenna") != null)
				{
					this.ModuleName = "ModuleDeployableAntenna";
					this.antenna = protoPart.FindModule("ModuleDeployableAntenna");
				}
				else if (protoPart.FindModule("ModuleAnimateGeneric") != null
					&& !protoPart.partName.Contains("Lander")) // see above
				{
					this.ModuleName = "ModuleAnimateGeneric";
					this.antenna = protoPart.FindModule("ModuleAnimateGeneric");
				}
			}
			else
			{
				this.antenna = protoModule;
				this.ModuleName = ModuleName;
			}
		}

		public override string Name => "antenna";

		public override string Status
		{
			get
			{
				switch (ModuleName)
				{
					case "ModuleDeployableAntenna":
						string state = Lib.Proto.GetString(antenna, "deployState");
						switch (state)
						{
							case "EXTENDED": return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.Kolor.Green);
							case "RETRACTED": return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.Kolor.Yellow);
							case "BROKEN": return Lib.Color(Localizer.Format("#KERBALISM_Generic_BROKEN"), Lib.Kolor.Red);
						}
						break;
					case "ModuleAnimateGeneric":
						return Lib.Proto.GetFloat(antenna, "animSpeed") > 0 ?
							Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.Kolor.Green) :
							Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.Kolor.Yellow);
					case "ModuleDataTransmitter":
						return "fixed";
					case "ModuleRTAntenna":
						return Lib.Proto.GetBool(antenna, "IsRTActive")
						  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.Kolor.Green)
						  : Lib.Color(Localizer.Format("#KERBALISM_Generic_INACTIVE"), Lib.Kolor.Yellow);
				}
				return Localizer.Format("#KERBALISM_Antenna_statu_unknown");//"unknown"
			}
		}

		public override bool IsVisible
		{
			get
			{
				switch (ModuleName)
				{
					case "ModuleDataTransmitter":
						return false;
					default:
						return true;
				}
			}
		}

		public override void Ctrl(bool value)
		{
			switch (ModuleName)
			{
				case "ModuleDeployableAntenna":
					if (Lib.Proto.GetString(antenna, "deployState") == "BROKEN") return;
					Lib.Proto.Set(antenna, "deployState", value ? "EXTENDED" : "RETRACTED");
					break;
				case "ModuleAnimateGeneric":
					Lib.Proto.Set(antenna, "animSpeed", !value ? 1f : 0f);
					Lib.Proto.Set(antenna, "animTime", !value ? 1f : 0f);
					break;
				case "ModuleRTAntenna":
					Lib.Proto.Set(antenna, "IsRTActive", value);
					break;
			}
		}

		public override void Toggle()
		{
			switch (ModuleName)
			{
				case "ModuleDeployableAntenna":
					Ctrl(Lib.Proto.GetString(antenna, "deployState") != "EXTENDED");
					break;
				case "ModuleAnimateGeneric":
					Ctrl(Lib.Proto.GetFloat(antenna, "animSpeed") > 0);
					break;
				case "ModuleRTAntenna":
					Ctrl(!Lib.Proto.GetBool(antenna, "IsRTActive"));
					break;
			}
		}


	}
}
