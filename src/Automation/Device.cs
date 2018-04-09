using System;


namespace KERBALISM
{


	public abstract class Device
	{
		// return device name
		public abstract string name();

		// return part id
		public abstract uint part();

		// return short device status string
		public abstract string info();

		// control the device using a value
		public abstract void ctrl(bool value);

		// toggle the device state
		public abstract void toggle();

		// generate unique id for the module
		// - multiple same-type components in the same part will have the same id
		public uint id() { return part() + (uint)name().GetHashCode(); }
	}


} // KERBALISM