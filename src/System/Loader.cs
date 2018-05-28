using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;


namespace KERBALISM
{


	public sealed class Loader : LoadingSystem
	{
		public static void Init()
		{
			// log version
			Lib.Log("version " + Lib.Version());

			// parse settings
			Settings.Parse();

			// parse profile
			Profile.Parse();

			// detect features
			Features.Detect();

			// get configs from DB
			UrlDir.UrlFile root = null;
			foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) { root = url.parent; break; }

			// inject MM patches on-the-fly, so that profile/features can be queried with NEEDS[]
			Inject(root, "Profile", Lib.UppercaseFirst(Settings.Profile));
			if (Features.Reliability) Inject(root, "Feature", "Reliability");
			if (Features.Deploy) Inject(root, "Feature", "Deploy");
			if (Features.SpaceWeather) Inject(root, "Feature", "SpaceWeather");
			if (Features.Automation) Inject(root, "Feature", "Automation");
			if (Features.Science) Inject(root, "Feature", "Science");
			if (Features.Radiation) Inject(root, "Feature", "Radiation");
			if (Features.Shielding) Inject(root, "Feature", "Shielding");
			if (Features.LivingSpace) Inject(root, "Feature", "LivingSpace");
			if (Features.Comfort) Inject(root, "Feature", "Comfort");
			if (Features.Poisoning) Inject(root, "Feature", "Poisoning");
			if (Features.Pressure) Inject(root, "Feature", "Pressure");
			if (Features.Habitat) Inject(root, "Feature", "Habitat");
			if (Features.Supplies) Inject(root, "Feature", "Supplies");
		}

		// inject an MM patch on-the-fly, so that NEEDS[TypeId] can be used in MM patches
		static void Inject(UrlDir.UrlFile root, string type, string id)
		{
			root.configs.Add(new UrlDir.UrlConfig(root, new ConfigNode(Lib.BuildString("@Kerbalism:FOR[", type, id, "]"))));
		}


		public override bool IsReady() { return true; }
		public override string ProgressTitle() { return "Kerbalism"; }
		public override float ProgressFraction() { return 0f; }
		public override void StartLoad() { Init(); }
	}


	// the name is choosen so that the awake method is called after ModuleManager one
	// this is necessary because MM inject its loader at index 1, so we need to inject
	// our own after it, at index 1 (so that it run just before MM)
	[KSPAddon(KSPAddon.Startup.Instantly, false)]
	public sealed class PatchInjector : MonoBehaviour
	{
		public void Awake()
		{
			// inject loader
			List<LoadingSystem> loaders = LoadingScreen.Instance.loaders;
			GameObject go = new GameObject("Kerbalism");
			Loader loader = go.AddComponent<Loader>();
			int index = loaders.FindIndex(k => k is GameDatabase);
			loaders.Insert(index + 1, loader);
		}
	}

} // KERBALISM