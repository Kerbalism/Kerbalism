using HarmonyLib;
using System;
using System.Reflection;

namespace KERBALISM
{
	internal static class SCANsatHarmony
	{
		private class ScanPassState
		{
			public Int16[,] Snapshot;
			public short DivertMask;
			public bool Active;
		}

		private static FieldInfo coverageField;
		private static bool patchApplied;
		private static bool patchFailed;
		private static bool warnedFailOpen;

		public static bool InterceptEnabled => patchApplied && !patchFailed && Features.Science;

		public static void Apply(Harmony harmony)
		{
			if (harmony == null || patchApplied || patchFailed)
				return;

			try
			{
				Type controllerType = AccessTools.TypeByName("SCANsat.SCANcontroller");
				Type dataType = AccessTools.TypeByName("SCANsat.SCAN_Data.SCANdata");
				Type scansatModuleType = AccessTools.TypeByName("SCANsat.SCAN_PartModules.SCANsat");

				if (controllerType == null || dataType == null)
				{
					IntegrationUtils.LogError("SCANsat types not found; science integration disabled.");
					patchFailed = true;
					return;
				}

				coverageField = AccessTools.Field(dataType, "coverage");
				if (coverageField == null)
				{
					IntegrationUtils.LogError("SCANsat SCANdata.coverage field not found; science integration disabled.");
					patchFailed = true;
					WarnFailOpen();
					return;
				}

				MethodInfo doScanPass = AccessTools.Method(controllerType, "doScanPass");
				if (doScanPass == null)
				{
					IntegrationUtils.LogError("SCANsat doScanPass not found; science integration disabled.");
					patchFailed = true;
					WarnFailOpen();
					return;
				}

				harmony.Patch(
					doScanPass,
					prefix: new HarmonyMethod(typeof(SCANsatHarmony), nameof(DoScanPassPrefix)),
					postfix: new HarmonyMethod(typeof(SCANsatHarmony), nameof(DoScanPassPostfix)));

				if (scansatModuleType != null)
				{
					MethodInfo fixedUpdate = AccessTools.Method(scansatModuleType, "FixedUpdate", Type.EmptyTypes);
					if (fixedUpdate != null)
					{
						harmony.Patch(
							fixedUpdate,
							prefix: new HarmonyMethod(typeof(SCANsatHarmony), nameof(SCANsatFixedUpdatePrefix)));
					}
				}

				patchApplied = true;
				IntegrationUtils.Log("SCANsat coverage intercept enabled.");
			}
			catch (Exception e)
			{
				patchFailed = true;
				IntegrationUtils.LogError("SCANsat Harmony patch failed: " + e);
				WarnFailOpen();
			}
		}

		private static void WarnFailOpen()
		{
			if (warnedFailOpen)
				return;
			warnedFailOpen = true;
			Message.Post(
				Lib.Color("SCANsat / Kerbalism", Lib.Kolor.Orange, true),
				"Coverage intercept unavailable; SCANsat map updates will not be deferred.");
		}

		/// <summary>
		/// Skip SCANsat's own EC unregister loop when Kerbalism owns science for this part.
		/// KerbalismScansat consumes EC and scales flush instead.
		/// </summary>
		private static bool SCANsatFixedUpdatePrefix(PartModule __instance)
		{
			if (!InterceptEnabled || __instance == null || __instance.part == null)
				return true;

			return __instance.part.FindModuleImplementing<KerbalismScansat>() == null;
		}

		private static void DoScanPassPrefix(Vessel v, object data, ref ScanPassState __state)
		{
			__state = null;

			if (!InterceptEnabled || v == null || data == null || coverageField == null)
				return;

			if (!v.TryGetVesselDataTemp(out VesselData vd) || !vd.IsSimulated)
				return;

			short divertMask = ScanCoverageStore.GetDivertMask(v);
			if (divertMask == 0)
				return;

			var coverage = coverageField.GetValue(data) as Int16[,];
			if (coverage == null)
				return;

			__state = new ScanPassState
			{
				Snapshot = ScanGrid.Clone(coverage),
				DivertMask = divertMask,
				Active = true
			};
		}

		private static void DoScanPassPostfix(Vessel v, object data, ScanPassState __state)
		{
			if (__state == null || !__state.Active || v == null || data == null || coverageField == null)
				return;

			var coverage = coverageField.GetValue(data) as Int16[,];
			if (coverage == null || __state.Snapshot == null)
				return;

			int bodyIndex = v.mainBody.flightGlobalsIndex;
			Int16[,] pending = null;
			// Cells already held as pending/files must not be counted again: the live map is
			// cleared for diverted bits, so SCANsat will keep re-painting the same ground track.
			Int16[,] claimed = ScanCoverageStore.GetClaimedCoverage(v, bodyIndex);
			short divertMask = __state.DivertMask;

			for (int x = 0; x < ScanGrid.Width; x++)
			{
				for (int y = 0; y < ScanGrid.Height; y++)
				{
					short added = (short)(coverage[x, y] & ~__state.Snapshot[x, y]);
					short divert = (short)(added & divertMask);
					if (divert == 0)
						continue;

					// Map stays unchanged for diverted bits until science is credited.
					coverage[x, y] = (short)(coverage[x, y] & ~divert);

					short held = claimed != null ? claimed[x, y] : (short)0;
					short novel = (short)(divert & ~held);
					if (novel == 0)
						continue;

					// Scanning without EC or storage still gets removed from the live map,
					// but isn't retained as science. Partial EC accepts a proportional sample.
					short accepted = ScanCoverageStore.GetCaptureMask(v.id, bodyIndex, x, y, novel);
					if (accepted != 0)
					{
						if (pending == null)
							pending = ScanCoverageStore.GetPending(v.id, bodyIndex, true);
						pending[x, y] |= accepted;
						if (claimed == null)
							claimed = ScanGrid.Create();
						claimed[x, y] |= accepted;
					}
				}
			}
		}
	}
}
