// ====================================================================================================================
// computer device interface
// ====================================================================================================================


using System;


namespace KERBALISM {


public abstract class Device
{
  // return short device status string
  public abstract string info();

  // control the device using a value
  public abstract void ctrl(double value);
}


} // KERBALISM