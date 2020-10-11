using System;
using System.Text;

namespace KERBALISM
{
	public class ModuleKsmRadiationEmitter : KsmPartModule<ModuleKsmRadiationEmitter, RadiationEmitterData>, IBackgroundModule, IPlannerModule
	{
		private static StringBuilder sb = new StringBuilder();

		[KSPField] public string title = string.Empty;  // GUI name of the status action in the PAW
		[KSPField] public bool canToggle = false;       // true if the effect can be toggled on/off
		[KSPField] public double radiation;             // radiation in rad/s
		[KSPField] public double ecRate;                // EC consumption rate per-second (optional)

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public string status;  // rate of radiation emitted/shielded

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				if (title.Length == 0)
				{
					title = radiation >= 0.0 ? "Radiation emitter" : "Radiation shield";
				}
			}
		}

		public override void OnStart(StartState state)
		{
			Fields["status"].guiName = title;
			Events["Toggle"].active = canToggle;
			Actions["Action"].active = canToggle;

			// deal with non-toggable emitters
			if (!canToggle)
				moduleData.running = true;
		}

		public void Update()
		{
			// update ui
			status = moduleData.running ? Lib.HumanReadableRadiation(Math.Abs(radiation)) : Local.Emitter_none;//"none"
			Events["Toggle"].guiName = Lib.StatusToggle(part.partInfo.title, moduleData.running ? Local.Generic_ACTIVE : Local.Generic_DISABLED);
		}

		public void FixedUpdate()
		{
			if (moduleData.running && ecRate > 0.0)
			{
				moduleData.VesselData.ResHandler.ElectricCharge.Consume(ecRate * Kerbalism.elapsed_s, ResourceBroker.GetOrCreate(title));
			}
		}

		public void BackgroundUpdate(VesselData vd, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, double elapsed_s)
		{
			if (!protoPart.TryGetModuleDataOfType(out RadiationEmitterData backgroundData))
				return;

			if (backgroundData.running && ecRate > 0.0)
			{
				vd.ResHandler.ElectricCharge.Consume(ecRate * elapsed_s, ResourceBroker.GetOrCreate(title));
			}
		}

		public void PlannerUpdate(VesselResHandler resHandler, VesselDataShip vesselData)
		{
			if (moduleData.running && ecRate > 0.0)
			{
				moduleData.VesselData.ResHandler.ElectricCharge.Consume(ecRate, ResourceBroker.GetOrCreate(title));
			}
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public void Toggle()
		{
			// switch status
			if (!canToggle) return;
			moduleData.running = !moduleData.running;
		}

		// action groups
		[KSPAction("#KERBALISM_Emitter_Action")]
		public void Action(KSPActionParam param)
		{
			Toggle();
		}

		// part tooltip
		public override string GetInfo()
		{
			sb.Clear();

			if (radiation > 0.0)
				sb.AppendKSPLine(Local.Emitter_EmitIonizing);
			else
				sb.AppendKSPLine(Local.Emitter_ReduceIncoming);

			sb.AppendKSPNewLine();

			sb.AppendInfo(radiation >= 0.0 ? Local.Emitter_Emitted : Local.Emitter_ActiveShielding, Lib.HumanReadableRadiation(Math.Abs(radiation)));

			if (ecRate > 0.0)
				sb.AppendInfo(Local.Planner_consumed, Lib.HumanReadableRate(-ecRate, "F3", "EC", true));

			return sb.ToString();
		}

		// Automation support

		public override AutomationAdapter[] CreateAutomationAdapter(KsmPartModule module, ModuleData moduleData)
		{
			if (!canToggle)
				return null;
			return new AutomationAdapter[] { new RadiationEmitterAutomationAdapter(module, ModuleData) };
		}

		private class RadiationEmitterAutomationAdapter : AutomationAdapter
		{
			public RadiationEmitterAutomationAdapter(KsmPartModule module, ModuleData moduleData) : base(module, moduleData) { }

			override public void Ctrl(bool value)
			{
				if ((moduleData as RadiationEmitterData).running != value) (moduleData as RadiationEmitterData).running = value;
			}

			override public void Toggle()
			{
				(moduleData as RadiationEmitterData).running = !(moduleData as RadiationEmitterData).running;
			}

			override public string Status => Lib.Color((moduleData as RadiationEmitterData).running, Local.Generic_ON, Lib.Kolor.Green, Local.Generic_OFF, Lib.Kolor.Yellow);

			override public bool IsVisible => true;

			public override string Name => "radiationEmitter";
		}
	}
}
