using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;


namespace KERBALISM {


public sealed class Loader : LoadingSystem
{
  public static void init()
  {
    // log version
    Lib.Log("version " + Lib.Version());

    // parse settings
    Settings.parse();

    // parse profile
    Profile.parse();

    // detect features
    Features.detect();

    // get configs from DB
    UrlDir.UrlFile root = null;
    foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) { root = url.parent; break; }

    // inject MM patches on-the-fly, so that profile/features can be queried with NEEDS[]
    inject(root, "Profile", Lib.UppercaseFirst(Settings.Profile));
    if (Features.Reliability) inject(root, "Feature", "Reliability");
    if (Features.Signal) inject(root, "Feature", "Signal");
    if (Features.SpaceWeather) inject(root, "Feature", "SpaceWeather");
    if (Features.Automation) inject(root, "Feature", "Automation");
    if (Features.Science) inject(root, "Feature", "Science");
    if (Features.Radiation) inject(root, "Feature", "Radiation");
    if (Features.Shielding) inject(root, "Feature", "Shielding");
    if (Features.LivingSpace) inject(root, "Feature", "LivingSpace");
    if (Features.Comfort) inject(root, "Feature", "Comfort");
    if (Features.Poisoning) inject(root, "Feature", "Poisoning");
    if (Features.Pressure) inject(root, "Feature", "Pressure");
    if (Features.Habitat) inject(root, "Feature", "Habitat");
    if (Features.Supplies) inject(root, "Feature", "Supplies");
  }

  // inject an MM patch on-the-fly, so that NEEDS[TypeId] can be used in MM patches
  static void inject(UrlDir.UrlFile root, string type, string id)
  {
    root.configs.Add(new UrlDir.UrlConfig(root, new ConfigNode(Lib.BuildString("@Kerbalism:FOR[", type, id, "]"))));
  }


  public override bool IsReady() { return true; }
  public override string ProgressTitle() { return "Kerbalism"; }
  public override float ProgressFraction() { return 0f; }
  public override void StartLoad() { init(); }
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

