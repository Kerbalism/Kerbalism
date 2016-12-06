using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class BodyInfo : Window
{
  // ctor
  public BodyInfo()
  : base(260u, 245u, 41u, 20u, Styles.win)
  {
    // enable global access
    instance = this;

    // show/hide at start based on user setting
    open = Settings.ShowBodyInfo;
  }

  // called every frame
  public override bool prepare()
  {
    // only show in mapview
    if (!MapView.MapIsEnabled) return false;

    // only show if there is a selected body and that body is not the sun
    CelestialBody body = Lib.SelectedBody();
    if (body == null || (body.flightGlobalsIndex == 0 && !Features.Radiation)) return false;

    return open;
  }

  // draw the window
  public override void render()
  {
    // shortcut
    CelestialBody sun = FlightGlobals.Bodies[0];

    // get selected body
    CelestialBody body = Lib.SelectedBody();

    // draw pseudo-title
    if (Panel.title(body.bodyName.ToUpper())) Close();

    // for all bodies except the sun
    if (body != sun)
    {
      // calculate simulation values
      double atmo_factor = Sim.AtmosphereFactor(body, 0.7071);
      double gamma_factor = Sim.GammaTransparency(body, 0.0);
      double sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
      Vector3d sun_dir = (sun.position - body.position).normalized;
      double solar_flux = Sim.SolarFlux(sun_dist) * atmo_factor;
      double albedo_flux = Sim.AlbedoFlux(body, body.position + sun_dir * body.Radius);
      double body_flux = Sim.BodyFlux(body, 0.0);
      double total_flux = solar_flux + albedo_flux + body_flux + Sim.BackgroundFlux();
      double temperature = body.atmosphere ? body.GetTemperature(0.0) : Sim.BlackBodyTemperature(total_flux);

      // calculate night-side temperature
      double total_flux_min = Sim.AlbedoFlux(body, body.position - sun_dir * body.Radius) + body_flux + Sim.BackgroundFlux();
      double temperature_min = Sim.BlackBodyTemperature(total_flux_min);

      // calculate radiation at body surface
      double radiation = Radiation.ComputeSurface(body, gamma_factor);

      // surface panel
      string temperature_str = body.atmosphere
        ? Lib.HumanReadableTemp(temperature)
        : Lib.BuildString(Lib.HumanReadableTemp(temperature_min), " / ", Lib.HumanReadableTemp(temperature));
      Panel.section("SURFACE");
      Panel.content("temperature", temperature_str);
      Panel.content("solar flux", Lib.HumanReadableFlux(solar_flux));
      if (Features.Radiation) Panel.content("radiation", Lib.HumanReadableRadiation(radiation));
      Panel.space();

      // atmosphere panel
      if (body.atmosphere)
      {
        Panel.section("ATMOSPHERE");
        Panel.content("breathable", body == FlightGlobals.GetHomeBody() && body.atmosphereContainsOxygen ? "yes" : "no");
        Panel.content("light absorption", Lib.HumanReadablePerc(1.0 - Sim.AtmosphereFactor(body, 0.7071)));
        if (Features.Radiation) Panel.content("gamma absorption", Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(body, 0.0)));
        Panel.space();
      }
    }

    // rendering panel
    if (Features.Radiation)
    {
      Panel.section("RENDERING");
      Panel.content("inner belt",   Radiation.show_inner ? "<color=green>show</color>" : "<color=red>hide</color>", ref Radiation.show_inner);
      Panel.content("outer belt",   Radiation.show_outer ? "<color=green>show</color>" : "<color=red>hide</color>", ref Radiation.show_outer);
      Panel.content("magnetopause", Radiation.show_pause ? "<color=green>show</color>" : "<color=red>hide</color>", ref Radiation.show_pause);
      Panel.space();
    }
  }

  public override float height()
  {
    CelestialBody body = Lib.SelectedBody();
    return 20.0f
      + (body.flightGlobalsIndex != 0 ? Panel.height(2 + (Features.Radiation ? 1 : 0)) : 0.0f)
      + (body.flightGlobalsIndex != 0 && body.atmosphere ? Panel.height(2 + (Features.Radiation ? 1 : 0)) : 0.0f)
      + (Features.Radiation ? Panel.height(3) : 0.0f);
  }

  // show the window
  public static void Open()
  {
    instance.open = true;
  }

  // close the window
  public static void Close()
  {
    instance.open = false;
  }

  // toggle the window
  public static void Toggle()
  {
    instance.open = !instance.open;
  }

  // return true if the window is open
  public static bool IsOpen()
  {
    return instance.open;
  }

  // open/close the window
  bool open = true;

  // permit global access
  static BodyInfo instance;
}


} // KERBALISM