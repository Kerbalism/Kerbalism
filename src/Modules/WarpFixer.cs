using System;
using UnityEngine;


namespace KERBALISM {


// This module is used to disable stock solar panel output, by setting rate to zero.
// The EC is instead produced using the resource cache, that give us correct behaviour
// independent from timewarp speed and vessel EC capacity.
// The stock module was not simply replaced with a custom solar panel module, because
// dealing with tracking and with the "solar panel transforms zoo" was a pain.
// We reuse computations done by the stock module as much as possible.
public sealed class WarpFixer : PartModule
{
  public override void OnStart(StartState state)
  {
    // don't break tutorial scenarios
    if (Lib.DisableScenario(this)) return;

    // do nothing in the editor
    if (state == StartState.Editor) return;

    // get panel if any
    panel = part.FindModuleImplementing<ModuleDeployableSolarPanel>();
    if (panel != null)
    {
      // store rate
      rate = panel.resHandler.outputResources[0].rate;

      // reset rate
      // - This break mods that evaluate solar panel output for a reason or another (eg: AmpYear, BonVoyage).
      //   We fix that by exploiting the fact that resHandler was introduced in KSP recently, and most of
      //   these mods weren't updated to reflect the changes or are not aware of them, and are still reading
      //   chargeRate. However the stock solar panel ignore chargeRate value during FixedUpdate.
      //   So we only reset resHandler rate.
      panel.resHandler.outputResources[0].rate = 0.0f;

      // hide ui
      panel.Fields["status"].guiActive = false;
      panel.Fields["sunAOA"].guiActive = false;
      panel.Fields["flowRate"].guiActive = false;
    }
  }

  public void FixedUpdate()
  {
    // do nothing in editor
    if (Lib.IsEditor()) return;

    // do nothing if there isn't a solar panel
    if (panel == null) return;

    // get resource handler
    resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

    // get vessel data from cache
    vessel_info info = Cache.VesselInfo(vessel);

    // do nothing if vessel is invalid
    if (!info.is_valid) return;

    // detect if sunlight is evaluated analytically
    bool analytical_sunlight = info.sunlight > 0.0 && info.sunlight < 1.0;

    // detect occlusion from other vessel parts
    // - we are only interested when the sunlight evaluation is discrete
    var collider = panel.hit.collider;
    bool locally_occluded = !analytical_sunlight && collider != null && info.sunlight > 0.0;

    // if panel is enabled and extended, and if sun is not occluded, not even locally
    if (panel.isEnabled && panel.deployState == ModuleDeployablePart.DeployState.EXTENDED && info.sunlight > 0.0 && !locally_occluded)
    {
      // calculate cosine factor
      // - the stock module is already computing the tracking direction
      double cosine_factor = Math.Max(Vector3d.Dot(info.sun_dir, panel.trackingDotTransform.forward), 0.0);

      // calculate normalized solar flux
      // - this include fractional sunlight if integrated over orbit
      // - this include atmospheric absorption if inside an atmosphere
      double norm_solar_flux = info.solar_flux / Sim.SolarFluxAtHome();

      // calculate output
      double output = rate                              // nominal panel charge rate at 1 AU
                    * norm_solar_flux                   // normalized flux at panel distance from sun
                    * cosine_factor;                    // cosine factor of panel orientation

      // produce EC
      ec.Produce(output * Kerbalism.elapsed_s);

      // update ui
      field_visibility = info.sunlight * 100.0;
      field_atmosphere = info.atmo_factor * 100.0;
      field_exposure = cosine_factor * 100.0;
      field_output = output;
      Fields["field_visibility"].guiActive = analytical_sunlight;
      Fields["field_atmosphere"].guiActive = info.atmo_factor < 1.0;
      Fields["field_exposure"].guiActive = true;
      Fields["field_output"].guiActive = true;
    }
    // if panel is disabled, retracted, or in shadow
    else
    {
      // hide ui
      Fields["field_visibility"].guiActive = false;
      Fields["field_atmosphere"].guiActive = false;
      Fields["field_exposure"].guiActive = false;
      Fields["field_output"].guiActive = false;
    }

    // update status ui
    field_status = analytical_sunlight
    ? "<color=#ffff22>Integrated over the orbit</color>"
    : locally_occluded
    ? "<color=#ff2222>Occluded by vessel</color>"
    : info.sunlight < 1.0
    ? "<color=#ff2222>Occluded by celestial body</color>"
    : string.Empty;
    Fields["field_status"].guiActive = field_status.Length > 0;
  }

  ModuleDeployableSolarPanel panel;
  double rate;

  [KSPField(guiActive = false, guiName = "Status")] public string field_status;
  [KSPField(guiActive = false, guiName = "Visibility", guiUnits = "%", guiFormat = "F0")] public double field_visibility;
  [KSPField(guiActive = false, guiName = "Atmosphere", guiUnits = "%", guiFormat = "F0")] public double field_atmosphere;
  [KSPField(guiActive = false, guiName = "Exposure", guiUnits = "%", guiFormat = "F0")] public double field_exposure;
  [KSPField(guiActive = false, guiName = "Output", guiUnits = " kW", guiFormat = "F3")] public double field_output;
}


} // KERBALISM

