// ===================================================================================================================
// implement magnetosphere and radiation mechanics
// ===================================================================================================================


using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using UnityEngine;


namespace KERBALISM {


// store data for a radiation environment model
// and can evaluate signed distance from the inner & outer belt and the magnetopause
public sealed class RadiationModel
{
  // ctor: default
  public RadiationModel()
  {
  }

  // ctor: deserialize
  public RadiationModel(ConfigNode node)
  {
    name = Lib.ConfigValue(node, "name", "");

    has_inner = Lib.ConfigValue(node, "has_inner", false);
    inner_dist = Lib.ConfigValue(node, "inner_dist", 0.0f);
    inner_radius = Lib.ConfigValue(node, "inner_radius", 0.0f);
    inner_compression = Lib.ConfigValue(node, "inner_compression", 1.0f);
    inner_extension = Lib.ConfigValue(node, "inner_extension", 1.0f);
    inner_deform = Lib.ConfigValue(node, "inner_deform", 0.0f);
    inner_quality = Lib.ConfigValue(node, "inner_quality", 30.0f);

    has_outer = Lib.ConfigValue(node, "has_outer", false);
    outer_dist = Lib.ConfigValue(node, "outer_dist", 0.0f);
    outer_radius = Lib.ConfigValue(node, "outer_radius", 0.0f);
    outer_compression = Lib.ConfigValue(node, "outer_compression", 1.0f);
    outer_extension = Lib.ConfigValue(node, "outer_extension", 1.0f);
    outer_border_start = Lib.ConfigValue(node, "outer_border_start", 0.1f);
    outer_border_end = Lib.ConfigValue(node, "outer_border_end", 0.1f);
    outer_deform = Lib.ConfigValue(node, "outer_deform", 0.0f);
    outer_quality = Lib.ConfigValue(node, "outer_quality", 40.0f);

    has_pause = Lib.ConfigValue(node, "has_pause", false);
    pause_radius = Lib.ConfigValue(node, "pause_radius", 0.0f);
    pause_compression = Lib.ConfigValue(node, "pause_compression", 1.0f);
    pause_extension = Lib.ConfigValue(node, "pause_extension", 1.0f);
    pause_height_scale = Lib.ConfigValue(node, "pause_height_scale", 1.0f);
    pause_deform = Lib.ConfigValue(node, "pause_deform", 0.0f);
    pause_quality = Lib.ConfigValue(node, "pause_quality", 20.0f);
  }


