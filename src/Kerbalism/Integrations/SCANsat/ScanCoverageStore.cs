using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace KERBALISM
{
	/// <summary>
	/// Per-vessel pending SCANsat coverage that has been scanned but not yet stored in a File.
	/// </summary>
	internal static class ScanCoverageStore
	{
		private struct CaptureState
		{
			public short SensorMask;
			public double Factor;
		}

		private struct Key : IEquatable<Key>
		{
			public readonly Guid VesselId;
			public readonly int BodyIndex;

			public Key(Guid vesselId, int bodyIndex)
			{
				VesselId = vesselId;
				BodyIndex = bodyIndex;
			}

			public bool Equals(Key other) => VesselId.Equals(other.VesselId) && BodyIndex == other.BodyIndex;
			public override bool Equals(object obj) => obj is Key other && Equals(other);
			public override int GetHashCode() => VesselId.GetHashCode() * 397 ^ BodyIndex;
		}

		private static readonly Dictionary<Key, Int16[,]> pending = new Dictionary<Key, Int16[,]>();
		private static readonly Dictionary<Guid, short> divertMaskCache = new Dictionary<Guid, short>();
		private static readonly Dictionary<Guid, Dictionary<uint, CaptureState>> captureStates
			= new Dictionary<Guid, Dictionary<uint, CaptureState>>();
		private static readonly Dictionary<int, Int16[,]> deferredApplications
			= new Dictionary<int, Int16[,]>();
		private static int divertMaskFrame = -1;

		private static FieldInfo coverageField;
		private static MethodInfo updateCoverageMethod;
		private static MethodInfo getDataMethod;
		private static bool applyReady;
		private static bool applyFailed;

		public static void ClearVessel(Guid vesselId)
		{
			var remove = new List<Key>();
			foreach (var kv in pending)
			{
				if (kv.Key.VesselId == vesselId)
					remove.Add(kv.Key);
			}

			foreach (var key in remove)
				pending.Remove(key);

			divertMaskCache.Remove(vesselId);
			captureStates.Remove(vesselId);
		}

		public static Int16[,] GetPending(Guid vesselId, int bodyIndex, bool create)
		{
			var key = new Key(vesselId, bodyIndex);
			if (pending.TryGetValue(key, out Int16[,] grid))
				return grid;

			if (!create)
				return null;

			grid = ScanGrid.Create();
			pending[key] = grid;
			return grid;
		}

		public static double PendingCoveragePercent(Guid vesselId, int bodyIndex, short sensorMask)
		{
			Int16[,] grid = GetPending(vesselId, bodyIndex, false);
			return ScanGrid.CoveragePercent(grid, sensorMask);
		}

		public static List<int> GetPendingBodyIndices(Guid vesselId)
		{
			var bodyIndices = new List<int>();
			foreach (var kv in pending)
			{
				if (kv.Key.VesselId == vesselId && !ScanGrid.IsEmpty(kv.Value))
					bodyIndices.Add(kv.Key.BodyIndex);
			}
			return bodyIndices;
		}

		public static Int16[,] TakePending(Guid vesselId, int bodyIndex, short sensorMask)
		{
			var key = new Key(vesselId, bodyIndex);
			if (!pending.TryGetValue(key, out Int16[,] source))
				return null;

			Int16[,] payload = ScanGrid.ExtractMask(source, sensorMask);
			if (ScanGrid.IsEmpty(payload))
				return null;

			ScanGrid.ClearMask(source, sensorMask);
			if (ScanGrid.IsEmpty(source))
				pending.Remove(key);
			return payload;
		}

		/// <summary>
		/// Coverage already owned by this vessel for a body: pending cells plus scan payloads
		/// stored in science files. Used to ignore SCANsat re-paints of the same cells while
		/// the live map is kept blank until transmit/recovery.
		/// </summary>
		public static Int16[,] GetClaimedCoverage(Vessel vessel, int bodyIndex)
		{
			if (vessel == null)
				return null;

			Int16[,] claimed = null;
			Int16[,] pend = GetPending(vessel.id, bodyIndex, false);
			if (!ScanGrid.IsEmpty(pend))
				claimed = ScanGrid.Clone(pend);

			if (!vessel.TryGetVesselDataTemp(out VesselData vd))
				return claimed;

			OrStoredScanCoverage(vd, bodyIndex, ref claimed);
			return claimed;
		}

		public static Int16[,] GetStoredScanCoverage(VesselData vd, int bodyIndex)
		{
			Int16[,] stored = null;
			OrStoredScanCoverage(vd, bodyIndex, ref stored);
			return stored;
		}

		private static void OrStoredScanCoverage(VesselData vd, int bodyIndex, ref Int16[,] target)
		{
			if (vd == null)
				return;

			foreach (Drive drive in Drive.GetDrives(vd, true))
			{
				foreach (File file in drive.files.Values)
				{
					if (!file.HasScanPayload)
						continue;

					int fileBody = file.scanBodyIndex;
					if (fileBody < 0 && file.subjectData?.Situation?.Body != null)
						fileBody = file.subjectData.Situation.Body.flightGlobalsIndex;
					if (fileBody != bodyIndex)
						continue;

					if (target == null)
						target = ScanGrid.Create();
					ScanGrid.Or(target, file.ScanCoverage);
				}
			}
		}

		/// <summary>
		/// Drop pending cells that are already present in stored scan files, so flushes cannot
		/// inflate file size by recounting the same ground track.
		/// </summary>
		public static void StripStoredFromPending(VesselData vd, Guid vesselId, int bodyIndex, short sensorMask)
		{
			Int16[,] pend = GetPending(vesselId, bodyIndex, false);
			if (ScanGrid.IsEmpty(pend) || sensorMask == 0)
				return;

			Int16[,] stored = GetStoredScanCoverage(vd, bodyIndex);
			if (ScanGrid.IsEmpty(stored))
				return;

			for (int x = 0; x < ScanGrid.Width; x++)
			{
				for (int y = 0; y < ScanGrid.Height; y++)
				{
					short dup = (short)(pend[x, y] & stored[x, y] & sensorMask);
					if (dup != 0)
						pend[x, y] = (short)(pend[x, y] & ~dup);
				}
			}

			if (ScanGrid.IsEmpty(pend))
				pending.Remove(new Key(vesselId, bodyIndex));
		}

		/// <summary>
		/// Shrink oversized scan files so size matches unique coverage weight (fixes saves that
		/// accumulated duplicates before re-scan filtering).
		/// </summary>
		public static void ReconcileScanFileSize(VesselData vd, SubjectData subject, short sensorMask)
		{
			if (vd == null || subject?.ExpInfo == null || sensorMask == 0)
				return;

			double dataSize = subject.ExpInfo.DataSize;
			if (dataSize <= double.Epsilon)
				return;

			foreach (Drive drive in Drive.GetDrives(vd, true))
			{
				if (!drive.files.TryGetValue(subject, out File file) || !file.HasScanPayload)
					continue;

				// Unique coverage cannot exceed a full-sphere file; skip the common no-op path.
				if (file.size <= dataSize + 1e-3)
					continue;

				double expected = dataSize * ScanGrid.AreaWeight(file.ScanCoverage, sensorMask)
					/ (ScanGrid.FullSphereWeight * Math.Max(1, ScanGrid.CountBits(sensorMask)));
				if (expected < 0.0)
					expected = 0.0;
				if (expected > dataSize)
					expected = dataSize;

				if (file.size <= expected + 1e-6)
					continue;

				double excess = file.size - expected;
				file.size = expected;
				subject.RemoveDataCollectedInFlight(excess);
			}
		}

		public static void QueueRemainingRecoveredPending(Guid vesselId)
		{
			var vesselKeys = new List<Key>();
			foreach (var kv in pending)
			{
				if (kv.Key.VesselId == vesselId)
					vesselKeys.Add(kv.Key);
			}

			foreach (Key key in vesselKeys)
			{
				Int16[,] payload = pending[key];
				if (!ScanGrid.IsEmpty(payload))
				{
					if (!deferredApplications.TryGetValue(key.BodyIndex, out Int16[,] queued))
					{
						queued = ScanGrid.Create();
						deferredApplications[key.BodyIndex] = queued;
					}
					ScanGrid.Or(queued, payload);
				}
				pending.Remove(key);
			}

			captureStates.Remove(vesselId);
			InvalidateDivertMask(vesselId);
		}

		public static short GetDivertMask(Vessel vessel)
		{
			if (vessel == null || !Features.Science)
				return 0;

			if (divertMaskFrame != UnityEngine.Time.frameCount)
			{
				divertMaskCache.Clear();
				divertMaskFrame = UnityEngine.Time.frameCount;
			}

			if (divertMaskCache.TryGetValue(vessel.id, out short cached))
				return cached;

			short mask = 0;
			if (vessel.loaded)
			{
				foreach (Part part in vessel.Parts)
				{
					bool scanning = false;
					foreach (PartModule module in part.Modules)
					{
						if (module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
						{
							scanning = Lib.ReflectionValue<bool>(module, "scanning");
							break;
						}
					}

					if (!scanning)
						continue;

					foreach (PartModule module in part.Modules)
					{
						if (module is KerbalismScansat scansat && scansat.SensorType != 0)
							mask |= (short)scansat.SensorType;
					}
				}
			}
			else if (vessel.protoVessel != null)
			{
				foreach (ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
				{
					bool scanning = false;
					foreach (ProtoPartModuleSnapshot module in part.modules)
					{
						if (module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
						{
							scanning = Lib.Proto.GetBool(module, "scanning");
							break;
						}
					}

					if (!scanning)
						continue;

					foreach (ProtoPartModuleSnapshot module in part.modules)
					{
						if (module.moduleName != "KerbalismScansat")
							continue;

						int sensorType = (int)Lib.Proto.GetUInt(module, "sensorType");
						mask |= (short)sensorType;
					}
				}
			}

			divertMaskCache[vessel.id] = mask;
			return mask;
		}

		public static void SetCaptureState(Guid vesselId, uint partId, short sensorMask, double factor)
		{
			if (!captureStates.TryGetValue(vesselId, out Dictionary<uint, CaptureState> vesselStates))
			{
				vesselStates = new Dictionary<uint, CaptureState>();
				captureStates[vesselId] = vesselStates;
			}

			vesselStates[partId] = new CaptureState
			{
				SensorMask = sensorMask,
				Factor = Lib.Clamp(factor, 0.0, 1.0)
			};
		}

		public static void RemoveCaptureState(Guid vesselId, uint partId)
		{
			if (captureStates.TryGetValue(vesselId, out Dictionary<uint, CaptureState> vesselStates))
			{
				vesselStates.Remove(partId);
				if (vesselStates.Count == 0)
					captureStates.Remove(vesselId);
			}
		}

		public static short GetCaptureMask(Guid vesselId, int bodyIndex, int x, int y, short addedMask)
		{
			if (addedMask == 0
				|| !captureStates.TryGetValue(vesselId, out Dictionary<uint, CaptureState> vesselStates))
				return 0;

			short accepted = 0;
			for (int bitIndex = 0; bitIndex < 16; bitIndex++)
			{
				short bit = (short)(1 << bitIndex);
				if ((addedMask & bit) == 0)
					continue;

				double factor = 0.0;
				foreach (CaptureState state in vesselStates.Values)
				{
					if ((state.SensorMask & bit) != 0)
						factor = Math.Max(factor, state.Factor);
				}

				if (factor >= 1.0 || (factor > 0.0 && CaptureSample(vesselId, bodyIndex, x, y, bitIndex, factor)))
					accepted |= bit;
			}

			return accepted;
		}

		private static bool CaptureSample(Guid vesselId, int bodyIndex, int x, int y, int bitIndex, double factor)
		{
			unchecked
			{
				int hash = vesselId.GetHashCode();
				hash = hash * 397 ^ bodyIndex;
				hash = hash * 397 ^ x;
				hash = hash * 397 ^ y;
				hash = hash * 397 ^ bitIndex;
				hash = hash * 397 ^ (int)Math.Floor(Planetarium.GetUniversalTime());
				uint value = (uint)hash;
				return value / (double)uint.MaxValue < factor;
			}
		}

		public static void InvalidateDivertMask(Guid vesselId)
		{
			divertMaskCache.Remove(vesselId);
		}

		public static void MergeVessel(Guid sourceVesselId, Guid destinationVesselId)
		{
			if (sourceVesselId == destinationVesselId)
				return;

			var sourceKeys = new List<Key>();
			foreach (var kv in pending)
			{
				if (kv.Key.VesselId == sourceVesselId)
					sourceKeys.Add(kv.Key);
			}

			foreach (Key sourceKey in sourceKeys)
			{
				Int16[,] sourceGrid = pending[sourceKey];
				Int16[,] destinationGrid = GetPending(destinationVesselId, sourceKey.BodyIndex, true);
				ScanGrid.Or(destinationGrid, sourceGrid);
				pending.Remove(sourceKey);
			}

			if (captureStates.TryGetValue(sourceVesselId, out Dictionary<uint, CaptureState> sourceStates))
			{
				if (!captureStates.TryGetValue(destinationVesselId, out Dictionary<uint, CaptureState> destinationStates))
				{
					destinationStates = new Dictionary<uint, CaptureState>();
					captureStates[destinationVesselId] = destinationStates;
				}

				foreach (var state in sourceStates)
					destinationStates[state.Key] = state.Value;
				captureStates.Remove(sourceVesselId);
			}
			InvalidateDivertMask(sourceVesselId);
			InvalidateDivertMask(destinationVesselId);
		}

		public static void RepartitionAfterUndock(Vessel oldVessel, Vessel newVessel)
		{
			if (oldVessel == null || newVessel == null || oldVessel.id == newVessel.id)
				return;

			short oldMask = GetOwnedSensorMask(oldVessel);
			short newMask = GetOwnedSensorMask(newVessel);
			short moveMask = (short)(newMask & ~oldMask);

			if (moveMask != 0)
			{
				var oldKeys = new List<Key>();
				foreach (var kv in pending)
				{
					if (kv.Key.VesselId == oldVessel.id)
						oldKeys.Add(kv.Key);
				}

				foreach (Key oldKey in oldKeys)
				{
					Int16[,] sourceGrid = pending[oldKey];
					Int16[,] movedGrid = ScanGrid.ExtractMask(sourceGrid, moveMask);
					if (ScanGrid.IsEmpty(movedGrid))
						continue;

					Int16[,] destinationGrid = GetPending(newVessel.id, oldKey.BodyIndex, true);
					ScanGrid.Or(destinationGrid, movedGrid);
					ScanGrid.ClearMask(sourceGrid, moveMask);
					if (ScanGrid.IsEmpty(sourceGrid))
						pending.Remove(oldKey);
				}
			}

			if (captureStates.TryGetValue(oldVessel.id, out Dictionary<uint, CaptureState> oldStates))
			{
				var newPartIds = new HashSet<uint>();
				if (newVessel.loaded)
				{
					foreach (Part part in newVessel.Parts)
						newPartIds.Add(part.flightID);
				}
				else if (newVessel.protoVessel != null)
				{
					foreach (ProtoPartSnapshot part in newVessel.protoVessel.protoPartSnapshots)
						newPartIds.Add(part.flightID);
				}

				if (!captureStates.TryGetValue(newVessel.id, out Dictionary<uint, CaptureState> newStates))
				{
					newStates = new Dictionary<uint, CaptureState>();
					captureStates[newVessel.id] = newStates;
				}

				foreach (uint partId in newPartIds)
				{
					if (oldStates.TryGetValue(partId, out CaptureState state))
					{
						newStates[partId] = state;
						oldStates.Remove(partId);
					}
				}

				if (oldStates.Count == 0)
					captureStates.Remove(oldVessel.id);
				if (newStates.Count == 0)
					captureStates.Remove(newVessel.id);
			}
			InvalidateDivertMask(oldVessel.id);
			InvalidateDivertMask(newVessel.id);
		}

		private static short GetOwnedSensorMask(Vessel vessel)
		{
			short mask = 0;
			if (vessel.loaded)
			{
				foreach (Part part in vessel.Parts)
				{
					foreach (PartModule module in part.Modules)
					{
						if (module is KerbalismScansat scansat)
							mask |= (short)scansat.SensorType;
					}
				}
			}
			else if (vessel.protoVessel != null)
			{
				foreach (ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
				{
					foreach (ProtoPartModuleSnapshot module in part.modules)
					{
						if (module.moduleName == "KerbalismScansat")
							mask |= (short)Lib.Proto.GetUInt(module, "sensorType");
					}
				}
			}

			return mask;
		}

		public static bool EnsureApplyReflection()
		{
			if (applyReady)
				return true;
			if (applyFailed)
				return false;

			try
			{
				Type dataType = AccessTools.TypeByName("SCANsat.SCAN_Data.SCANdata");
				Type utilType = AccessTools.TypeByName("SCANsat.SCANUtil");
				if (dataType == null || utilType == null)
				{
					applyFailed = true;
					return false;
				}

				coverageField = AccessTools.Field(dataType, "coverage");
				updateCoverageMethod = AccessTools.Method(dataType, "updateCoverage");
				getDataMethod = AccessTools.Method(utilType, "getData", new[] { typeof(CelestialBody) });
				if (coverageField == null || updateCoverageMethod == null || getDataMethod == null)
				{
					applyFailed = true;
					return false;
				}

				applyReady = true;
				return true;
			}
			catch (Exception e)
			{
				IntegrationUtils.LogError("SCANsat apply reflection failed: " + e);
				applyFailed = true;
				return false;
			}
		}

		public static bool ApplyPayloadToBody(CelestialBody body, Int16[,] payload)
		{
			if (body == null || ScanGrid.IsEmpty(payload) || !EnsureApplyReflection())
				return false;

			try
			{
				object data = getDataMethod.Invoke(null, new object[] { body });
				if (data == null)
					return false;

				var coverage = coverageField.GetValue(data) as Int16[,];
				if (coverage == null)
					return false;

				ScanGrid.Or(coverage, payload);
				updateCoverageMethod.Invoke(data, null);
				return true;
			}
			catch (Exception e)
			{
				IntegrationUtils.LogError("Applying deferred SCANsat coverage failed: " + e);
				return false;
			}
		}

		public static bool ApplyFilePayload(File file)
		{
			if (file == null || !file.HasScanPayload)
				return true;

			CelestialBody body = file.subjectData?.Situation?.Body;
			if (body == null && file.scanBodyIndex >= 0 && file.scanBodyIndex < FlightGlobals.Bodies.Count)
				body = FlightGlobals.Bodies[file.scanBodyIndex];

			if (!ApplyPayloadToBody(body, file.ScanCoverage))
				return false;

			file.ClearScanPayload();
			return true;
		}

		/// <summary>
		/// Recovery destroys the source vessel, so transfer a payload that can't be applied
		/// immediately into a globally persisted retry queue.
		/// </summary>
		public static void ApplyOrQueueRecoveredFilePayload(File file)
		{
			if (file == null || !file.HasScanPayload || ApplyFilePayload(file))
				return;

			int bodyIndex = file.scanBodyIndex;
			if (bodyIndex < 0 && file.subjectData?.Situation?.Body != null)
				bodyIndex = file.subjectData.Situation.Body.flightGlobalsIndex;
			if (bodyIndex < 0)
				return;

			if (!deferredApplications.TryGetValue(bodyIndex, out Int16[,] queued))
			{
				queued = ScanGrid.Create();
				deferredApplications[bodyIndex] = queued;
			}

			ScanGrid.Or(queued, file.ScanCoverage);
			file.ClearScanPayload();
		}

		public static void RetryDeferredApplications()
		{
			if (deferredApplications.Count == 0 || FlightGlobals.Bodies == null)
				return;

			var appliedBodies = new List<int>();
			foreach (var kv in deferredApplications)
			{
				if (kv.Key >= 0
					&& kv.Key < FlightGlobals.Bodies.Count
					&& ApplyPayloadToBody(FlightGlobals.Bodies[kv.Key], kv.Value))
				{
					appliedBodies.Add(kv.Key);
				}
			}

			foreach (int bodyIndex in appliedBodies)
				deferredApplications.Remove(bodyIndex);
		}

		public static void SaveGlobal(ConfigNode node)
		{
			if (deferredApplications.Count == 0)
				return;

			ConfigNode root = node.AddNode("scan_deferred_apply");
			foreach (var kv in deferredApplications)
			{
				if (ScanGrid.IsEmpty(kv.Value))
					continue;

				ConfigNode bodyNode = root.AddNode("body");
				bodyNode.AddValue("bodyIndex", kv.Key);
				bodyNode.AddValue("coverage", ScanGrid.Encode(kv.Value));
			}
		}

		public static void LoadGlobal(ConfigNode node)
		{
			pending.Clear();
			captureStates.Clear();
			divertMaskCache.Clear();
			divertMaskFrame = -1;
			deferredApplications.Clear();
			ConfigNode root = node.GetNode("scan_deferred_apply");
			if (root == null)
				return;

			foreach (ConfigNode bodyNode in root.GetNodes("body"))
			{
				int bodyIndex = Lib.ConfigValue(bodyNode, "bodyIndex", -1);
				Int16[,] grid = ScanGrid.Decode(Lib.ConfigValue(bodyNode, "coverage", string.Empty));
				if (bodyIndex >= 0 && !ScanGrid.IsEmpty(grid))
					deferredApplications[bodyIndex] = grid;
			}
		}

		public static void SaveVessel(Guid vesselId, ConfigNode node)
		{
			ConfigNode root = null;
			foreach (var kv in pending)
			{
				if (kv.Key.VesselId != vesselId || ScanGrid.IsEmpty(kv.Value))
					continue;

				if (root == null)
					root = node.AddNode("scan_pending");

				ConfigNode bodyNode = root.AddNode("body");
				bodyNode.AddValue("bodyIndex", kv.Key.BodyIndex);
				bodyNode.AddValue("coverage", ScanGrid.Encode(kv.Value));
			}
		}

		public static void LoadVessel(Guid vesselId, ConfigNode node)
		{
			ClearVessel(vesselId);

			ConfigNode root = node.GetNode("scan_pending");
			if (root == null)
				return;

			foreach (ConfigNode bodyNode in root.GetNodes("body"))
			{
				int bodyIndex = Lib.ConfigValue(bodyNode, "bodyIndex", -1);
				Int16[,] grid = ScanGrid.Decode(Lib.ConfigValue(bodyNode, "coverage", string.Empty));
				if (bodyIndex < 0 || ScanGrid.IsEmpty(grid))
					continue;

				pending[new Key(vesselId, bodyIndex)] = grid;
			}
		}
	}
}
