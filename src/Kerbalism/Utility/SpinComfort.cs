using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	/// <summary>
	/// Whole-vessel spin artificial-gravity helper for firm-ground comfort.
	/// Grants firm ground when enough crew capacity sits at sufficient cylindrical
	/// radius about the spin axis through the vessel CoM — independent of which
	/// seats are currently occupied (crew are assumed to use the living volume).
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
			/// <summary>True when the root and every crewable part share the same angular velocity.</summary>
			public bool coherent;
			/// <summary>True when at least one crewable part was evaluated.</summary>
			public bool hasCrewableParts;
			/// <summary>World-space spin axis used for stability checks.</summary>
			public Vector3 axisWorld;
			/// <summary>Crew seats whose artificial gravity meets the configured minimum.</summary>
			public int qualifyingCapacity;
			/// <summary>Total crew capacity of all crewable parts.</summary>
			public int totalCrewCapacity;
			/// <summary>Lowest gee among seats that meet the minimum; 0 if none.</summary>
			public double marginalGee;
			/// <summary>Cylindrical radius (m) of the marginal qualifying seat.</summary>
			public double marginalRadius;
			/// <summary>Vessel spin rate in revolutions per minute.</summary>
			public double rpm;
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
			public int qualifyingCapacity;
			public int seatsNeeded;
			public int crewDemand;
			public double marginalRadius;
			public double geeAtMaxRpm;
			public double rpmRequired;
			public float requiredGee;
			public float maxRpm;
			public float coverageRatio;
			public bool usesAssignedCrew;
		}

		private struct SeatRadius
		{
			public int capacity;
			public double radius;
		}

		/// <summary>
		/// Evaluate current vessel spin metrics. Only valid for loaded, unpacked vessels
		/// with an active root rigidbody. Callers must leave previous snapshots untouched
		/// when <see cref="Sample.available"/> is false.
		/// </summary>
		public static Sample Evaluate(Vessel v, float requiredGee)
		{
			Sample sample = new Sample();
			if (v == null || !v.loaded || v.packed)
				return sample;
			if (!IsFinite(requiredGee) || requiredGee <= 0.0f)
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

			bool anyCrewable = false;
			int qualifyingCapacity = 0;
			int totalCrewCapacity = 0;
			double marginalGee = double.PositiveInfinity;
			double marginalRadius = double.PositiveInfinity;
			double maxRpm = sample.rpm;

			foreach (Part p in v.parts)
			{
				if (p == null)
					continue;

				int capacity = p.CrewCapacity;
				if (capacity <= 0)
					continue;

				anyCrewable = true;
				totalCrewCapacity += capacity;

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
					sample.hasCrewableParts = true;
					sample.qualifyingCapacity = 0;
					sample.totalCrewCapacity = totalCrewCapacity;
					sample.marginalGee = 0.0;
					sample.marginalRadius = 0.0;
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

				if (gee + double.Epsilon >= requiredGee)
				{
					qualifyingCapacity += capacity;
					if (gee < marginalGee)
					{
						marginalGee = gee;
						marginalRadius = radius;
					}
				}
			}

			sample.hasCrewableParts = anyCrewable;
			sample.rpm = maxRpm;
			sample.totalCrewCapacity = totalCrewCapacity;
			sample.qualifyingCapacity = qualifyingCapacity;
			if (qualifyingCapacity <= 0)
			{
				sample.marginalGee = 0.0;
				sample.marginalRadius = 0.0;
			}
			else
			{
				sample.marginalGee = marginalGee;
				sample.marginalRadius = marginalRadius;
			}
			return sample;
		}

		/// <summary>
		/// Apply current difficulty thresholds to a (possibly persisted) sample.
		/// Enough crew-capacity at the required gee must cover the aboard crew.
		/// </summary>
		public static bool Qualifies(
			bool snapshotValid,
			int qualifyingCapacity,
			int crewCount,
			double rpm,
			bool enabled,
			float coverageRatio,
			float maxRpm)
		{
			if (!enabled || !snapshotValid || crewCount <= 0)
				return false;
			if (!IsFinite(rpm) || !IsFinite(coverageRatio) || !IsFinite(maxRpm))
				return false;
			if (qualifyingCapacity < 0 || rpm < 0.0 || maxRpm < 0.0f || coverageRatio <= 0.0f)
				return false;
			if (rpm > maxRpm)
				return false;
			return qualifyingCapacity >= SeatsNeeded(crewCount, coverageRatio);
		}

		public static int SeatsNeeded(int crewCount, float coverageRatio)
		{
			if (crewCount <= 0 || !IsFinite(coverageRatio) || coverageRatio <= 0.0f)
				return int.MaxValue;
			double needed = crewCount * (double)coverageRatio;
			if (!IsFinite(needed))
				return int.MaxValue;
			return Math.Max(1, (int)Math.Ceiling(needed - 1e-9));
		}

		/// <summary>
		/// Pre-compute whether an editor ship can meet spin firm-ground thresholds.
		/// Uses assigned crew count when available, otherwise the ship's full seat count.
		/// Spin axis is chosen as the root-part axis that covers the required seats
		/// at the lowest RPM.
		/// </summary>
		public static EditorEstimate EvaluateEditor(
			List<Part> parts,
			int crewDemand,
			float requiredGee,
			float maxRpm,
			float coverageRatio)
		{
			EditorEstimate estimate = new EditorEstimate
			{
				requiredGee = requiredGee,
				maxRpm = maxRpm,
				coverageRatio = coverageRatio
			};

			if (parts == null
				|| parts.Count == 0
				|| !IsFinite(requiredGee)
				|| !IsFinite(maxRpm)
				|| !IsFinite(coverageRatio)
				|| requiredGee <= 0.0f
				|| maxRpm <= 0.0f
				|| coverageRatio <= 0.0f)
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
			List<int> capacities = new List<int>();
			int totalCapacity = 0;
			foreach (Part p in parts)
			{
				if (p == null)
					continue;

				// Habitat sets live capacity to zero while disabled, deploying or inflating.
				int capacity = p.CrewCapacity;
				if (capacity <= 0)
					continue;

				crewParts.Add(p);
				capacities.Add(capacity);
				totalCapacity += capacity;
			}

			estimate.crewPartCount = crewParts.Count;
			if (crewParts.Count == 0)
			{
				estimate.available = true;
				return estimate;
			}

			estimate.usesAssignedCrew = crewDemand > 0 && crewDemand < totalCapacity;
			estimate.crewDemand = crewDemand > 0 ? crewDemand : totalCapacity;
			estimate.seatsNeeded = SeatsNeeded(estimate.crewDemand, coverageRatio);

			Transform rootT = root.transform;
			Vector3[] axes = new Vector3[]
			{
				rootT.right.normalized,
				rootT.up.normalized,
				rootT.forward.normalized
			};

			int bestQualifying = -1;
			double bestMarginalRadius = -1.0;
			double bestRpmRequired = double.PositiveInfinity;
			List<SeatRadius> bestSeats = null;
			double omegaMax = maxRpm * (2.0 * Math.PI) / 60.0;

			foreach (Vector3 axis in axes)
			{
				if (axis.sqrMagnitude < 1e-8f)
					continue;

				List<SeatRadius> axisSeats = new List<SeatRadius>(crewParts.Count);
				int qualifying = 0;
				double innermostQualifyingRadius = double.PositiveInfinity;
				bool axisValid = true;

				for (int i = 0; i < crewParts.Count; i++)
				{
					Part p = crewParts[i];
					int capacity = capacities[i];
					Vector3 r = p.transform.position - com;
					double radius = Vector3.Cross(r, axis).magnitude;
					if (!IsFinite(radius))
					{
						axisValid = false;
						break;
					}

					axisSeats.Add(new SeatRadius { capacity = capacity, radius = radius });

					double gee = omegaMax * omegaMax * radius / StandardGravity;
					if (IsFinite(gee) && gee + double.Epsilon >= requiredGee)
					{
						qualifying += capacity;
						if (radius < innermostQualifyingRadius)
							innermostQualifyingRadius = radius;
					}
				}

				if (!axisValid)
					continue;

				double requiredRadius;
				double rpmRequired = RpmRequiredForSeats(
					axisSeats,
					estimate.seatsNeeded,
					requiredGee,
					out requiredRadius);
				bool canCover = IsFinite(rpmRequired);
				bool bestCanCover = IsFinite(bestRpmRequired);

				// Prefer the axis that covers the required seats at the lowest RPM.
				// If no axis can cover them, retain the most useful max-RPM estimate.
				bool better = canCover
					? !bestCanCover
						|| rpmRequired < bestRpmRequired - 1e-9
						|| (Math.Abs(rpmRequired - bestRpmRequired) <= 1e-9
							&& qualifying > bestQualifying)
					: !bestCanCover
						&& (qualifying > bestQualifying
							|| (qualifying == bestQualifying
								&& innermostQualifyingRadius > bestMarginalRadius));

				if (better)
				{
					bestQualifying = qualifying;
					bestMarginalRadius = canCover
						? requiredRadius
						: double.IsPositiveInfinity(innermostQualifyingRadius)
							? -1.0
							: innermostQualifyingRadius;
					bestRpmRequired = rpmRequired;
					bestSeats = axisSeats;
				}
			}

			if (bestSeats == null || bestQualifying < 0)
				return estimate;

			estimate.available = true;
			estimate.qualifyingCapacity = bestQualifying;
			estimate.marginalRadius = Math.Max(0.0, bestMarginalRadius);

			double omegaMaxRpm = maxRpm * (2.0 * Math.PI) / 60.0;
			estimate.geeAtMaxRpm = estimate.marginalRadius > 0.0
				? omegaMaxRpm * omegaMaxRpm * estimate.marginalRadius / StandardGravity
				: 0.0;
			if (!IsFinite(estimate.geeAtMaxRpm))
				return new EditorEstimate();

			estimate.rpmRequired = bestRpmRequired;
			estimate.qualifies = estimate.qualifyingCapacity >= estimate.seatsNeeded
				&& estimate.rpmRequired <= maxRpm
				&& IsFinite(estimate.rpmRequired);
			return estimate;
		}

		/// <summary>
		/// Lowest RPM that puts at least <paramref name="seatsNeeded"/> seats at
		/// <paramref name="requiredGee"/>, using the outermost seats first.
		/// </summary>
		private static double RpmRequiredForSeats(
			List<SeatRadius> seats,
			int seatsNeeded,
			float requiredGee,
			out double marginalRadius)
		{
			marginalRadius = 0.0;
			if (seats == null || seats.Count == 0 || seatsNeeded <= 0 || requiredGee <= 0.0f)
				return double.PositiveInfinity;

			List<SeatRadius> sorted = new List<SeatRadius>(seats);
			sorted.Sort((a, b) => b.radius.CompareTo(a.radius));

			int accumulated = 0;
			double innermost = double.PositiveInfinity;
			foreach (SeatRadius seat in sorted)
			{
				if (seat.radius <= 1e-6)
					continue;
				accumulated += seat.capacity;
				if (seat.radius < innermost)
					innermost = seat.radius;
				if (accumulated >= seatsNeeded)
					break;
			}

			if (accumulated < seatsNeeded || !IsFinite(innermost) || innermost <= 1e-6)
				return double.PositiveInfinity;

			marginalRadius = innermost;
			double omegaNeeded = Math.Sqrt(requiredGee * StandardGravity / innermost);
			double rpm = omegaNeeded * 60.0 / (2.0 * Math.PI);
			return IsFinite(rpm) ? rpm : double.PositiveInfinity;
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
