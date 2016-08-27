// ====================================================================================================================
// represent a file or a device in the computer
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class File
{
  public File()
  {}

  public File(string content)
  {
    this.content = content;
  }

  public File(Device device)
  {
    this.device = device;
  }

  public File(ConfigNode node)
  {
    content = Lib.ConfigValue(node, "content", string.Empty).Replace("$NEWLINE", "\n").Replace("$COMMENT", "//");
    send = Lib.ConfigValue(node, "send", string.Empty);
    data = Lib.ConfigValue(node, "data", 0.0);
    // note: device files are never serialized
  }

  public void save(ConfigNode node)
  {
    node.AddValue("content", content.Replace("\n", "$NEWLINE").Replace("//", "$COMMENT"));
    node.AddValue("send", send);
    node.AddValue("data", data);
  }

  public string                 content = "";             // the file content as a string
  public string                 send = "";                // the filename of the device this file is being transmitted to, if not empty
  public double                 data;                     // store science data amount in bits
  public Device                 device;                   // device associated with the file
}


} // KERBALISM