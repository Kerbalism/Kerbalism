using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM {


public interface ISpecifics
{
  Specifics Specs();
}

public sealed class Specifics
{
  public Specifics()
  {
    entries = new List<Entry>();
  }

  public void add(string label, string value = "")
  {
    Entry e = new Entry();
    e.label = label;
    e.value = value;
    entries.Add(e);
  }

  public string info(string desc = "")
  {
    StringBuilder sb = new StringBuilder();
    if (desc.Length > 0)
    {
      sb.Append("<i>");
      sb.Append(desc);
      sb.Append("</i>\n\n");
    }
    foreach(Entry e in entries)
    {
      sb.Append(e.label);
      if (e.value.Length > 0)
      {
        sb.Append(": <b>");
        sb.Append(e.value);
        sb.Append("</b>");
      }
      sb.Append("\n");
    }
    return sb.ToString();
  }

  public class Entry
  {
    public string label = string.Empty;
    public string value = string.Empty;
  }

  public List<Entry> entries;
}


} // KERBALISM