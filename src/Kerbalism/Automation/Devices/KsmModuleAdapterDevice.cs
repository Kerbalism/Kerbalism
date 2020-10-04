using System;
namespace KERBALISM
{
	public class KsmModuleAdapterDevice : Device
	{
		private readonly uint id;
		private readonly string name;
		private readonly string displayName;
		private readonly string partName;
		private readonly AutomationAdapter automationAdapter;
		private readonly ModuleData moduleData;

		public KsmModuleAdapterDevice(KsmPartModule prefab, ProtoPartSnapshot protoPart, AutomationAdapter adapter)
		{
			name = prefab.part.partInfo.title;
			displayName = prefab is IModuleInfo ? ((IModuleInfo)prefab).GetModuleTitle() : prefab.GUIName;
			id = protoPart.flightID;
			partName = prefab.part.partInfo.title;
			automationAdapter = adapter;
			protoPart.TryGetModuleDataOfType(prefab.ModuleDataType, out moduleData);
		}

		public KsmModuleAdapterDevice(KsmPartModule ksmModule, AutomationAdapter adapter)
		{
			name = ksmModule.part.partInfo.title;
			displayName = ksmModule is IModuleInfo ? ((IModuleInfo)ksmModule).GetModuleTitle() : ksmModule.GUIName;
			id = ksmModule.part.flightID;
			partName = ksmModule.part.partInfo.title;
			automationAdapter = adapter;
			moduleData = ksmModule.ModuleData;
		}

		public override string Name => automationAdapter.Name ?? name;

		public override string DisplayName => automationAdapter.DisplayName ?? displayName;

		public override uint PartId => id;

		public override string PartName => partName;

		public override string Status => automationAdapter.Status;

		public override bool IsVisible => moduleData != null && automationAdapter.IsVisible;

		public override void Ctrl(bool value)
		{
			automationAdapter.Ctrl(value);
		}

		public override void Toggle()
		{
			automationAdapter.Toggle();
		}

		public override DeviceIcon Icon => automationAdapter.Icon ?? base.Icon;

		public override string Tooltip => automationAdapter.Tooltip ?? base.Tooltip;

		public override void OnUpdate() => automationAdapter.OnUpdate();
	}
}
