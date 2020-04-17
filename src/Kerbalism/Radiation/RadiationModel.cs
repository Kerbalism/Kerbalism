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


		public float InnerFunc(Vector3 p)
		{
			p.x *= p.x < 0.0f ? inner_extension : inner_compression;
			float q1 = Mathf.Sqrt((p.x * p.x + p.z * p.z) * inner_deform_xy) - inner_dist;
			float d1 = Mathf.Sqrt(q1 * q1 + p.y * p.y) - inner_radius;
			float q2 = Mathf.Sqrt((p.x * p.x + p.z * p.z) * inner_border_deform_xy) - inner_border_dist;
			float d2 = Mathf.Sqrt(q2 * q2 + p.y * p.y) - inner_border_radius;
			return Mathf.Max(d1, -d2) + (inner_deform > 0.001 ? (Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * inner_deform : 0.0f);
		}

		public Vector3 InnerDomain()
		{
			float p = Mathf.Max((inner_dist + inner_radius), (inner_border_dist + inner_border_radius));
			float w = p * Mathf.Sqrt(1 / Mathf.Min(inner_deform_xy, inner_border_deform_xy));
			return new Vector3((w / inner_compression + w / inner_extension) * 0.5f, Mathf.Max(inner_radius, inner_border_radius), w) * (1.0f + inner_deform);
		}

		public Vector3 InnerOffset()
		{
			float p = Mathf.Max((inner_dist + inner_radius), (inner_border_dist + inner_border_radius));
			float w = p * Mathf.Sqrt(1 / Mathf.Min(inner_deform_xy, inner_border_deform_xy));
			return new Vector3(w / inner_compression - (w / inner_compression + w / inner_extension) * 0.5f, 0.0f, 0.0f);
		}

		public float OuterFunc(Vector3 p)
		{
			p.x *= p.x < 0.0f ? outer_extension : outer_compression;
			float q1 = Mathf.Sqrt((p.x * p.x + p.z * p.z) * outer_deform_xy) - outer_dist;
			float d1 = Mathf.Sqrt(q1 * q1 + p.y * p.y) - outer_radius;
			float q2 = Mathf.Sqrt((p.x * p.x + p.z * p.z) * outer_border_deform_xy) - outer_border_dist;
			float d2 = Mathf.Sqrt(q2 * q2 + p.y * p.y) - outer_border_radius;
			return Mathf.Max(d1, -d2) + (outer_deform > 0.001 ? (Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * outer_deform : 0.0f);
		}

		public Vector3 OuterDomain()
		{
			float p = Mathf.Max((outer_dist + outer_radius), (outer_border_dist + outer_border_radius));
			float w = p * Mathf.Sqrt(1 / Mathf.Min(outer_deform_xy, outer_border_deform_xy));
			return new Vector3((w / outer_compression + w / outer_extension) * 0.5f, Mathf.Max(outer_radius, outer_border_radius), w) * (1.0f + outer_deform);
		}

		public Vector3 OuterOffset()
		{
			float p = Mathf.Max((outer_dist + outer_radius), (outer_border_dist + outer_border_radius));
			float w = p * Mathf.Sqrt(1 / Mathf.Min(outer_deform_xy, outer_border_deform_xy));
			return new Vector3(w / outer_compression - (w / outer_compression + w / outer_extension) * 0.5f, 0.0f, 0.0f);
		}

		public float PauseFunc(Vector3 p)
		{
			p.x *= p.x < 0.0f ? pause_extension : pause_compression;
			p.y *= pause_height_scale;
			return p.magnitude - pause_radius
			  + (pause_deform > 0.001 ? (Mathf.Sin(p.x * 5.0f) * Mathf.Sin(p.y * 7.0f) * Mathf.Sin(p.z * 6.0f)) * pause_deform : 0.0f);
		}

		public Vector3 PauseDomain()
		{
			return new Vector3((pause_radius / pause_compression + pause_radius / pause_extension) * 0.5f,
			  pause_radius / pause_height_scale, pause_radius) * (1.0f + pause_deform);
		}

		public Vector3 PauseOffset()
		{
			return new Vector3(pause_radius / pause_compression - (pause_radius / pause_compression + pause_radius / pause_extension) * 0.5f, 0.0f, 0.0f);
		}

		public bool HasField()
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
}
