
using System;

namespace KERBALISM
{
	public abstract class AutomationAdapter
	{
		protected ModuleData moduleData { get; }
		protected KsmPartModule module { get; }

		protected AutomationAdapter(KsmPartModule module, ModuleData moduleData)
		{
			this.module = module;
			this.moduleData = moduleData;
		}

		public virtual Device.DeviceIcon Icon { get; }
		public virtual string Tooltip { get; }
		public abstract string Name { get; }
		public virtual string DisplayName { get; }

		public abstract string Status { get; }
		public virtual bool IsVisible { get; protected set; } = true;


		public abstract void Ctrl(bool value);
		public abstract void Toggle();

		public virtual void OnUpdate() { }
	}
}
