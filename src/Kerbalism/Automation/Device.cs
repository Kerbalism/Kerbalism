using System;
using UnityEngine;

namespace KERBALISM
{


	public abstract class Device
	{
		public class DeviceIcon
		{
			public Texture2D texture;
			public string tooltip;
			public Action onClick;

			public DeviceIcon(Texture2D texture, string tooltip = "", Action onClick = null)
			{
				this.texture = texture;
				this.tooltip = tooltip;
				this.onClick = onClick;
			}
		}

		// return device name
		public abstract string Name();

		// return part id
		public abstract uint Part();

		// return short device status string
		public abstract string Status();

		// return tooltip string
		public virtual string Tooltip() => string.Empty;

		// return icon/button
		public virtual DeviceIcon Icon => null;

		// control the device using a value
		public abstract void Ctrl(bool value);

		// toggle the device state
		public abstract void Toggle();

		// generate unique id for the module
		// - multiple same-type components in the same part will have the same id
		public uint Id() { return Part() + (uint)Name().GetHashCode(); }

		public virtual bool IsVisible() { return true; }

		public virtual void OnUpdate() { }
	}


} // KERBALISM
