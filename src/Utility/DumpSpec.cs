using System;
using System.Collections.Generic;


namespace KERBALISM {


public sealed class DumpSpecs
{
  public DumpSpecs(string value)
  {
    // if empty or false: don't dump anything
    if (value.Length == 0 || value.ToLower() == "false")
    {
      any = false;
      list = new List<string>();
    }
    // if true: dump everything
    else if (value.ToLower() == "true")
    {
      any = true;
      list = new List<string>();
    }
    // all other cases: dump only specified resources
    else
    {
      any = false;
      list = Lib.Tokenize(value, ',');
    }
  }

  // return true if the resource should dump
  public bool check(string res_name)
  {
    return any || list.Contains(res_name);
  }

  bool         any;
  List<string> list;
}


} // KERBALISM



