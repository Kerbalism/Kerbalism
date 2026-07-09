using HarmonyLib;
using System.Collections;
using System.Reflection;

namespace KERBALISM
{
	internal static class SpaceDustHarmony
	{
		private static readonly System.Type MapOverlayType = AccessTools.TypeByName("SpaceDust.MapOverlay");
		private static readonly System.Type ParticleFieldType = AccessTools.TypeByName("SpaceDust.ParticleField");
		private static readonly MethodInfo MapOverlayRemoveBodyFields = MapOverlayType == null
			? null
			: AccessTools.Method(MapOverlayType, "RemoveBodyFields");
		private static readonly FieldInfo MapOverlayDrawnFields = MapOverlayType == null
			? null
			: AccessTools.Field(MapOverlayType, "drawnFields");
		private static readonly MethodInfo ParticleFieldSetVisible = ParticleFieldType == null
			? null
			: AccessTools.Method(ParticleFieldType, "SetVisible", new[] { typeof(bool) });

		public static void Apply(Harmony harmony)
		{
			System.Type harvesterType = AccessTools.TypeByName("SpaceDust.ModuleSpaceDustHarvester");
			MethodInfo fixedUpdate = harvesterType?.GetMethod(
				"FixedUpdate",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo prefix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(SpaceDustFixedUpdatePrefix));
			MethodInfo postfix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(SpaceDustFixedUpdatePostfix));
			if (fixedUpdate != null)
				harmony.Patch(fixedUpdate, new HarmonyMethod(prefix), new HarmonyMethod(postfix));

			System.Type backgroundType = AccessTools.TypeByName("SpaceDust.SpaceDustHarvesterBackground");
			MethodInfo backgroundProcess = backgroundType == null
				? null
				: AccessTools.Method(backgroundType, "Process", new[] { typeof(ProtoPartModuleSnapshot), typeof(Vessel) });
			if (backgroundProcess == null && backgroundType != null)
				backgroundProcess = AccessTools.Method(backgroundType, "Process");
			MethodInfo backgroundPrefix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(SpaceDustBackgroundProcessPrefix));
			if (backgroundProcess != null && backgroundPrefix != null)
				harmony.Patch(backgroundProcess, new HarmonyMethod(backgroundPrefix));

			PatchMapOverlayParticles(harmony);
		}

		private static void PatchMapOverlayParticles(Harmony harmony)
		{
			if (MapOverlayType == null)
				return;

			MethodInfo onMapExited = AccessTools.Method(MapOverlayType, "OnMapExited");
			MethodInfo onMapExitedPostfix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(MapOverlayOnMapExitedPostfix));
			if (onMapExited != null && onMapExitedPostfix != null)
				harmony.Patch(onMapExited, postfix: new HarmonyMethod(onMapExitedPostfix));

			MethodInfo mapOverlayUpdate = AccessTools.Method(MapOverlayType, "Update");
			MethodInfo mapOverlayUpdatePrefix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(MapOverlayUpdatePrefix));
			if (mapOverlayUpdate != null && mapOverlayUpdatePrefix != null)
				harmony.Patch(mapOverlayUpdate, prefix: new HarmonyMethod(mapOverlayUpdatePrefix));

			if (ParticleFieldType == null)
				return;

			MethodInfo particleUpdate = AccessTools.Method(ParticleFieldType, "Update");
			MethodInfo particleUpdatePrefix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(ParticleFieldUpdatePrefix));
			if (particleUpdate != null && particleUpdatePrefix != null)
				harmony.Patch(particleUpdate, prefix: new HarmonyMethod(particleUpdatePrefix));
		}

		/// <summary>
		/// SpaceDust map bands are parented to celestial bodies; if OnMapExited is missed they render in flight
		/// with no toolbar toggle (SpaceDust only registers its button for map/tracking station scenes).
		/// </summary>
		private static void MapOverlayOnMapExitedPostfix(object __instance)
		{
			RemoveMapOverlayBodyFields(__instance);
		}

		private static void MapOverlayUpdatePrefix(object __instance)
		{
			if (!ShouldHideMapOverlayParticles())
				return;

			RemoveMapOverlayBodyFields(__instance);
		}

		private static bool ParticleFieldUpdatePrefix(object __instance)
		{
			if (!ShouldHideMapOverlayParticles())
				return true;

			ParticleFieldSetVisible?.Invoke(__instance, new object[] { false });
			return false;
		}

		private static bool ShouldHideMapOverlayParticles()
		{
			return HighLogic.LoadedSceneIsFlight && !MapView.MapIsEnabled;
		}

		private static void RemoveMapOverlayBodyFields(object mapOverlay)
		{
			if (mapOverlay == null || MapOverlayRemoveBodyFields == null)
				return;

			if (MapOverlayDrawnFields != null)
			{
				IList drawnFields = MapOverlayDrawnFields.GetValue(mapOverlay) as IList;
				if (drawnFields == null || drawnFields.Count == 0)
					return;
			}

			MapOverlayRemoveBodyFields.Invoke(mapOverlay, null);
		}

		private static void SpaceDustFixedUpdatePrefix(PartModule __instance)
		{
			if (__instance.part.FindModuleImplementing<SpaceDustHarvesterKerbalismUpdater>() != null)
				SpaceDustResourceBlocker.EnterBlock();
		}

		private static void SpaceDustFixedUpdatePostfix(PartModule __instance)
		{
			if (__instance?.part?.FindModuleImplementing<SpaceDustHarvesterKerbalismUpdater>() == null)
				return;

			SpaceDustHarvesterKerbalismUpdater.SyncNativeUiAfterFixedUpdate(__instance);
			SpaceDustResourceBlocker.ExitBlock();
		}

		private static bool SpaceDustBackgroundProcessPrefix(ProtoPartModuleSnapshot ___protoMiner, Vessel ___ves)
		{
			ProtoPartModuleSnapshot harvester = ___protoMiner;
			Vessel vessel = ___ves;

			if (harvester == null || vessel?.protoVessel == null)
				return true;

			foreach (ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
			{
				if (!part.modules.Contains(harvester))
					continue;

				if (!part.modules.Exists(module => module.moduleName == "SpaceDustHarvesterKerbalismUpdater"))
					return true;

				return false;
			}

			return true;
		}
	}
}
