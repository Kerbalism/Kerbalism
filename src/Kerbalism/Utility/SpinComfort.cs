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

		public struct Sample
		{
			/// <summary>True when physics state was readable and a snapshot can be stored.</summary>
			public bool available;
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

			Rigidbody rb = root.Rigidbody;
			if (rb == null)
				return sample;

			Vector3 omegaWorld = rb.angularVelocity; // rad/s, world space
			double omega = omegaWorld.magnitude;
			double rpm = omega * 60.0 / (2.0 * Math.PI);

			if (omega < 1e-8)
			{
				sample.available = true;
				sample.minGee = 0.0;
				sample.rpm = 0.0;
				sample.worstRadius = 0.0;
				return sample;
			}

			Vector3 omegaHat = omegaWorld / (float)omega;
			Vector3 com = v.CurrentCoM;
			double omegaSq = omega * omega;

			bool anyOccupied = false;
			double minGee = double.PositiveInfinity;
			double worstRadius = double.PositiveInfinity;

			foreach (Part p in v.parts)
			{
				if (p == null || p.protoModuleCrew == null || p.protoModuleCrew.Count == 0)
					continue;

				anyOccupied = true;
				Vector3 r = p.transform.position - com;
				double radius = Vector3.Cross(r, omegaHat).magnitude;
				double gee = omegaSq * radius / StandardGravity;
				if (gee < minGee)
				{
					minGee = gee;
					worstRadius = radius;
				}
			}

			sample.available = true;
			sample.rpm = rpm;
			if (!anyOccupied)
			{
				sample.minGee = 0.0;
				sample.worstRadius = 0.0;
			}
			else
			{
				sample.minGee = minGee;
				sample.worstRadius = worstRadius;
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
			if (requiredGee <= 0.0f || maxRpm < 0.0f)
				return false;
			return minGee >= requiredGee && rpm <= maxRpm;
		}

		/// <summary>
		/// Pre-compute whether an editor ship can meet spin firm-ground thresholds.
		/// Uses prefab crew capacity so disabled Habitats still count as design seats.
		/// Spin axis is chosen as the root-part axis that maximizes the worst-case crew radius.
		/// </summary>
		public static EditorEstimate EvaluateEditor(List<Part> parts, float requiredGee, float maxRpm)
		{
			EditorEstimate estimate = new EditorEstimate
			{
				requiredGee = requiredGee,
				maxRpm = maxRpm
			};

			if (parts == null || parts.Count == 0 || requiredGee <= 0.0f || maxRpm <= 0.0f)
				return estimate;

			Part root = EditorLogic.RootPart;
			if (root == null)
				root = parts[0];
			if (root == null)
				return estimate;

			Vector3 com;
			if (!TryGetEditorCoM(parts, out com))
				return estimate;

			List<Part> crewParts = new List<Part>();
			foreach (Part p in parts)
			{
				if (p == null || p.partInfo == null || p.partInfo.partPrefab == null)
					continue;
				if (p.partInfo.partPrefab.CrewCapacity > 0)
					crewParts.Add(p);
			}

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

				if (minRadius > bestMinRadius && !double.IsInfinity(minRadius))
					bestMinRadius = minRadius;
			}

			if (bestMinRadius < 0.0)
				return estimate;

			estimate.available = true;
			estimate.worstRadius = bestMinRadius;

			double omegaMax = maxRpm * (2.0 * Math.PI) / 60.0;
			estimate.geeAtMaxRpm = omegaMax * omegaMax * bestMinRadius / StandardGravity;

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

		private static bool TryGetEditorCoM(List<Part> parts, out Vector3 com)
		{
			com = Vector3.zero;
			float mass = 0.0f;
			foreach (Part p in parts)
			{
				if (p == null)
					continue;
				float partMass = p.mass + p.GetResourceMass();
				if (partMass <= 0.0f)
					partMass = 0.001f;
				com += p.transform.position * partMass;
				mass += partMass;
			}

			if (mass <= 0.0f)
				return false;

			com /= mass;
			return true;
		}
	}
}
