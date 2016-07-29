using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM {


public sealed class MapRenderer : MonoBehaviour
{
  public MapRenderer()
  {
    // enable global access
    instance = this;

    // load the material
    mat = new Material(Shader.Find("Particles/Additive (Soft)"));
  }

  void OnPostRender()
  {
    // note: do the transformation here in PostRender, to avoid flickering when camera move

    // note: we tried using a textured quad (both mapview orbit material and custom one) to do
    // some antialiasing, but there is some weird perspective-correction going on (that shouldn't be there)
    // this problem isn't present in ortho projection of course, but then the celestial bodies do not occlude the quads

    // avoid 1-frame line rendering in flight view
    if (MapView.MapIsEnabled)
    {
      // store stuff
      Vector3 a;
      Vector3 b;
      Vector3 screen_a;
      Vector3 screen_b;
      Vector3 p;
      Vector2 perp;
      Vector3 v;

      // get camera
      var cam = PlanetariumCamera.Camera;

      // enable the material
      mat.SetPass(0);

      // start rendering lines
      GL.Begin(GL.LINES);

      // for each line we got
      foreach(line_data line in lines)
      {
        // transform to scaled space (the planetarium camera is in scaled space),
        a = ScaledSpace.LocalToScaledSpace(line.a);
        b = ScaledSpace.LocalToScaledSpace(line.b);

        // transform into screen space (non-normalized)
        screen_a = cam.WorldToScreenPoint(a);
        screen_b = cam.WorldToScreenPoint(b);

        // calculate perpendicular vector in screen space
        perp = screen_b - screen_a;
        perp = new Vector2(-perp.y, perp.x).normalized;

        // set the color
        line.color.a *= 0.666f;
        GL.Color(line.color);

        // commit the central line
        GL.Vertex3(a.x, a.y, a.z);
        GL.Vertex3(b.x, b.y, b.z);

        // antialiasing
        for(int i=0; i<3; ++i)
        {
          v = perp * (float)(i + 1) * 0.7071f * 0.5f;
          line.color.a *= 0.666f;
          GL.Color(line.color);
          p = cam.ScreenToWorldPoint(new Vector3(screen_a.x - v.x, screen_a.y - v.y, screen_a.z));
          GL.Vertex3(p.x, p.y, p.z);
          p = cam.ScreenToWorldPoint(new Vector3(screen_b.x - v.x, screen_b.y - v.y, screen_b.z));
          GL.Vertex3(p.x, p.y, p.z);
          p = cam.ScreenToWorldPoint(new Vector3(screen_a.x + v.x, screen_a.y + v.y, screen_a.z));
          GL.Vertex3(p.x, p.y, p.z);
          p = cam.ScreenToWorldPoint(new Vector3(screen_b.x + v.x, screen_b.y + v.y, screen_b.z));
          GL.Vertex3(p.x, p.y, p.z);
        }
      }

      // stop rendering lines
      GL.End();
    }

    // remove all committed lines
    lines.Clear();
  }

  // commit a line
  public static void commit(Vector3d a, Vector3d b, Color color)
  {
    // don't commit anything if the renderer isn't ready
    if (instance == null) return;

    // create a new line
    line_data line = new line_data();
    line.a = a;
    line.b = b;
    line.color = color;

    // commit it
    instance.lines.Add(line);
  }


  // store a committed line
  class line_data
  {
    public Vector3d a;
    public Vector3d b;
    public Color color;
  };


  List<line_data> lines = new List<line_data>(32);  // set of committed lines
  Material mat;                                     // the material used
  static MapRenderer instance;                      // permit global access
}





} // KERBALISM