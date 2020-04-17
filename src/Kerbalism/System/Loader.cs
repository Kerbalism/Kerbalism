using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using Harmony;


namespace KERBALISM
{

	public static class MM40Injector
	{
		private static List<string> injectors = new List<string>();

		public static void AddInjector(string type, string id)
		{
			injectors.Add(type + id);
		}

		public static IEnumerable<string> ModuleManagerAddToModList()
		{
			return injectors;
		}
	}

	// the name is chosen so that the awake method is called after ModuleManager,
	// this is necessary because MM injects its loader at index 1, so we need to inject
	// our own after it, at index 1 (so that it runs just before MM)
	[KSPAddon(KSPAddon.Startup.Instantly, false)]
	public sealed class Loader : MonoBehaviour
	{
		private bool mmPostLoadDone = false;

		public void Start()
		{
			// log version
			Lib.Log("Version : " + Lib.KerbalismVersion + " - Build : " + Lib.KerbalismDevBuild);

			if (LocalHelpers.GenerateEnglishLoc)
				LocalHelpers.GenerateLoc();

			if (LocalHelpers.UpdateNonEnglishLoc)
				LocalHelpers.RegenerateNonEnglishLoc();

			// detect features
			Features.Parse();

			// get configs from DB
			UrlDir.UrlFile root = null;
			foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs)
			{ root = url.parent; break; }

			// inject features as MM patches on-the-fly, so they can be queried with NEEDS[]
			if (Features.Failures) Inject(root, "Kerbalism", "Failures");
			if (Features.Science) Inject(root, "Kerbalism", "Science");
			if (Features.Radiation) Inject(root, "Kerbalism", "Radiation");
			if (Features.LifeSupport) Inject(root, "Kerbalism", "LifeSupport");
			if (Features.Stress) Inject(root, "Kerbalism", "Stress");

			// Create harmony instance
			HarmonyInstance harmony = HarmonyInstance.Create("Kerbalism");

			// Search all Kerbalism classes for standalone patches 
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			// Add other patches
			ErrorManager.SetupPatches(harmony);
			B9PartSwitch.Init(harmony);

			// register loading callbacks
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				GameEvents.OnPartLoaderLoaded.Add(OnPartLoaderLoaded);
			}
		}

		// Called by ModuleManager, after it has patched the game database, but before the part compilation.
		public void ModuleManagerPostLoad()
		{
			Settings.ParseTime();
			string calendarInfo = Lib.SetupCalendar();
			Lib.Log($"Parsing calendar : {calendarInfo}");
			Lib.Log($"{Lib.HoursInDayExact.ToString()} exact hours per day, {Lib.DaysInYearExact.ToString()} exact days per year");
			Lib.Log($"{Lib.HoursInDayFloored.ToString()} floored hours per day, {Lib.DaysInYearFloored.ToString()} floored days per year");
			Settings.Parse();
			Settings.CheckMods();
			Profile.Parse();
			ErrorManager.CheckErrors(true);
			mmPostLoadDone = true;
		}

		void OnDestroy()
		{
			GameEvents.OnPartLoaderLoaded.Remove(OnPartLoaderLoaded);
		}

		// inject an MM patch on-the-fly, so that NEEDS[TypeId] can be used in MM patches
		static void Inject(UrlDir.UrlFile root, string type, string id)
		{
			Lib.Log(Lib.BuildString("Injecting ", type, id));
			if (ModuleManager.MM_major >= 4)
			{
				MM40Injector.AddInjector(type, id);
			}
			else
			{
				root.configs.Add(new UrlDir.UrlConfig(root, new ConfigNode(Lib.BuildString("@Kerbalism:FOR[", type, id, "]"))));
			}
		}

		void OnPartLoaderLoaded()
		{
			if (!mmPostLoadDone)
			{
				ErrorManager.AddError(true, "ModuleManager not found");
				ErrorManager.CheckErrors(true);
			}

			// save the habitat volume/surface cache
			if (ModuleKsmHabitat.habitatDatabase == null)
				return;

			ConfigNode fakeNode = new ConfigNode();

			foreach (KeyValuePair<string, Lib.PartVolumeAndSurfaceInfo> habInfo in ModuleKsmHabitat.habitatDatabase)
			{
				ConfigNode node = new ConfigNode(ModuleKsmHabitat.habitatDataCacheNodeName);
				node.AddValue("partName", habInfo.Key.Replace('.', '_'));
				habInfo.Value.Save(node);
				fakeNode.AddNode(node);
			}

			fakeNode.Save(ModuleKsmHabitat.HabitatDataCachePath);
		}
	}

} // KERBALISM
