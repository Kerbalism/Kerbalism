using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public static class PartVolumeAndSurface
	{
		// static game wide volume / surface cache
		public static Dictionary<string, Definition> partDatabase;
		public const string cacheNodeName = "KERBALISM_PART_VSINFO";
		public static string cachePath => Path.Combine(Lib.KerbalismRootPath, "PartsVS.cache");

		public static void EvaluatePrefabAtCompilation(AvailablePart availablePart)
		{
			if (partDatabase == null)
			{
				partDatabase = new Dictionary<string, Definition>();

				ConfigNode dbRootNode = ConfigNode.Load(cachePath);
				ConfigNode[] habInfoNodes = dbRootNode?.GetNodes(cacheNodeName);

				if (habInfoNodes != null)
				{
					for (int i = 0; i < habInfoNodes.Length; i++)
					{
						string partName = habInfoNodes[i].GetValue("partName") ?? string.Empty;
						if (!string.IsNullOrEmpty(partName) && !partDatabase.ContainsKey(partName))
							partDatabase.Add(partName, new Definition(habInfoNodes[i]));
					}
				}
			}

			Part prefab = availablePart.partPrefab;

			bool requireEvaluation =
				prefab.physicalSignificance == Part.PhysicalSignificance.FULL
				&& prefab.attachMode == AttachModes.STACK
				&& prefab.mass + prefab.GetResourceMass() > 0.25
				&& PartBoundsVolume(prefab, false) > 0.25;

			List<IVolumeAndSurfaceModule> vsModules = new List<IVolumeAndSurfaceModule>();
			foreach (PartModule module in prefab.Modules)
			{
				if (module is IVolumeAndSurfaceModule vsModule)
				{
					vsModules.Add(vsModule);
					requireEvaluation = true;
				}
			}

			if (!requireEvaluation)
				return;

			if (!partDatabase.TryGetValue(prefab.name, out Definition partInfo))
			{
				foreach (IVolumeAndSurfaceModule vsModule in vsModules)
				{
					vsModule.SetupPrefabPartModel();
				}

				partInfo = GetPartVolumeAndSurface(prefab, Settings.VolumeAndSurfaceLogging);
				partInfo.GetUsingMethod(Method.Best, out partInfo.volume, out partInfo.surface, false);
				partDatabase.Add(prefab.name, partInfo);
			}

			foreach (IVolumeAndSurfaceModule vsModule in vsModules)
			{
				vsModule.GetVolumeAndSurfaceResults(partInfo);
			}
		}

		public static void SaveCache()
		{
			// save the habitat volume/surface cache
			if (partDatabase == null)
				return;

			ConfigNode fakeNode = new ConfigNode();

			foreach (KeyValuePair<string, Definition> habInfo in partDatabase)
			{
				ConfigNode node = new ConfigNode(cacheNodeName);
				node.AddValue("partName", habInfo.Key);
				habInfo.Value.Save(node);
				fakeNode.AddNode(node);
			}

			fakeNode.Save(cachePath);
		}

		public static Definition GetDefinition(Part part)
		{
			if (partDatabase.TryGetValue(part.name, out Definition info))
				return info;

			return null;
		}

		public const double boundsCylinderVolumeFactor = 0.785398;
		public const double boundsCylinderSurfaceFactor = 0.95493;

		/// <summary>
		/// return the volume of a part bounding box, in m^3
		/// note: this can only be called when part has not been rotated
		/// </summary>
		public static double PartBoundsVolume(Part p, bool applyCylinderFactor = false)
		{
			return applyCylinderFactor ? BoundsVolume(GetPartBounds(p)) * boundsCylinderVolumeFactor : BoundsVolume(GetPartBounds(p));
		}

		/// <summary>
		/// return the surface of a part bounding box, in m^2
		/// note: this can only be called when part has not been rotated
		/// </summary>
		public static double PartBoundsSurface(Part p, bool applyCylinderFactor = false)
		{
			return applyCylinderFactor ? BoundsSurface(GetPartBounds(p)) * boundsCylinderSurfaceFactor : BoundsSurface(GetPartBounds(p));
		}

		public static double BoundsVolume(Bounds bb)
		{
			Vector3 size = bb.size;
			return size.x * size.y * size.z;
		}

		public static double BoundsSurface(Bounds bb)
		{
			Vector3 size = bb.size;
			double a = size.x;
			double b = size.y;
			double c = size.z;
			return 2.0 * (a * b + a * c + b * c);
		}

		public static double BoundsIntersectionVolume(Bounds a, Bounds b)
		{
			Vector3 aMin = a.min;
			Vector3 aMax = a.max;
			Vector3 bMin = b.min;
			Vector3 bMax = b.max;

			Vector3 intersectionSize = default;
			intersectionSize.x = Math.Max(Math.Min(aMax.x, bMax.x) - Math.Max(aMin.x, bMin.x), 0f);
			intersectionSize.y = Math.Max(Math.Min(aMax.y, bMax.y) - Math.Max(aMin.y, bMin.y), 0f);
			intersectionSize.z = Math.Max(Math.Min(aMax.z, bMax.z) - Math.Max(aMin.z, bMin.z), 0f);

			return intersectionSize.x * intersectionSize.y * intersectionSize.z;
		}

		/// <summary>
		/// Get the part currently active geometry bounds. Similar to the Part.GetPartRendererBound() method but don't account for inactive renderers.
		/// Note : bounds are world axis aligned, meaning they will change if the part is rotated.
		/// </summary>
		public static Bounds GetPartBounds(Part part) => GetTransformRootAndChildrensBounds(part.transform);

		private static Bounds GetTransformRootAndChildrensBounds(Transform transform)
		{
			Bounds bounds = default;
			Renderer[] renderers = transform.GetComponentsInChildren<Renderer>(false);

			bool firstRenderer = true;
			foreach (Renderer renderer in renderers)
			{
				if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
					continue;

				if (firstRenderer)
				{
					bounds = renderer.bounds;
					firstRenderer = false;
					continue;
				}
				bounds.Encapsulate(renderer.bounds);
			}

			return bounds;
		}

		public class Definition
		{
			public Method bestMethod = Method.Best;

			public double volume = 0.0;
			public double surface = 0.0;

			public double boundsVolume = 0.0;
			public double boundsSurface = 0.0;

			public double colliderVolume = 0.0;
			public double colliderSurface = 0.0;

			public double meshVolume = 0.0;
			public double meshSurface = 0.0;

			public double attachNodesSurface = 0.0;

			public Definition() { }

			public Definition(ConfigNode node)
			{
				bestMethod = Lib.ConfigEnum(node, "bestMethod", Method.Best);
				boundsVolume = Lib.ConfigValue(node, "boundsVolume", 0.0);
				boundsSurface = Lib.ConfigValue(node, "boundsSurface", 0.0);
				colliderVolume = Lib.ConfigValue(node, "colliderVolume", 0.0);
				colliderSurface = Lib.ConfigValue(node, "colliderSurface", 0.0);
				meshVolume = Lib.ConfigValue(node, "meshVolume", 0.0);
				meshSurface = Lib.ConfigValue(node, "meshSurface", 0.0);
				attachNodesSurface = Lib.ConfigValue(node, "attachNodesSurface", 0.0);
			}

			public void Save(ConfigNode node)
			{
				node.AddValue("bestMethod", bestMethod.ToString());
				node.AddValue("boundsVolume", boundsVolume.ToString("G17"));
				node.AddValue("boundsSurface", boundsSurface.ToString("G17"));
				node.AddValue("colliderVolume", colliderVolume.ToString("G17"));
				node.AddValue("colliderSurface", colliderSurface.ToString("G17"));
				node.AddValue("meshVolume", meshVolume.ToString("G17"));
				node.AddValue("meshSurface", meshSurface.ToString("G17"));
				node.AddValue("attachNodesSurface", attachNodesSurface.ToString("G17"));
			}

			public double GetBestVolume()
			{
				switch (bestMethod)
				{
					case Method.Bounds: return boundsVolume;
					case Method.Collider: return colliderVolume;
					case Method.Mesh: return meshVolume;
				}
				return 0.0;
			}

			public double GetBestSurface(bool substractAttachNodesSurface = true)
			{
				switch (bestMethod)
				{
					case Method.Bounds: return substractAttachNodesSurface ? SubstractNodesSurface(boundsSurface, attachNodesSurface) : boundsSurface;
					case Method.Collider: return substractAttachNodesSurface ? SubstractNodesSurface(colliderSurface, attachNodesSurface) : colliderSurface;
					case Method.Mesh: return substractAttachNodesSurface ? SubstractNodesSurface(meshSurface, attachNodesSurface) : meshSurface;
				}
				return 0.0;
			}

			public void GetUsingBestMethod(out double volume, out double surface, bool substractAttachNodesSurface = true)
			{
				GetUsingMethod(bestMethod, out volume, out surface, substractAttachNodesSurface);
			}

			public void GetUsingMethod(Method method, out double volume, out double surface, bool substractAttachNodesSurface = true)
			{
				if (method == Method.Best)
				{
					method = bestMethod;
				}

				switch (method)
				{
					case Method.Bounds:
						volume = boundsVolume;
						surface = substractAttachNodesSurface ? SubstractNodesSurface(boundsSurface, attachNodesSurface) : boundsSurface;
						return;
					case Method.Collider:
						volume = colliderVolume;
						surface = substractAttachNodesSurface ? SubstractNodesSurface(colliderSurface, attachNodesSurface) : colliderSurface;
						return;
					case Method.Mesh:
						volume = meshVolume;
						surface = substractAttachNodesSurface ? SubstractNodesSurface(meshSurface, attachNodesSurface) : meshSurface;
						return;
					default:
						volume = 0.0;
						surface = 0.0;
						return;
				}
			}

			private double SubstractNodesSurface(double surface, double nodesSurface)
			{
				return Math.Max(surface * 0.5, surface - nodesSurface);
			}
		}

		public enum Method
		{
			Best = 0,
			Bounds,
			Collider,
			Mesh
		}

		private struct MeshInfo : IEquatable<MeshInfo>
		{
			public string name;
			public double volume;
			public double surface;
			public Bounds bounds;
			public double boundsVolume;

			public MeshInfo(string name, double volume, double surface, Bounds bounds)
			{
				this.name = name;
				this.volume = volume;
				this.surface = surface;
				this.bounds = bounds;
				boundsVolume = bounds.size.x * bounds.size.y * bounds.size.z;
			}

			public override string ToString()
			{
				return $"\"{name}\" : VOLUME={volume.ToString("0.00m3")} - SURFACE={surface.ToString("0.00m2")} - BOUNDS VOLUME={boundsVolume.ToString("0.00m3")}";
			}

			public bool Equals(MeshInfo other)
			{
				return volume == other.volume && surface == other.surface && bounds == other.bounds;
			}

			public override bool Equals(object obj) => Equals((MeshInfo)obj);

			public static bool operator ==(MeshInfo lhs, MeshInfo rhs) => lhs.Equals(rhs);

			public static bool operator !=(MeshInfo lhs, MeshInfo rhs) => !lhs.Equals(rhs);

			public override int GetHashCode() => volume.GetHashCode() ^ surface.GetHashCode() ^ bounds.GetHashCode();
		}

		// As a general rule, at least one of the two mesh based methods will return accurate results.
		// This is very dependent on how the model is done. Specifically, results will be inaccurate in the following cases : 
		// - non closed meshes, larger holes = higher error
		// - overlapping meshes. Obviously any intersection will cause the volume/surface to be higher
		// - surface area will only be accurate in the case of a single mesh per part. A large number of meshes will result in very inaccurate surface evaluation.
		// - results may not be representative of the habitable volume if there are a lot of large structural or "technical" shapes like fuel tanks, shrouds, interstages, integrated engines, etc...

		// Note on surface : surface in kerbalism is meant as the surface of the habitat outer hull exposed to the environment,
		// that's why it make sense to substract the attach nodes area, as that surface will usually by covered by connnected parts.

		/// <summary>
		/// Estimate the part volume and surface by using 3 possible methods : 3D meshes, 3D collider meshes or axis aligned bounding box.
		/// Uses the currently enabled meshes/colliders, and will work with skinned meshes (inflatables).
		/// VERY SLOW, 20-100 ms per call, use it only once and cache the results
		/// </summary>
		/// <param name="part">An axis aligned part, with its geometry in the desired state (mesh switching / animations).</param>
		/// <param name="logAll">If true, the result of all 3 methods will be logged</param>
		/// <param name="ignoreSkinnedMeshes">If true, the volume/surface of deformable meshes (ex : inflatables) will be ignored</param>
		/// <param name="rootTransform">if specified, only bounds/meshes/colliders on this transform and its children will be used</param>
		/// <returns>surface/volume results for the 3 methods, and the best method to use</returns>
		public static Definition GetPartVolumeAndSurface(
			Part part,
			bool logAll = false,
			bool ignoreSkinnedMeshes = false,
			Transform rootTransform = null)
		{
			if (logAll) Lib.Log($"====== Volume and surface evaluation for part :{part.name} ======");

			if (rootTransform == null) rootTransform = part.transform;

			Definition results = new Definition();

			if (logAll) Lib.Log("Searching for meshes...");
			List<MeshInfo> meshInfos = GetPartMeshesVolumeAndSurface(rootTransform, ignoreSkinnedMeshes);
			int usedMeshCount = GetMeshesTotalVolumeAndSurface(meshInfos, out results.meshVolume, out results.meshSurface, logAll);


			if (logAll) Lib.Log("Searching for colliders...");
			// Note that we only account for mesh colliders and ignore any box/sphere/capsule collider because :
			// - they usually are used as an array of overlapping box colliders, giving very unreliable results
			// - they are often used for hollow geometry like trusses
			// - they are systematically used for a variety of non shape related things like ladders/handrails/hatches hitboxes (note that it is be possible to filter those by checking for the "Airlock" or "Ladder" tag on the gameobject)
			List<MeshInfo> colliderMeshInfos = GetPartMeshCollidersVolumeAndSurface(rootTransform);
			int usedCollidersCount = GetMeshesTotalVolumeAndSurface(colliderMeshInfos, out results.colliderVolume, out results.colliderSurface, logAll);

			Bounds partBounds = GetTransformRootAndChildrensBounds(rootTransform);
			results.boundsVolume = BoundsVolume(partBounds);
			results.boundsSurface = BoundsSurface(partBounds);

			// If volume is greater than 90% the bounds volume or less than 0.25 m3 it's obviously wrong
			double validityFactor = 0.9;
			bool colliderIsValid = results.colliderVolume < results.boundsVolume * validityFactor && results.colliderVolume > 0.25;
			bool meshIsValid = results.meshVolume < results.boundsVolume * validityFactor && results.meshVolume > 0.25;


			if (!colliderIsValid && !meshIsValid)
				results.bestMethod = Method.Bounds;
			else if (!colliderIsValid)
				results.bestMethod = Method.Mesh;
			else if (!meshIsValid)
				results.bestMethod = Method.Collider;
			else
			{
				// we consider that both methods are accurate if the volume difference is less than 10%
				double volumeDifference = Math.Abs(results.colliderVolume - results.meshVolume) / Math.Max(results.colliderVolume, results.meshVolume);

				// in case the returned volumes are similar, the method that use the less collider / mesh count will be more accurate for surface
				if (volumeDifference < 0.2 && (usedCollidersCount != usedMeshCount))
					results.bestMethod = usedCollidersCount < usedMeshCount ? Method.Collider : Method.Mesh;
				// in case the returned volumes are still not completely off from one another, favor the result that used only one mesh
				else if (volumeDifference < 0.75 && usedCollidersCount == 1 && usedMeshCount != 1)
					results.bestMethod = Method.Collider;
				else if (volumeDifference < 0.75 && usedMeshCount == 1 && usedCollidersCount != 1)
					results.bestMethod = Method.Mesh;
				// in other cases, the method that return the largest volume is usually right
				else
					results.bestMethod = results.colliderVolume > results.meshVolume ? Method.Collider : Method.Mesh;
			}

			foreach (AttachNode attachNode in part.attachNodes)
			{
				// its seems the standard way of disabling a node involve
				// reducing the rendered radius to 0.001f
				if (attachNode.radius < 0.1f)
					continue;

				switch (attachNode.size)
				{
					case 0: results.attachNodesSurface += 0.3068; break;// 0.625 m disc
					case 1: results.attachNodesSurface += 1.2272; break;// 1.25 m disc
					case 2: results.attachNodesSurface += 4.9090; break;// 2.5 m disc
					case 3: results.attachNodesSurface += 11.045; break;// 3.75 m disc
					case 4: results.attachNodesSurface += 19.635; break;// 5 m disc
				}
			}

			if (logAll)
			{
				double rawColliderVolume = 0.0;
				double rawColliderSurface = 0.0;
				int colliderCount = 0;
				if (colliderMeshInfos != null)
				{
					rawColliderVolume = colliderMeshInfos.Sum(p => p.volume);
					rawColliderSurface = colliderMeshInfos.Sum(p => p.surface);
					colliderCount = colliderMeshInfos.Count();
				}

				double rawMeshVolume = 0.0;
				double rawMeshSurface = 0.0;
				int meshCount = 0;
				if (meshInfos != null)
				{
					rawMeshVolume = meshInfos.Sum(p => p.volume);
					rawMeshSurface = meshInfos.Sum(p => p.surface);
					meshCount = meshInfos.Count();
				}

				results.GetUsingBestMethod(out double volume, out double surface, true);

				Lib.Log($"Evaluation results :");
				Lib.Log($"Bounds method :   Volume:{results.boundsVolume.ToString("0.00m3")} - Surface:{results.boundsSurface.ToString("0.00m2")} - Max valid volume:{(results.boundsVolume * validityFactor).ToString("0.00m3")}");
				Lib.Log($"Collider method : Volume:{results.colliderVolume.ToString("0.00m3")} - Surface:{results.colliderSurface.ToString("0.00m2")} - Raw volume:{rawColliderVolume.ToString("0.00m3")} - Raw surface:{rawColliderSurface.ToString("0.00m2")} - Meshes: {usedCollidersCount}/{colliderCount} (valid/raw)");
				Lib.Log($"Mesh method :     Volume:{results.meshVolume.ToString("0.00m3")} - Surface:{results.meshSurface.ToString("0.00m2")} - Raw volume:{rawMeshVolume.ToString("0.00m3")} - Raw surface:{rawMeshSurface.ToString("0.00m2")} - Meshes: {usedMeshCount}/{meshCount} (valid/raw)");
				Lib.Log($"Attach nodes surface : {results.attachNodesSurface.ToString("0.00m2")}");
				Lib.Log($"Returned result : Volume:{volume.ToString("0.00m3")} - Surface:{surface.ToString("0.00m2")} - Method used : {results.bestMethod.ToString()}");
			}

			return results;
		}

		private static int GetMeshesTotalVolumeAndSurface(List<MeshInfo> meshInfos, out double volume, out double surface, bool logAll = false)
		{
			volume = 0.0;
			surface = 0.0;
			int usedMeshesCount = 0;

			if (meshInfos == null || meshInfos.Count() == 0)
				return usedMeshesCount;

			// sort the meshes by their volume, largest first
			meshInfos.Sort((x, y) => y.volume.CompareTo(x.volume));

			// only account for meshes that are have at least 25% the volume of the biggest mesh, or are at least 0.5 m3, whatever is smaller
			double minMeshVolume = Math.Min(meshInfos[0].volume * 0.25, 0.5);

			for (int i = 0; i < meshInfos.Count; i++)
			{
				MeshInfo meshInfo = meshInfos[i];

				// for each mesh bounding box, get the volume of all other meshes bounding boxes intersections
				double intersectedVolume = 0.0;
				foreach (MeshInfo otherMeshInfo in meshInfos)
				{
					if (meshInfo == otherMeshInfo)
						continue;

					// Don't account large meshes whose bounding box volume is greater than 3 times their mesh volume because
					// their bounding box contains too much empty space that may enclose anpther mesh.
					// Typical case : the torus mesh of a gravity ring will enclose the central core mesh
					if (otherMeshInfo.volume > 10.0 && otherMeshInfo.boundsVolume > otherMeshInfo.volume * 3.0)
						continue;

					intersectedVolume += BoundsIntersectionVolume(meshInfo.bounds, otherMeshInfo.bounds);
				}

				if (meshInfo.volume < minMeshVolume)
				{
					if (logAll) Lib.Log($"Found {meshInfo.ToString()} - INTERSECTED VOLUME={intersectedVolume.ToString("0.00m3")} - Mesh rejected : too small");
					continue;
				}

				// exclude meshes whose intersected volume is greater than 75% their bounding box volume
				// always accept the first mesh (since it's the largest, we can assume it's other meshes that intersect it)
				if (i > 0 && intersectedVolume / meshInfo.boundsVolume > 0.75)
				{
					if (logAll) Lib.Log($"Found {meshInfo.ToString()} - INTERSECTED VOLUME={intersectedVolume.ToString("0.00m3")} - Mesh rejected : it is inside another mesh");
					continue;
				}

				if (logAll) Lib.Log($"Found {meshInfo.ToString()} - INTERSECTED VOLUME={intersectedVolume.ToString("0.00m3")} - Mesh accepted");
				usedMeshesCount++;
				volume += meshInfo.volume;

				// account for the full surface of the biggest mesh, then only half for the others
				if (i == 0)
					surface += meshInfo.surface;
				else
					surface += meshInfo.surface * 0.5;
			}

			return usedMeshesCount;
		}

		private static List<MeshInfo> GetPartMeshesVolumeAndSurface(Transform partRootTransform, bool ignoreSkinnedMeshes)
		{
			List<MeshInfo> meshInfos = new List<MeshInfo>();

			if (!ignoreSkinnedMeshes)
			{
				SkinnedMeshRenderer[] skinnedMeshRenderers = partRootTransform.GetComponentsInChildren<SkinnedMeshRenderer>(false);
				for (int i = 0; i < skinnedMeshRenderers.Length; i++)
				{
					SkinnedMeshRenderer skinnedMeshRenderer = skinnedMeshRenderers[i];
					Mesh animMesh = new Mesh();
					skinnedMeshRenderer.BakeMesh(animMesh);

					MeshInfo meshInfo = new MeshInfo(
						skinnedMeshRenderer.transform.name,
						MeshVolume(animMesh.vertices, animMesh.triangles),
						MeshSurface(animMesh.vertices, animMesh.triangles),
						skinnedMeshRenderer.bounds);

					meshInfos.Add(meshInfo);
				}
			}

			MeshFilter[] meshFilters = partRootTransform.GetComponentsInChildren<MeshFilter>(false);
			int count = meshFilters.Length;

			if (count == 0)
				return meshInfos;

			foreach (MeshFilter meshFilter in meshFilters)
			{
				// Ignore colliders
				if (meshFilter.gameObject.GetComponent<MeshCollider>() != null)
					continue;

				// Ignore non rendered meshes
				MeshRenderer renderer = meshFilter.gameObject.GetComponent<MeshRenderer>();
				if (renderer == null || !renderer.enabled)
					continue;

				Mesh mesh = meshFilter.sharedMesh;
				Vector3 scaleVector = meshFilter.transform.lossyScale;
				float scale = scaleVector.x * scaleVector.y * scaleVector.z;

				Vector3[] vertices;
				if (scale != 1f)
					vertices = ScaleMeshVertices(mesh.vertices, scaleVector);
				else
					vertices = mesh.vertices;

				MeshInfo meshInfo = new MeshInfo(
					meshFilter.transform.name,
					MeshVolume(vertices, mesh.triangles),
					MeshSurface(vertices, mesh.triangles),
					renderer.bounds);

				meshInfos.Add(meshInfo);
			}

			return meshInfos;
		}

		private static List<MeshInfo> GetPartMeshCollidersVolumeAndSurface(Transform partRootTransform)
		{
			MeshCollider[] meshColliders = partRootTransform.GetComponentsInChildren<MeshCollider>(false);
			int count = meshColliders.Length;

			List<MeshInfo> meshInfos = new List<MeshInfo>(count);

			if (count == 0)
				return meshInfos;

			foreach (MeshCollider meshCollider in meshColliders)
			{
				Mesh mesh = meshCollider.sharedMesh;
				Vector3 scaleVector = meshCollider.transform.lossyScale;
				float scale = scaleVector.x * scaleVector.y * scaleVector.z;

				Vector3[] vertices;
				if (scale != 1f)
					vertices = ScaleMeshVertices(mesh.vertices, scaleVector);
				else
					vertices = mesh.vertices;

				MeshInfo meshInfo = new MeshInfo(
					meshCollider.transform.name,
					MeshVolume(vertices, mesh.triangles),
					MeshSurface(vertices, mesh.triangles),
					meshCollider.bounds);

				meshInfos.Add(meshInfo);
			}

			return meshInfos;
		}

		/// <summary>
		/// Scale a vertice array (note : this isn't enough to produce a valid unity mesh, would need to recalculate normals and UVs)
		/// </summary>
		private static Vector3[] ScaleMeshVertices(Vector3[] sourceVertices, Vector3 scale)
		{
			Vector3[] scaledVertices = new Vector3[sourceVertices.Length];
			for (int i = 0; i < sourceVertices.Length; i++)
			{
				scaledVertices[i] = new Vector3(
					sourceVertices[i].x * scale.x,
					sourceVertices[i].y * scale.y,
					sourceVertices[i].z * scale.z);
			}
			return scaledVertices;
		}

		/// <summary>
		/// Calculate a mesh surface in m^2. WARNING : slow
		/// Very accurate as long as the mesh is fully closed
		/// </summary>
		private static double MeshSurface(Vector3[] vertices, int[] triangles)
		{
			if (triangles.Length == 0)
				return 0.0;

			double sum = 0.0;

			for (int i = 0; i < triangles.Length; i += 3)
			{
				Vector3 corner = vertices[triangles[i]];
				Vector3 a = vertices[triangles[i + 1]] - corner;
				Vector3 b = vertices[triangles[i + 2]] - corner;

				sum += Vector3.Cross(a, b).magnitude;
			}

			return sum / 2.0;
		}

		/// <summary>
		/// Calculate a mesh volume in m^3. WARNING : slow
		/// Very accurate as long as the mesh is fully closed
		/// </summary>
		private static double MeshVolume(Vector3[] vertices, int[] triangles)
		{
			double volume = 0f;
			if (triangles.Length == 0)
				return volume;

			Vector3 o = new Vector3(0f, 0f, 0f);
			// Computing the center mass of the polyhedron as the fourth element of each mesh
			for (int i = 0; i < triangles.Length; i++)
			{
				o += vertices[triangles[i]];
			}
			o = o / triangles.Length;

			// Computing the sum of the volumes of all the sub-polyhedrons
			for (int i = 0; i < triangles.Length; i += 3)
			{
				Vector3 p1 = vertices[triangles[i + 0]];
				Vector3 p2 = vertices[triangles[i + 1]];
				Vector3 p3 = vertices[triangles[i + 2]];
				volume += SignedVolumeOfTriangle(p1, p2, p3, o);
			}
			return Math.Abs(volume);
		}

		private static float SignedVolumeOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 o)
		{
			Vector3 v1 = p1 - o;
			Vector3 v2 = p2 - o;
			Vector3 v3 = p3 - o;

			return Vector3.Dot(Vector3.Cross(v1, v2), v3) / 6f; ;
		}
	}
}
