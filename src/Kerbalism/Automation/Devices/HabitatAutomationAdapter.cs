using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using System.Text;

namespace KERBALISM
{
	public class HabitatAutomationAdapter : AutomationAdapter
	{
		public HabitatAutomationAdapter(KsmPartModule module, ModuleData moduleData) : base(module, moduleData) { }

		private HabitatData data => moduleData as HabitatData;

		public override string Status => ModuleKsmHabitat.PressureStateString(data);

		public override string Name => "habitat";

		public override string Tooltip => "Pressure: " + ModuleKsmHabitat.MainInfoString(module as ModuleKsmHabitat, data);

		public override void OnUpdate()
		{
			IsVisible = data.IsDeployed;
		}

		public override void Ctrl(bool value)
		{
			switch (data.pressureState)
			{
				case HabitatData.PressureState.Pressurized:
				case HabitatData.PressureState.Pressurizing:
					if(!value)
						data.updateHandler.DepressurizingStartEvt();
					break;
				case HabitatData.PressureState.Breatheable:
				case HabitatData.PressureState.Depressurized:
				case HabitatData.PressureState.DepressurizingAboveThreshold:
				case HabitatData.PressureState.DepressurizingBelowThreshold:
					if(value)
						data.updateHandler.PressurizingStartEvt();
					break;
			}
		}

		public override void Toggle()
		{
			ModuleKsmHabitat.TryTogglePressure(module as ModuleKsmHabitat, data);
		}
	}
} // KERBALISM
