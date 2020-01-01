using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using UnityEngine;


namespace KERBALISM
{
    // store data for a radiation environment model
    // and can evaluate signed distance from the inner & outer belt and the magnetopause
    public class RadiationModel
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
            inner_deform_xy = Lib.ConfigValue(node, "inner_deform_xy", 1.0f);
            inner_compression = Lib.ConfigValue(node, "inner_compression", 1.0f);
            inner_extension = Lib.ConfigValue(node, "inner_extension", 1.0f);
            inner_border_dist = Lib.ConfigValue(node, "inner_border_dist", 0.0001f);
            inner_border_radius = Lib.ConfigValue(node, "inner_border_radius", 0.0f);
            inner_border_deform_xy = Lib.ConfigValue(node, "inner_border_deform_xy", 1.0f);
            inner_deform = Lib.ConfigValue(node, "inner_deform", 0.0f);
            inner_quality = Lib.ConfigValue(node, "inner_quality", 30.0f);

            has_outer = Lib.ConfigValue(node, "has_outer", false);
            outer_dist = Lib.ConfigValue(node, "outer_dist", 0.0f);
            outer_radius = Lib.ConfigValue(node, "outer_radius", 0.0f);
            outer_deform_xy = Lib.ConfigValue(node, "outer_deform_xy", 1.0f);
            outer_compression = Lib.ConfigValue(node, "outer_compression", 1.0f);
            outer_extension = Lib.ConfigValue(node, "outer_extension", 1.0f);
            outer_border_dist = Lib.ConfigValue(node, "outer_border_dist", 0.001f);
            outer_border_radius = Lib.ConfigValue(node, "outer_border_radius", 0.0f);
            outer_border_deform_xy = Lib.ConfigValue(node, "outer_border_deform_xy", 1.0f);
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


