using ModuleWheels;
using KSP.Localization;

namespace KERBALISM
{
	public class LandingGearEC : DeviceEC
	{
		public LandingGearEC(ModuleWheelDeployment landingGear, double extra_Deploy)
		{
			this.landingGear = landingGear;
			this.extra_Deploy = extra_Deploy;
		}

		protected override bool IsConsuming
		{
			get
			{
				if (landingGear.stateString == Localizer.Format("#autoLOC_6002270") || landingGear.stateString == Localizer.Format("#autoLOC_234856"))
				{
					actualCost = extra_Deploy;
					return true;
				}
				return false;
			}
		}

		public override void GUI_Update(bool isEnabled)
		{
			Lib.LogDebugStack("Buttons is '{0}' for '{1}' landingGear", Lib.LogLevel.Message, (isEnabled == true ? "ON" : "OFF"), landingGear.part.partInfo.title);
			landingGear.Events["EventToggle"].active = isEnabled;
		}

		public override void FixModule(bool isEnabled)
		{
			ToggleActions(landingGear, isEnabled);
		}

		ModuleWheelDeployment landingGear;
	}
}
