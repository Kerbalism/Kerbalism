using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Notepad : Window
{
  public Notepad()
  : base(260u, 560u, 150u, 20u, Styles.win_gray)
  {
    // enable global access
    instance = this;

    // setup styles
    txt_style = new GUIStyle();
    txt_style.fontSize = 12;
    txt_style.wordWrap = true;
    txt_style.normal.textColor = Color.black;
    txt_style.stretchWidth = true;
    txt_style.stretchHeight = true;
  }


  public override bool prepare()
  {
    // forget file if vessel doen't exist anymore
    if (vessel_id != Guid.Empty && FlightGlobals.Vessels.Find(k => k.id == vessel_id) == null)
    {
      vessel_id = Guid.Empty;
    }

    // do nothing if there isn't a vessel specified
    if (vessel_id == Guid.Empty) return false;

    // disable camera mouse scrolling on mouse over
    if (contains(Event.current.mousePosition))
    {
      GameSettings.AXIS_MOUSEWHEEL.primary.scale = 0.0f;
    }

    return true;
  }


  // draw the window
  public override void render()
  {
    // draw pseudo-title
    GUILayout.BeginHorizontal(Styles.title_container);
    GUILayout.Label(Styles.empty, Styles.left_icon);
    GUILayout.Label("NOTEPAD", Styles.title_text);
    GUILayout.Label(Styles.close, Styles.right_icon);
    if (Lib.IsClicked()) Close();
    GUILayout.EndHorizontal();

    // draw the text area
    // note: deal with corner-case when user hit close: for a frame vessel is not valid
    if (vessel_id != Guid.Empty)
    {
      // get vessel
      Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

      // get vessel data from db
      VesselData vd = DB.Vessel(v);

      // show text area
      scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);
      vd.notes = GUILayout.TextArea(vd.notes, txt_style);
      GUILayout.EndScrollView();
    }
  }


  public override float height()
  {
    return 366.0f;
  }


  public static void Open(Vessel v)
  {
    // setting vessel show the window
    instance.vessel_id = v.id;
  }


  public static void Close()
  {
    // resetting vessel hide the window
    instance.vessel_id = Guid.Empty;
  }


  public static void Toggle(Vessel v)
  {
    // if vessel is different, show it
    // if vessel is the same, hide it
    if (instance.vessel_id == v.id) Close();
    else Open(v);
  }

  // styles
  GUIStyle txt_style;

  // used by scroll area
  Vector2 scroll_pos;

  // store vessel id
  Guid vessel_id;

  // permit global access
  static Notepad instance;
}


} // KERBALISM

