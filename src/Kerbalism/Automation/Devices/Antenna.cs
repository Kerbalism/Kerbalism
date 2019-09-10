using KSP.Localization;
using static ModuleDeployablePart;

namespace KERBALISM
{
	public class Antenna : Device
	{
		/// <summary>
		/// Generic module
		/// </summary>
		public Antenna(PartModule antenna, string ModuleName)
		{
			this.antenna = antenna;
			if (ModuleName == "ModuleDataTransmitter")
			{
				// do we have an animation
				animDefault = antenna.part.FindModuleImplementing<ModuleDeployableAntenna>();
				specialCase = antenna.part.FindModuleImplementing<ModuleAnimateGeneric>();
				if (animDefault != null) this.ModuleName = "ModuleDeployableAntenna";
				else if (specialCase != null) this.ModuleName = "ModuleAnimateGeneric";
				else this.ModuleName = "ModuleDataTransmitter";
			}
			else this.ModuleName = ModuleName;
		}

		public override string Name()
		{
			return "antenna";
		}

		public override uint Part()
		{
			return antenna.part.flightID;
		}

		public override string Info()
		{
			switch (ModuleName)
			{
				case "ModuleDeployableAntenna":
					switch (animDefault.deployState)
					{
						case DeployState.EXTENDED: return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.KColor.Green);
						case DeployState.RETRACTED: return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.KColor.Yellow);
						case DeployState.BROKEN: return Lib.Color(Localizer.Format("#KERBALISM_Generic_BROKEN"), Lib.KColor.Red);
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
					return specialCase.animSpeed > 0 ? Lib.Color("deployed", Lib.KColor.Green) : Lib.Color("retracted", Lib.KColor.Yellow);
				case "ModuleDataTransmitter":
					return "fixed";
				case "ModuleRTAntenna":
					return Lib.ReflectionValue<bool>(antenna, "IsRTActive")
					  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.KColor.Green)
					  : Lib.Color(Localizer.Format("#KERBALISM_Generic_INACTIVE"), Lib.KColor.Yellow);
			}
			return "unknown";
		}

		public override void Ctrl(bool value)
		{
			switch (ModuleName)
			{
				case "ModuleDeployableAntenna":
					if (animDefault.deployState == DeployState.BROKEN) return;
					if (value) animDefault.Extend(); else animDefault.Retract(); break;
				case "ModuleAnimateGeneric": specialCase.Toggle(); break;
				case "ModuleRTAntenna": Lib.ReflectionValue(antenna, "IsRTActive", value); break;
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
					Ctrl(!Lib.ReflectionValue<bool>(antenna, "IsRTActive"));
					break;
			}
		}

		public override bool IsVisible()
		{
			switch (ModuleName)
			{
				case "ModuleDataTransmitter":
					return false;
				default:
					return true;
			}
		}

		private readonly PartModule antenna;
		private readonly ModuleAnimateGeneric specialCase;
		private readonly ModuleDeployableAntenna animDefault;
		private readonly string ModuleName;
	}

	public sealed class ProtoPartAntenna : Device
	{
		public ProtoPartAntenna(ProtoPartModuleSnapshot antenna, ProtoPartSnapshot partSnap, Vessel v, string ModuleName, uint part_id)
		{
			this.protoPartSnap = partSnap;
			this.vessel = v;
			this.part_id = part_id;

			if (ModuleName == "ModuleDataTransmitter")
			{
				if (partSnap.FindModule("ModuleDeployableAntenna") != null)
				{
					this.ModuleName = "ModuleDeployableAntenna";
					this.antenna = partSnap.FindModule("ModuleDeployableAntenna");
				}
				else if (partSnap.FindModule("ModuleAnimateGeneric") != null)
				{
					this.ModuleName = "ModuleAnimateGeneric";
					this.antenna = partSnap.FindModule("ModuleAnimateGeneric");
				}
				else
				{
					this.ModuleName = "ModuleDataTransmitter";
					this.antenna = antenna;
				}
			}
			else
			{
				this.antenna = antenna;
				this.ModuleName = ModuleName;
			}
		}

		public override string Name()
		{
			return "antenna";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			switch (ModuleName)
			{
				case "ModuleDeployableAntenna":
					string state = Lib.Proto.GetString(antenna, "deployState");
					switch (state)
					{
						case "EXTENDED": return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.KColor.Green);
						case "RETRACTED": return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.KColor.Yellow);
						case "BROKEN": return Lib.Color(Localizer.Format("#KERBALISM_Generic_BROKEN"), Lib.KColor.Red);
					}
					break;
				case "ModuleAnimateGeneric":
					return Lib.Proto.GetFloat(antenna, "animSpeed") > 0 ?
						Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.KColor.Green) :
						Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.KColor.Yellow);
				case "ModuleDataTransmitter":
					return "fixed";
				case "ModuleRTAntenna":
					return Lib.Proto.GetBool(antenna, "IsRTActive")
					  ? Lib.Color(Localizer.Format("#KERBALISM_Generic_ACTIVE"), Lib.KColor.Green)
					  : Lib.Color(Localizer.Format("#KERBALISM_Generic_INACTIVE"), Lib.KColor.Yellow);
			}
			return "unknown";
		}

		public override bool IsVisible()
		{
			switch (ModuleName)
			{
				case "ModuleDataTransmitter":
					return false;
				default:
					return true;
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

		private readonly ProtoPartModuleSnapshot antenna;
		private readonly ProtoPartSnapshot protoPartSnap;
		private readonly Vessel vessel;
		private readonly uint part_id;
		private readonly string ModuleName;
	}
}
