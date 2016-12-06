using System;
using System.Text;


namespace KERBALISM {


public class ReadArchive
{
  public ReadArchive(string data)
  {
    this.data = data;
  }

  public void load(out int integer)
  {
    integer = data[index] - 32;
    ++index;
  }

  public void load(out string text)
  {
    int len;
    load(out len);
    text = data.Substring(index, len);
    index += len;
  }

  public void load(out double value)
  {
    string s;
    load(out s);
    value = Lib.Parse.ToDouble(s);
  }

  string data;
  int index;
}


public class WriteArchive
{
  public void save(int integer)
  {
    integer = Lib.Clamp(integer + 32, 32, 255);
    sb.Append((char)integer);
  }

  public void save(string text)
  {
    save(text.Length);
    sb.Append(text.Substring(0, Math.Min(255 - 32, text.Length)));
  }

  public void save(double value)
  {
    save(value.ToString());
  }

  public string serialize()
  {
    return sb.ToString();
  }

  StringBuilder sb = new StringBuilder();
}


} // KERBALISM

