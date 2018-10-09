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

		public override void GUI_Update(bool hasEnergy)
		{
			Lib.Debug("Buttons is '{0}' for '{1}' landingGear", (hasEnergy == true ? "ON" : "OFF"), landingGear.part.partInfo.title);
			landingGear.Events["EventToggle"].active = hasEnergy;
		}

		public override void FixModule(bool hasEnergy)
		{
			ToggleActions(landingGear, hasEnergy);
		}

		ModuleWheelDeployment landingGear;
	}
}