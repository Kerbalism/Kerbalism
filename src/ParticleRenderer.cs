using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


// a simple particle renderer
public static class ParticleRenderer
{
  // pseudo-ctor
  public static void init()
  {
    // load shader
    mat = Lib.GetShader("full_point_particle");

    // create mesh
    mesh = new Mesh();
    mesh.MarkDynamic();
  }


  // commit a particle to the renderer
  // note: this function may trigger actual rendering
  public static void commit(Vector3 p, float size, Color32 color)
  {
    points[point_index].Set(p.x, p.y, p.z);
    sizes[point_index].Set(size, 0.0f);
    colors[point_index] = color;
    indices.Add(point_index);
    ++point_index;

    if (point_index == max_particles)
    {
      // update mesh data
      mesh.vertices = points;
      mesh.uv = sizes;
      mesh.colors32 = colors;
      mesh.SetIndices(indices.ToArray(), MeshTopology.Points, 0);

      // enable material
      mat.SetPass(0);

      // render mesh
      Graphics.DrawMeshNow(mesh, Matrix4x4.identity);

      // cleanup
      indices.Clear();
      point_index = 0;
    }
  }


  // render all particles
  // note: this only render all particles not already rendered during commit
  public static void render()
  {
    if (point_index > 0)
    {
      // update mesh data
      mesh.vertices = points;
      mesh.uv = sizes;
      mesh.colors32 = colors;
      mesh.SetIndices(indices.ToArray(), MeshTopology.Points, 0);

      // enable material
      mat.SetPass(0);

      // render mesh
      Graphics.DrawMeshNow(mesh, Matrix4x4.identity);

      // cleanup
      indices.Clear();
      point_index = 0;
    }
  }



  const int max_particles = 64000;                           // max particles per-mesh
  static Vector3[] points = new Vector3[max_particles];      // mesh data: position
  static Vector2[] sizes = new Vector2[max_particles];       // mesh data: size in pixels
  static Color32[] colors = new Color32[max_particles];      // mesh data: 32bit color
  static List<int> indices = new List<int>(max_particles);   // mesh data: indices
  static Mesh mesh;                                          // mesh used as set of VBA
  static Material mat;                                       // material used
  static int point_index;                                    // index of next point in the arrays
}


} // KERBALISM