        public float Inner_func(Vector3 p)
        {
            p.x *= p.x < 0.0f ? inner_extension : inner_compression;
            float q1 = Mathf.Sqrt((p.x * p.x + p.z * p.z) * inner_deform_xy) - inner_dist;
            float d1 = Mathf.Sqrt(q1 * q1 + p.y * p.y) - inner_radius;
            float q2 = Mathf.Sqrt((p.x * p.x + p.z * p.z) * inner_border_deform_xy) - inner_border_dist;
            float d2 = Mathf.Sqrt(q2 * q2 + p.y * p.y) - inner_border_radius;
            return Mathf.Max(d1, -d2) + (inner_deform > 0.001 ? (Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * inner_deform : 0.0f);
        }

        public Vector3 Inner_domain()
        {
            float p = Mathf.Max((inner_dist + inner_radius), (inner_border_dist + inner_border_radius));
            float w = p * Mathf.Sqrt(1 / Mathf.Min(inner_deform_xy, inner_border_deform_xy));
            return new Vector3((w / inner_compression + w / inner_extension) * 0.5f, Mathf.Max(inner_radius, inner_border_radius), w) * (1.0f + inner_deform);
        }

        public Vector3 Inner_offset()
        {
            float p = Mathf.Max((inner_dist + inner_radius), (inner_border_dist + inner_border_radius));
            float w = p * Mathf.Sqrt(1 / Mathf.Min(inner_deform_xy, inner_border_deform_xy));
            return new Vector3(w / inner_compression - (w / inner_compression + w / inner_extension) * 0.5f, 0.0f, 0.0f);
        }

        public float Outer_func(Vector3 p)
        {
            p.x *= p.x < 0.0f ? outer_extension : outer_compression;
            float q1 = Mathf.Sqrt((p.x * p.x + p.z * p.z) * outer_deform_xy) - outer_dist;
            float d1 = Mathf.Sqrt(q1 * q1 + p.y * p.y) - outer_radius;
            float q2 = Mathf.Sqrt((p.x * p.x + p.z * p.z) * outer_border_deform_xy) - outer_border_dist;
            float d2 = Mathf.Sqrt(q2 * q2 + p.y * p.y) - outer_border_radius;
            return Mathf.Max(d1, -d2) + (outer_deform > 0.001 ? (Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * outer_deform : 0.0f);
        }

        public Vector3 Outer_domain()
        {
            float p = Mathf.Max((outer_dist + outer_radius), (outer_border_dist + outer_border_radius));
            float w = p * Mathf.Sqrt(1 / Mathf.Min(outer_deform_xy, outer_border_deform_xy));
            return new Vector3((w / outer_compression + w / outer_extension) * 0.5f, Mathf.Max(outer_radius, outer_border_radius), w) * (1.0f + outer_deform);
        }

        public Vector3 Outer_offset()
        {
            float p = Mathf.Max((outer_dist + outer_radius), (outer_border_dist + outer_border_radius));
            float w = p * Mathf.Sqrt(1 / Mathf.Min(outer_deform_xy, outer_border_deform_xy));
            return new Vector3(w / outer_compression - (w / outer_compression + w / outer_extension) * 0.5f, 0.0f, 0.0f);
        }

        public float Pause_func(Vector3 p)
        {
            p.x *= p.x < 0.0f ? pause_extension : pause_compression;
            p.y *= pause_height_scale;
            return p.magnitude - pause_radius
              + (pause_deform > 0.001 ? (Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * pause_deform : 0.0f);
        }

        public Vector3 Pause_domain()
        {
            return new Vector3((pause_radius / pause_compression + pause_radius / pause_extension) * 0.5f,
              pause_radius / pause_height_scale, pause_radius) * (1.0f + pause_deform);
        }

        public Vector3 Pause_offset()
        {
            return new Vector3(pause_radius / pause_compression - (pause_radius / pause_compression + pause_radius / pause_extension) * 0.5f, 0.0f, 0.0f);
        }

        public bool Has_field()
        {
            return has_inner || has_outer || has_pause;
        }


        public string name;                     // name of the type of radiation environment

        public bool has_inner;                  // true if there is an inner radiation ring
        public float inner_dist;                // distance from inner belt center to body center
        public float inner_radius;              // radius of inner belt torus
        public float inner_deform_xy;           // wanted (high / diameter) ^ 2
        public float inner_compression;         // compression factor in sun-exposed side
        public float inner_extension;           // extension factor opposite to sun-exposed side
        public float inner_border_dist;         // center of the inner torus we substract
        public float inner_border_radius;       // radius of the inner torus we substract
        public float inner_border_deform_xy;    // wanted (high / diameter) ^ 2
        public float inner_deform;              // size of sin deformation (scale hard-coded to [5,7,6])
        public float inner_quality;             // quality at the border

        public bool has_outer;                  // true if there is an outer radiation ring
        public float outer_dist;                // distance from outer belt center to body center
        public float outer_radius;              // radius of outer belt torus
        public float outer_deform_xy;           // wanted (high / diameter) ^ 2
        public float outer_compression;         // compression factor in sun-exposed side
        public float outer_extension;           // extension factor opposite to sun-exposed side
        public float outer_border_dist;         // center of the outer torus we substract
        public float outer_border_radius;       // radius of the outer torus we substract
        public float outer_border_deform_xy;    // wanted (high / diameter) ^ 2
        public float outer_deform;              // size of sin deformation (scale hard-coded to [5,7,6])
        public float outer_quality;             // quality at the border

        public bool has_pause;                  // true if there is a magnetopause
        public float pause_radius;              // basic radius of magnetopause
        public float pause_compression;         // compression factor in sun-exposed side
        public float pause_extension;           // extension factor opposite to sun-exposed side
        public float pause_height_scale;        // vertical compression factor
        public float pause_deform;              // size of sin deformation (scale is hardcoded as [5,7,6])
        public float pause_quality;             // quality at the border

        public ParticleMesh inner_pmesh;        // used to render the inner belt
        public ParticleMesh outer_pmesh;        // used to render the outer belt
        public ParticleMesh pause_pmesh;        // used to render the magnetopause

        // default radiation model
        public static RadiationModel none = new RadiationModel();
    }


    // store data about radiation for a body
    public class RadiationBody
    {
        // ctor: default
        public RadiationBody(CelestialBody body)
        {
            this.model = RadiationModel.none;
            this.body = body;
            this.reference = 0;
            this.geomagnetic_pole = new Vector3(0.0f, 1.0f, 0.0f);
        }

        // ctor: deserialize
        public RadiationBody(ConfigNode node, Dictionary<string, RadiationModel> models, CelestialBody body)
        {
            name = Lib.ConfigValue(node, "name", "");
            radiation_inner = Lib.ConfigValue(node, "radiation_inner", 0.0) / 3600.0;
            radiation_inner_gradient = Lib.ConfigValue(node, "radiation_inner_gradient", 3.3);
            radiation_outer = Lib.ConfigValue(node, "radiation_outer", 0.0) / 3600.0;
            radiation_outer_gradient = Lib.ConfigValue(node, "radiation_outer_gradient", 2.2);
            radiation_pause = Lib.ConfigValue(node, "radiation_pause", 0.0) / 3600.0;
            radiation_surface = Lib.ConfigValue(node, "radiation_surface", -1.0) / 3600.0;
            solar_cycle = Lib.ConfigValue(node, "solar_cycle", -1.0);
            solar_cycle_offset = Lib.ConfigValue(node, "solar_cycle_offset", 0.0);
            geomagnetic_pole_lat = Lib.ConfigValue(node, "geomagnetic_pole_lat", 90.0f);
            geomagnetic_pole_lon = Lib.ConfigValue(node, "geomagnetic_pole_lon", 0.0f);
            geomagnetic_offset = Lib.ConfigValue(node, "geomagnetic_offset", 0.0f);
            reference = Lib.ConfigValue(node, "reference", Lib.GetParentSun(body).flightGlobalsIndex);

            // get the radiation environment
            if (!models.TryGetValue(Lib.ConfigValue(node, "radiation_model", ""), out model)) model = RadiationModel.none;

            // get the body
            this.body = body;

            float lat = (float)(geomagnetic_pole_lat * Math.PI / 180.0);
            float lon = (float)(geomagnetic_pole_lon * Math.PI / 180.0);

            float x = Mathf.Cos(lat) * Mathf.Cos(lon);
            float y = Mathf.Sin(lat);
            float z = Mathf.Cos(lat) * Mathf.Sin(lon);
            geomagnetic_pole = new Vector3(x, y, z).normalized;

            if (Lib.IsSun(body))
            {
                // suns without a solar cycle configuration default to a cycle of 6 years
                // (set to 0 if you really want none)
                if (solar_cycle < 0)
                    solar_cycle = Lib.HoursInDay * 3600 * Lib.DaysInYear * 6;

                // add a rather nominal surface radiation for suns that have no config
                // for comparison: the stock kerbin sun has a surface radiation of 47 rad/h, which gives 0.01 rad/h near Kerbin
                // (set to 0 if you really want none)
                if (radiation_surface < 0)
                    radiation_surface = 10.0 * 3600.0;
            }

            // calculate point emitter strength r0 at center of body
            if (radiation_surface > 0)
                radiation_r0 = radiation_surface * 4 * Math.PI * body.Radius * body.Radius;
        }

        public string name;            // name of the body
        public double radiation_inner; // rad/h inside inner belt
        public double radiation_inner_gradient; // how quickly the radiation rises as you go deeper into the belt
        public double radiation_outer; // rad/h inside outer belt
        public double radiation_outer_gradient; // how quickly the radiation rises as you go deeper into the belt
        public double radiation_pause; // rad/h inside magnetopause
        public double radiation_surface; // rad/h of gamma radiation on the surface
        public double radiation_r0 = 0.0; // rad/h of gamma radiation at the center of the body (calculated from radiation_surface)
        public double solar_cycle;     // interval time of solar activity (11 years for sun)
        public double solar_cycle_offset; // time to add to the universal time when calculating the cycle, used to have cycles that don't start at 0
        public int reference;          // index of the body that determine x-axis of the gsm-space
        public float geomagnetic_pole_lat = 90.0f;
        public float geomagnetic_pole_lon = 0.0f;
        public float geomagnetic_offset = 0.0f;
        public Vector3 geomagnetic_pole;

        public bool inner_visible = true;
        public bool outer_visible = true;
        public bool pause_visible = true;

        // shortcut to the radiation environment
        public RadiationModel model;

        // shortcut to the body
        public CelestialBody body;

        /// <summary> Returns the magnetopause radiation level, accounting for solar activity cycles </summary>
        public double RadiationPause()
        {
            if (solar_cycle > 0)
            {
                return radiation_pause + radiation_pause * 0.2 * SolarActivity();
            }
            return radiation_pause;
        }

        /// <summary> Return a number [-0.15 .. 1.05] that represents current solar activity, clamped to [0 .. 1] if clamp is true </summary>
        public double SolarActivity(bool clamp = true)
        {
            if (solar_cycle <= 0) return 0;

            var t = (solar_cycle_offset + Planetarium.GetUniversalTime()) / solar_cycle * 2 * Math.PI; // Math.Sin/Cos works with radians

            // this gives a pseudo-erratic curve, see https://www.desmos.com/calculator/q5flvzvxia
            // in range -0.15 .. 1.05
            var r = (-Math.Cos(t) + Math.Sin(t * 75) / 5 + 0.9) / 2.0;
            if (clamp) r = Lib.Clamp(r, 0.0, 1.0);
            return r;
        }
    }

    // the radiation system
    public static class Radiation
    {
        // pseudo-ctor
        public static void Init()
        {
            // if radiation is disabled
            if (!Features.Radiation)
            {
                // create default environments for all planets
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    bodies.Add(body.bodyName, new RadiationBody(body));
                }

                // and do nothing else
                return;
            }

            // parse RadiationModel
            var rad_nodes = Lib.ParseConfigs("RadiationModel");
            foreach (var rad_node in rad_nodes)
            {
                string name = Lib.ConfigValue(rad_node, "name", "");
                if (!models.ContainsKey(name)) models.Add(name, new RadiationModel(rad_node));
            }

            // parse RadiationBody
            var body_nodes = Lib.ParseConfigs("RadiationBody");
            foreach (var body_node in body_nodes)
            {
                string name = Lib.ConfigValue(body_node, "name", "");
                if (!bodies.ContainsKey(name))
                {
                    CelestialBody body = FlightGlobals.Bodies.Find(k => k.bodyName == name);
                    if (body == null) continue; // skip non-existing bodies
                    bodies.Add(name, new RadiationBody(body_node, models, body));
                }
            }

            // create body environments for all the other planets
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (!bodies.ContainsKey(body.bodyName))
                {
                    bodies.Add(body.bodyName, new RadiationBody(body));
                }
            }

            // remove unused models
            List<string> to_remove = new List<string>();
            foreach (var rad_pair in models)
            {
                bool used = false;
                foreach (var body_pair in bodies)
                {
                    if (body_pair.Value.model == rad_pair.Value) { used = true; break; }
                }
                if (!used) to_remove.Add(rad_pair.Key);
            }
            foreach (string s in to_remove) models.Remove(s);

            // start particle-fitting thread
            preprocess_thread = new Thread(Preprocess)
            {
                Name = "particle-fitting",
                IsBackground = true
            };
            preprocess_thread.Start();
        }


        // do the particle-fitting in another thread
        public static void Preprocess()
        {
            // deduce number of particles
            int inner_count = 150000;
            int outer_count = 600000;
            int pause_count = 250000;
            if (Settings.LowQualityRendering)
            {
                inner_count /= 5;
                outer_count /= 5;
                pause_count /= 5;
            }

            // start time
            UInt64 time = Lib.Clocks();

            // create all magnetic fields and do particle-fitting
            List<string> done = new List<string>();
            foreach (var pair in models)
            {
                // get radiation data
                RadiationModel mf = pair.Value;

                // skip if model is already done
                if (done.Contains(mf.name)) continue;

                // add to the skip list
                done.Add(mf.name);

                // if it has a field
                if (mf.Has_field())
                {
                    // some feedback in the log
                    // note: can't use BuildString here, as it is not thread-safe
                    Lib.Log("particle-fitting '" + mf.name + "'...");
                }

                // particle-fitting for the inner radiation belt
                if (mf.has_inner)
                {
                    mf.inner_pmesh = new ParticleMesh(mf.Inner_func, mf.Inner_domain(), mf.Inner_offset(), inner_count, mf.inner_quality);
                }

                // particle-fitting for the outer radiation belt
                if (mf.has_outer)
                {
                    mf.outer_pmesh = new ParticleMesh(mf.Outer_func, mf.Outer_domain(), mf.Outer_offset(), outer_count, mf.outer_quality);
                }

                // particle-fitting for the magnetopause
                if (mf.has_pause)
                {
                    mf.pause_pmesh = new ParticleMesh(mf.Pause_func, mf.Pause_domain(), mf.Pause_offset(), pause_count, mf.pause_quality);
                }
            }

            // measure time required
            // note: can't use BuildString here, as it is not thread-safe
            Lib.Log("particle-fitting completed in " + Lib.Seconds(Lib.Clocks() - time).ToString("F3") + " seconds");
        }


        // generate gsm-space frame of reference
        // - origin is at body position
        // - the x-axis point to reference body
        // - the rotation axis is used as y-axis initial guess
        // - the space is then orthonormalized
        // - if the reference body is the same as the body,
        //   the galactic rotation vector is used as x-axis instead
        public static Space Gsm_space(RadiationBody rb, bool tilted)
        {
            CelestialBody body = rb.body;
            CelestialBody reference = FlightGlobals.Bodies[rb.reference];

            Space gsm;
            gsm.origin = ScaledSpace.LocalToScaledSpace(body.position);
            gsm.scale = ScaledSpace.InverseScaleFactor * (float)body.Radius;
            if (body != reference)
            {
                gsm.x_axis = ((Vector3)ScaledSpace.LocalToScaledSpace(reference.position) - gsm.origin).normalized;
                if (!tilted)
                {
                    gsm.y_axis = body.RotationAxis; //< initial guess
                    gsm.z_axis = Vector3.Cross(gsm.x_axis, gsm.y_axis).normalized;
                    gsm.y_axis = Vector3.Cross(gsm.z_axis, gsm.x_axis).normalized; //< orthonormalize
                }
                else
                {
                    /* "Do not try and tilt the planet, that's impossible.
					 * Instead, only try to realize the truth...there is no tilt.
					 * Then you'll see that it is not the planet that tilts, it is
					 * the rest of the universe."
					 * 
					 * - The Matrix
					 * 
					 * 		 
					 * the orbits are inclined (with respect to the equator of the
					 * Earth), but all axes are parallel. and aligned with the unity
					 * world z axis. or is it y? whatever, KSP uses two conventions
					 * in different places.
					 * if you use Principia, the current main body (or if there is
					 * none, e.g. in the space centre or tracking station, the home
					 * body) is not tilted (its axis is the unity vertical.	 
					 * you can fetch the full orientation (tilt and rotation) of any
					 * body (including the current main body) in the current unity
					 * frame (which changes of course, because sometimes KSP uses a
					 * rotating frame, and because Principia tilts the universe
					 * differently if the current main body changes) as the
					 * orientation of the scaled space body
					 * 		 
					 * body.scaledBody.transform.rotation or something along those lines
					 * 
					 * - egg
					 */

                    Vector3 pole = rb.geomagnetic_pole;
                    Quaternion rotation = body.scaledBody.transform.rotation;
                    gsm.y_axis = (rotation * pole).normalized;

                    gsm.z_axis = Vector3.Cross(gsm.x_axis, gsm.y_axis).normalized;
                    gsm.x_axis = Vector3.Cross(gsm.y_axis, gsm.z_axis).normalized; //< orthonormalize
                }
            }
            else
            {
                // galactic
                gsm.x_axis = new Vector3(1.0f, 0.0f, 0.0f);
                gsm.y_axis = new Vector3(0.0f, 1.0f, 0.0f);
                gsm.z_axis = new Vector3(0.0f, 0.0f, 1.0f);
            }

            gsm.origin = gsm.origin + gsm.y_axis * (gsm.scale * rb.geomagnetic_offset);

            return gsm;
        }


        // render the fields of the interesting body
        public static void Render()
        {
            // get interesting body
            CelestialBody body = Interesting_body();

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
                        Message.Post("<color=#00ffff><b>Fitting particles to signed distance fields</b></color>", "Come back in a minute");
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
                    if (!Settings.LowQualityRendering)
                    {
                        // load shader
                        mat = Lib.GetShader("MiniParticle");

                        // configure shader
                        mat.SetColor("POINT_COLOR", new Color(0.33f, 0.33f, 0.33f, 0.1f));
                    }
                    else
                    {
                        // load shader
                        mat = Lib.GetShader("PointParticle");

                        // configure shader
                        mat.SetColor("POINT_COLOR", new Color(0.33f, 0.33f, 0.33f, 0.1f));
                        mat.SetFloat("POINT_SIZE", 4.0f);
                    }
                }

                // generate radii-normalized GMS space
                RadiationBody rb = Info(body);
                Space gsm_tilted = Gsm_space(rb, true);

                // [debug] show axis
                //LineRenderer.Commit(gsm_tilted.origin, gsm_tilted.origin + gsm_tilted.x_axis * gsm_tilted.scale * 5.0f, Color.red);
                //LineRenderer.Commit(gsm_tilted.origin, gsm_tilted.origin + gsm_tilted.y_axis * gsm_tilted.scale * 5.0f, Color.green);
                //LineRenderer.Commit(gsm_tilted.origin, gsm_tilted.origin + gsm_tilted.z_axis * gsm_tilted.scale * 5.0f, Color.blue);

                // enable material
                mat.SetPass(0);

                // get magnetic field data
                RadiationModel mf = rb.model;

                // render active body fields
                Matrix4x4 m_tilted = gsm_tilted.Look_at();
                if (show_inner && mf.has_inner && rb.inner_visible) mf.inner_pmesh.Render(m_tilted);
                if (show_outer && mf.has_outer && rb.outer_visible) mf.outer_pmesh.Render(m_tilted);
                if (show_pause && mf.has_pause && rb.pause_visible) mf.pause_pmesh.Render(Gsm_space(rb, false).Look_at());
            }
        }

        public static RadiationBody Info(CelestialBody body)
        {
            RadiationBody rb;
            return bodies.TryGetValue(body.bodyName, out rb) ? rb : null; //< this should never happen
        }

        /// <summary> Calculate radiation at a given distance to an emitter by inverse square law </summary>
        public static double DistanceRadiation(double radiation, double distance)
        {
            // result = radiation / (4 * Pi * r^2)
            return radiation / Math.Max(1.0, 4 * Math.PI * distance * distance);
        }

        /// <summary> Returns the radiation emitted by the body at the center, adjusted by solar activity cycle </summary>
        private static double RadiationR0(RadiationBody rb)
        {
            // for easier configuration, the radiation model sets the radiation on the surface of the body.
            // from there, it decreases according to the inverse square law with distance from the surface.

            var r0 = rb.radiation_r0; // precomputed

            // if there is a solar cycle, add a bit of radiation variation relative to current activity

            if (rb.solar_cycle > 0)
            {
                var activity = rb.SolarActivity() * 0.3;
                r0 = r0 + r0 * activity;
            }

            return r0;
        }

        // return the total environent radiation at position specified
        public static double Compute(Vessel v, Vector3d position, double gamma_transparency, double sunlight, out bool blackout,
                                     out bool magnetosphere, out bool inner_belt, out bool outer_belt, out bool interstellar, out double shieldedRadiation)
        {
            // prepare out parameters
            blackout = false;
            magnetosphere = false;
            inner_belt = false;
            outer_belt = false;
            interstellar = false;
            shieldedRadiation = 0.0;

            // no-op when Radiation is disabled
            if (!Features.Radiation) return 0.0;

            // store stuff
            Space gsm;
            Vector3 p;
            double D;
            double r;

            // accumulate radiation
            double radiation = 0.0;
            CelestialBody body = v.mainBody;
            while (body != null)
            {
                // Compute radiation values from overlapping 3d fields (belts + magnetospheres)

                RadiationBody rb = Info(body);
                RadiationModel mf = rb.model;

                // activity is [-0.15..1.05]
                var activity = rb.SolarActivity(false);

                if (mf.Has_field())
                {
                    // transform to local space once
                    var scaled_position = ScaledSpace.LocalToScaledSpace(position);

                    // generate radii-normalized GSM space
                    gsm = Gsm_space(rb, true);

                    // move the point in GSM space
                    p = gsm.Transform_in(scaled_position);

                    // accumulate radiation and determine pause/belt flags
                    if (mf.has_inner)
                    {
                        D = mf.Inner_func(p);
                        inner_belt |= D < 0;

                        // allow for radiation field to grow/shrink with solar activity
                        D -= activity * 0.25 / mf.inner_radius;
                        r = RadiationInBelt(D, mf.inner_radius, rb.radiation_inner_gradient);
                        radiation += r * rb.radiation_inner * (1 + activity * 0.3);
                    }
                    if (mf.has_outer)
                    {
                        D = mf.Outer_func(p);
                        outer_belt |= D < 0;

                        // allow for radiation field to grow/shrink with solar activity
                        D -= activity * 0.25 / mf.outer_radius;
                        r = RadiationInBelt(D, mf.outer_radius, rb.radiation_outer_gradient);
                        radiation += r * rb.radiation_outer * (1 + activity * 0.3);
                    }
                    if (mf.has_pause)
                    {
                        gsm = Gsm_space(rb, false);
                        p = gsm.Transform_in(scaled_position);
                        D = mf.Pause_func(p);

                        radiation += Lib.Clamp(D / -0.1332f, 0.0f, 1.0f) * rb.RadiationPause();

                        magnetosphere |= D < 0.0f && !Lib.IsSun(rb.body); //< ignore heliopause
                        interstellar |= D > 0.0f && Lib.IsSun(rb.body); //< outside heliopause
                    }
                }

                if (rb.radiation_surface > 0 && body != v.mainBody)
                {
                    Vector3d direction;
                    double distance;
					if (Sim.IsBodyVisible(v, position, body, v.KerbalismData().EnvVisibleBodies, out direction, out distance))
					{
						var r0 = RadiationR0(rb);
						var r1 = DistanceRadiation(r0, distance);

						// clamp to max. surface radiation. when loading on a rescaled system, the vessel can appear to be within the sun for a few ticks
						radiation += Math.Min(r1, rb.radiation_surface);
#if DEBUG_RADIATION
						if (v.loaded) Lib.Log("Radiation " + v + " from surface of " + body + ": " + Lib.HumanReadableRadiation(radiation) + " gamma: " + Lib.HumanReadableRadiation(r1));
#endif
					}
                }

                // avoid loops in the chain
                body = (body.referenceBody != null && body.referenceBody.referenceBody == body) ? null : body.referenceBody;
            }

            // add extern radiation
            radiation += Settings.ExternRadiation / 3600.0;

#if DEBUG_RADIATION
			if (v.loaded) Lib.Log("Radiation " + v + " extern: " + Lib.HumanReadableRadiation(radiation) + " gamma: " + Lib.HumanReadableRadiation(Settings.ExternRadiation));
#endif

			// apply gamma transparency if inside atmosphere
			radiation *= gamma_transparency;

#if DEBUG_RADIATION
			if (v.loaded) Lib.Log("Radiation " + v + " after gamma: " + Lib.HumanReadableRadiation(radiation) + " transparency: " + gamma_transparency);
#endif
			// add surface radiation of the body itself
			if(Lib.IsSun(v.mainBody) && v.altitude < v.mainBody.Radius)
			if(v.altitude > v.mainBody.Radius)
			{
				radiation += DistanceRadiation(RadiationR0(Info(v.mainBody)), v.altitude);

			}

#if DEBUG_RADIATION
			if (v.loaded) Lib.Log("Radiation " + v + " from current main body: " + Lib.HumanReadableRadiation(radiation) + " gamma: " + Lib.HumanReadableRadiation(DistanceRadiation(RadiationR0(Info(v.mainBody)), v.altitude)));
#endif

			shieldedRadiation = radiation;

            // if there is a storm in progress
            if (Storm.InProgress(v))
            {
                // inside a magnetopause (except heliosphere), blackout the signal
                // outside, add storm radiations modulated by sun visibility
                if (magnetosphere) blackout = true;
                else
                {
                    var vd = v.KerbalismData();

                    var activity = Info(vd.EnvMainSun.SunData.body).SolarActivity(false) / 2.0;
                    var strength = PreferencesRadiation.Instance.StormRadiation * sunlight * (activity + 0.5);

                    radiation += strength;
                    shieldedRadiation += vd.EnvHabitatInfo.AverageHabitatRadiation(strength);
                }
            }

            // add emitter radiation after atmosphere transparency
            var emitterRadiation = Emitter.Total(v);
            radiation += emitterRadiation;
            shieldedRadiation += emitterRadiation;

#if DEBUG_RADIATION
			if (v.loaded) Lib.Log("Radiation " + v + " after emitters: " + Lib.HumanReadableRadiation(radiation) + " shielded " + Lib.HumanReadableRadiation(shieldedRadiation));
#endif

			// for EVAs, add the effect of nearby emitters
			if (v.isEVA)
            {
                var nearbyEmitters = Emitter.Nearby(v);
				radiation += nearbyEmitters;
                shieldedRadiation += nearbyEmitters;
#if DEBUG_RADIATION
				if (v.loaded) Lib.Log("Radiation " + v + " nearby emitters " + Lib.HumanReadableRadiation(nearbyEmitters));
#endif
			}

			var passiveShielding = PassiveShield.Total(v);
			shieldedRadiation -= passiveShielding;

#if DEBUG_RADIATION
			if (v.loaded) Lib.Log("Radiation " + v + " passiveShielding " + Lib.HumanReadableRadiation(passiveShielding));
			if (v.loaded) Lib.Log("Radiation " + v + " before clamp: " + Lib.HumanReadableRadiation(radiation) + " shielded " + Lib.HumanReadableRadiation(shieldedRadiation));
#endif

			// clamp radiation to positive range
			// note: we avoid radiation going to zero by using a small positive value
			radiation = Math.Max(radiation, Nominal);
            shieldedRadiation = Math.Max(shieldedRadiation, Nominal);

#if DEBUG_RADIATION
			if (v.loaded) Lib.Log("Radiation " + v + " after clamp: " + Lib.HumanReadableRadiation(radiation) + " shielded " + Lib.HumanReadableRadiation(shieldedRadiation));
#endif
			// return radiation
			return radiation;
        }

        /// <summary>
        /// Return the factor for the radiation in a belt at the given depth
        /// </summary>
        /// <param name="depth">distance from the border of the belt, in panetary radii. negative while in belt.</param>
        /// <param name="scale">represents the 'thickness' of the belt (radius of the outer torus)</param>
        /// <param name="gradient">represents how steeply the value will increase</param>
        public static double RadiationInBelt(double depth, double scale, double gradient = 1.5)
        {
            return Lib.Clamp(gradient * -depth / scale, 0.0, 1.0);
        }

        // return the surface radiation for the body specified (used by body info panel)
        public static double ComputeSurface(CelestialBody b, double gamma_transparency)
        {
            if (!Features.Radiation) return 0.0;

            // store stuff
            Space gsm;
            Vector3 p;
            double D;

            // transform to local space once
            Vector3d position = ScaledSpace.LocalToScaledSpace(b.position);

            // accumulate radiation
            double radiation = 0.0;
            CelestialBody body = b;
            while (body != null)
            {
                RadiationBody rb = Info(body);
                RadiationModel mf = rb.model;

                var activity = rb.SolarActivity(false);

                if (mf.Has_field())
                {
                    // generate radii-normalized GSM space
                    gsm = Gsm_space(rb, true);

                    // move the poing in GSM space
                    p = gsm.Transform_in(position);

                    // accumulate radiation and determine pause/belt flags
                    if (mf.has_inner)
                    {
                        D = mf.Inner_func(p);
                        // allow for radiation field to grow/shrink with solar activity
                        D -= activity * 0.25 / mf.inner_radius;

                        var r = RadiationInBelt(D, mf.inner_radius, rb.radiation_inner_gradient);
                        radiation += r * rb.radiation_inner * (1 + activity * 0.3);
                    }
                    if (mf.has_outer)
                    {
                        D = mf.Outer_func(p);
                        // allow for radiation field to grow/shrink with solar activity
                        D -= activity * 0.25 / mf.outer_radius;

                        var r = RadiationInBelt(D, mf.outer_radius, rb.radiation_outer_gradient);
                        radiation += r * rb.radiation_outer * (1 + activity * 0.3);
                    }
                    if (mf.has_pause)
                    {
                        gsm = Gsm_space(rb, false);
                        p = gsm.Transform_in(position);
                        D = mf.Pause_func(p);
                        radiation += Lib.Clamp(D / -0.1332f, 0.0f, 1.0f) * rb.RadiationPause();
                    }
                }

                if (rb.radiation_surface > 0 && body != b)
                {
                    // add surface radiation emitted from other body
                    double distance = (b.position - body.position).magnitude;
                    var r0 = RadiationR0(rb);
                    var r1 = DistanceRadiation(r0, distance);

					// Lib.Log("Surface radiation on " + b + " from " + body + ": " + Lib.HumanReadableRadiation(r1) + " distance " + distance);

					// clamp to max. surface radiation. when loading on a rescaled system, the vessel can appear to be within the sun for a few ticks
					radiation += Math.Min(r1, rb.radiation_surface);
                }

                // avoid loops in the chain
                body = (body.referenceBody != null && body.referenceBody.referenceBody == body) ? null : body.referenceBody;
            }

            // add extern radiation
            radiation += Settings.ExternRadiation / 3600.0;

            // Lib.Log("Radiation subtotal on " + b + ": " + Lib.HumanReadableRadiation(radiation) + ", gamma " + gamma_transparency);

            // scale radiation by gamma transparency if inside atmosphere
            radiation *= gamma_transparency;
			// Lib.Log("srf scaled on " + b + ": " + Lib.HumanReadableRadiation(radiation));

			// add surface radiation of the body itself
			RadiationBody bodyInfo = Info(b);
			// clamp to max. bodyInfo.radiation_surface to avoid extreme radiation effects while loading a vessel on rescaled systems
            radiation += Math.Min(bodyInfo.radiation_surface, DistanceRadiation(RadiationR0(bodyInfo), b.Radius));

            // Lib.Log("Radiation on " + b + ": " + Lib.HumanReadableRadiation(radiation) + ", own surface radiation " + Lib.HumanReadableRadiation(DistanceRadiation(RadiationR0(Info(b)), b.Radius)));

            // Lib.Log("radiation " + radiation + " nominal " + Nominal);

            // clamp radiation to positive range
            // note: we avoid radiation going to zero by using a small positive value
            radiation = Math.Max(radiation, Nominal);

            return radiation;
        }

        // show warning message when a vessel cross a radiation belt
        public static void BeltWarnings(Vessel v, VesselData vd)
        {
            // if radiation is enabled
            if (Features.Radiation)
            {
                // we only show the warning for manned vessels, or for all vessels the first time its crossed
                bool must_warn = vd.CrewCount > 0 || !DB.landmarks.belt_crossing;

                // are we inside a belt
                bool inside_belt = vd.EnvInnerBelt || vd.EnvOuterBelt;

                // show the message
                if (inside_belt && !vd.msg_belt && must_warn)
                {
                    Message.Post(Lib.BuildString("<b>", v.vesselName, "</b> is crossing <i>", v.mainBody.bodyName, " radiation belt</i>"), "Exposed to extreme radiation");
                    vd.msg_belt = true;
                }
                else if (!inside_belt && vd.msg_belt)
                {
                    // no message after crossing the belt
                    vd.msg_belt = false;
                }

                // record first belt crossing
                if (inside_belt) DB.landmarks.belt_crossing = true;

                // record first heliopause crossing
                if (vd.EnvInterstellar) DB.landmarks.heliopause_crossing = true;
            }
        }

        // deduce first interesting body for radiation in the body chain
        static CelestialBody Interesting_body(CelestialBody body)
        {
            if (Info(body).model.Has_field()) return body;      // main body has field
            else if (body.referenceBody != null                 // it has a ref body
              && body.referenceBody.referenceBody != body)      // avoid loops in planet setup (eg: OPM)
                return Interesting_body(body.referenceBody);      // recursively
            else return null;                                   // nothing in chain
        }

        static CelestialBody Interesting_body()
        {
            var target = PlanetariumCamera.fetch.target;
            return
                target == null
              ? null
              : target.celestialBody != null
              ? Interesting_body(target.celestialBody)
              : target.vessel != null
              ? Interesting_body(target.vessel.mainBody)
              : null;
        }

        /// <summary>
        /// Calculate radiation shielding efficiency. Parameter shielding should be in range [0..1]
        /// <para>
        /// If you have a thickness which stops a known amount of radiation of a known and constant
        /// type, then if you have a new thickness, you can calculate how much it stops by:
        /// </
        /// Stoppage = 1 - ((1 - AmountStoppedByKnownThickness)^(NewThickness / KnownThickness))
        /// <para>
        /// source : http://www.projectrho.com/public_html/rocket/radiation.php#id--Radiation_Shielding--Shielding--Shield_Rating
        /// </para>
        /// </summary>
        public static double ShieldingEfficiency(double shielding)
        {
            return 1 - Math.Pow(1 - PreferencesRadiation.Instance.shieldingEfficiency, Lib.Clamp(shielding, 0.0, 1.0));
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

        // nominal radiation is used to never allow zero radiation
        public static double Nominal = 0.0003 / 3600.0; // < 3 mrad/h is nominal
    }

} // KERBALISM
