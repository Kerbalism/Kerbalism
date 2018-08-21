using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{


	public sealed class DeployableAntennaDevice : Device
	{
		public DeployableAntennaDevice(ModuleDeployableAntenna antenna)
		{
			this.antenna = antenna;
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
			switch (antenna.deployState)
			{
				case ModuleDeployableAntenna.DeployState.EXTENDED: return "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_EXTENDED") + " </color>";
				case ModuleDeployableAntenna.DeployState.RETRACTED: return "<color=red>" + Localizer.Format("#KERBALISM_Generic_RETRACTED") + "</color>";
				case ModuleDeployableAntenna.DeployState.BROKEN: return "<color=red>" + Localizer.Format("#KERBALISM_Generic_BROKEN") + "</color>";
				case ModuleDeployableAntenna.DeployState.EXTENDING: return Localizer.Format("#KERBALISM_Generic_EXTENDING");
				case ModuleDeployableAntenna.DeployState.RETRACTING: return Localizer.Format("#KERBALISM_Generic_RETRACTING");
			}
			return "unknown";
		}

		public override void Ctrl(bool value)
		{
			if (!value && !antenna.retractable) return;
			if (antenna.deployState == ModuleDeployableAntenna.DeployState.BROKEN) return;
			if (value) antenna.Extend();
			else antenna.Retract();
		}

		public override void Toggle()
		{
			Ctrl(antenna.deployState != ModuleDeployableAntenna.DeployState.EXTENDED);
		}

		ModuleDeployableAntenna antenna;
	}


	public sealed class ProtoDeployableAntennaDevice : Device
	{
		public ProtoDeployableAntennaDevice(ProtoPartModuleSnapshot antenna, ModuleDeployableAntenna prefab, uint part_id)
		{
			this.antenna = antenna;
			this.prefab = prefab;
			this.part_id = part_id;
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
			string state = Lib.Proto.GetString(antenna, "deployState");
			switch (state)
			{
				case "EXTENDED": return "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_EXTENDED") + "</color>";
				case "RETRACTED": return "<color=red>" + Localizer.Format("#KERBALISM_Generic_RETRACTED") + "</color>";
				case "BROKEN": return "<color=red>" + Localizer.Format("#KERBALISM_Generic_BROKEN") + "</color>";
			}
			return "unknown";
		}

		public override void Ctrl(bool value)
		{
			if (!value && !prefab.retractable) return;
			if (Lib.Proto.GetString(antenna, "deployState") == "BROKEN") return;
			Lib.Proto.Set(antenna, "deployState", value ? "EXTENDED" : "RETRACTED");
		}

		public override void Toggle()
		{
			Ctrl(Lib.Proto.GetString(antenna, "deployState") != "EXTENDED");
		}

		private readonly ProtoPartModuleSnapshot antenna;
		private ModuleDeployableAntenna prefab;
		private readonly uint part_id;
	}


} // KERBALISM
