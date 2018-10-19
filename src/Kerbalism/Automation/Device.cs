using System;


namespace KERBALISM
{


	public abstract class Device
	{
		// return device name
		public abstract string Name();

		// return part id
		public abstract uint Part();

		// return short device status string
		public abstract string Info();

		// control the device using a value
		public abstract void Ctrl(bool value);

		// toggle the device state
		public abstract void Toggle();

		// generate unique id for the module
		// - multiple same-type components in the same part will have the same id
		public uint Id() { return Part() + (uint)Name().GetHashCode(); }

		public virtual bool IsVisible() { return true; }
	}


} // KERBALISM