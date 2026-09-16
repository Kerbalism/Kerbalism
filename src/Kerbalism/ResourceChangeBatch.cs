using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Coalesces resource graph updates during explicit startup scopes (see Kerbalism#934).
	/// Outside a scope all stock setters and events remain immediate.
	/// </summary>
	public static class ResourceChangeBatch
	{
		private enum ChangeKind
		{
			FlowState,
			FlowMode,
			ResourceList
		}

		private struct PendingChange
		{
			public ChangeKind Kind;
			public PartResource Resource;
			public Part Part;
			public bool FromState;
			public bool ToState;
			public PartResource.FlowMode FromMode;
			public PartResource.FlowMode ToMode;
		}

		private sealed class BatchScope : IDisposable
		{
			private bool disposed;

			public void Dispose()
			{
				if (disposed)
					return;

				disposed = true;
				batchDepth--;
			}
		}

		private static readonly List<PendingChange> pendingChanges = new List<PendingChange>(64);
		private static readonly HashSet<Part> dirtyParts = new HashSet<Part>();
		private static readonly HashSet<PartSet> suppressedPartSets = new HashSet<PartSet>();
		private static FieldInfo flowModeField;
		private static bool flowModeFieldResolved;
		private static int batchDepth;
		private static bool mutatedThisCycle;
		private static bool runnerActive;
		private static bool replaying;
		private static ResourceChangeBatchRunner runner;

		/// <summary>
		/// True only while queued events are being replayed. Harmony prefixes use this
		/// to skip redundant stock graph updates without hiding events from other listeners.
		/// </summary>
		internal static bool IsReplaying => replaying;

		/// <summary>
		/// Suppress only PartSets that will be explicitly rebuilt after replay.
		/// Editor/UI/custom PartSets continue processing the original events.
		/// </summary>
		internal static bool ShouldSuppressPartSet(PartSet partSet)
		{
			return replaying && partSet != null && suppressedPartSets.Contains(partSet);
		}

		/// <summary> Begin an explicit startup batch. Scopes may be nested. </summary>
		public static IDisposable Begin()
		{
			batchDepth++;
			return new BatchScope();
		}

		public static void SetFlowState(PartResource resource, bool flowState)
		{
			if (resource == null)
				return;

			if (!IsCollecting)
			{
				resource.flowState = flowState;
				return;
			}

			bool previous = resource._flowState;
			if (previous == flowState)
				return;

			resource._flowState = flowState;
			SyncSimulationResourceFlowState(resource, flowState);
			Queue(new PendingChange
			{
				Kind = ChangeKind.FlowState,
				Resource = resource,
				Part = resource.part,
				FromState = previous,
				ToState = flowState
			});
		}

		public static void SetFlowMode(PartResource resource, PartResource.FlowMode flowMode)
		{
			if (resource == null)
				return;

			if (!IsCollecting)
			{
				resource.flowMode = flowMode;
				return;
			}

			if (TryGetFlowModeField(out FieldInfo field))
			{
				object current = field.GetValue(resource);
				if (current is PartResource.FlowMode currentMode && currentMode == flowMode)
					return;

				field.SetValue(resource, flowMode);
				SyncSimulationResourceFlowMode(resource, field, flowMode);
				Queue(new PendingChange
				{
					Kind = ChangeKind.FlowMode,
					Resource = resource,
					Part = resource.part,
					FromMode = current is PartResource.FlowMode previousMode ? previousMode : resource.flowMode,
					ToMode = flowMode
				});
				return;
			}

			// Compatibility fallback for an unknown KSP version.
			if (resource.flowMode != flowMode)
				resource.flowMode = flowMode;
		}

		private static void SyncSimulationResourceFlowState(PartResource resource, bool flowState)
		{
			if (resource.simulationResource || resource.part == null)
				return;

			PartResource sim = resource.part.SimulationResources?[resource.resourceName];
			if (sim != null)
				sim._flowState = flowState;
		}

		private static void SyncSimulationResourceFlowMode(PartResource resource, FieldInfo field, PartResource.FlowMode flowMode)
		{
			if (resource.simulationResource || resource.part == null)
				return;

			PartResource sim = resource.part.SimulationResources?[resource.resourceName];
			if (sim != null)
				field.SetValue(sim, flowMode);
		}

		public static void NotifyListChanged(Part part)
		{
			if (part == null)
				return;

			if (!IsCollecting)
			{
				GameEvents.onPartResourceListChange.Fire(part);
				return;
			}

			Queue(new PendingChange
			{
				Kind = ChangeKind.ResourceList,
				Part = part
			});
		}

		/// <summary> Flush queued startup events immediately. </summary>
		public static void FlushNow()
		{
			mutatedThisCycle = false;
			FlushPending();
		}

		private static bool IsCollecting => batchDepth > 0 || replaying;

		private static void Queue(PendingChange change)
		{
			pendingChanges.Add(change);
			if (change.Part != null)
				dirtyParts.Add(change.Part);
			mutatedThisCycle = true;
			EnsureRunner();
		}

		private static void EnsureRunner()
		{
			if (runner == null)
			{
				GameObject go = new GameObject(nameof(ResourceChangeBatchRunner));
				UnityEngine.Object.DontDestroyOnLoad(go);
				runner = go.AddComponent<ResourceChangeBatchRunner>();
			}

			if (!runnerActive)
			{
				runnerActive = true;
				runner.StartCoroutine(CoalesceAndFlush());
			}
		}

		private static IEnumerator CoalesceAndFlush()
		{
			try
			{
				// Wait until a full frame passes with no further resource mutations so
				// multi-frame vessel loads (many Start/OnStart calls) still collapse to
				// one notification wave.
				do
				{
					mutatedThisCycle = false;
					yield return null;
				}
				while (mutatedThisCycle || batchDepth > 0);

				FlushPending();
			}
			finally
			{
				runnerActive = false;
				if (pendingChanges.Count > 0)
					EnsureRunner();
			}
		}

		private static void FlushPending()
		{
			if (pendingChanges.Count == 0)
				return;

			PendingChange[] changes = pendingChanges.ToArray();
			Part[] changedParts = new Part[dirtyParts.Count];
			dirtyParts.CopyTo(changedParts);
			pendingChanges.Clear();
			dirtyParts.Clear();

			HashSet<Vessel> vessels = new HashSet<Vessel>();
			ShipConstruct editorShip = null;
			for (int i = 0; i < changedParts.Length; i++)
			{
				Part part = changedParts[i];
				if (part == null)
					continue;

				if (part.vessel != null)
				{
					vessels.Add(part.vessel);
				}
				else if (HighLogic.LoadedSceneIsEditor
					&& EditorLogic.fetch?.ship != null
					&& EditorLogic.fetch.ship.parts.Contains(part))
				{
					editorShip = EditorLogic.fetch.ship;
				}
			}

			// Only these sets are covered by the one-shot rebuilds below. Other
			// PartSets (resource UI, third-party/custom sets) must process every event.
			suppressedPartSets.Clear();
			foreach (Vessel vessel in vessels)
				AddSuppressedPartSets(vessel);
			if (editorShip != null)
				AddSuppressedPartSets(editorShip);

			// Replay every original event, in order, so third-party listeners retain the
			// stock API semantics. Harmony prefixes below skip only the costly stock
			// PartSet/Vessel handlers during this replay.
			replaying = true;
			try
			{
				for (int i = 0; i < changes.Length; i++)
					Replay(changes[i]);
			}
			finally
			{
				replaying = false;
				suppressedPartSets.Clear();
			}

			// Replace skipped handlers with one full refresh per loaded vessel/ship.
			foreach (Vessel vessel in vessels)
			{
				if (vessel != null)
					vessel.UpdateResourceSets();
			}

			editorShip?.UpdateResourceSets();
		}

		private static void AddSuppressedPartSets(Vessel vessel)
		{
			if (vessel == null)
				return;

			AddSuppressedPartSet(vessel.resourcePartSet);
			AddSuppressedPartSet(vessel.simulationResourcePartSet);
			AddSuppressedPartSets(vessel.crossfeedSets);
			AddSuppressedPartSets(vessel.simulationCrossfeedSets);
			AddSuppressedPartSets(vessel.parts);
		}

		private static void AddSuppressedPartSets(ShipConstruct ship)
		{
			if (ship == null)
				return;

			AddSuppressedPartSet(ship.resourcePartSet);
			AddSuppressedSimulationPartSets(ship.parts);
		}

		private static void AddSuppressedPartSets(List<PartSet> partSets)
		{
			if (partSets == null)
				return;

			for (int i = 0; i < partSets.Count; i++)
				AddSuppressedPartSet(partSets[i]);
		}

		private static void AddSuppressedPartSets(List<Part> parts)
		{
			if (parts == null)
				return;

			for (int i = 0; i < parts.Count; i++)
			{
				Part part = parts[i];
				if (part == null)
					continue;

				AddSuppressedPartSet(part.crossfeedPartSet);
				AddSuppressedPartSet(part.simulationCrossfeedPartSet);
			}
		}

		private static void AddSuppressedSimulationPartSets(List<Part> parts)
		{
			if (parts == null)
				return;

			for (int i = 0; i < parts.Count; i++)
			{
				Part part = parts[i];
				if (part != null)
					AddSuppressedPartSet(part.simulationCrossfeedPartSet);
			}
		}

		private static void AddSuppressedPartSet(PartSet partSet)
		{
			if (partSet != null)
				suppressedPartSets.Add(partSet);
		}

		private static void Replay(PendingChange change)
		{
			switch (change.Kind)
			{
				case ChangeKind.FlowState:
					if (change.Resource == null)
						return;
					change.Resource._flowState = change.ToState;
					SyncSimulationResourceFlowState(change.Resource, change.ToState);
					GameEvents.onPartResourceFlowStateChange.Fire(
						new GameEvents.HostedFromToAction<PartResource, bool>(
							change.Resource, change.FromState, change.ToState));
					return;

				case ChangeKind.FlowMode:
					if (change.Resource == null || !TryGetFlowModeField(out FieldInfo field))
						return;
					field.SetValue(change.Resource, change.ToMode);
					SyncSimulationResourceFlowMode(change.Resource, field, change.ToMode);
					GameEvents.onPartResourceFlowModeChange.Fire(
						new GameEvents.HostedFromToAction<PartResource, PartResource.FlowMode>(
							change.Resource, change.FromMode, change.ToMode));
					return;

				case ChangeKind.ResourceList:
					if (change.Part != null)
						GameEvents.onPartResourceListChange.Fire(change.Part);
					return;
			}
		}

		private static bool TryGetFlowModeField(out FieldInfo field)
		{
			if (!flowModeFieldResolved)
			{
				flowModeFieldResolved = true;
				const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				flowModeField = typeof(PartResource).GetField("_flowMode", flags)
					?? typeof(PartResource).GetField("<flowMode>k__BackingField", flags);

				if (flowModeField == null)
					Lib.Log("PartResource flowMode backing field not found; falling back to property setter", Lib.LogLevel.Warning);
			}

			field = flowModeField;
			return field != null;
		}

		private sealed class ResourceChangeBatchRunner : MonoBehaviour { }
	}

	[HarmonyPatch(typeof(PartSet), "OnFlowStateChange")]
	internal static class PartSetOnFlowStateChangeBatchPatch
	{
		private static bool Prefix(PartSet __instance) => !ResourceChangeBatch.ShouldSuppressPartSet(__instance);
	}

	[HarmonyPatch(typeof(PartSet), "OnFlowModeChange")]
	internal static class PartSetOnFlowModeChangeBatchPatch
	{
		private static bool Prefix(PartSet __instance) => !ResourceChangeBatch.ShouldSuppressPartSet(__instance);
	}

	[HarmonyPatch(typeof(Vessel), "UpdateResourceSetsEventCheckPart")]
	internal static class VesselResourceListChangeBatchPatch
	{
		private static bool Prefix() => !ResourceChangeBatch.IsReplaying;
	}
}
