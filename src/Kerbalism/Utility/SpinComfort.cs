using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	/// <summary>
	/// Whole-vessel spin artificial-gravity helper for firm-ground comfort.
	/// Uses world-space rigidbody angular velocity and the cylindrical radius of
	/// each occupied crew part about the spin axis through the vessel CoM.
	/// </summary>
	public static class SpinComfort
	{
		public const double StandardGravity = 9.80665;
		public const double RequiredStableSeconds = 2.0;
		public const double MaxStableAxisRateDegrees = 5.0;
		public const double MaxStableRelativeRpmRate = 0.1;
		public const double MaxStableAbsoluteRpmRate = 0.05;

		private const double AngularVelocityEpsilon = 1e-8;
		private const double MaxRelativeAngularVelocityDifference = 0.05;
		private const double MaxAbsoluteAngularVelocityDifference = 0.005;

		public struct Sample
		{
			/// <summary>True when physics state was readable and a snapshot can be stored.</summary>
			public bool available;
			/// <summary>True when the root and every occupied part share the same angular velocity.</summary>
			public bool coherent;
			/// <summary>True when at least one occupied crew part was evaluated.</summary>
			public bool hasOccupiedParts;
			/// <summary>World-space spin axis used for stability checks.</summary>
			public Vector3 axisWorld;
			/// <summary>Minimum artificial gravity (g) among occupied crew parts; 0 if none/spinless.</summary>
			public double minGee;
			/// <summary>Vessel spin rate in revolutions per minute.</summary>
			public double rpm;
			/// <summary>Cylindrical radius (m) of the occupied part with the lowest gee.</summary>
			public double worstRadius;
		}

		/// <summary>
		/// Editor/planner estimate: no live spin, so radii are taken about the best of the
		/// root part's principal axes and gravity is evaluated at the configured max RPM.
		/// </summary>
		public struct EditorEstimate
		{
			public bool available;
			public bool qualifies;
			public int crewPartCount;
			public double worstRadius;
			public double geeAtMaxRpm;
			public double rpmRequired;
			public float requiredGee;
			public float maxRpm;
			public bool usesAssignedCrew;
		}

		/// <summary>
		/// Evaluate current vessel spin metrics. Only valid for loaded, unpacked vessels
		/// with an active root rigidbody. Callers must leave previous snapshots untouched
		/// when <see cref="Sample.available"/> is false.
		/// </summary>
		public static Sample Evaluate(Vessel v)
		{
			Sample sample = new Sample();
			if (v == null || !v.loaded || v.packed)
				return sample;

			Part root = v.rootPart;
			if (root == null)
				return sample;

			Rigidbody rootRb = GetPhysicalRigidbody(root);
			if (rootRb == null)
				return sample;

			sample.available = true;
			Vector3 rootOmegaWorld = rootRb.angularVelocity; // rad/s, world space
			Vector3 com = v.CurrentCoM;
			if (!IsFinite(rootOmegaWorld) || !IsFinite(com))
				return sample;

			double rootOmega = rootOmegaWorld.magnitude;
			if (!IsFinite(rootOmega))
				return sample;

			sample.coherent = true;
			sample.rpm = OmegaToRpm(rootOmega);
			sample.axisWorld = rootOmega > AngularVelocityEpsilon
				? rootOmegaWorld / (float)rootOmega
				: Vector3.zero;

			bool anyOccupied = false;
			double minGee = double.PositiveInfinity;
			double worstRadius = double.PositiveInfinity;
			double maxRpm = sample.rpm;

			foreach (Part p in v.parts)
			{
				if (p == null || p.protoModuleCrew == null || p.protoModuleCrew.Count == 0)
					continue;

				anyOccupied = true;
				Rigidbody partRb = GetPhysicalRigidbody(p);
				if (partRb == null)
				{
					sample.available = false;
					return sample;
				}
				if (!IsFinite(partRb.angularVelocity) || !IsFinite(p.transform.position))
				{
					sample.coherent = false;
					return sample;
				}

				Vector3 partOmegaWorld = partRb.angularVelocity;
				double partOmega = partOmegaWorld.magnitude;
				if (!IsFinite(partOmega))
				{
					sample.coherent = false;
					return sample;
				}
				double allowedDifference = Math.Max(
					MaxAbsoluteAngularVelocityDifference,
					Math.Max(rootOmega, partOmega) * MaxRelativeAngularVelocityDifference);
				if ((partOmegaWorld - rootOmegaWorld).magnitude > allowedDifference)
				{
					sample.coherent = false;
					sample.hasOccupiedParts = true;
					sample.minGee = 0.0;
					sample.worstRadius = 0.0;
					sample.rpm = Math.Max(maxRpm, OmegaToRpm(partOmega));
					return sample;
				}

				if (partOmega <= AngularVelocityEpsilon)
				continue;

				Vector3 partOmegaHat = partOmegaWorld / (float)partOmega;
				Vector3 r = p.transform.position - com;
				double radius = Vector3.Cross(r, partOmegaHat).magnitude;
				double gee = partOmega * partOmega * radius / StandardGravity;
				double partRpm = OmegaToRpm(partOmega);
				maxRpm = Math.Max(maxRpm, partRpm);
				if (!IsFinite(radius) || !IsFinite(gee) || !IsFinite(partRpm))
				{
					sample.coherent = false;
					return sample;
				}
				if (gee < minGee)
				{
					minGee = gee;
					worstRadius = radius;
				}
			}

			sample.hasOccupiedParts = anyOccupied;
			sample.rpm = maxRpm;
			if (!anyOccupied)
			{
				sample.minGee = 0.0;
				sample.worstRadius = 0.0;
			}
			else
			{
				sample.minGee = double.IsPositiveInfinity(minGee) ? 0.0 : minGee;
				sample.worstRadius = double.IsPositiveInfinity(worstRadius) ? 0.0 : worstRadius;
			}
			return sample;
		}

		/// <summary>
		/// Apply current difficulty thresholds to a (possibly persisted) sample.
		/// All occupied crew parts must meet minGee and the whole vessel must stay under maxRpm.
		/// </summary>
		public static bool Qualifies(bool snapshotValid, double minGee, double rpm, bool enabled, float requiredGee, float maxRpm)
		{
			if (!enabled || !snapshotValid)
				return false;
			if (!IsFinite(minGee) || !IsFinite(rpm) || !IsFinite(requiredGee) || !IsFinite(maxRpm))
				return false;
			if (requiredGee <= 0.0f || maxRpm < 0.0f || minGee < 0.0 || rpm < 0.0)
				return false;
			return minGee >= requiredGee && rpm <= maxRpm;
		}

		/// <summary>
		/// Pre-compute whether an editor ship can meet spin firm-ground thresholds.
		/// Uses assigned crew parts when available, otherwise falls back to all prefab seats.
		/// Spin axis is chosen as the root-part axis that maximizes the worst-case crew radius.
		/// </summary>
		public static EditorEstimate EvaluateEditor(
			List<Part> parts,
			VesselCrewManifest crewManifest,
			float requiredGee,
			float maxRpm)
		{
			EditorEstimate estimate = new EditorEstimate
			{
				requiredGee = requiredGee,
				maxRpm = maxRpm
			};

			if (parts == null
				|| parts.Count == 0
				|| !IsFinite(requiredGee)
				|| !IsFinite(maxRpm)
				|| requiredGee <= 0.0f
				|| maxRpm <= 0.0f)
				return estimate;

			Part root = EditorLogic.RootPart;
			if (root == null)
				root = parts[0];
			if (root == null)
				return estimate;

			Vector3 com;
			if (!TryGetEditorCoM(parts, out com))
				return estimate;

			List<Part> assignedCrewParts = new List<Part>();
			List<Part> crewableParts = new List<Part>();
			foreach (Part p in parts)
			{
				if (p == null || p.partInfo == null || p.partInfo.partPrefab == null)
					continue;
				if (p.partInfo.partPrefab.CrewCapacity > 0)
				{
					crewableParts.Add(p);
					if (GetAssignedCrewCount(p, crewManifest) > 0)
						assignedCrewParts.Add(p);
				}
			}

			List<Part> crewParts = assignedCrewParts.Count > 0 ? assignedCrewParts : crewableParts;
			estimate.usesAssignedCrew = assignedCrewParts.Count > 0;
			estimate.crewPartCount = crewParts.Count;
			if (crewParts.Count == 0)
			{
				estimate.available = true;
				return estimate;
			}

			Transform rootT = root.transform;
			Vector3[] axes = new Vector3[]
			{
				rootT.right.normalized,
				rootT.up.normalized,
				rootT.forward.normalized
			};

			double bestMinRadius = -1.0;
			foreach (Vector3 axis in axes)
			{
				if (axis.sqrMagnitude < 1e-8f)
					continue;

				double minRadius = double.PositiveInfinity;
				foreach (Part p in crewParts)
				{
					Vector3 r = p.transform.position - com;
					double radius = Vector3.Cross(r, axis).magnitude;
					if (radius < minRadius)
						minRadius = radius;
				}

				if (minRadius > bestMinRadius && IsFinite(minRadius))
					bestMinRadius = minRadius;
			}

			if (bestMinRadius < 0.0)
				return estimate;

			estimate.available = true;
			estimate.worstRadius = bestMinRadius;

			double omegaMax = maxRpm * (2.0 * Math.PI) / 60.0;
			estimate.geeAtMaxRpm = omegaMax * omegaMax * bestMinRadius / StandardGravity;
			if (!IsFinite(estimate.geeAtMaxRpm))
				return new EditorEstimate();

			if (bestMinRadius > 1e-6)
			{
				double omegaNeeded = Math.Sqrt(requiredGee * StandardGravity / bestMinRadius);
				estimate.rpmRequired = omegaNeeded * 60.0 / (2.0 * Math.PI);
			}
			else
			{
				estimate.rpmRequired = double.PositiveInfinity;
			}

			estimate.qualifies = estimate.geeAtMaxRpm >= requiredGee && estimate.rpmRequired <= maxRpm;
			return estimate;
		}

		/// <summary>
		/// Hash only the occupied editor parts and their crew counts. Moving crew between
		/// parts changes the hash; swapping seats within one part does not affect geometry.
		/// </summary>
		public static int EditorCrewAssignmentHash(List<Part> parts, VesselCrewManifest crewManifest)
		{
			if (parts == null || crewManifest == null)
				return 0;

			unchecked
			{
				int hash = 17;
				foreach (Part p in parts)
				{
					if (p == null)
						continue;

					int assignedCrew = GetAssignedCrewCount(p, crewManifest);
					if (assignedCrew == 0)
						continue;

					hash = hash * 31 + (int)p.craftID;
					hash = hash * 31 + assignedCrew;
				}
				return hash;
			}
		}

		private static bool TryGetEditorCoM(List<Part> parts, out Vector3 com)
		{
			com = Vector3.zero;
			float mass = 0.0f;
			foreach (Part p in parts)
			{
				if (p == null)
					continue;

				float partMass = p.mass
					+ p.GetResourceMass()
					+ p.GetModuleMass(p.mass, ModifierStagingSituation.CURRENT);
				if (!IsFinite(partMass))
					return false;
				if (partMass <= 0.0f)
					continue;

				Part physicalPart = p;
				while (physicalPart.PhysicsSignificance == 1 && physicalPart.parent != null)
					physicalPart = physicalPart.parent;

				Vector3 partCom = physicalPart.transform.TransformPoint(physicalPart.CoMOffset);
				if (!IsFinite(partCom))
					return false;

				com += partCom * partMass;
				mass += partMass;
			}

			if (mass <= 0.0f)
				return false;

			com /= mass;
			return IsFinite(com);
		}

		private static int GetAssignedCrewCount(Part p, VesselCrewManifest crewManifest)
		{
			if (p == null || crewManifest == null)
				return 0;

			PartCrewManifest partManifest = crewManifest.GetPartCrewManifest(p.craftID);
			if (partManifest == null || partManifest.partCrew == null)
				return 0;

			int count = 0;
			foreach (string crewName in partManifest.partCrew)
			{
				if (!string.IsNullOrEmpty(crewName))
					count++;
			}
			return count;
		}

		public static bool IsFinite(double value)
		{
			return !double.IsNaN(value) && !double.IsInfinity(value);
		}

		public static bool IsFinite(float value)
		{
			return !float.IsNaN(value) && !float.IsInfinity(value);
		}

		public static bool IsFinite(Vector3 value)
		{
			return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
		}

		private static double OmegaToRpm(double omega)
		{
			return omega * 60.0 / (2.0 * Math.PI);
		}

		private static Rigidbody GetPhysicalRigidbody(Part p)
		{
			while (p != null)
			{
				if (p.Rigidbody != null)
					return p.Rigidbody;
				p = p.parent;
			}
			return null;
		}
	}
}
