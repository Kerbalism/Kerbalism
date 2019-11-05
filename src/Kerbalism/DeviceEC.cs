using System.Collections.Generic;

namespace KERBALISM
{
	public abstract class DeviceEC
	{
		public KeyValuePair<bool, double> GetConsume()
		{
			return new KeyValuePair<bool, double>(IsConsuming, actualCost);
		}

		protected abstract bool IsConsuming { get; }

		public abstract void GUI_Update(bool isEnabled);

		public abstract void FixModule(bool isEnabled);

		public void ToggleActions(PartModule partModule, bool value)
		{
			Lib.LogDebugStack("Part '{0}'.'{1}', setting actions to {2}", partModule.part.partInfo.title, partModule.moduleName, value ? "ON" : "OFF");
			foreach (BaseAction ac in partModule.Actions)
			{
				ac.active = value;
			}
		}

		// Return
		public double actualCost;
		public double extra_Cost;
		public double extra_Deploy;
	}
}