using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// Tracks SCANsat coverage converted into Kerbalism science. SCANsat coverage is global
	/// per body/sensor, so the observation watermark and unstored data must be global too.
	/// </summary>
	internal static class ScanCoverageStore
	{
		private struct Key : IEquatable<Key>
		{
			public readonly int BodyIndex;
			public readonly string ExperimentType;
			public readonly int SensorType;

			public Key(int bodyIndex, string experimentType, int sensorType)
			{
				BodyIndex = bodyIndex;
				ExperimentType = experimentType ?? string.Empty;
				SensorType = sensorType;
			}

			public bool Equals(Key other) =>
				BodyIndex == other.BodyIndex
				&& SensorType == other.SensorType
				&& string.Equals(ExperimentType, other.ExperimentType, StringComparison.Ordinal);

			public override bool Equals(object obj) => obj is Key other && Equals(other);
			public override int GetHashCode() =>
				((BodyIndex * 397) ^ SensorType) * 397 ^ ExperimentType.GetHashCode();
		}

		private sealed class Entry
		{
			public bool HasObservedCoverage;
			public double ObservedCoverage;
			public double PendingSize;
		}

		private static readonly Dictionary<Key, Entry> entries = new Dictionary<Key, Entry>();

		/// <summary>
		/// Observe the global SCANsat coverage and enqueue its growth exactly once. The first
		/// observation establishes a baseline, so upgrading an existing save doesn't award
		/// historical coverage.
		/// </summary>
		public static void ObserveCoverage(int bodyIndex, string experimentType, int sensorType,
			double currentCoverage, double dataSize)
		{
			if (bodyIndex < 0 || sensorType == 0 || string.IsNullOrEmpty(experimentType)
				|| double.IsNaN(currentCoverage) || double.IsInfinity(currentCoverage) || currentCoverage < 0.0)
				return;

			currentCoverage = Lib.Clamp(currentCoverage, 0.0, 100.0);
			Key key = new Key(bodyIndex, experimentType, sensorType);
			if (!entries.TryGetValue(key, out Entry entry))
			{
				entry = new Entry();
				entries.Add(key, entry);
			}

			if (!entry.HasObservedCoverage)
			{
				entry.HasObservedCoverage = true;
				entry.ObservedCoverage = currentCoverage;
				return;
			}

			double coverageDelta = currentCoverage - entry.ObservedCoverage;
			if (coverageDelta > double.Epsilon)
			{
				if (!double.IsNaN(dataSize) && !double.IsInfinity(dataSize) && dataSize > double.Epsilon)
					entry.PendingSize += dataSize * coverageDelta / 100.0;
				entry.ObservedCoverage = currentCoverage;
			}
		}

		public static double PendingSize(int bodyIndex, string experimentType, int sensorType)
		{
			if (bodyIndex < 0 || sensorType == 0 || string.IsNullOrEmpty(experimentType))
				return 0.0;
			return entries.TryGetValue(new Key(bodyIndex, experimentType, sensorType), out Entry entry)
				? Math.Max(0.0, entry.PendingSize)
				: 0.0;
		}

		/// <summary>
		/// Add already-generated data from a pre-global-ledger save. This deliberately doesn't
		/// initialize or advance the coverage watermark.
		/// </summary>
		public static void AddPendingSize(int bodyIndex, string experimentType, int sensorType, double size)
		{
			if (bodyIndex < 0 || sensorType == 0 || string.IsNullOrEmpty(experimentType)
				|| double.IsNaN(size) || double.IsInfinity(size) || size <= double.Epsilon)
				return;

			Key key = new Key(bodyIndex, experimentType, sensorType);
			if (!entries.TryGetValue(key, out Entry entry))
			{
				entry = new Entry();
				entries.Add(key, entry);
			}
			entry.PendingSize += size;
		}

		public static void CommitStoredSize(int bodyIndex, string experimentType, int sensorType, double storedSize)
		{
			if (bodyIndex < 0 || sensorType == 0 || string.IsNullOrEmpty(experimentType)
				|| double.IsNaN(storedSize) || double.IsInfinity(storedSize) || storedSize <= double.Epsilon)
				return;

			if (entries.TryGetValue(new Key(bodyIndex, experimentType, sensorType), out Entry entry))
				entry.PendingSize = Math.Max(0.0, entry.PendingSize - storedSize);
		}

		public static void SaveGlobal(ConfigNode node)
		{
			ConfigNode root = null;
			foreach (KeyValuePair<Key, Entry> kv in entries)
			{
				Entry state = kv.Value;
				if (!state.HasObservedCoverage && state.PendingSize <= double.Epsilon)
					continue;

				if (root == null)
					root = node.AddNode("scan_global");

				ConfigNode entry = root.AddNode("entry");
				entry.AddValue("bodyIndex", kv.Key.BodyIndex);
				entry.AddValue("experimentType", kv.Key.ExperimentType);
				entry.AddValue("sensorType", kv.Key.SensorType);
				entry.AddValue("initialized", state.HasObservedCoverage);
				if (state.HasObservedCoverage)
					entry.AddValue("observed", state.ObservedCoverage);
				if (state.PendingSize > double.Epsilon)
					entry.AddValue("pending", state.PendingSize);
			}
		}

		public static void LoadGlobal(ConfigNode node)
		{
			entries.Clear();
			ConfigNode root = node.GetNode("scan_global");
			if (root == null)
				return;

			foreach (ConfigNode entryNode in root.GetNodes("entry"))
			{
				int bodyIndex = Lib.ConfigValue(entryNode, "bodyIndex", -1);
				string experimentType = Lib.ConfigValue(entryNode, "experimentType", string.Empty);
				int sensorType = Lib.ConfigValue(
					entryNode,
					"sensorType",
					SCANsat.ScienceSensorType(experimentType));
				if (bodyIndex < 0 || sensorType == 0 || string.IsNullOrEmpty(experimentType))
					continue;

				bool initialized = Lib.ConfigValue(entryNode, "initialized", entryNode.HasValue("observed"));
				double observed = Lib.Clamp(Lib.ConfigValue(entryNode, "observed", 0.0), 0.0, 100.0);
				double pending = Math.Max(0.0, Lib.ConfigValue(entryNode, "pending", 0.0));
				if (double.IsNaN(observed) || double.IsInfinity(observed))
					observed = 0.0;
				if (double.IsNaN(pending) || double.IsInfinity(pending))
					pending = 0.0;
				entries[new Key(bodyIndex, experimentType, sensorType)] = new Entry
				{
					HasObservedCoverage = initialized,
					ObservedCoverage = observed,
					PendingSize = pending
				};
			}
		}
	}
}
