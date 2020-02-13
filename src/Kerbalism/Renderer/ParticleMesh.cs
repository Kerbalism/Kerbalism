using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	// store an arbitrary number of static particles in a set of meshes
	public sealed class ParticleMesh
	{
		// create a particle mesh from a set of points
		public ParticleMesh(List<Vector3> points)
		{
			this.points = points;
		}


		// create a particle mesh by fitting points on an implicit surface defined by a signed distance field
		public ParticleMesh(Func<Vector3, float> dist_func, Vector3 domain_hsize, Vector3 domain_offset, int particle_count, float quality)
		{
			// store stuff
			Vector3 p;
			float D;

			// hard-limit on sample count, to avoid infinite sampling when the distance function is positive everywhere
			int sample_limit = particle_count * 1000;

			// divide once
			float thickness = 1.0f / quality;

			// preallocate position container
			points = new List<Vector3>(particle_count);

			// particle-fitting
			int samples = 0;
			int i = 0;
			while (i < particle_count && samples < sample_limit)
			{
				// generate random position inside bounding volume
				p.x = Lib.FastRandomFloat() * domain_hsize.x + domain_offset.x;
				p.y = Lib.FastRandomFloat() * domain_hsize.y + domain_offset.y;
				p.z = Lib.FastRandomFloat() * domain_hsize.z + domain_offset.z;

				// calculate signed distance
				D = dist_func(p);

				if (D <= 0.0f) // if inside
				{
					// this displays the exact radiation field border
					if(D <= 0.0 && D > - thickness)
					{
						points.Add(p);
						++i;
					}
				}

				// count samples
				++samples;
			}

			// DO NOT LOG FROM A THREAD
			// some feedback on the samples going above the limit
			// if (i < particle_count) Lib.Log("particle-fitting reached hard limit at " + Lib.HumanReadablePerc((double)i / (double)particle_count));
		}


		void Compile()
		{
			// max number of particles that can be stored in a unity mesh
			const int max_particles = 64000;

			// create the set of meshes
			meshes = new List<Mesh>(points.Count / max_particles + 1);
			Mesh m;
			List<Vector3> t_points = new List<Vector3>(max_particles);
			List<int> t_indexes = new List<int>(max_particles);
			for (var i = 0; i < points.Count; ++i)
			{
				t_points.Add(points[i]);
				t_indexes.Add(t_indexes.Count);
				if (t_indexes.Count > max_particles || i == points.Count - 1)
				{
					m = new Mesh();
					m.SetVertices(t_points);
					m.SetIndices(t_indexes.ToArray(), MeshTopology.Points, 0);
					meshes.Add(m);
					t_points.Clear();
					t_indexes.Clear();
				}
			}
			points = null;
		}


		// render all the meshes
		public void Render(Matrix4x4 m)
		{
			if (meshes == null)
			{
				Compile();
			}

			for (var i = 0; i < meshes.Count; ++i)
			{
				Graphics.DrawMeshNow(meshes[i], m);
			}
		}


		List<Vector3> points;     // set of points
		List<Mesh> meshes;        // set of meshes
	}


} // KERBALISM