  public float inner_func(Vector3 p)
  {
    p.x *= p.x < 0.0f ? inner_extension : inner_compression;
    float q = Mathf.Sqrt(p.x * p.x + p.z * p.z) - inner_dist;
    return Mathf.Sqrt(q * q + p.y * p.y) - inner_radius
      + (inner_deform > 0.001 ? (Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * inner_deform : 0.0f);
  }

  public Vector3 inner_domain()
  {
    float w = inner_dist + inner_radius;
    return new Vector3((w / inner_compression + w / inner_extension) * 0.5f, inner_radius, w) * (1.0f + inner_deform);
  }

  public Vector3 inner_offset()
  {
    float w = inner_dist + inner_radius;
    return new Vector3(w / inner_compression - (w / inner_compression + w / inner_extension) * 0.5f, 0.0f, 0.0f);
  }

  public float outer_func(Vector3 p)
  {
    p.x *= p.x < 0.0f ? outer_extension : outer_compression;
    float q = Mathf.Sqrt(p.x * p.x + p.z * p.z) - outer_dist;
    float k = Lib.Mix(outer_border_start, outer_border_end, Lib.Clamp(q / outer_dist, 0.0f, 1.0f));
    float d1 = Mathf.Sqrt(q * q + p.y * p.y) - outer_radius;
    float d2 = d1 + k;
    return Mathf.Max(d1, -d2) + (outer_deform > 0.001 ?(Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * outer_deform : 0.0f);
  }

  public Vector3 outer_domain()
  {
    float w = outer_dist + outer_radius;
    return new Vector3((w / outer_compression + w / outer_extension) * 0.5f, outer_radius, w) * (1.0f + outer_deform);
  }

  public Vector3 outer_offset()
  {
    float w = outer_dist + outer_radius;
    return new Vector3(w / outer_compression - (w / outer_compression + w / outer_extension) * 0.5f, 0.0f, 0.0f);
  }

  public float pause_func(Vector3 p)
  {
    p.x *= p.x < 0.0f ? pause_extension : pause_compression;
    p.y *= pause_height_scale;
    return p.magnitude - pause_radius
      + (pause_deform > 0.001 ? (Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * pause_deform : 0.0f);
  }

  public Vector3 pause_domain()
  {
    return new Vector3((pause_radius / pause_compression + pause_radius / pause_extension) * 0.5f,
      pause_radius / pause_height_scale, pause_radius) * (1.0f + pause_deform);
  }

  public Vector3 pause_offset()
  {
    return new Vector3(pause_radius / pause_compression - (pause_radius / pause_compression + pause_radius / pause_extension) * 0.5f, 0.0f, 0.0f);
  }

  public bool has_field()
  {
    return has_inner || has_outer || has_pause;
  }


  public string name;               // name of the type of radiation environment

  public bool has_inner;            // true if there is an inner radiation ring
  public float inner_dist;          // distance from inner belt center to body center
  public float inner_radius;        // radius of inner belt torus
  public float inner_compression;   // compression factor in sun-exposed side
  public float inner_extension;     // extension factor opposite to sun-exposed side
  public float inner_deform;        // size of sin deformation (scale hard-coded to [5,7,6])
  public float inner_quality;       // quality at the border

  public bool has_outer;            // true if there is an outer radiation ring
  public float outer_dist;          // distance from outer belt center to body center
  public float outer_radius;        // radius of outer belt torus
  public float outer_compression;   // compression factor in sun-exposed side
  public float outer_extension;     // extension factor opposite to sun-exposed side
  public float outer_border_start;  // thickness at zero distance
  public float outer_border_end;    // thickness at max distance
  public float outer_deform;        // size of sin deformation (scale hard-coded to [5,7,6])
  public float outer_quality;       // quality at the border

  public bool has_pause;            // true if there is a magnetopause
  public float pause_radius;        // basic radius of magnetopause
  public float pause_compression;   // compression factor in sun-exposed side
  public float pause_extension;     // extension factor opposite to sun-exposed side
  public float pause_height_scale;  // vertical compression factor
  public float pause_deform;        // size of sin deformation (scale is hardcoded as [5,7,6])
  public float pause_quality;       // quality at the border

  public ParticleMesh inner_pmesh;  // used to render the inner belt
  public ParticleMesh outer_pmesh;  // used to render the outer belt
  public ParticleMesh pause_pmesh;  // used to render the magnetopause

  // default radiation model
  public static RadiationModel none = new RadiationModel();
}


// store data about radiation for a body
public sealed class RadiationBody
{
  // ctor: default
  public RadiationBody(CelestialBody body)
  {
    this.model = RadiationModel.none;
    this.body = body;
  }

  // ctor: deserialize
  public RadiationBody(ConfigNode node, Dictionary<string, RadiationModel> models)
  {
    name = Lib.ConfigValue(node, "name", "");
    radiation_inner = Lib.ConfigValue(node, "radiation_inner", 0.0) / 3600.0;
    radiation_outer = Lib.ConfigValue(node, "radiation_outer", 0.0) / 3600.0;
    radiation_pause = Lib.ConfigValue(node, "radiation_pause", 0.0) / 3600.0;

    // get the radiation environment
    if (!models.TryGetValue(Lib.ConfigValue(node, "radiation_model", ""), out model)) model = RadiationModel.none;

    // get the body, UB if it doesn't exist
    body = FlightGlobals.Bodies.Find(k => k.name == name);
  }


  public string name;               // name of the body
  public double radiation_inner;    // rad/s inside inner belt
  public double radiation_outer;    // rad/s inside outer belt
  public double radiation_pause;    // rad/s inside magnetopause

  // shortcut to the radiation environment
  public RadiationModel model;

  // shortcut to the body, could be null
  public CelestialBody body;
}


// the radiation system
public static class Radiation
{
  // pseudo-ctor
  public static void init()
  {
    // parse RadiationModel
    var rad_nodes = Lib.ParseConfigs("RadiationModel");
    foreach(var rad_node in rad_nodes)
    {
      string name = Lib.ConfigValue(rad_node, "name", "");
      if (!models.ContainsKey(name)) models.Add(name, new RadiationModel(rad_node));
    }

    // parse RadiationBody
    var body_nodes = Lib.ParseConfigs("RadiationBody");
    foreach(var body_node in body_nodes)
    {
      string name = Lib.ConfigValue(body_node, "name", "");
      if (!bodies.ContainsKey(name)) bodies.Add(name, new RadiationBody(body_node, models));
    }

    // create body environments for all the other planets
    foreach(CelestialBody body in FlightGlobals.Bodies)
    {
      if (!bodies.ContainsKey(body.bodyName))
      {
        bodies.Add(body.bodyName, new RadiationBody(body));
      }
    }

    // remove unused models
    List<string> to_remove = new List<string>();
    foreach(var rad_pair in models)
    {
      bool used = false;
      foreach(var body_pair in bodies)
      {
        if (body_pair.Value.model == rad_pair.Value) { used = true; break; }
      }
      if (!used) to_remove.Add(rad_pair.Key);
    }
    foreach(string s in to_remove) models.Remove(s);

    // start particle-fitting thread
    preprocess_thread = new Thread(preprocess);
    preprocess_thread.Name = "particle-fitting";
    preprocess_thread.IsBackground = true;
    preprocess_thread.Start();
  }


  // do the particle-fitting in another thread
  public static void preprocess()
  {
    // deduce number of particles
    int inner_count = 150000;
    int outer_count = 600000;
    int pause_count = 250000;
    if (Settings.LowQualityFieldRendering)
    {
      inner_count /= 5;
      outer_count /= 5;
      pause_count /= 5;
    }

    // start time
    UInt64 time = Lib.Clocks();

    // create all magnetic fields and do particle-fitting
    List<string> done = new List<string>();
    foreach(var pair in models)
    {
      // get radiation data
      RadiationModel mf = pair.Value;

      // skip if type already done
      if (done.Contains(mf.name)) continue;

      // add to the skip list
      done.Add(mf.name);

      // if it has a field
      if (mf.has_field())
      {
        // some feedback in the log
        Lib.Log(Lib.BuildString("particle-fitting '", mf.name, "'..."));
      }

      // particle-fitting for the inner radiation belt
      if (mf.has_inner)
      {
        mf.inner_pmesh = new ParticleMesh(mf.inner_func, mf.inner_domain(), mf.inner_offset(), inner_count, mf.inner_quality);
      }

      // particle-fitting for the outer radiation belt
      if (mf.has_outer)
      {
        mf.outer_pmesh = new ParticleMesh(mf.outer_func, mf.outer_domain(), mf.outer_offset(), outer_count, mf.outer_quality);
      }

      // particle-fitting for the magnetopause
      if (mf.has_pause)
      {
        mf.pause_pmesh = new ParticleMesh(mf.pause_func, mf.pause_domain(), mf.pause_offset(), pause_count, mf.pause_quality);
      }
    }

    // measure time required
    Lib.Log(Lib.BuildString("particle-fitting completed in ", Lib.Seconds(Lib.Clocks() - time).ToString("F3"), " seconds"));
  }


  // generate GSM-space coordinate
  // note: we use the rotation axis as magnetic axis
  // note: we let the basis became not orthonormal when the sun direction
  // is not perpendicular with the magnetic axis, to get a nice deformation effect
  public static Space gsm_space(CelestialBody body)
  {
    Space gsm;
    gsm.origin = ScaledSpace.LocalToScaledSpace(body.position);
    gsm.scale = ScaledSpace.InverseScaleFactor * (float)body.Radius;
    gsm.x_axis = body.flightGlobalsIndex > 0
      ?((Vector3)ScaledSpace.LocalToScaledSpace(FlightGlobals.Bodies[0].position) - gsm.origin).normalized
      : new Vector3(1.0f, 0.0f, 0.0f); //< galactic rotation
    gsm.y_axis = body.flightGlobalsIndex > 0
      ? (Vector3)body.RotationAxis
      : new Vector3(0.0f, 1.0f, 0.0f);
    gsm.z_axis = body.flightGlobalsIndex > 0
      ? Vector3.Cross(gsm.x_axis, gsm.y_axis).normalized
      : new Vector3(0.0f, 0.0f, 1.0f);
    return gsm;
  }


  // render the fields of the active body
  public static void render()
  {
    // get target body
    CelestialBody body = target_body();

    // maintain visualization modes
    if (body == null)
    {
      show_inner = false;
      show_outer = false;
      show_pause = false;
    }
    else
    {
      if (Input.GetKeyDown(KeyCode.Keypad0))
      {
        if (show_inner || show_outer || show_pause)
        {
          show_inner = false;
          show_outer = false;
          show_pause = false;
        }
        else
        {
          show_inner = true;
          show_outer = true;
          show_pause = true;
        }
      }
      if (Input.GetKeyDown(KeyCode.Keypad1))
      {
        show_inner = true;
        show_outer = false;
        show_pause = false;
      }
      if (Input.GetKeyDown(KeyCode.Keypad2))
      {
        show_inner = false;
        show_outer = true;
        show_pause = false;
      }
      if (Input.GetKeyDown(KeyCode.Keypad3))
      {
        show_inner = false;
        show_outer = false;
        show_pause = true;
      }
    }


    // if there is an active body, and at least one of the modes is active
    if (body != null && (show_inner || show_outer || show_pause))
    {
      // if we don't know if preprocessing is completed
      if (preprocess_thread != null)
      {
        // if the preprocess thread has not done yet
        if (preprocess_thread.IsAlive)
        {
          // disable all modes
          show_inner = false;
          show_outer = false;
          show_pause = false;

          // tell the user and do nothing
          Message.Post("<color=cyan><b>Fitting particles to signed distance fields</b></color>", "Come back in a minute");
          return;
        }

        // wait for particle-fitting thread to cleanup
        preprocess_thread.Join();

        // preprocessing is complete
        preprocess_thread = null;
      }


      // load and configure shader
      if (mat == null)
      {
        if (!Settings.LowQualityFieldRendering)
        {
          // load shader
          mat = Lib.GetShader("mini_particle");

          // configure shader
          mat.SetColor("POINT_COLOR", new Color(0.33f, 0.33f, 0.33f, 0.1f));
        }
        else
        {
          // load shader
          mat = Lib.GetShader("point_particle");

          // configure shader
          mat.SetColor("POINT_COLOR", new Color(0.33f, 0.33f, 0.33f, 0.1f));
          mat.SetFloat("POINT_SIZE", 4.0f);
        }
      }


      // enable material
      mat.SetPass(0);

      // generate radii-normalized GMS space, then convert to matrix
      Matrix4x4 m = gsm_space(body).look_at();

      // [debug] show axis
      //MapRenderer.commit_line(gsm.origin, gsm.origin + gsm.x_axis * gsm.scale * 5.0f, Color.red);
      //MapRenderer.commit_line(gsm.origin, gsm.origin + gsm.y_axis * gsm.scale * 5.0f, Color.green);
      //MapRenderer.commit_line(gsm.origin, gsm.origin + gsm.z_axis * gsm.scale * 5.0f, Color.blue);

      // get magnetic field data
      RadiationModel mf = Info(body).model;

      // render active body fields
      if (show_inner && mf.has_inner) mf.inner_pmesh.render(m);
      if (show_outer && mf.has_outer) mf.outer_pmesh.render(m);
      if (show_pause && mf.has_pause) mf.pause_pmesh.render(m);
    }
  }


  public static RadiationBody Info(CelestialBody body)
  {
    RadiationBody rb;
    return bodies.TryGetValue(body.bodyName, out rb) ? rb : null; //< this should never happen
  }


  // return the total environent radiation at position specified
  public static double Compute(Vessel v, double gamma_transparency, out bool blackout, out int inner_body, out int outer_body, out int pause_body)
  {
    blackout = false;
    inner_body = 0;
    outer_body = 0;
    pause_body = 0;

    double radiation = Settings.InterstellarRadiation; // radiation outside all fields
    CelestialBody body = v.mainBody;
    while(body != null)
    {
      RadiationBody rb = Info(body);
      RadiationModel mf = rb.model;
      if (mf.has_field())
      {
        // generate radii-normalized GSM space
        Space gsm = gsm_space(rb.body);

        // move the poing in GSM space
        Vector3 p = gsm.transform_in(ScaledSpace.LocalToScaledSpace(v.GetWorldPos3D()));

        // determine if inside zones
        bool inside_inner = mf.has_inner && mf.inner_func(p) < 0.0f;
        bool inside_outer = mf.has_outer && mf.outer_func(p) < 0.0f;
        bool inside_pause = mf.has_pause && mf.pause_func(p) < 0.0f;

        // remember body of last zone
        // note: we ignore the sun, on purpose, by having the 'null' index being 0
        if (inside_inner) inner_body = rb.body.flightGlobalsIndex;
        if (inside_outer) outer_body = rb.body.flightGlobalsIndex;
        if (inside_pause) pause_body = rb.body.flightGlobalsIndex;

        // return radiation if inside a zone
        if (inside_inner)      { radiation = rb.radiation_inner; break; }
        else if (inside_outer) { radiation = rb.radiation_outer; break; }
        else if (inside_pause) { radiation = rb.radiation_pause; break; }
      }

      // avoid loops in the chain
      body = (body.referenceBody != null && body.referenceBody.referenceBody == body) ? null : body.referenceBody;
    }

    bool stormed = Storm.InProgress(v);
    if (stormed && pause_body > 0) blackout = true;
    if (stormed && pause_body == 0) radiation += Settings.StormRadiation;
    return radiation * gamma_transparency;
  }


  // return percentual of radiations blocked by shielding
  public static double Shielding(double level)
  {
    return level * Settings.ShieldingEfficiency;
  }


  // return percentual of radiations blocked by shielding
  public static double Shielding(double amount, double capacity)
  {
    return capacity > double.Epsilon ? Settings.ShieldingEfficiency * amount / capacity : 0.0;
  }


  // return percentage of radiations blocked by shielding
  public static double Shielding(Vessel v)
  {
    return Shielding(ResourceCache.Info(v, "Shielding").level);
  }


  // return percentage of radiations blocked by shielding
  public static double Shielding(ConnectedLivingSpace.ICLSSpace space)
  {
    double amount = 0.0;
    double capacity = 0.0;
    foreach(var part in space.Parts)
    {
      amount += Lib.Amount(part.Part, "Shielding");
      capacity += Lib.Capacity(part.Part, "Shielding");
    }
    double level = capacity > double.Epsilon ? amount / capacity : 0.0;
    return Shielding(level);
  }


  // return a verbose description of shielding capability
  // - shielding_factor: you can use Shielding level here
  public static string ShieldingToString(double shielding_factor)
  {
    if (shielding_factor <= double.Epsilon) return "none";
    if (shielding_factor <= 0.25) return "poor";
    if (shielding_factor <= 0.50) return "moderate";
    if (shielding_factor <= 0.75) return "decent";
    return "hardened";
  }


  // show warning message when a vessel crossing a radiation belt
  public static void beltWarnings(Vessel v, vessel_info vi, vessel_data vd)
  {
    // do nothing without a radiation rule
    if (Kerbalism.rad_rule == null) return;

    // we only show it for manned vessels, but the first time we also show it for probes
    if (vi.crew_count > 0 || DB.NotificationData().first_belt_crossing == 0)
    {
      bool inside_belt = vi.inner_body > 0 || vi.outer_body > 0;
      if (inside_belt && vd.msg_belt < 1)
      {
        Message.Post(Lib.BuildString("<b>", v.vesselName, "</b> is crossing <i>", v.mainBody.bodyName, " radiation belt</i>"), "Exposed to extreme radiation");
        vd.msg_belt = 1;
        DB.NotificationData().first_belt_crossing = 1; //< record first belt crossing
      }
      else if (!inside_belt && vd.msg_belt > 0)
      {
        // no message after crossing the belt
        vd.msg_belt = 0;
      }
    }
  }


  // deduce first interesting body for radiation in the body chain
  static CelestialBody target_body(CelestialBody body)
  {
    if (Info(body).model.has_field()) return body;  // main body has field
    else if (body.referenceBody != null             // it has a ref body
      && body.referenceBody.referenceBody != body)  // avoid loops in planet setup (eg: OPM)
      return target_body(body.referenceBody);       // recursively
    else return null;                               // nothing in chain
  }
  public static CelestialBody target_body()
  {
    var target = PlanetariumCamera.fetch.target;
    return
        target == null
      ? null
      : target.celestialBody != null
      ? target_body(target.celestialBody)
      : target.vessel != null
      ? target_body(target.vessel.mainBody)
      : null;
  }


  static Dictionary<string, RadiationModel> models = new Dictionary<string, RadiationModel>(16);
  static Dictionary<string, RadiationBody> bodies = new Dictionary<string, RadiationBody>(32);

  // thread used to do the particle-fitting
  static Thread preprocess_thread;

  // material used to render the fields
  static Material mat;

  // current visualization modes
  public static bool show_inner;
  public static bool show_outer;
  public static bool show_pause;
}


} // KERBALISM
