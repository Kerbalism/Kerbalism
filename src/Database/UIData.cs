using System;


namespace KERBALISM {


  public class UIData
  {
    public UIData()
    {
      win_left = 280u;
      win_top = 100u;
      map_viewed = false;
    }

    public UIData(ConfigNode node)
    {
      win_left = Lib.ConfigValue(node, "win_left", 280u);
      win_top = Lib.ConfigValue(node, "win_top", 100u);
      map_viewed = Lib.ConfigValue(node, "map_viewed", false);
    }

    public void save(ConfigNode node)
    {
      node.AddValue("win_left", win_left);
      node.AddValue("win_top", win_top);
      node.AddValue("map_viewed", map_viewed);
    }

    public uint win_left;       // popout window position left
    public uint win_top;        // popout window position top
    public bool map_viewed;     // has the user entered map-view/tracking-station
  }


} // KERBALISM