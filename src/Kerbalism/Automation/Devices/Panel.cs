using KSP.Localization;
using System;

namespace KERBALISM
{
	public sealed class PanelDevice : LoadedDevice<SolarPanelFixer>
	{
		public PanelDevice(SolarPanelFixer module) : base(module) { }

		public override string Name
		{
			get
			{
				if (module.SolarPanel.IsRetractable())
					return "solar panel (deployable)";
				else
					return "solar panel (non retractable)";
			}
		}

		public override string Status
		{
			get
			{
				switch (module.state)
				{
					case SolarPanelFixer.PanelState.Retracted: return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.Kolor.Yellow);
					case SolarPanelFixer.PanelState.Extending: return Localizer.Format("#KERBALISM_Generic_EXTENDING");
					case SolarPanelFixer.PanelState.Extended: return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.Kolor.Green);
					case SolarPanelFixer.PanelState.Retracting: return Localizer.Format("#KERBALISM_Generic_RETRACTING");
				}
				return "unknown";
			}
		}

		public override bool IsVisible => module.SolarPanel.SupportAutomation(module.state);

		public override void Ctrl(bool value)
		{
			if (value && module.state == SolarPanelFixer.PanelState.Retracted) module.ToggleState();
			if (!value && module.state == SolarPanelFixer.PanelState.Extended) module.ToggleState();
		}

		public override void Toggle()
		{
			if (module.state == SolarPanelFixer.PanelState.Retracted || module.state == SolarPanelFixer.PanelState.Extended)
				module.ToggleState();
		}
	}

	public sealed class ProtoPanelDevice : ProtoDevice<SolarPanelFixer>
	{
		public ProtoPanelDevice(SolarPanelFixer prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name
		{
			get
			{
				if (prefab.SolarPanel.IsRetractable())
					return "solar panel (deployable)";
				else
					return "solar panel (non retractable)";
			}
		}

		public override uint PartId => protoPart.flightID;

		public override string Status
		{
			get
			{
				string state = Lib.Proto.GetString(protoModule, "state");
				switch (state)
				{
					case "Retracted": return Lib.Color(Localizer.Format("#KERBALISM_Generic_RETRACTED"), Lib.Kolor.Yellow);
					case "Extended": return Lib.Color(Localizer.Format("#KERBALISM_Generic_EXTENDED"), Lib.Kolor.Green);
				}
				return "unknown";
			}
		}

		public override bool IsVisible => prefab.SolarPanel.SupportProtoAutomation(protoModule);

		public override void Ctrl(bool value)
		{
			SolarPanelFixer.PanelState state = (SolarPanelFixer.PanelState)Enum.Parse(typeof(SolarPanelFixer.PanelState), Lib.Proto.GetString(protoModule, "state"));
			if ((value && state == SolarPanelFixer.PanelState.Retracted)
				||
				(!value && state == SolarPanelFixer.PanelState.Extended))
			SolarPanelFixer.ProtoToggleState(prefab, protoModule, state);
		}

		public override void Toggle()
		{
			SolarPanelFixer.PanelState state = (SolarPanelFixer.PanelState)Enum.Parse(typeof(SolarPanelFixer.PanelState), Lib.Proto.GetString(protoModule, "state"));
			SolarPanelFixer.ProtoToggleState(prefab, protoModule, state);
		}
	}


} // KERBALISM
