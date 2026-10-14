using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace KERBALISM
{
	/// <summary>
	/// Shared, duration-weighted vessel/body position infrastructure.
	/// KSP state is snapshotted on the main thread; all orbital position work is
	/// executed through Unity Jobs and is eligible for KSPBurst compilation.
	/// </summary>
	public static class SubStepSimulation
	{
		public const double DefaultMaxStepSeconds = 30.0;
		private const int MaxSamplesPerBatch = 128;
		private const double TimeMatchTolerance = 1e-5;

		private sealed class VesselCache
		{
			public SubStepIntervalResult Current;
			public SubStepIntervalResult Previous;
		}

		private sealed class VesselRequest
		{
			public Vessel Vessel;
			public SubStepVesselNative Snapshot;
			public SubStepGeometrySample[] Samples;
			public SubStepSunSample[][] SunSamples;
		}

		private static readonly Dictionary<Guid, VesselCache> cache = new Dictionary<Guid, VesselCache>();
		private static NativeArray<SubStepTimeNative> timeBuffer;
		private static NativeArray<SubStepBodyNative> bodySnapshotBuffer;
		private static NativeArray<SubStepVesselNative> vesselSnapshotBuffer;
		private static NativeArray<SubStepStarNative> starSnapshotBuffer;
		private static NativeArray<double3> bodyPositionBuffer;
		private static NativeArray<SubStepVesselGeometryNative> vesselGeometryBuffer;
		private static NativeArray<SubStepSunNative> sunResultBuffer;

		public static int LastBodyPositionEvaluations { get; private set; }
		public static double LastJobsMilliseconds { get; private set; }
		public static double LastCompleteWaitMilliseconds { get; private set; }

		public static void Init()
		{
			Clear();
		}

		public static void Clear()
		{
			cache.Clear();
			foreach (VesselData vd in DB.VesselDatas)
				vd.SetSubStepInterval(null);
			DisposeBuffers();
			LastBodyPositionEvaluations = 0;
			LastJobsMilliseconds = 0.0;
			LastCompleteWaitMilliseconds = 0.0;
		}

		private static void DisposeBuffers()
		{
			if (sunResultBuffer.IsCreated) sunResultBuffer.Dispose();
			if (vesselGeometryBuffer.IsCreated) vesselGeometryBuffer.Dispose();
			if (bodyPositionBuffer.IsCreated) bodyPositionBuffer.Dispose();
			if (starSnapshotBuffer.IsCreated) starSnapshotBuffer.Dispose();
			if (vesselSnapshotBuffer.IsCreated) vesselSnapshotBuffer.Dispose();
			if (bodySnapshotBuffer.IsCreated) bodySnapshotBuffer.Dispose();
			if (timeBuffer.IsCreated) timeBuffer.Dispose();
		}

		public static void Invalidate(Guid vesselId)
		{
			cache.Remove(vesselId);
			Vessel vessel = FlightGlobals.FindVessel(vesselId);
			vessel?.KerbalismData().SetSubStepInterval(null);
		}

		/// <summary>
		/// Precompute one shared batch for loaded simulated vessels. Calls made by
		/// PartModules later in the same update reuse these exact results.
		/// </summary>
		public static void PrepareLoadedVessels(IList<Vessel> vessels, double elapsedSeconds)
		{
			if (vessels == null || !RequiresIntervalSampling(elapsedSeconds) || !HighLogic.LoadedSceneIsFlight)
				return;

			double endUT = Planetarium.GetUniversalTime();
			List<Vessel> eligible = new List<Vessel>();
			for (int i = 0; i < vessels.Count; i++)
			{
				Vessel vessel = vessels[i];
				if (vessel != null && vessel.loaded && vessel.KerbalismData().IsSimulated
					&& !TryGetCached(vessel.id, endUT - elapsedSeconds, endUT, out _))
					eligible.Add(vessel);
			}

			if (eligible.Count > 0)
				ComputeBatch(eligible, endUT - elapsedSeconds, endUT);
		}

		public static bool RequiresIntervalSampling(double elapsedSeconds)
		{
			return elapsedSeconds > DefaultMaxStepSeconds;
		}

		/// <summary>Get or synchronously schedule/complete an interval for one vessel.</summary>
		public static SubStepIntervalResult GetOrCreate(Vessel vessel, double elapsedSeconds)
		{
			double endUT = Planetarium.GetUniversalTime();
			return GetOrCreate(vessel, endUT - Math.Max(0.0, elapsedSeconds), endUT);
		}

		/// <summary>
		/// Idempotently get the exact retrospective interval requested by a
		/// scheduler or consumer. Matching consumers share one completed batch.
		/// </summary>
		public static SubStepIntervalResult GetOrCreate(Vessel vessel, double startUT, double endUT)
		{
			double elapsedSeconds = endUT - startUT;
			if (vessel == null)
				return SubStepIntervalResult.Invalid(null, startUT, endUT, "null vessel");
			if (elapsedSeconds <= 0.0)
				return SubStepIntervalResult.Invalid(vessel, startUT, endUT, "empty interval");

			if (TryGetCached(vessel.id, startUT, endUT, out SubStepIntervalResult cached))
				return cached;

			ComputeBatch(new List<Vessel> { vessel }, startUT, endUT);
			return cache[vessel.id].Current;
		}

		private static bool TryGetCached(Guid vesselId, double startUT, double endUT, out SubStepIntervalResult result)
		{
			result = null;
			if (!cache.TryGetValue(vesselId, out VesselCache entry))
				return false;

			if (Matches(entry.Current, startUT, endUT))
			{
				result = entry.Current;
				return true;
			}
			if (Matches(entry.Previous, startUT, endUT))
			{
				result = entry.Previous;
				return true;
			}
			return false;
		}

		private static bool Matches(SubStepIntervalResult result, double startUT, double endUT)
		{
			return result != null
				&& Math.Abs(result.StartUT - startUT) <= TimeMatchTolerance
				&& Math.Abs(result.EndUT - endUT) <= TimeMatchTolerance;
		}

		private static void Store(SubStepIntervalResult result)
		{
			if (!cache.TryGetValue(result.VesselId, out VesselCache entry))
			{
				entry = new VesselCache();
				cache.Add(result.VesselId, entry);
			}

			entry.Previous = entry.Current;
			entry.Current = result;
		}

		private static void ComputeBatch(List<Vessel> vessels, double startUT, double endUT)
		{
			Profiler.BeginSample("Kerbalism.SubStepSimulation");
			double elapsed = endUT - startUT;
			if (elapsed <= 0.0)
			{
				Profiler.EndSample();
				return;
			}
			if (Lib.HasPrincipia)
			{
				const string reason = "Principia non-Keplerian trajectories";
				for (int i = 0; i < vessels.Count; i++)
					StoreInvalid(vessels[i], startUT, endUT, reason);
				Profiler.EndSample();
				return;
			}

			List<CelestialBody> orderedBodies = BuildBodyOrder(out string bodyOrderFailure);
			if (orderedBodies == null)
			{
				for (int i = 0; i < vessels.Count; i++)
					StoreInvalid(vessels[i], startUT, endUT, bodyOrderFailure);
				Profiler.EndSample();
				return;
			}
			Dictionary<CelestialBody, int> bodySlots = new Dictionary<CelestialBody, int>(orderedBodies.Count);
			for (int i = 0; i < orderedBodies.Count; i++)
				bodySlots.Add(orderedBodies[i], i);

			List<VesselRequest> requests = new List<VesselRequest>(vessels.Count);
			for (int i = 0; i < vessels.Count; i++)
			{
				Vessel vessel = vessels[i];
				if (TryCreateVesselSnapshot(vessel, bodySlots, startUT, endUT,
					out SubStepVesselNative snapshot, out string reason))
				{
					requests.Add(new VesselRequest { Vessel = vessel, Snapshot = snapshot });
				}
				else
				{
					StoreInvalid(vessel, startUT, endUT, reason);
				}
			}

			if (requests.Count == 0)
			{
				Profiler.EndSample();
				return;
			}

			// Bound each sample's duration: capping the whole interval's sample
			// count can repeatedly sample the same orbital phase after long gaps.
			int sampleCount = Math.Max(1, (int)Math.Ceiling(elapsed / DefaultMaxStepSeconds));
			double sampleDuration = elapsed / sampleCount;
			int batchCapacity = Math.Min(MaxSamplesPerBatch, sampleCount);
			int vesselCount = requests.Count;
			int bodyCount = orderedBodies.Count;
			int sunCount = Sim.suns.Count;

			LastBodyPositionEvaluations = sampleCount * bodyCount;
			LastJobsMilliseconds = 0.0;
			LastCompleteWaitMilliseconds = 0.0;
			JobHandle outstandingHandle = default;
			bool hasOutstandingHandle = false;

			try
			{
				EnsureCapacity(ref timeBuffer, batchCapacity);
				EnsureCapacity(ref bodySnapshotBuffer, bodyCount);
				EnsureCapacity(ref vesselSnapshotBuffer, vesselCount);
				EnsureCapacity(ref starSnapshotBuffer, sunCount);
				EnsureCapacity(ref bodyPositionBuffer, batchCapacity * bodyCount);
				EnsureCapacity(ref vesselGeometryBuffer, batchCapacity * vesselCount);
				EnsureCapacity(ref sunResultBuffer, batchCapacity * vesselCount * sunCount);

				NativeArray<SubStepTimeNative> times = timeBuffer;
				NativeArray<SubStepBodyNative> bodySnapshots = bodySnapshotBuffer;
				NativeArray<SubStepVesselNative> vesselSnapshots = vesselSnapshotBuffer;
				NativeArray<SubStepStarNative> starSnapshots = starSnapshotBuffer;
				NativeArray<double3> bodyPositions = bodyPositionBuffer;
				NativeArray<SubStepVesselGeometryNative> vesselGeometry = vesselGeometryBuffer;
				NativeArray<SubStepSunNative> sunResults = sunResultBuffer;

				for (int i = 0; i < bodyCount; i++)
				{
					bodySnapshots[i] = CreateBodySnapshot(orderedBodies[i], bodySlots, endUT);
					if (bodySnapshots[i].ParentBody >= 0 && bodySnapshots[i].Orbit.IsValid == 0)
						throw new InvalidOperationException("unsupported body orbit");
				}

				for (int i = 0; i < vesselCount; i++)
				{
					vesselSnapshots[i] = requests[i].Snapshot;
					requests[i].Samples = new SubStepGeometrySample[sampleCount];
					requests[i].SunSamples = new SubStepSunSample[sunCount][];
					for (int sunIndex = 0; sunIndex < sunCount; sunIndex++)
						requests[i].SunSamples[sunIndex] = new SubStepSunSample[sampleCount];
				}

				for (int i = 0; i < sunCount; i++)
				{
					if (!bodySlots.TryGetValue(Sim.suns[i].body, out int sunBodyIndex))
						throw new InvalidOperationException("sun missing from body hierarchy");
					double fluxScale = Sim.suns[i].SolarFlux(1.0, false);
					if (!IsFinite(fluxScale) || fluxScale < 0.0)
						throw new InvalidOperationException("invalid stellar flux snapshot");
					starSnapshots[i] = new SubStepStarNative
					{
						BodyIndex = sunBodyIndex,
						FluxScale = fluxScale
					};
				}

				System.Diagnostics.Stopwatch jobsTimer = Settings.SubStepSimulationLogging
					? new System.Diagnostics.Stopwatch() : null;
				System.Diagnostics.Stopwatch completeWaitTimer = Settings.SubStepSimulationLogging
					? new System.Diagnostics.Stopwatch() : null;
				for (int sampleOffset = 0; sampleOffset < sampleCount; sampleOffset += MaxSamplesPerBatch)
				{
					int batchCount = Math.Min(MaxSamplesPerBatch, sampleCount - sampleOffset);
					for (int i = 0; i < batchCount; i++)
					{
						int sampleIndex = sampleOffset + i;
						double sampleStart = startUT + sampleIndex * sampleDuration;
						double duration = sampleIndex == sampleCount - 1 ? endUT - sampleStart : sampleDuration;
						times[i] = new SubStepTimeNative
						{
							UT = sampleStart + 0.5 * duration,
							Duration = duration
						};
					}

					jobsTimer?.Start();
					JobHandle bodyHandle = new SubStepBodyPositionJob
					{
						Times = times,
						Bodies = bodySnapshots,
						BodyCount = bodyCount,
						Positions = bodyPositions
					}.Schedule(batchCount, 1);
					outstandingHandle = bodyHandle;
					hasOutstandingHandle = true;

					JobHandle vesselHandle = new SubStepVesselPositionJob
					{
						Times = times,
						Bodies = bodySnapshots,
						Vessels = vesselSnapshots,
						BodyPositions = bodyPositions,
						BodyCount = bodyCount,
						VesselCount = vesselCount,
						Geometry = vesselGeometry
					}.Schedule(batchCount * vesselCount, 32, bodyHandle);
					outstandingHandle = vesselHandle;

					JobHandle solarHandle = new SubStepSolarVisibilityJob
					{
						Bodies = bodySnapshots,
						Vessels = vesselSnapshots,
						BodyPositions = bodyPositions,
						VesselGeometry = vesselGeometry,
						Stars = starSnapshots,
						VesselCount = vesselCount,
						BodyCount = bodyCount,
						SunCount = sunCount,
						Results = sunResults
					}.Schedule(batchCount * vesselCount * sunCount, 32, vesselHandle);
					outstandingHandle = solarHandle;

					completeWaitTimer?.Start();
					solarHandle.Complete();
					hasOutstandingHandle = false;
					completeWaitTimer?.Stop();
					jobsTimer?.Stop();
					ValidateBodyPositions(bodyPositions, batchCount * bodyCount);
					if (Settings.SubStepSimulationLogging)
						ValidateBatch(requests, orderedBodies, times, bodyPositions, vesselGeometry,
							sunResults, endUT, batchCount, vesselCount, sunCount);
					ReadBatchResults(requests, orderedBodies, times, bodyPositions, vesselGeometry,
						sunResults, sampleOffset, batchCount, vesselCount, sunCount);
				}
				if (Settings.SubStepSimulationLogging)
				{
					LastJobsMilliseconds = jobsTimer.Elapsed.TotalMilliseconds;
					LastCompleteWaitMilliseconds = completeWaitTimer.Elapsed.TotalMilliseconds;
					Lib.Log("Substeps budget: positions={0}, samples={1}, step={2:F3}s, jobs={3:F3}ms, wait={4:F3}ms",
						Lib.LogLevel.Message, LastBodyPositionEvaluations, sampleCount, sampleDuration,
						LastJobsMilliseconds, LastCompleteWaitMilliseconds);
				}
				BuildManagedResults(requests, startUT, endUT, sunCount);
			}
			catch (Exception e)
			{
				Lib.Log("Substep batch failed: {0}", Lib.LogLevel.Error, e);
				for (int i = 0; i < requests.Count; i++)
					StoreInvalid(requests[i].Vessel, startUT, endUT, e.GetType().Name);
			}
			finally
			{
				if (hasOutstandingHandle)
				{
					try
					{
						outstandingHandle.Complete();
					}
					catch (Exception completionException)
					{
						Lib.Log("Substep cleanup completion failed: {0}", Lib.LogLevel.Error, completionException);
					}
				}
				Profiler.EndSample();
			}
		}

		private static void StoreInvalid(Vessel vessel, double startUT, double endUT, string reason)
		{
			Store(SubStepIntervalResult.Invalid(vessel, startUT, endUT, reason));
		}

		private static void ValidateBodyPositions(NativeArray<double3> bodyPositions, int positionCount)
		{
			for (int i = 0; i < positionCount; i++)
				if (!math.all(math.isfinite(bodyPositions[i])))
					throw new InvalidOperationException("non-finite body geometry");
		}

		private static bool HasFiniteVesselResults(NativeArray<SubStepVesselGeometryNative> vesselGeometry,
			NativeArray<SubStepSunNative> sunResults, int vesselIndex, int vesselCount, int sampleCount, int sunCount)
		{
			for (int timeIndex = 0; timeIndex < sampleCount; timeIndex++)
			{
				int sampleVesselIndex = timeIndex * vesselCount + vesselIndex;
				SubStepVesselGeometryNative geometry = vesselGeometry[sampleVesselIndex];
				if (!math.all(math.isfinite(geometry.Position)) || !IsFinite(geometry.Latitude)
					|| !IsFinite(geometry.Longitude) || !IsFinite(geometry.Altitude))
					return false;
				for (int sunIndex = 0; sunIndex < sunCount; sunIndex++)
				{
					SubStepSunNative sun = sunResults[sampleVesselIndex * sunCount + sunIndex];
					if (!math.all(math.isfinite(sun.Direction)) || !IsFinite(sun.CenterDistance) || !IsFinite(sun.RawFlux))
						return false;
				}
			}
			return true;
		}

		private static bool IsFinite(double value)
		{
			return !double.IsNaN(value) && !double.IsInfinity(value);
		}

		private static void EnsureCapacity<T>(ref NativeArray<T> buffer, int length) where T : struct
		{
			if (buffer.IsCreated && buffer.Length >= length)
				return;
			int capacity = buffer.IsCreated ? Math.Max(length, buffer.Length * 2) : length;
			if (buffer.IsCreated)
				buffer.Dispose();
			buffer = new NativeArray<T>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
		}

		private static void ValidateBatch(List<VesselRequest> requests, List<CelestialBody> bodies,
			NativeArray<SubStepTimeNative> times, NativeArray<double3> bodyPositions,
			NativeArray<SubStepVesselGeometryNative> vesselGeometry, NativeArray<SubStepSunNative> sunResults,
			double endUT, int sampleCount, int vesselCount, int sunCount)
		{
			double durationSum = 0.0;
			for (int i = 0; i < sampleCount; i++)
				durationSum += times[i].Duration;

			int[] checkSamples = sampleCount == 1
				? new[] { 0 }
				: new[] { 0, sampleCount / 2, sampleCount - 1 };
			double maxBodyError = 0.0;
			double maxVesselError = 0.0;
			double maxAltitudeError = 0.0;
			double maxLatitudeError = 0.0;
			double maxLongitudeError = 0.0;
			double maxRawFluxRelativeError = 0.0;
			int visibilityMismatches = 0;

			for (int c = 0; c < checkSamples.Length; c++)
			{
				int timeIndex = checkSamples[c];
				double ut = times[timeIndex].UT;
				for (int bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
				{
					Vector3d expected = bodies[bodyIndex].orbit == null
						? bodies[bodyIndex].position
						: bodies[bodyIndex].getTruePositionAtUT(ut);
					double error = (expected - ToVector(bodyPositions[timeIndex * bodies.Count + bodyIndex])).magnitude;
					maxBodyError = Math.Max(maxBodyError, error);
				}

				for (int vesselIndex = 0; vesselIndex < vesselCount; vesselIndex++)
				{
					if (!HasFiniteVesselResults(vesselGeometry, sunResults, vesselIndex, vesselCount, sampleCount, sunCount))
						continue;
					Vessel vessel = requests[vesselIndex].Vessel;
					Vector3d expected = ReferenceVesselPosition(vessel, ut, endUT);
					SubStepVesselGeometryNative actual = vesselGeometry[timeIndex * vesselCount + vesselIndex];
					double error = (expected - ToVector(actual.Position)).magnitude;
					maxVesselError = Math.Max(maxVesselError, error);

					Vector3d expectedBodyPosition = vessel.mainBody.orbit == null
						? vessel.mainBody.position
						: vessel.mainBody.getTruePositionAtUT(ut);
					Vector3d expectedRelative = expected - expectedBodyPosition;
					double expectedAltitude = expectedRelative.magnitude - vessel.mainBody.Radius;
					maxAltitudeError = Math.Max(maxAltitudeError, Math.Abs(expectedAltitude - actual.Altitude));

					Vector3d polarAxis = vessel.mainBody.BodyFrame.Rotation.swizzle * Vector3d.up;
					double angleDegrees = (ut - endUT) * vessel.mainBody.rotPeriodRecip * 360.0;
					Vector3d relativeAtCurrentRotation = QuaternionD.AngleAxis(angleDegrees, polarAxis) * expectedRelative;
					Vector3d currentFramePosition = vessel.mainBody.position + relativeAtCurrentRotation;
					double expectedLatitude = vessel.mainBody.GetLatitude(currentFramePosition);
					double expectedLongitude = vessel.mainBody.GetLongitude(currentFramePosition);
					maxLatitudeError = Math.Max(maxLatitudeError, Math.Abs(expectedLatitude - actual.Latitude));
					maxLongitudeError = Math.Max(maxLongitudeError,
						Math.Abs(NormalizeDegrees(expectedLongitude - actual.Longitude)));

					for (int sunIndex = 0; sunIndex < sunCount; sunIndex++)
					{
						bool expectedVisible = ReferenceSunVisible(
							vessel, expected, Sim.suns[sunIndex].body, bodies, ut);
						int sampleVesselIndex = timeIndex * vesselCount + vesselIndex;
						SubStepSunNative actualSun = sunResults[sampleVesselIndex * sunCount + sunIndex];
						bool actualVisible = actualSun.Visible != 0;
						if (expectedVisible != actualVisible)
							visibilityMismatches++;
						CelestialBody sunBody = Sim.suns[sunIndex].body;
						Vector3d sunPosition = sunBody.orbit == null
							? sunBody.position
							: sunBody.getTruePositionAtUT(ut);
						double expectedRawFlux = Sim.suns[sunIndex].SolarFlux(
							(sunPosition - expected).magnitude, false);
						double relativeFluxError = Math.Abs(actualSun.RawFlux - expectedRawFlux)
							/ Math.Max(expectedRawFlux, 1e-12);
						maxRawFluxRelativeError = Math.Max(maxRawFluxRelativeError, relativeFluxError);
					}
				}
			}

			Lib.Log("Substeps validation: samples={0}, vessels={1}, duration={2:F6}, bodyError={3:F3}m, vesselError={4:F3}m, altitudeError={5:F3}m, latError={6:E3}deg, lonError={7:E3}deg, rawFluxRelativeError={8:E3}, visibilityMismatch={9}",
				Lib.LogLevel.Message, sampleCount, vesselCount, durationSum, maxBodyError, maxVesselError,
				maxAltitudeError, maxLatitudeError, maxLongitudeError, maxRawFluxRelativeError,
				visibilityMismatches);
		}

		private static Vector3d ReferenceVesselPosition(Vessel vessel, double ut, double endUT)
		{
			if (!Lib.Landed(vessel) && vessel.orbit != null)
				return vessel.orbit.getTruePositionAtUT(ut);

			CelestialBody body = vessel.mainBody;
			Vector3d bodyPosition = body.orbit == null ? body.position : body.getTruePositionAtUT(ut);
			Vector3d surface = body.GetRelSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude);
			surface = (surface.x * body.BodyFrame.X + surface.z * body.BodyFrame.Y
				+ surface.y * body.BodyFrame.Z).xzy;
			Vector3d polarAxis = body.BodyFrame.Rotation.swizzle * Vector3d.up;
			double angleDegrees = body.rotPeriodRecip * (endUT - ut) * 360.0;
			return bodyPosition + QuaternionD.AngleAxis(angleDegrees, polarAxis) * surface;
		}

		private static bool ReferenceSunVisible(Vessel vessel, Vector3d vesselPosition,
			CelestialBody sun, List<CelestialBody> bodies, double ut)
		{
			Vector3d sunPosition = sun.orbit == null ? sun.position : sun.getTruePositionAtUT(ut);
			Vector3d toSun = sunPosition - vesselPosition;
			double centerDistance = toSun.magnitude;
			if (centerDistance <= 0.0)
				return false;
			Vector3d direction = toSun / centerDistance;
			double rayLength = Math.Max(0.0, centerDistance - sun.Radius);

			for (int i = 0; i < bodies.Count; i++)
			{
				CelestialBody body = bodies[i];
				if (body == sun || body.Radius <= 0.0)
					continue;
				Vector3d bodyPosition = body.orbit == null ? body.position : body.getTruePositionAtUT(ut);
				Vector3d toBody = bodyPosition - vesselPosition;
				if (Lib.Landed(vessel) && body == vessel.mainBody && body.Radius < 100000.0)
				{
					double horizonDot = Vector3d.Dot(toBody.normalized, direction);
					if (horizonDot > 0.01)
						return false;
					continue;
				}
				double projection = Vector3d.Dot(toBody, direction);
				if (projection > 0.0 && projection < rayLength
					&& (direction * projection - toBody).sqrMagnitude < body.Radius * body.Radius)
					return false;
			}
			return true;
		}

		private static double NormalizeDegrees(double degrees)
		{
			degrees %= 360.0;
			if (degrees > 180.0) degrees -= 360.0;
			if (degrees < -180.0) degrees += 360.0;
			return degrees;
		}

		private static void ReadBatchResults(List<VesselRequest> requests, List<CelestialBody> bodies,
			NativeArray<SubStepTimeNative> times,
			NativeArray<double3> bodyPositions, NativeArray<SubStepVesselGeometryNative> vesselGeometry,
			NativeArray<SubStepSunNative> sunResults, int sampleOffset,
			int sampleCount, int vesselCount, int sunCount)
		{
			for (int vesselIndex = 0; vesselIndex < vesselCount; vesselIndex++)
			{
				VesselRequest request = requests[vesselIndex];
				if (request.Samples == null)
					continue;
				if (!HasFiniteVesselResults(vesselGeometry, sunResults, vesselIndex, vesselCount, sampleCount, sunCount))
				{
					// Body positions are shared, but a failed vessel conic must not
					// discard the other vessels' completed intervals.
					request.Samples = null;
					continue;
				}
				int mainBodySlot = request.Snapshot.MainBody;
				CelestialBody mainBody = bodies[mainBodySlot];
				for (int timeIndex = 0; timeIndex < sampleCount; timeIndex++)
				{
					int sampleVesselIndex = timeIndex * vesselCount + vesselIndex;
					SubStepVesselGeometryNative geometry = vesselGeometry[sampleVesselIndex];
					SubStepTimeNative time = times[timeIndex];
					request.Samples[sampleOffset + timeIndex] = new SubStepGeometrySample(time.UT, time.Duration,
						ToVector(geometry.Position), geometry.Latitude, geometry.Longitude, geometry.Altitude);
					double3 radial = geometry.Position - bodyPositions[timeIndex * bodies.Count + mainBodySlot];
					for (int sunIndex = 0; sunIndex < sunCount; sunIndex++)
					{
						SubStepSunNative nativeSun = sunResults[sampleVesselIndex * sunCount + sunIndex];
						double atmoFactor = AtmosphereFactor(mainBody, geometry.Altitude, radial, nativeSun.Direction);
						request.SunSamples[sunIndex][sampleOffset + timeIndex] = new SubStepSunSample(
							time.UT, time.Duration, ToVector(nativeSun.Direction), nativeSun.CenterDistance,
							nativeSun.Visible != 0, atmoFactor, nativeSun.RawFlux);
					}
				}
			}
		}

		private static void BuildManagedResults(List<VesselRequest> requests, double startUT, double endUT, int sunCount)
		{
			double elapsed = endUT - startUT;
			for (int vesselIndex = 0; vesselIndex < requests.Count; vesselIndex++)
			{
				VesselRequest request = requests[vesselIndex];
				Vessel vessel = request.Vessel;
				if (request.Samples == null)
				{
					StoreInvalid(vessel, startUT, endUT, "non-finite vessel or solar geometry");
					continue;
				}
				Dictionary<int, SubStepSunResult> suns = new Dictionary<int, SubStepSunResult>(sunCount);
				Vector3d endpointPosition = Lib.VesselPosition(vessel);

				for (int sunIndex = 0; sunIndex < sunCount; sunIndex++)
				{
					Sim.SunData sunData = Sim.suns[sunIndex];
					double visibleDuration = 0.0;
					double atmoDuration = 0.0;
					double integratedRawFlux = 0.0;
					double integratedFlux = 0.0;
					SubStepSunSample[] starSamples = request.SunSamples[sunIndex];

					for (int timeIndex = 0; timeIndex < starSamples.Length; timeIndex++)
					{
						SubStepSunSample sample = starSamples[timeIndex];
						atmoDuration += sample.Duration * sample.AtmosphericFactor;
						integratedRawFlux += sample.Duration * sample.RawFlux;
						integratedFlux += sample.Duration * sample.DirectFlux;
						if (sample.Visible)
							visibleDuration += sample.Duration;
					}

					Lib.DirectionAndDistance(endpointPosition, sunData.body, out Vector3d direction, out double distance);
					suns.Add(sunData.bodyIndex, new SubStepSunResult
					{
						BodyIndex = sunData.bodyIndex,
						EndpointDirection = direction,
						EndpointDistance = distance,
						SunlightFactor = visibleDuration / elapsed,
						AtmosphericFactor = atmoDuration / elapsed,
						AverageRawFlux = integratedRawFlux / elapsed,
						AverageDirectFlux = integratedFlux / elapsed,
						Samples = Array.AsReadOnly(starSamples)
					});
				}

				SubStepIntervalResult result = new SubStepIntervalResult(
					vessel.id, startUT, endUT, request.Samples, suns, null);
				Store(result);
			}
		}

		private static double AtmosphereFactor(CelestialBody body, double altitude, double3 radial, double3 sunDirection)
		{
			if (!body.atmosphere || altitude >= body.atmosphereDepth)
				return 1.0;

			altitude = Math.Abs(altitude);
			double pressure = body.GetPressure(altitude);
			if (pressure <= 0.0)
				return 1.0;

			double density = body.GetDensity(pressure, body.GetTemperature(altitude));
			double radialLength = math.length(radial);
			double cosine = radialLength > 0.0 ? math.dot(radial / radialLength, sunDirection) : 0.0;
			body.GetSolarAtmosphericEffects(cosine, density, out _, out double fluxFactor);
			return fluxFactor;
		}

		private static bool TryCreateVesselSnapshot(Vessel vessel, Dictionary<CelestialBody, int> bodySlots,
			double startUT, double endUT, out SubStepVesselNative snapshot, out string reason)
		{
			snapshot = default;
			reason = null;
			if (vessel == null || vessel.mainBody == null || !bodySlots.TryGetValue(vessel.mainBody, out int mainBody))
			{
				reason = "missing vessel main body";
				return false;
			}

			snapshot.MainBody = mainBody;
			if (Lib.Landed(vessel))
			{
				snapshot.Trajectory = 2;
				snapshot.Latitude = vessel.latitude;
				snapshot.Longitude = vessel.longitude;
				snapshot.Altitude = vessel.altitude;
				return true;
			}

			Orbit orbit = vessel.orbitDriver?.orbit;
			if (orbit == null || orbit.referenceBody != vessel.mainBody || double.IsNaN(orbit.inclination))
			{
				reason = "unsupported or discontinuous vessel orbit";
				return false;
			}
			if ((orbit.StartUT > 0.0 && startUT < orbit.StartUT - TimeMatchTolerance)
				|| (!double.IsInfinity(orbit.EndUT) && orbit.EndUT > 0.0
					&& endUT > orbit.EndUT + TimeMatchTolerance))
			{
				reason = "interval crosses vessel orbit patch";
				return false;
			}

			snapshot.Trajectory = 1;
			snapshot.Orbit = CreateOrbitSnapshot(orbit);
			if (snapshot.Orbit.IsValid == 0)
				reason = "unsupported vessel conic";
			return snapshot.Orbit.IsValid != 0;
		}

		private static SubStepBodyNative CreateBodySnapshot(CelestialBody body,
			Dictionary<CelestialBody, int> bodySlots, double referenceUT)
		{
			int parent = -1;
			Orbit orbit = body.orbitDriver?.orbit;
			if (orbit != null && orbit.referenceBody != null && orbit.referenceBody != body)
			{
				parent = bodySlots[orbit.referenceBody];
			}

			Vector3d north = (body.GetWorldSurfacePosition(90.0, 0.0, 0.0) - body.position).normalized;
			Vector3d prime = (body.GetWorldSurfacePosition(0.0, 0.0, 0.0) - body.position).normalized;
			Vector3d east = (body.GetWorldSurfacePosition(0.0, 90.0, 0.0) - body.position).normalized;

			return new SubStepBodyNative
			{
				ParentBody = parent,
				Orbit = parent >= 0 ? CreateOrbitSnapshot(orbit) : default,
				Radius = body.Radius,
				AngularVelocity = 2.0 * Math.PI * body.rotPeriodRecip,
				ReferenceUT = referenceUT,
				RootPosition = ToDouble3(body.position),
				North = ToDouble3(north),
				PrimeMeridian = ToDouble3(prime),
				East90 = ToDouble3(east)
			};
		}

		private static SubStepOrbitNative CreateOrbitSnapshot(Orbit orbit)
		{
			if (orbit == null || !IsFinite(orbit.eccentricity) || orbit.eccentricity < 0.0
				|| Math.Abs(orbit.eccentricity - 1.0) < 1e-8
				|| !IsFinite(orbit.meanMotion) || !IsFinite(orbit.semiLatusRectum)
				|| orbit.semiLatusRectum <= 0.0)
				return default;

			Vector3d basisX = Planetarium.Zup.WorldToLocal(orbit.OrbitFrame.X).xzy;
			Vector3d basisY = Planetarium.Zup.WorldToLocal(orbit.OrbitFrame.Y).xzy;
			return new SubStepOrbitNative
			{
				IsValid = 1,
				Eccentricity = orbit.eccentricity,
				MeanMotion = orbit.meanMotion,
				Epoch = orbit.epoch,
				ObTAtEpoch = orbit.ObTAtEpoch,
				Period = orbit.period,
				SemiLatusRectum = orbit.semiLatusRectum,
				BasisX = ToDouble3(basisX),
				BasisY = ToDouble3(basisY)
			};
		}

		private static List<CelestialBody> BuildBodyOrder(out string failureReason)
		{
			failureReason = null;
			if (FlightGlobals.Bodies == null || FlightGlobals.Bodies.Count == 0)
			{
				failureReason = "no celestial bodies";
				return null;
			}
			List<CelestialBody> result = new List<CelestialBody>(FlightGlobals.Bodies.Count);
			HashSet<CelestialBody> added = new HashSet<CelestialBody>();
			while (result.Count < FlightGlobals.Bodies.Count)
			{
				bool madeProgress = false;
				for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
				{
					CelestialBody body = FlightGlobals.Bodies[i];
					if (added.Contains(body))
						continue;
					CelestialBody parent = body.orbitDriver?.orbit?.referenceBody;
					if (parent == null || parent == body || added.Contains(parent))
					{
						added.Add(body);
						result.Add(body);
						madeProgress = true;
					}
				}

				if (!madeProgress)
				{
					failureReason = "cyclic or missing body hierarchy";
					return null;
				}
			}
			return result;
		}

		private static double3 ToDouble3(Vector3d value)
		{
			return new double3(value.x, value.y, value.z);
		}

		private static Vector3d ToVector(double3 value)
		{
			return new Vector3d(value.x, value.y, value.z);
		}
	}
}
