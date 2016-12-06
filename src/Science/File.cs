using System;


namespace KERBALISM {


public sealed class File
{
  public File(double amount=0.0)
  {
    size = amount;
    send = false;
  }

  public File(ConfigNode node)
  {
    size = Lib.ConfigValue(node, "size", 0.0);
    send = Lib.ConfigValue(node, "send", false);
  }

  public void save(ConfigNode node)
  {
    node.AddValue("size", size);
    node.AddValue("send", send);
  }

  public double size;   // data size in Mb
  public bool   send;   // send-home flag
}


} // KERBALISM