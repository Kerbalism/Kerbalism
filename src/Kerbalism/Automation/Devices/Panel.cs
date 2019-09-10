using KSP.Localization;
using System;

namespace KERBALISM
{

	public sealed class PanelDevice : Device
	{
		public PanelDevice(SolarPanelFixer panel)
		{
			this.panel = panel;
		}

		public override string Name()
		{
			if (panel.SolarPanel.IsRetractable())
				return "solar panel (deployable)";
			else
				return "solar panel (non retractable)";
		}

		public override uint Part()
		{
			return panel.part.flightID;
		}

		public override string Info()
		{
			switch (panel.state)
			{
				case SolarPanelFixer.PanelState.Retracted: return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.KColor.Yellow);
				case SolarPanelFixer.PanelState.Extending: return Localizer.Format("#KERBALISM_Generic_EXTENDING");
				case SolarPanelFixer.PanelState.Extended: return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.KColor.Green);
				case SolarPanelFixer.PanelState.Retracting: return Localizer.Format("#KERBALISM_Generic_RETRACTING");
			}
			return "unknown";
		}

		public override bool IsVisible()
		{
			return panel.SolarPanel.SupportAutomation(panel.state);
		}

		public override void Ctrl(bool value)
		{
			if (value && panel.state == SolarPanelFixer.PanelState.Retracted) panel.ToggleState();
			if (!value && panel.state == SolarPanelFixer.PanelState.Extended) panel.ToggleState();
		}

		public override void Toggle()
		{
			if (panel.state == SolarPanelFixer.PanelState.Retracted || panel.state == SolarPanelFixer.PanelState.Extended)
				panel.ToggleState();
		}

		SolarPanelFixer panel;
	}


	public sealed class ProtoPanelDevice : Device
	{
		public ProtoPanelDevice(ProtoPartModuleSnapshot panel, SolarPanelFixer prefab, uint part_id)
		{
			this.panel = panel;
			this.prefab = prefab;
			this.part_id = part_id;
		}

		public override string Name()
		{
			if (prefab.SolarPanel.IsRetractable())
				return "solar panel (deployable)";
			else
				return "solar panel (non retractable)";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			string state = Lib.Proto.GetString(panel, "state");
			switch (state)
			{
				case "Retracted": return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.KColor.Yellow);
				case "Extended": return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.KColor.Green);
			}
			return "unknown";
		}

		public override bool IsVisible()
		{
			return prefab.SolarPanel.SupportProtoAutomation(panel);
		}

		public override void Ctrl(bool value)
		{
			SolarPanelFixer.PanelState state = (SolarPanelFixer.PanelState)Enum.Parse(typeof(SolarPanelFixer.PanelState), Lib.Proto.GetString(panel, "state"));
			if ((value && state == SolarPanelFixer.PanelState.Retracted)
				||
				(!value && state == SolarPanelFixer.PanelState.Extended))
			SolarPanelFixer.ProtoToggleState(prefab, panel, state);
		}

		public override void Toggle()
		{
			SolarPanelFixer.PanelState state = (SolarPanelFixer.PanelState)Enum.Parse(typeof(SolarPanelFixer.PanelState), Lib.Proto.GetString(panel, "state"));
			SolarPanelFixer.ProtoToggleState(prefab, panel, state);
		}

		private readonly ProtoPartModuleSnapshot panel;
		private SolarPanelFixer prefab;
		private readonly uint part_id;
	}


} // KERBALISM
