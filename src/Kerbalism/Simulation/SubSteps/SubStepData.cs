using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KERBALISM
{
	/// <summary>
	/// One duration-weighted geometry sample in an elapsed simulation interval.
	/// Positions are in the same world/inertial frame used by KSP's orbit APIs.
	/// </summary>
	public readonly struct SubStepGeometrySample
	{
		public readonly double UT;
		public readonly double Duration;
		public readonly Vector3d Position;
		public readonly double Latitude;
		public readonly double Longitude;
		public readonly double Altitude;

		internal SubStepGeometrySample(double ut, double duration, Vector3d position, double latitude, double longitude, double altitude)
		{
			UT = ut;
			Duration = duration;
			Position = position;
			Latitude = latitude;
			Longitude = longitude;
			Altitude = altitude;
		}
	}

	/// <summary>One star's direct-light geometry and flux at a shared time sample.</summary>
	public readonly struct SubStepSunSample
	{
		public readonly double UT;
		public readonly double Duration;
		public readonly Vector3d Direction;
		public readonly double Distance;
		public readonly bool Visible;
		public readonly double AtmosphericFactor;
		public readonly double RawFlux;
		public readonly double DirectFlux;

		internal SubStepSunSample(double ut, double duration, Vector3d direction, double distance,
			bool visible, double atmosphericFactor, double rawFlux)
		{
			UT = ut;
			Duration = duration;
			Direction = direction;
			Distance = distance;
			Visible = visible;
			AtmosphericFactor = atmosphericFactor;
			RawFlux = rawFlux;
			DirectFlux = visible ? rawFlux * atmosphericFactor : 0.0;
		}
	}

	/// <summary>Duration-weighted interval averages and endpoint data for one star.</summary>
	public sealed class SubStepSunResult
	{
		public int BodyIndex { get; internal set; }
		public Vector3d EndpointDirection { get; internal set; }
		public double EndpointDistance { get; internal set; }
		public double EndpointRawFlux { get; internal set; }
		public double SunlightFactor { get; internal set; }
		public double AtmosphericFactor { get; internal set; }
		public double AverageRawFlux { get; internal set; }
		public double AverageUnshadowedFlux { get; internal set; }
		public double AverageDirectFlux { get; internal set; }
		public ReadOnlyCollection<SubStepSunSample> Samples { get; internal set; }
	}

	/// <summary>
	/// Immutable result shared by all consumers of one vessel elapsed interval.
	/// </summary>
	public sealed class SubStepIntervalResult
	{
		private readonly Dictionary<int, SubStepSunResult> suns;
		private readonly ReadOnlyDictionary<int, SubStepSunResult> readOnlySuns;

		public Guid VesselId { get; }
		public double StartUT { get; }
		public double EndUT { get; }
		public double ElapsedSeconds => EndUT - StartUT;
		public int Generation { get; }
		public string FallbackReason { get; }
		public bool IsValid => string.IsNullOrEmpty(FallbackReason) && Samples.Count > 0;
		public ReadOnlyCollection<SubStepGeometrySample> Samples { get; }
		public IReadOnlyDictionary<int, SubStepSunResult> Suns => readOnlySuns;

		internal SubStepIntervalResult(Guid vesselId, double startUT, double endUT, int generation,
			SubStepGeometrySample[] samples, Dictionary<int, SubStepSunResult> suns, string fallbackReason)
		{
			VesselId = vesselId;
			StartUT = startUT;
			EndUT = endUT;
			Generation = generation;
			Samples = Array.AsReadOnly(samples ?? Array.Empty<SubStepGeometrySample>());
			this.suns = suns ?? new Dictionary<int, SubStepSunResult>();
			readOnlySuns = new ReadOnlyDictionary<int, SubStepSunResult>(this.suns);
			FallbackReason = fallbackReason;
		}

		public bool TryGetSun(int bodyIndex, out SubStepSunResult result)
		{
			return suns.TryGetValue(bodyIndex, out result);
		}

		internal double SunlightFactorAt(int sampleIndex)
		{
			double rawFlux = 0.0;
			double visibleFlux = 0.0;
			foreach (SubStepSunResult sun in suns.Values)
			{
				SubStepSunSample sample = sun.Samples[sampleIndex];
				rawFlux += sample.RawFlux;
				if (sample.Visible)
					visibleFlux += sample.RawFlux;
			}
			return rawFlux > 0.0 ? visibleFlux / rawFlux : 0.0;
		}

		internal static SubStepIntervalResult Invalid(Vessel vessel, double startUT, double endUT, int generation, string reason)
		{
			return new SubStepIntervalResult(vessel == null ? Guid.Empty : vessel.id, startUT, endUT, generation,
				Array.Empty<SubStepGeometrySample>(), null, reason);
		}
	}
}
