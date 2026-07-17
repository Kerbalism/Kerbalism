using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;


namespace KERBALISM
{
	// TODO : SolarPanelFixer missing features :
	// - SSTU automation / better reliability support

	// This module is used to disable stock and other plugins solar panel EC output and provide specific support
	// EC must be produced using the resource cache, that give us correct behaviour independent from timewarp speed and vessel EC capacity.
	// To be able to support a custom module, we need to be able to do the following :
	// - (imperative) prevent the module from using the stock API calls to generate EC 
	// - (imperative) get the nominal rate at 1 AU
	// - (imperative) get the "suncatcher" transforms or vectors
	// - (imperative) get the "pivot" transforms or vectors if it's a tracking panel
	// - (imperative) get the "deployed" state if its a deployable panel.
	// - (imperative) get the "broken" state if the target module implement it
	// - (optional)   set the "deployed" state if its a deployable panel (both for unloaded and loaded vessels, with handling of the animation)
	// - (optional)   get the time effiency curve if its supported / defined
	// Notes :
	// - We don't support temperature efficiency curve
	// - We don't have any support for the animations, the target module must be able to keep handling them despite our hacks.
	// - Depending on how "hackable" the target module is, we use different approaches :
	//   either we disable the monobehavior and call the methods manually, or if possible we let it run and we just get/set what we need
	public class SolarPanelFixer : PartModule
	{
		#region Declarations
		/// <summary>Unit to show in the UI, this is the only configurable field for this module. Default is actually set in OnLoad and if a rateUnit is set for ElectricCharge and this is not specified, the rateUnit will be used instead.</summary>
		[KSPField]
		public string EcUIUnit = string.Empty;
		public bool hasRUI = false; // are we using a ResourceUnitInfo?

		/// <summary>Main PAW info label</summary>
		[KSPField(guiActive = false, guiActiveEditor = false, guiName = "#KERBALISM_SolarPanelFixer_mode")] //Solar Panel Mode
		public string panelMode = string.Empty;
		[KSPField(guiActive = true, guiActiveEditor = false, guiName = "#KERBALISM_SolarPanelFixer_Solarpanelstatus")]//Solar Panel Status
		public string panelStatus = string.Empty;
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_SolarPanelFixer_Solarpaneloutput")]//Solar Panel Output
		[UI_Toggle(enabledText = "#KERBALISM_SolarPanelFixer_simulated", disabledText = "#KERBALISM_SolarPanelFixer_ignored")]//<color=#00ff00>Simulated</color>""<color=#ffff00>Ignored</color>
		public bool editorEnabled = true;
		[KSPField(guiActive = false, guiActiveEditor = false, guiName = "#KERBALISM_SolarPanelFixer_energy")] //Energy Output
		public string panelStatusEnergy = string.Empty;
		[KSPField(guiActive = false, guiActiveEditor = false, guiName = "#KERBALISM_SolarPanelFixer_exposure")] //Exposure
		public string panelStatusExposure = string.Empty;
		[KSPField(guiActive = false, guiActiveEditor = false, guiName = "#KERBALISM_SolarPanelFixer_wear")] //Wear
		public string panelStatusWear = string.Empty;

		/// <summary>nominal rate at 1 UA (Kerbin distance from the sun)</summary>
		[KSPField(isPersistant = true)]
		public double nominalRate = 10.0; // doing this on the purpose of not breaking existing saves

		[KSPField(isPersistant = true)]
		public string resourceName = "ElectricCharge";

		/// <summary>current state of the module</summary>
		[KSPField(isPersistant = true)]
		public PanelState state;

		/// <summary>tracked star/sun body index</summary>
		[KSPField(isPersistant = true)]
		public int trackedSunIndex = 0;

		/// <summary>has the player manually selected the star to be tracked ?</summary>
		[KSPField(isPersistant = true)]
		private bool manualTracking = false;

		/// <summary>
		/// Time based output degradation curve. Keys in hours, values in [0;1] range.
		/// Copied from the target solar panel module if supported and present.
		/// If defined in the SolarPanelFixer config, the target module curve will be overriden.
		/// </summary>
		[KSPField(isPersistant = true)]
		public FloatCurve timeEfficCurve;
		private static FloatCurve teCurve = null;
		private bool prefabDefinesTimeEfficCurve = false;

		/// <summary>UT of part creation in flight, used to evaluate the timeEfficCurve</summary>
		[KSPField(isPersistant = true)]
		public double launchUT = -1.0;

		/// <summary>internal object for handling the various hacks depending on the target solar panel module</summary>
		public SupportedPanel SolarPanel { get; private set; }

		/// <summary>current state of the module</summary>
		public bool isInitialized = false;

		/// <summary>for tracking analytic mode changes and ui updating</summary>
		private bool analyticSunlight;

		/// <summary>can be used by external mods to get the current EC/s</summary>
		[KSPField]
		public double currentOutput;

		// The following fields are local to FixedUpdate() but are shared for status string updates in Update()
		// Their value can be inconsistent, don't rely on them for anything else
		private double exposureFactor;
		private double wearFactor;
		private ExposureState exposureState;
		private string mainOccludingPart;
		private string rateFormat;
		private StringBuilder sb;
		VesselData.SunInfo trackedSunInfo;

		public enum PanelState
		{
			Unknown = 0,
			Retracted,
			Extending,
			Extended,
			ExtendedFixed,
			Retracting,
			Static,
			Broken,
			Failure
		}

		public enum ExposureState
		{
			Disabled,
			Exposed,
			InShadow,
			OccludedTerrain,
			OccludedPart,
			BadOrientation
		}
		#endregion

		#region KSP/Unity methods + background update

		[KSPEvent(active = true, guiActive = true, guiName = "#KERBALISM_SolarPanelFixer_Selecttrackedstar")]//Select Tracked Star
		public void ManualTracking()
		{
			// Assemble the buttons
			DialogGUIBase[] options = new DialogGUIBase[Sim.suns.Count + 1];
			options[0] = new DialogGUIButton(Local.SolarPanelFixer_Automatic, () => { manualTracking = false; }, true);//"Automatic"
			for (int i = 0; i < Sim.suns.Count; i++)
			{
				CelestialBody body = Sim.suns[i].body;
				options[i + 1] = new DialogGUIButton(body.bodyDisplayName.Replace("^N", ""), () =>
				{
					manualTracking = true;
					trackedSunIndex = body.flightGlobalsIndex;
					SolarPanel.SetTrackedBody(body);
				}, true);
			}

			PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new MultiOptionDialog(
				Local.SolarPanelFixer_SelectTrackingBody,//"SelectTrackingBody"
				Local.SolarPanelFixer_SelectTrackedstar_msg,//"Select the star you want to track with this solar panel."
				Local.SolarPanelFixer_Selecttrackedstar,//"Select Tracked Star"
				UISkinManager.GetSkin("MainMenuSkin"),
				options), false, UISkinManager.GetSkin("MainMenuSkin"));
		}

		public override void OnAwake()
		{
			if (teCurve == null) teCurve = new FloatCurve();
		}

		public override void OnLoad(ConfigNode node)
		{
			if (SolarPanel == null) GetSolarPanelModule();
			if (SolarPanel != null) resourceName = SolarPanel.ResourceName;

			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				prefabDefinesTimeEfficCurve = node.HasNode("timeEfficCurve");
				if (string.IsNullOrEmpty(EcUIUnit))
				{
					var rui = Lib.GetResourceUnitInfo(resourceName);
					hasRUI = rui != null;
					if (hasRUI)
						EcUIUnit = rui.RateUnit;
					else
						EcUIUnit = resourceName == "ElectricCharge" ? "EC/s" : new string(resourceName.Where(c => char.IsUpper(c)).ToArray()) + "/s";
				}
			}
			if (SolarPanel == null)
				return;

			if (Lib.IsEditor()) return;

			// apply states changes we have done trough automation
			if ((state == PanelState.Retracted || state == PanelState.Extended || state == PanelState.ExtendedFixed) && state != SolarPanel.GetState())
				SolarPanel.SetDeployedStateOnLoad(state);

			// apply reliability broken state and ensure we are correctly initialized (in case we are repaired mid-flight)
			// note : this rely on the fact that the reliability module is disabling the SolarPanelFixer monobehavior from OnStart, after OnLoad has been called
			if (!isEnabled)
			{
				ReliabilityEvent(true);
				OnStart(StartState.None);
			}
		}

		public override void OnStart(StartState startState)
		{
			sb = new StringBuilder(256);

			// don't break tutorial scenarios
			// TODO : does this actually work ?
			if (Lib.DisableScenario(this)) return;

			if (SolarPanel == null && !GetSolarPanelModule())
			{
				isInitialized = true;
				return;
			}

			// disable everything if the target module data/logic acquisition has failed
			if (!SolarPanel.OnStart(isInitialized, ref nominalRate))
				enabled = isEnabled = moduleIsEnabled = false;

			isInitialized = true;

			if (!prefabDefinesTimeEfficCurve)
				timeEfficCurve = SolarPanel.GetTimeCurve();

			if (Lib.IsFlight() && launchUT < 0.0)
				launchUT = Planetarium.GetUniversalTime();

			// setup star selection GUI
			Events["ManualTracking"].active = Sim.suns.Count > 1 && SolarPanel.IsTracking;
			Events["ManualTracking"].guiActive = state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static;

			// setup target module animation for custom star tracking
			SolarPanel.SetTrackedBody(FlightGlobals.Bodies[trackedSunIndex]);

			// The value has been adjusted to three decimal places for more accurate display in PAW.
			// In some extreme cases, a power generation value of 0.004 may appear.
			rateFormat = "F3";
		}

		public override void OnSave(ConfigNode node)
		{
			// vessel can be null in OnSave (ex : on vessel creation)
			if (!Lib.IsFlight()
				|| vessel == null
				|| !isInitialized
				|| SolarPanel == null
				|| !Lib.Landed(vessel)
				|| exposureState == ExposureState.Disabled) // don't to broken panels ! (issue #492)
				return;

			// get vessel data
			VesselData vd = vessel.KerbalismData();

			// do nothing if vessel is invalid
			if (!vd.IsSimulated) return;
		}

		public void Update()
		{
			// sanity check
			if (SolarPanel == null) return;

			// call Update specfic handling, if any
			SolarPanel.OnUpdate();

			// Do nothing else in the editor
			if (Lib.IsEditor()) return;

			// Don't update PAW if not needed
			if (!part.IsPAWVisible()) return;

			// Update tracked body selection button (Kopernicus multi-star support)
			if (Events["ManualTracking"].active && (state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static))
			{
				Events["ManualTracking"].guiActive = true;
				Events["ManualTracking"].guiName = Lib.BuildString(Local.SolarPanelFixer_Trackedstar + " ", manualTracking ? ": " : Local.SolarPanelFixer_AutoTrack, FlightGlobals.Bodies[trackedSunIndex].bodyDisplayName.Replace("^N", ""));//"Tracked star"[Auto] : "
			}
			else
			{
				Events["ManualTracking"].guiActive = false;
			}

			// Update main status field text
			Fields["panelMode"].guiActive = true;
			if (analyticSunlight)
			{
				panelMode = Local.SolarPanelFixer_analytic;
			}
			else
			{
				panelMode = Local.SolarPanelFixer_realtime;
			}
			Fields["panelStatus"].guiActive = true;
			Fields["panelStatusEnergy"].guiActive = false;
			Fields["panelStatusExposure"].guiActive = false;
			Fields["panelStatusWear"].guiActive = false;
			bool addRate = false;
			switch (exposureState)
			{
				case ExposureState.InShadow:
					// In a multi-star environment, smooth transitions are possible when switching celestial bodies.
					if (currentOutput >= 1e-3)
					{
						goto case ExposureState.Exposed;
					}
					panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_inshadow + "</color>";//In Shadow
					addRate = true;
					break;
				case ExposureState.OccludedTerrain:
					panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_occludedbyterrain + "</color>";//Occluded By Terrain
					addRate = true;
					break;
				case ExposureState.OccludedPart:
					panelStatus = Lib.BuildString("<color=#ff2222>", Local.SolarPanelFixer_occludedby.Format(mainOccludingPart), "</color>");//Occluded By 
					addRate = true;
					break;
				case ExposureState.BadOrientation:
					// In a multi-star environment, smooth transitions are possible when switching celestial bodies.
					if (currentOutput > 1e-10)
					{
						goto case ExposureState.Exposed;
					}
					panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_badorientation + "</color>";//Bad Orientation
					addRate = true;
					break;
				case ExposureState.Disabled:
					Fields["panelMode"].guiActive = false;
					switch (state)
					{
						case PanelState.Retracted: panelStatus = Local.SolarPanelFixer_retracted; break;//"Retracted"
						case PanelState.Extending: panelStatus = Local.SolarPanelFixer_extending; break;//"Extending"
						case PanelState.Retracting: panelStatus = Local.SolarPanelFixer_retracting; break;//"Retracting"
						case PanelState.Broken: panelStatus = Local.SolarPanelFixer_broken; break;//"Broken"
						case PanelState.Failure: panelStatus = Local.SolarPanelFixer_failure; break;//"Failure"
						case PanelState.Unknown: panelStatus = Local.SolarPanelFixer_invalidstate; break;//"Invalid State"
					}
					break;
				case ExposureState.Exposed:
					// The value has been adjusted to three decimal places for more accurate display in PAW.
					if (currentOutput >= 1e-10 && currentOutput < 1e-3)
					{
						rateFormat = "F5";
					}
					else if (currentOutput >= 1e-3)
					{
						rateFormat = "F3";
					}
					Fields["panelStatusExposure"].guiActive = true;
					Fields["panelStatusEnergy"].guiActive = true;
					panelStatus = "<color=#eaff56>" + Local.SolarPanelFixer_sunDirect + "</color>"; //"Sun Direct"
					sb.Length = 0;
					if (Settings.UseSIUnits)
					{
						if (hasRUI)
							sb.Append(Lib.SIRate(currentOutput, resourceName.GetHashCode()));
						else
							sb.Append(Lib.SIRate(currentOutput, EcUIUnit));
						panelStatusEnergy = sb.ToString();
					}
					else
					{
						sb.Append(currentOutput.ToString(rateFormat));
						sb.Append(" ");
						sb.Append(EcUIUnit);
						panelStatusEnergy = sb.ToString();
					}
					sb.Length = 0;
					if (analyticSunlight)
					{
						Fields["panelStatus"].guiActive = false;
						Fields["panelStatusExposure"].guiActive = false;
					}
					else
					{
						Fields["panelStatus"].guiActive = true;
						Fields["panelStatusExposure"].guiActive = true;
						sb.Append(" ");
						sb.Append(exposureFactor.ToString("P0"));
						panelStatusExposure = sb.ToString();
					}
					sb.Length = 0;
					if (wearFactor < 1.0)
					{
						Fields["panelStatusWear"].guiActive = true;
						sb.Append(" ");
						sb.Append((1.0 - wearFactor).ToString("P0"));
						panelStatusWear = sb.ToString();
					}
					break;
			}
			if (addRate && currentOutput > 0.001)
			{
				if (Settings.UseSIUnits)
				{
					if (hasRUI)
						Lib.BuildString(Lib.SIRate(currentOutput, Lib.ECResID), ", ", panelStatus);
					else
						Lib.BuildString(Lib.SIRate(currentOutput, EcUIUnit), ", ", panelStatus);
				}
				else
				{
					Lib.BuildString(currentOutput.ToString(rateFormat), " ", EcUIUnit, ", ", panelStatus);
				}
			}
		}

		public void FixedUpdate()
		{
			Profiler.BeginSample("Kerbalism.SolarPanelFixer.FixedUpdate");
			// sanity check
			if (SolarPanel == null)
			{
				Profiler.EndSample();
				return;
			}

			// Keep resetting launchUT in prelaunch state. It is possible for that value to come from craft file which could result in panels being degraded from the start.
			if (Lib.IsFlight() && vessel != null && vessel.situation == Vessel.Situations.PRELAUNCH)
				launchUT = Planetarium.GetUniversalTime();

			// can't produce anything if not deployed, broken, etc
			PanelState newState = SolarPanel.GetState();
			if (state != newState)
			{
				state = newState;
				if (Lib.IsEditor() && (newState == PanelState.Extended || newState == PanelState.ExtendedFixed || newState == PanelState.Retracted))
					Lib.RefreshPlanner();
			}

			if (!(state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static))
			{
				exposureState = ExposureState.Disabled;
				currentOutput = 0.0;
				Profiler.EndSample();
				return;
			}

			// do nothing else in editor
			if (Lib.IsEditor())
			{
				Profiler.EndSample();
				return;
			}

			// get vessel data from cache
			VesselData vd = vessel.KerbalismData();

			// do nothing if vessel is invalid, or sun info not ready yet (first FixedUpdates after load)
			if (!vd.IsSimulated || vd.EnvSunsInfo == null)
			{
				Profiler.EndSample();
				return;
			}


			/**
			 * Automatic Tracking Logic: Determines the optimal target star for solar panels.
			 * Logic Flow:
			 * 1. State Check: Executes only if manual tracking is disabled and panels are deployed/active.
			 * 2. Analytic Mode: Selects the star with the highest SolarFlux (direct calculation).
			 * 3. Standard Mode:
			 *		- Primary: Selects the brightest visible star (SunlightFactor > 0.05).
			 *		- Fallback: If all stars are occluded (e.g., in a planet's shadow), selects the 
			 * geometrically closest star to ensure the panel is pre-aligned when exiting shadow.
			**/
			if (!manualTracking && (state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static))
			{
				VesselData.SunInfo bestSun = null;

				if (vd.EnvIsAnalytic)
				{
					// Mode A: Analytic Mode - Find the star providing the maximum solar flux
					double maxFlux = -1.0;
					foreach (var sunInfo in vd.EnvSunsInfo)
					{
						if (sunInfo.SolarFlux > maxFlux)
						{
							maxFlux = sunInfo.SolarFlux;
							bestSun = sunInfo;
						}
					}
				}
				else
				{
					// Mode B: Standard Mode - Find the brightest star that is not currently occluded
					double maxActiveFlux = -1.0;
					foreach (var sunInfo in vd.EnvSunsInfo)
					{
						if (sunInfo.SunlightFactor > 0.05)
						{
							if (sunInfo.SolarFlux > maxActiveFlux)
							{
								maxActiveFlux = sunInfo.SolarFlux;
								bestSun = sunInfo;
							}
						}
					}

					// Fallback: If all stars are blocked, target the nearest star by distance
					if (bestSun == null)
					{
						double minDistance = double.MaxValue;
						foreach (var sunInfo in vd.EnvSunsInfo)
						{
							double dist = Vector3d.Distance(vessel.GetWorldPos3D(), sunInfo.SunData.body.position);
							if (dist < minDistance)
							{
								minDistance = dist;
								bestSun = sunInfo;
							}
						}
					}
				}

				// Update tracked sun in auto mode
				if (bestSun != null && trackedSunIndex != bestSun.SunData.bodyIndex)
				{
					trackedSunIndex = bestSun.SunData.bodyIndex;
					SolarPanel.SetTrackedBody(bestSun.SunData.body);
				}
			}

			trackedSunInfo = null;
			for (int i = 0; i < vd.EnvSunsInfo.Count; i++)
			{
				if (vd.EnvSunsInfo[i].SunData.bodyIndex == trackedSunIndex)
				{
					trackedSunInfo = vd.EnvSunsInfo[i];
					break;
				}
			}
			if (trackedSunInfo == null && vd.EnvSunsInfo.Count > 0)
				trackedSunInfo = vd.EnvSunsInfo[0];

			if (trackedSunInfo == null)
			{
				currentOutput = 0.0;
				Profiler.EndSample();
				return;
			}

			if (trackedSunInfo.SunlightFactor == 0.0)
				exposureState = ExposureState.InShadow;
			else
				exposureState = ExposureState.Exposed;

			// --- A factor specifically designed to calculate actual electricity generation ---
			double powerFactor = 0.0;
			if (vd.EnvIsAnalytic)
			{
				analyticSunlight = true;
				powerFactor = CalculateMultiStarPowerAnalytic(vessel, vd.EnvSunsInfo, trackedSunInfo, SolarPanel.Type, SolarPanel.IsTracking);
			}
			else
			{
				analyticSunlight = false;
				// reset factors
				exposureFactor = 0.0;
				powerFactor = 0.0;

				// iterate over all stars, compute the exposure factor
				foreach (VesselData.SunInfo sunInfo in vd.EnvSunsInfo)
				{
					// ignore insignifiant flux from distant stars
					if (sunInfo != trackedSunInfo && sunInfo.SolarFlux < 1e-6)
						continue;

					double sunCosineFactor = 0.0;
					double sunOccludedFactor = 0.0;
					string occludingPart = null;

					// Get the cosine factor (alignement between the sun and the panel surface)
					sunCosineFactor = SolarPanel.GetCosineFactor(sunInfo.Direction);

					if (sunCosineFactor == 0.0)
					{
						if (sunInfo == trackedSunInfo)
							exposureState = ExposureState.BadOrientation;
						sunCosineFactor = 0.0;
					}
					else
					{
						// The panel is oriented toward the sun, do a physic raycast to check occlusion
						sunOccludedFactor = SolarPanel.GetOccludedFactor(sunInfo.Direction, out occludingPart, sunInfo != trackedSunInfo);

						if (sunInfo == trackedSunInfo && sunOccludedFactor == 0.0)
						{
							if (occludingPart != null)
							{
								exposureState = ExposureState.OccludedPart;
								mainOccludingPart = Lib.EllipsisMiddle(occludingPart, 15);
							}
							else
							{
								exposureState = ExposureState.OccludedTerrain;
							}
						}
					}

					if (sunInfo.SunlightFactor == 1.0)
					{
						// Core: Angle of the star * Occlusion of the star * (Actual flux of the star / Reference flux)
						double starDistanceFactor = sunInfo.SolarFlux / Sim.SolarFluxAtHome;
						powerFactor += sunCosineFactor * sunOccludedFactor * starDistanceFactor;

					}
					else if (sunInfo == trackedSunInfo)
					{
						exposureState = ExposureState.InShadow;
					}

					if (sunInfo == trackedSunInfo)
					{
						exposureFactor = sunCosineFactor * sunOccludedFactor;
					}
				}
			}

			wearFactor = 1.0;
			if (timeEfficCurve?.Curve.length > 1)
				wearFactor = Lib.Clamp(timeEfficCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0)), 0.0, 1.0);

			currentOutput = nominalRate * wearFactor * powerFactor;

			// ------------------------------------
			// ignore very small outputs
			if (currentOutput < 1e-10)
			{
				currentOutput = 0.0;
				Profiler.EndSample();
				return;
			}

			// get resource handler
			ResourceInfo res = ResourceCache.GetResource(vessel, resourceName);

			// produce resource
			res.Produce(currentOutput * Kerbalism.elapsed_s, ResourceBroker.SolarPanel);
			Profiler.EndSample();
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, SolarPanelFixer prefab, VesselData vd, ResourceInfo ec, double elapsed_s)
		{
			Profiler.BeginSample("SolarPanelFixer.BackgroundUpdate");
			// this is ugly spaghetti code but initializing the prefab at loading time is messy because the targeted solar panel module may not be loaded yet
			if (!prefab.isInitialized) prefab.OnStart(StartState.None);

			// OnStart may fail to bind a supported panel module (e.g. prefab part has SolarPanelFixer but no usable target).
			if (prefab.SolarPanel == null || !prefab.isEnabled || vd.EnvSunsInfo == null)
			{
				Profiler.EndSample();
				return;
			}

			// check if the panel is broken by Reliability
			// If Reliability targets ModuleDeployableSolarPanel, SolarPanelFixer (this module) remains enabled
			// so we have to manually check if the target module is broken
			for (int i = 0; i < p.modules.Count; i++)
			{
				if (p.modules[i].moduleName == "Reliability" && Lib.Proto.GetBool(p.modules[i], "broken"))
				{
					string type = Lib.Proto.GetString(p.modules[i], "type");
					if (type == "SolarPanelFixer" || (prefab.SolarPanel.TargetModule != null && type == prefab.SolarPanel.TargetModule.moduleName))
					{
						Profiler.EndSample();
						return;
					}
				}
			}

			if (prefab.resourceName != "ElectricCharge")
			{
				ec = ResourceCache.GetResource(v, prefab.resourceName);
			}

			string state = Lib.Proto.GetString(m, "state");
			if (!(state == "Static" || state == "Extended" || state == "ExtendedFixed"))
			{
				Profiler.EndSample();
				return;
			}

			double efficiencyFactor = 0.0;

			// Retrieve tracking info
			int trackedSunIndex = Lib.Proto.GetInt(m, "trackedSunIndex");
			bool manualTracking = Lib.Proto.GetBool(m, "manualTracking");
			bool isTracking = prefab.SolarPanel.IsTracking;

			VesselData.SunInfo trackedSunInfo = null;
			for (int i = 0; i < vd.EnvSunsInfo.Count; i++)
			{
				if (vd.EnvSunsInfo[i].SunData.bodyIndex == trackedSunIndex)
				{
					trackedSunInfo = vd.EnvSunsInfo[i];
					break;
				}
			}

			// Auto-tracking logic for background/analytic mode
			if (!manualTracking && isTracking && vd.EnvSunsInfo.Count > 0)
			{
				VesselData.SunInfo bestSun = null;

				if (vd.EnvIsAnalytic)
				{
					// Mode A: Analytic Mode - Find the star providing the maximum solar flux
					double maxFlux = -1.0;
					foreach (var sunInfo in vd.EnvSunsInfo)
					{
						if (sunInfo.SolarFlux > maxFlux)
						{
							maxFlux = sunInfo.SolarFlux;
							bestSun = sunInfo;
						}
					}
				}
				else
				{
					// Mode B: Standard Mode - Find the brightest star that is not currently occluded
					double maxActiveFlux = -1.0;
					foreach (var sunInfo in vd.EnvSunsInfo)
					{
						if (sunInfo.SunlightFactor > 0.05)
						{
							if (sunInfo.SolarFlux > maxActiveFlux)
							{
								maxActiveFlux = sunInfo.SolarFlux;
								bestSun = sunInfo;
							}
						}
					}

					// Fallback: If all stars are blocked, target the nearest star by distance
					if (bestSun == null)
					{
						double minDistance = double.MaxValue;
						Vector3d vesselPos = Lib.VesselPosition(v);
						foreach (var sunInfo in vd.EnvSunsInfo)
						{
							double dist = Vector3d.Distance(vesselPos, sunInfo.SunData.body.position);
							if (dist < minDistance)
							{
								minDistance = dist;
								bestSun = sunInfo;
							}
						}
					}
				}

				if (bestSun != null)
				{
					trackedSunInfo = bestSun;
					// Update the proto if the tracked sun has changed, so it persists
					if (trackedSunIndex != bestSun.SunData.bodyIndex)
					{
						Lib.Proto.Set(m, "trackedSunIndex", bestSun.SunData.bodyIndex);
					}
				}
			}

			if (trackedSunInfo == null && vd.EnvSunsInfo.Count > 0) trackedSunInfo = vd.EnvSunsInfo[0];
			if (trackedSunInfo == null)
			{
				Profiler.EndSample();
				return;
			}

			double powerFactor = CalculateMultiStarPowerAnalytic(v, vd.EnvSunsInfo, trackedSunInfo, prefab.SolarPanel.Type, isTracking);
			efficiencyFactor = powerFactor;

			// get wear factor (output degradation with time)
			if (m.moduleValues.HasNode("timeEfficCurve"))
			{
				teCurve.Load(m.moduleValues.GetNode("timeEfficCurve"));
				double launchUT = Lib.Proto.GetDouble(m, "launchUT");
				efficiencyFactor *= Lib.Clamp(teCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0)), 0.0, 1.0);
			}

			// get nominal panel charge rate at 1 AU
			// don't use the prefab value as some modules that does dynamic switching (SSTU) may have changed it
			double nominalRate = Lib.Proto.GetDouble(m, "nominalRate");

			// calculate output
			double output = nominalRate * efficiencyFactor;

			// produce EC
			ec.Produce(output * elapsed_s, ResourceBroker.SolarPanel);
			Profiler.EndSample();
		}
		#endregion

		#region Other methods
		public bool GetSolarPanelModule()
		{
			// handle the possibility of multiple solar panel and SolarPanelFixer modules on the part
			List<SolarPanelFixer> fixerModules = new List<SolarPanelFixer>();
			foreach (PartModule pm in part.Modules)
			{
				if (pm is SolarPanelFixer fixerModule)
					fixerModules.Add(fixerModule);
			}

			// find the module based on explicitely supported modules
			foreach (PartModule pm in part.Modules)
			{
				if (fixerModules.Exists(p => p.SolarPanel != null && p.SolarPanel.TargetModule == pm))
					continue;

				// mod supported modules
				switch (pm.moduleName)
				{
					case "ModuleCurvedSolarPanel": SolarPanel = new NFSCurvedPanel(); break;
					case "SSTUSolarPanelStatic": SolarPanel = new SSTUStaticPanel(); break;
					case "SSTUSolarPanelDeployable": SolarPanel = new SSTUVeryComplexPanel(); break;
					case "SSTUModularPart": SolarPanel = new SSTUVeryComplexPanel(); break;
					case "ModuleROSolar": SolarPanel = new ROConfigurablePanel(); break;
					case "USSolarSwitch": SolarPanel = new UniversalStorage2Panel(); break;
					case "KopernicusSolarPanel":
						Lib.Log("Part '" + part.partInfo.title + "' use the KopernicusSolarPanel module, please remove it from your config. Kerbalism has it's own support for Kopernicus", Lib.LogLevel.Warning);
						continue;
					default:
						if (pm is ModuleDeployableSolarPanel)
							SolarPanel = new StockPanel(); break;
				}

				if (SolarPanel != null)
				{
					SolarPanel.OnLoad(this, pm);
					break;
				}
			}

			if (SolarPanel == null)
			{
				Lib.Log("Could not find a supported solar panel module, disabling SolarPanelFixer module...", Lib.LogLevel.Warning);
				enabled = isEnabled = moduleIsEnabled = false;
				return false;
			}

			return true;
		}

		private static PanelState GetProtoState(ProtoPartModuleSnapshot protoModule)
		{
			return (PanelState)Enum.Parse(typeof(PanelState), Lib.Proto.GetString(protoModule, "state"));
		}

		private static void SetProtoState(ProtoPartModuleSnapshot protoModule, PanelState newState)
		{
			Lib.Proto.Set(protoModule, "state", newState.ToString());
		}

		public static void ProtoToggleState(SolarPanelFixer prefab, ProtoPartModuleSnapshot protoModule, PanelState currentState)
		{
			switch (currentState)
			{
				case PanelState.Retracted:
					if (prefab.SolarPanel.IsRetractable()) { SetProtoState(protoModule, PanelState.Extended); return; }
					SetProtoState(protoModule, PanelState.ExtendedFixed); return;
				case PanelState.Extended: SetProtoState(protoModule, PanelState.Retracted); return;
			}
		}

		public void ToggleState()
		{
			SolarPanel.ToggleState(state);
		}

		public void ReliabilityEvent(bool isBroken)
		{
			state = isBroken ? PanelState.Failure : SolarPanel.GetState();
			SolarPanel.Break(isBroken);
		}

		/// <summary>
		/// Determines if a single star can potentially be occluded (analytical approximation).
		/// </summary>
		private static bool CanStarCauseEclipse(Vessel v, Vector3d starDir)
		{
			if (v.LandedOrSplashed) return true;

			CelestialBody body = v.mainBody;
			double r_p = body.Radius;
			double r_o = v.altitude + r_p;

			// Angular semi-diameter of the planet
			double sinTheta = Math.Min(r_p / r_o, 1.0);
			double theta = Math.Asin(sinTheta);

			// Orbital normal (In KSP, convert orbit normal to world coordinates)
			Vector3d orbitNormal = v.orbit.GetOrbitNormal().xzy.normalized;

			// Cosine of the star's elevation relative to the orbital plane (essentially sin(inclination))
			double cosI = Math.Abs(Vector3d.Dot(starDir.normalized, orbitNormal));

			// Physical check: If the elevation angle is high enough, the vessel never enters the cylindrical shadow
			return cosI <= Math.Sin(theta);
		}

		/// <summary>
		/// Precise check for occlusion at a specific time (Cylindrical Shadow Model).
		/// </summary>
		private static bool IsOccludedAtTime(Vessel v, Vector3d starDir, double ut)
		{
			Vector3d pos = v.orbit.getPositionAtUT(ut);
			Vector3d bodyPos = v.mainBody.getPositionAtUT(ut);
			Vector3d relativePos = pos - bodyPos;

			// 1. Check if the vessel is on the night side of the planet
			if (Vector3d.Dot(relativePos, starDir) >= 0)
				return false;

			// 2. Calculate the perpendicular distance from the vessel to the shadow axis
			// The shadow axis is a line passing through the planet center parallel to the light rays
			Vector3d projOnSunDir = Vector3d.Project(relativePos, starDir);
			Vector3d distVec = relativePos - projOnSunDir;

			return distVec.magnitude < v.mainBody.Radius;
		}

		/// <summary>
		/// Final implementation: Semi-analytical approach with low-sample expectation accumulation.
		/// Guarantees energy conservation and computational efficiency for both single and multi-star systems.
		/// </summary>
		public static double CalculateMultiStarPowerAnalytic(Vessel v, List<VesselData.SunInfo> suns, VesselData.SunInfo mainSun, ModuleDeployableSolarPanel.PanelType panelType, bool isTracking)
		{
			// Landing/Splashdown Status Handling
			if (Lib.Landed(v))
			{
				return CalculateLandedMultiStarPower(v, suns, mainSun, panelType, isTracking);
			}
			double orbitPeriod = v.orbit.period;
			double ut0 = Planetarium.GetUniversalTime();

			double totalPowerExpectation = 0.0;
			bool isAnalytic = v.KerbalismData().EnvIsAnalytic;
			foreach (var sun in suns)
			{
				if (sun.SolarFlux < 1e-6) continue;

				// Calculate effective incidence angle for this star relative to the panel
				double effectiveCos = 0.0;

				if (panelType == ModuleDeployableSolarPanel.PanelType.SPHERICAL)
				{
					effectiveCos = 0.25;
				}
				else if (panelType == ModuleDeployableSolarPanel.PanelType.CYLINDRICAL)
				{
					effectiveCos = 1.0 / Math.PI;
				}
				else // FLAT
				{
					if (isTracking)
					{
						if (Lib.IsFlight()) { }
						// If tracking, the panel aligns perfectly with the 'mainSun'
						if (sun == mainSun)
						{
							effectiveCos = 1.0; // Primary star, maximum efficiency
						}
						else
						{
							// Efficiency for secondary stars = cosine of the angle between them and the primary star
							// Only contributes if the secondary star is on the "front" side of the panel (angle < 90 deg)
							double cosBetween = Vector3d.Dot(mainSun.Direction.normalized, sun.Direction.normalized);
							effectiveCos = Math.Max(0.0, cosBetween);
						}
					}
					else
					{
						effectiveCos = 1.5 / Math.PI;
					}
				}
				// Accumulate Energy Expectation
				// sun.SolarFlux is fully pre-calculated with occlusion and atmosphere logic.
				totalPowerExpectation += (sun.SolarFlux / Sim.SolarFluxAtHome) * effectiveCos;
			}

			return totalPowerExpectation;
		}

		/// <summary>
		/// Calculates multi-star solar power generation for a landed vessel.
		/// </summary>
		public static double CalculateLandedMultiStarPower(Vessel v, List<VesselData.SunInfo> suns, VesselData.SunInfo mainSun, ModuleDeployableSolarPanel.PanelType panelType, bool isTracking)
		{
			double totalPower = 0.0;

			// --------- Surface normal (works for loaded & unloaded) ----------
			Vector3d vesselPos = Lib.VesselPosition(v);
			Vector3d surfaceNormal = (vesselPos - v.mainBody.position).normalized;
			double latitude = Math.Asin(Vector3d.Dot(surfaceNormal, v.mainBody.transform.up));

			bool isAnalytic = v.KerbalismData().EnvIsAnalytic;
			foreach (var sun in suns)
			{
				double duty = 1.0;
				if (isAnalytic)
				{
					if (sun.SunData.SolarFlux(sun.Distance) < 1e-6) continue;
				}
				else
				{
					if (sun.SolarFlux < 1e-6) continue;
				}

				Vector3d sunDir = sun.Direction.normalized;
				if (isAnalytic)
				{
					// --------- 1. Daylight Duty Cycle ----------
					duty = CalculateSurfaceDaylightDuty(v, v.mainBody, latitude, sunDir, sun.SunData.body);
				}
				if (duty <= 0.0)
					continue;

				// --------- 2. Effective incidence (analytic expectation) ----------
				double effectiveCos;
				bool isLockedToSun = v.mainBody.tidallyLocked && v.mainBody.referenceBody == sun.SunData.body;

				if (panelType == ModuleDeployableSolarPanel.PanelType.SPHERICAL)
				{
					effectiveCos = 0.25;
				}
				else if (panelType == ModuleDeployableSolarPanel.PanelType.CYLINDRICAL)
				{
					effectiveCos = 1.0 / Math.PI;
				}
				else // FLAT
				{
					if (isTracking)
					{
						if (sun == mainSun)
						{
							// Tracking panel: expected cosine on surface
							effectiveCos = 1.0;
						}
						else
						{
							double cosBetween = Vector3d.Dot(mainSun.Direction.normalized, sunDir);
							effectiveCos = Math.Max(0.0, cosBetween);
						}
					}
					else
					{
						if (isLockedToSun)
						{
							// Static panel on locked body: Use actual surface incidence
							// This assumes horizontal placement, which is the standard analytic assumption
							double dot = Vector3d.Dot(surfaceNormal, sunDir);
							effectiveCos = Math.Max(0.0, dot);
						}
						else
						{
							effectiveCos = 1.5 / Math.PI;
						}
					}
				}
				if (isAnalytic)
				{
					double rawFlux = sun.SunData.SolarFlux(sun.Distance);
					totalPower += duty * effectiveCos * (rawFlux * sun.AtmoFactor / Sim.SolarFluxAtHome);
				}
				else
				{
					totalPower += effectiveCos * (sun.SolarFlux / Sim.SolarFluxAtHome);
				}
			}
			return totalPower;
		}
		private static double CalculateSurfaceDaylightDuty(Vessel v, CelestialBody body, double latitude, Vector3d sunDir, CelestialBody sunBody)
		{
			// Tidally locked body
			// Only apply if the body is locked to the sun we are evaluating
			if (body.tidallyLocked && body.referenceBody == sunBody)
			{
				Vector3d vesselPos = Lib.VesselPosition(v);
				Vector3d bodyPos = body.position;
				Vector3d surfaceNormal = (vesselPos - bodyPos).normalized;
				Vector3d toSun = sunDir.normalized;
				double cos = Vector3d.Dot(surfaceNormal, toSun);
				return cos > 0.0 ? 1.0 : 0.0;
			}

			// Non-locked body: analytic daylight fraction
			// cos(H0) = -tan(phi) * tan(delta)
			double declination = Math.Asin(Vector3d.Dot(sunDir, body.transform.up));

			double cosH0 = -Math.Tan(latitude) * Math.Tan(declination);

			if (cosH0 >= 1.0) return 0.0;   // polar night
			if (cosH0 <= -1.0) return 1.0;  // polar day

			double H0 = Math.Acos(cosH0);
			return H0 / Math.PI;
		}
		#endregion

		#region Abstract class for common interaction with supported PartModules
		public abstract class SupportedPanel
		{
			/// <summary>Reference to the SolarPanelFixer, must be set from OnLoad</summary>
			protected SolarPanelFixer fixerModule;

			/// <summary>Reference to the target module</summary>
			public abstract PartModule TargetModule { get; }

			/// <summary>
			/// Will be called by the SolarPanelFixer OnLoad, must set the partmodule reference.
			/// GetState() must be able to return the correct state after this has been called
			/// </summary>
			public abstract void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule);

			/// <summary> Main inititalization method called from OnStart, every hack we do must be done here (In particular the one preventing the target module from generating EC)</summary>
			/// <param name="initialized">will be true if the method has already been called for this module (OnStart can be called multiple times in the editor)</param>
			/// <param name="nominalRate">nominal rate at 1AU</param>
			/// <returns>must return false is something has gone wrong, will disable the whole module</returns>
			public abstract bool OnStart(bool initialized, ref double nominalRate);

			/// <summary>Must return a [0;1] scalar evaluating the local occlusion factor (usually with a physic raycast already done by the target module)</summary>
			/// <param name="occludingPart">if the occluding object is a part, name of the part. MUST return null in all other cases.</param>
			/// <param name="analytic">if true, the returned scalar must account for the given sunDir, so we can't rely on the target module own raycast</param>
			public abstract double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false);

			/// <summary>Must return a [0;1] scalar evaluating the angle of the given sunDir on the panel surface (usually a dot product clamped to [0;1])</summary>
			/// <param name="analytic">if true and the panel is orientable, the returned scalar must be the best possible output (must use the rotation around the pivot)</param>
			public abstract double GetCosineFactor(Vector3d sunDir, bool analytic = false);

			/// <summary>must return the state of the panel, must be able to work before OnStart has been called</summary>
			public abstract PanelState GetState();

			/// <summary>Can be overridden if the target module implement a time efficiency curve. Keys are in hours, values are a scalar in the [0:1] range.</summary>
			public virtual FloatCurve GetTimeCurve() { return new FloatCurve(new Keyframe[] { new Keyframe(0f, 1f) }); }

			/// <summary>Called at Update(), can contain target module specific hacks</summary>
			public virtual void OnUpdate() { }

			/// <summary>Is the panel a sun-tracking panel</summary>
			public virtual bool IsTracking => false;

			/// <summary>Type of the panel</summary>
			public virtual ModuleDeployableSolarPanel.PanelType Type => ModuleDeployableSolarPanel.PanelType.FLAT;

			/// <summary>
			/// Peak cosine/exposure factor for planner nominal estimates.
			/// SPHERICAL is fixed at 0.25; CYLINDRICAL peaks at 1/π; FLAT peaks at 1.
			/// </summary>
			public virtual double GetMaxCosineFactor()
			{
				switch (Type)
				{
					case ModuleDeployableSolarPanel.PanelType.SPHERICAL:
						return 0.25;
					case ModuleDeployableSolarPanel.PanelType.CYLINDRICAL:
						return 1.0 / Math.PI;
					default:
						return 1.0;
				}
			}

			/// <summary>Kopernicus stars support : must set the animation tracked body</summary>
			public virtual void SetTrackedBody(CelestialBody body) { }

			/// <summary>Reliability : specific hacks for the target module that must be applied when the panel is disabled by a failure</summary>
			public virtual void Break(bool isBroken) { }

			public virtual string ResourceName => "ElectricCharge";

			/// <summary>Automation : override this with "return false" if the module doesn't support automation when loaded</summary>
			public virtual bool SupportAutomation(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted:
					case PanelState.Extending:
					case PanelState.Extended:
					case PanelState.Retracting:
						return true;
					default:
						return false;
				}
			}

			/// <summary>Automation : override this with "return false" if the module doesn't support automation when unloaded</summary>
			public virtual bool SupportProtoAutomation(ProtoPartModuleSnapshot protoModule)
			{
				switch (Lib.Proto.GetString(protoModule, "state"))
				{
					case "Retracted":
					case "Extended":
						return true;
					default:
						return false;
				}
			}

			/// <summary>Automation : this must work when called on the prefab module</summary>
			public virtual bool IsRetractable() { return false; }

			/// <summary>Automation : must be implemented if the panel is extendable</summary>
			public virtual void Extend() { }

			/// <summary>Automation : must be implemented if the panel is retractable</summary>
			public virtual void Retract() { }

			///<summary>Automation : Called OnLoad, must set the target module persisted extended/retracted fields to reflect changes done trough automation while unloaded</summary>
			public virtual void SetDeployedStateOnLoad(PanelState state) { }

			///<summary>Automation : convenience method</summary>
			public void ToggleState(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted: Extend(); return;
					case PanelState.Extended: Retract(); return;
				}
			}
		}

		private abstract class SupportedPanel<T> : SupportedPanel where T : PartModule
		{
			public T panelModule;
			public override PartModule TargetModule => panelModule;
		}
		#endregion

		#region Stock module support (ModuleDeployableSolarPanel)
		// stock solar panel module support
		// - we don't support the temperatureEfficCurve
		// - we override the stock UI
		// - we still reuse most of the stock calculations
		// - we let the module fixedupdate/update handle animations/suncatching
		// - we prevent stock EC generation by reseting the reshandler rate
		// - FLAT / CYLINDRICAL / SPHERICAL panel types are supported via GetCosineFactor
		private class StockPanel : SupportedPanel<ModuleDeployableSolarPanel>
		{
			private Transform sunCatcherPosition;   // middle point of the panel surface (usually). Use only position, panel surface direction depend on the pivot transform, even for static panels.
			private Transform sunCatcherPivot;      // If it's a tracking panel, "up" is the pivot axis and "position" is the pivot position. In any case "forward" is the panel surface normal.

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{
				this.fixerModule = fixerModule;
				panelModule = (ModuleDeployableSolarPanel)targetModule;
			}

			public override string ResourceName => panelModule.resourceName;

			public override ModuleDeployableSolarPanel.PanelType Type => panelModule.panelType;

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
				// hide stock ui
				panelModule.Fields["sunAOA"].guiActive = false;
				panelModule.Fields["flowRate"].guiActive = false;
				panelModule.Fields["status"].guiActive = false;

				if (sunCatcherPivot == null)
					sunCatcherPivot = panelModule.part.FindModelComponent<Transform>(panelModule.pivotName);
				if (sunCatcherPosition == null)
					sunCatcherPosition = panelModule.part.FindModelTransform(panelModule.secondaryTransformName);

				if (sunCatcherPosition == null)
				{
					Lib.Log("Could not find suncatcher transform `{0}` in part `{1}`", Lib.LogLevel.Error, panelModule.secondaryTransformName, panelModule.part.name);
					return false;
				}

				// avoid rate lost due to OnStart being called multiple times in the editor
				if (panelModule.resHandler.outputResources[0].rate == 0.0)
					return true;

				nominalRate = panelModule.resHandler.outputResources[0].rate;
				// reset target module rate
				// - This can break mods that evaluate solar panel output for a reason or another (eg: AmpYear, BonVoyage).
				//   We fix that by exploiting the fact that resHandler was introduced in KSP recently, and most of
				//   these mods weren't updated to reflect the changes or are not aware of them, and are still reading
				//   chargeRate. However the stock solar panel ignore chargeRate value during FixedUpdate.
				//   So we only reset resHandler rate.
				panelModule.resHandler.outputResources[0].rate = 0.0;

				return true;
			}

			// akwardness award : stock timeEfficCurve use 24 hours days (1/(24*60/60)) as unit for the curve keys, we convert that to hours
			public override FloatCurve GetTimeCurve()
			{

				if (panelModule.timeEfficCurve?.Curve.length > 1)
				{
					FloatCurve timeCurve = new FloatCurve();
					foreach (Keyframe key in panelModule.timeEfficCurve.Curve.keys)
						timeCurve.Add(key.time * 24f, key.value, key.inTangent * (1f / 24f), key.outTangent * (1f / 24f));
					return timeCurve;
				}
				return base.GetTimeCurve();
			}

			// detect occlusion from the scene colliders using the stock module physics raycast, or our own if analytic mode = true
			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludingFactor = 1.0;
				occludingPart = null;
				RaycastHit raycastHit;
				if (analytic)
				{
					if (sunCatcherPosition == null)
						sunCatcherPosition = panelModule.part.FindModelTransform(panelModule.secondaryTransformName);

					if (sunCatcherPosition == null)
						return occludingFactor;

					Physics.Raycast(sunCatcherPosition.position + (sunDir * panelModule.raycastOffset), sunDir, out raycastHit, 10000f);
				}
				else
				{
					raycastHit = panelModule.hit;
				}

				if (raycastHit.collider != null)
				{
					Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.collider.gameObject);
					if (blockingPart != null)
					{
						// avoid panels from occluding themselves
						if (blockingPart == panelModule.part)
							return occludingFactor;

						occludingPart = blockingPart.partInfo.title;
					}
					occludingFactor = 0.0;
				}
				return occludingFactor;
			}

			// we use the current panel orientation, only doing it ourself when analytic = true
			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
#if DEBUG_SOLAR
				SolarDebugDrawer.DebugLine(sunCatcherPosition.position, sunCatcherPosition.position + sunCatcherPivot.forward, Color.yellow);
				if (panelModule.isTracking) SolarDebugDrawer.DebugLine(sunCatcherPivot.position, sunCatcherPivot.position + (sunCatcherPivot.up * -1f), Color.blue);
#endif
				switch (panelModule.panelType)
				{
					case ModuleDeployableSolarPanel.PanelType.FLAT:
						if (!analytic)
						{
							// trackingDotTransform can be null for a few FixedUpdates after vessel unpack
							if (panelModule.trackingDotTransform == null)
								return 0.0;
							return Math.Max(Vector3d.Dot(sunDir, panelModule.trackingDotTransform.forward), 0.0);
						}

						if (sunCatcherPivot == null)
							return 0.0;

						if (panelModule.isTracking)
							return Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(sunDir, sunCatcherPivot.up)));
						else
							return Math.Max(Vector3d.Dot(sunDir, sunCatcherPivot.forward), 0.0);

					case ModuleDeployableSolarPanel.PanelType.CYLINDRICAL:
						// An orientable cylindrical panel can reach peak exposure when the sun is perpendicular to its axis
						if (analytic && panelModule.isTracking)
							return 1.0 / Math.PI;
						if (panelModule.trackingDotTransform == null)
							return 0.0;
						return Math.Max((1.0 - Math.Abs(Vector3d.Dot(sunDir, panelModule.trackingDotTransform.forward))) * (1.0 / Math.PI), 0.0);
					case ModuleDeployableSolarPanel.PanelType.SPHERICAL:
						return 0.25;
					default:
						return 0.0;
				}
			}

			public override PanelState GetState()
			{
				// Detect modified TotalEnergyRate (B9PS switching of the stock module or ROSolar built-in switching)
				if (panelModule.resHandler.outputResources[0].rate != 0.0)
				{
					OnStart(false, ref fixerModule.nominalRate);
				}

				if (!panelModule.useAnimation)
				{
					if (panelModule.deployState == ModuleDeployablePart.DeployState.BROKEN)
						return PanelState.Broken;

					return PanelState.Static;
				}

				switch (panelModule.deployState)
				{
					case ModuleDeployablePart.DeployState.EXTENDED:
						if (!IsRetractable()) return PanelState.ExtendedFixed;
						return PanelState.Extended;
					case ModuleDeployablePart.DeployState.RETRACTED: return PanelState.Retracted;
					case ModuleDeployablePart.DeployState.RETRACTING: return PanelState.Retracting;
					case ModuleDeployablePart.DeployState.EXTENDING: return PanelState.Extending;
					case ModuleDeployablePart.DeployState.BROKEN: return PanelState.Broken;
				}
				return PanelState.Unknown;
			}

			public override void SetDeployedStateOnLoad(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted:
						panelModule.deployState = ModuleDeployablePart.DeployState.RETRACTED;
						break;
					case PanelState.Extended:
					case PanelState.ExtendedFixed:
						panelModule.deployState = ModuleDeployablePart.DeployState.EXTENDED;
						break;
				}
			}

			public override void Extend() { panelModule.Extend(); }

			public override void Retract() { panelModule.Retract(); }

			public override bool IsRetractable() { return panelModule.retractable; }

			public override void Break(bool isBroken)
			{
				// reenable the target module
				panelModule.isEnabled = !isBroken;
				panelModule.enabled = !isBroken;
				if (isBroken) panelModule.part.FindModelComponents<Animation>().ForEach(k => k.Stop()); // stop the animations if we are disabling it
			}

			public override bool IsTracking => panelModule.isTracking;

			public override void SetTrackedBody(CelestialBody body)
			{
				panelModule.trackingBody = body;
				panelModule.GetTrackingBodyTransforms();
			}

			public override void OnUpdate()
			{
				panelModule.flowRate = (float)fixerModule.currentOutput;
			}
		}
		#endregion

		#region Near Future Solar support (ModuleCurvedSolarPanel)
		// Near future solar curved panel support
		// - We prevent the NFS module from running (disabled at MonoBehavior level)
		// - We replicate the behavior of its FixedUpdate()
		// - We call its Update() method but we disable the KSPFields UI visibility.
		private class NFSCurvedPanel : SupportedPanel<PartModule>
		{
			private Transform[] sunCatchers;    // model transforms named after the "PanelTransformName" field
			private bool deployable;            // "Deployable" field
			private Action panelModuleUpdate;   // delegate for the module Update() method

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{
				this.fixerModule = fixerModule;
				panelModule = targetModule;
				deployable = Lib.ReflectionValue<bool>(panelModule, "Deployable");
			}

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
#if !DEBUG_SOLAR
				try
				{
#endif
					// get a delegate for Update() method (avoid performance penality of reflection)
					panelModuleUpdate = (Action)Delegate.CreateDelegate(typeof(Action), panelModule, "Update");

					// since we are disabling the MonoBehavior, ensure the module Start() has been called
					Lib.ReflectionCall(panelModule, "Start");

					// get transform name from module
					string transform_name = Lib.ReflectionValue<string>(panelModule, "PanelTransformName");

					// get panel components
					sunCatchers = panelModule.part.FindModelTransforms(transform_name);
					if (sunCatchers.Length == 0) return false;

					// disable the module at the Unity level, we will handle its updates manually
					panelModule.enabled = false;

					// return panel nominal rate
					nominalRate = Lib.ReflectionValue<float>(panelModule, "TotalEnergyRate");

					return true;
#if !DEBUG_SOLAR
				}
				catch (Exception ex)
				{
					Lib.Log("SolarPanelFixer : exception while getting ModuleCurvedSolarPanel data : " + ex.Message);
					return false;
				}
#endif
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludedFactor = 1.0;
				occludingPart = null;

				RaycastHit raycastHit;
				foreach (Transform panel in sunCatchers)
				{
					if (Physics.Raycast(panel.position + (sunDir * 0.25), sunDir, out raycastHit, 10000f))
					{
						if (occludingPart == null && raycastHit.collider != null)
						{
							Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.transform.gameObject);
							if (blockingPart != null)
							{
								// avoid panels from occluding themselves
								if (blockingPart == panelModule.part)
									continue;

								occludingPart = blockingPart.partInfo.title;
							}
							occludedFactor -= 1.0 / sunCatchers.Length;
						}
					}
				}

				if (occludedFactor < 1E-5) occludedFactor = 0.0;
				return occludedFactor;
			}

			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				double cosineFactor = 0.0;

				foreach (Transform panel in sunCatchers)
				{
					cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.forward), 0.0);
#if DEBUG_SOLAR
					SolarDebugDrawer.DebugLine(panel.position, panel.position + panel.forward, Color.yellow);
#endif
				}

				return cosineFactor / sunCatchers.Length;
			}

			public override void OnUpdate()
			{
				// manually call the module Update() method since we have disabled the unity Monobehavior
				panelModuleUpdate();

				// hide ui fields
				foreach (BaseField field in panelModule.Fields)
				{
					field.guiActive = false;
				}
			}

			public override PanelState GetState()
			{
				// Detect modified TotalEnergyRate (B9PS switching of the target module)
				double newrate = Lib.ReflectionValue<float>(panelModule, "TotalEnergyRate");
				if (newrate != fixerModule.nominalRate)
				{
					OnStart(false, ref fixerModule.nominalRate);
				}

				string stateStr = Lib.ReflectionValue<string>(panelModule, "SavedState");
				Type enumtype = typeof(ModuleDeployablePart.DeployState);
				if (!Enum.IsDefined(enumtype, stateStr))
				{
					if (!deployable) return PanelState.Static;
					return PanelState.Unknown;
				}

				ModuleDeployablePart.DeployState state = (ModuleDeployablePart.DeployState)Enum.Parse(enumtype, stateStr);

				switch (state)
				{
					case ModuleDeployablePart.DeployState.EXTENDED:
						if (!deployable) return PanelState.Static;
						return PanelState.Extended;
					case ModuleDeployablePart.DeployState.RETRACTED: return PanelState.Retracted;
					case ModuleDeployablePart.DeployState.RETRACTING: return PanelState.Retracting;
					case ModuleDeployablePart.DeployState.EXTENDING: return PanelState.Extending;
					case ModuleDeployablePart.DeployState.BROKEN: return PanelState.Broken;
				}
				return PanelState.Unknown;
			}

			public override void SetDeployedStateOnLoad(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted:
						Lib.ReflectionValue(panelModule, "SavedState", "RETRACTED");
						break;
					case PanelState.Extended:
						Lib.ReflectionValue(panelModule, "SavedState", "EXTENDED");
						break;
				}
			}

			public override void Extend() { Lib.ReflectionCall(panelModule, "DeployPanels"); }

			public override void Retract() { Lib.ReflectionCall(panelModule, "RetractPanels"); }

			public override bool IsRetractable() { return true; }

			public override void Break(bool isBroken)
			{
				// in any case, the monobehavior stays disabled
				panelModule.enabled = false;
				if (isBroken)
					panelModule.isEnabled = false; // hide the extend/retract UI
				else
					panelModule.isEnabled = true; // show the extend/retract UI
			}
		}
		#endregion

		#region SSTU static multi-panel module support (SSTUSolarPanelStatic)
		// - We prevent the module from running (disabled at MonoBehavior level and KSP level)
		// - We replicate the behavior by ourselves
		private class SSTUStaticPanel : SupportedPanel<PartModule>
		{
			private Transform[] sunCatchers;    // model transforms named after the "PanelTransformName" field

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{ this.fixerModule = fixerModule; panelModule = targetModule; }

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
				// disable it completely
				panelModule.enabled = panelModule.isEnabled = panelModule.moduleIsEnabled = false;
#if !DEBUG_SOLAR
				try
				{
#endif
					// method that parse the suncatchers "suncatcherTransforms" config string into a List<string>
					Lib.ReflectionCall(panelModule, "parseTransformData");
					// method that get the transform list (panelData) from the List<string>
					Lib.ReflectionCall(panelModule, "findTransforms");
					// get the transforms
					sunCatchers = Lib.ReflectionValue<List<Transform>>(panelModule, "panelData").ToArray();
					// the nominal rate defined in SSTU is per transform
					nominalRate = Lib.ReflectionValue<float>(panelModule, "resourceAmount") * sunCatchers.Length;
					return true;
#if !DEBUG_SOLAR
				}
				catch (Exception ex)
				{
					Lib.Log("SolarPanelFixer : exception while getting SSTUSolarPanelStatic data : " + ex.Message);
					return false;
				}
#endif
			}

			// exactly the same code as NFS curved panel
			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				double cosineFactor = 0.0;

				foreach (Transform panel in sunCatchers)
				{
					cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.forward), 0.0);
#if DEBUG_SOLAR
					SolarDebugDrawer.DebugLine(panel.position, panel.position + panel.forward, Color.yellow);
#endif
				}

				return cosineFactor / sunCatchers.Length;
			}

			// exactly the same code as NFS curved panel
			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludedFactor = 1.0;
				occludingPart = null;

				RaycastHit raycastHit;
				foreach (Transform panel in sunCatchers)
				{
					if (Physics.Raycast(panel.position + (sunDir * 0.25), sunDir, out raycastHit, 10000f))
					{
						if (occludingPart == null && raycastHit.collider != null)
						{
							Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.transform.gameObject);
							if (blockingPart != null)
							{
								// avoid panels from occluding themselves
								if (blockingPart == panelModule.part)
									continue;

								occludingPart = blockingPart.partInfo.title;
							}
							occludedFactor -= 1.0 / sunCatchers.Length;
						}
					}
				}

				if (occludedFactor < 1E-5) occludedFactor = 0.0;
				return occludedFactor;
			}

			public override PanelState GetState() { return PanelState.Static; }

			public override bool SupportAutomation(PanelState state) { return false; }

			public override bool SupportProtoAutomation(ProtoPartModuleSnapshot protoModule) { return false; }

			public override void Break(bool isBroken)
			{
				// in any case, everything stays disabled
				panelModule.enabled = panelModule.isEnabled = panelModule.moduleIsEnabled = false;
			}
		}
		#endregion

		#region SSTU deployable/tracking multi-panel support (SSTUSolarPanelDeployable/SSTUModularPart)
		// SSTU common support for all solar panels that rely on the SolarModule/AnimationModule classes
		// - We prevent stock EC generation by setting to 0.0 the fields from where SSTU is getting the rates
		// - We use our own data structure that replicate the multiple panel per part possibilities, it store the transforms we need
		// - We use an aggregate of the nominal rate of each panel and assume all panels on the part are the same (not an issue currently, but the possibility exists in SSTU)
		// - Double-pivot panels that use multiple partmodules (I think there is only the "ST-MST-ISS solar truss" that does that) aren't supported
		// - Automation is currently not supported. Might be doable, but I don't have to mental strength to deal with it.
		// - Reliability is 100% untested and has a very barebones support. It should disable the EC output but not animations nor extend/retract ability.
		private class SSTUVeryComplexPanel : SupportedPanel<PartModule>
		{
			private object solarModuleSSTU; // instance of the "SolarModule" class
			private object animationModuleSSTU; // instance of the "AnimationModule" class
			private Func<string> getAnimationState; // delegate for the AnimationModule.persistentData property (string of the animState struct)
			private List<SSTUPanelData> panels;
			private TrackingType trackingType = TrackingType.Unknown;
			private enum TrackingType { Unknown = 0, Fixed, SinglePivot, DoublePivot }
			private string currentModularVariant;

			private class SSTUPanelData
			{
				public Transform pivot;
				public Axis pivotAxis;
				public SSTUSunCatcher[] suncatchers;

				public class SSTUSunCatcher
				{
					public object objectRef; // reference to the "SuncatcherData" class instance, used to get the raycast hit (direct ref to the RaycastHit doesn't work)
					public Transform transform;
					public Axis axis;
				}

				public bool IsValid => suncatchers[0].transform != null;
				public Vector3 PivotAxisVector => GetDirection(pivot, pivotAxis);
				public int SuncatcherCount => suncatchers.Length;
				public Vector3 SuncatcherPosition(int index) => suncatchers[index].transform.position;
				public Vector3 SuncatcherAxisVector(int index) => GetDirection(suncatchers[index].transform, suncatchers[index].axis);
				public RaycastHit SuncatcherHit(int index) => Lib.ReflectionValue<RaycastHit>(suncatchers[index].objectRef, "hitData");

				public enum Axis { XPlus, XNeg, YPlus, YNeg, ZPlus, ZNeg }
				public static Axis ParseSSTUAxis(object sstuAxis) { return (Axis)Enum.Parse(typeof(Axis), sstuAxis.ToString()); }
				private Vector3 GetDirection(Transform transform, Axis axis)
				{
					switch (axis) // I hope I got this right
					{
						case Axis.XPlus: return transform.right;
						case Axis.XNeg: return transform.right * -1f;
						case Axis.YPlus: return transform.up;
						case Axis.YNeg: return transform.up * -1f;
						case Axis.ZPlus: return transform.forward;
						case Axis.ZNeg: return transform.forward * -1f;
						default: return Vector3.zero;
					}
				}
			}

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{ this.fixerModule = fixerModule; panelModule = targetModule; }

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
#if !DEBUG_SOLAR
				try
				{
#endif
					// get a reference to the "SolarModule" class instance, it has everything we need (transforms, rates, etc...)
					switch (panelModule.moduleName)
					{
						case "SSTUModularPart":
							solarModuleSSTU = Lib.ReflectionValue<object>(panelModule, "solarFunctionsModule");
							currentModularVariant = Lib.ReflectionValue<string>(panelModule, "currentSolar");
							break;
						case "SSTUSolarPanelDeployable":
							solarModuleSSTU = Lib.ReflectionValue<object>(panelModule, "solarModule");
							break;
						default:
							return false;
					}

					// Get animation module
					animationModuleSSTU = Lib.ReflectionValue<object>(solarModuleSSTU, "animModule");
					// Get animation state property delegate
					PropertyInfo prop = animationModuleSSTU.GetType().GetProperty("persistentData");
					getAnimationState = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), animationModuleSSTU, prop.GetGetMethod());

					// SSTU stores the sum of the nominal output for all panels in the part, we retrieve it
					float newNominalrate = Lib.ReflectionValue<float>(solarModuleSSTU, "standardPotentialOutput");
					// OnStart can be called multiple times in the editor, but we might already have reset the rate
					// In the editor, if the "no panel" variant is selected, newNominalrate will be 0.0, so also check initialized
					if (newNominalrate > 0.0 || initialized == false)
					{
						nominalRate = newNominalrate;
						// reset the rate sum in the SSTU module. This won't prevent SSTU from generating EC, but this way we can keep track of what we did
						// don't doit in the editor as it isn't needed and we need it in case of variant switching
						if (Lib.IsFlight()) Lib.ReflectionValue(solarModuleSSTU, "standardPotentialOutput", 0f);
					}

					panels = new List<SSTUPanelData>();
					object[] panelDataArray = Lib.ReflectionValue<object[]>(solarModuleSSTU, "panelData"); // retrieve the PanelData class array that contain suncatchers and pivots data arrays
					foreach (object panel in panelDataArray)
					{
						object[] suncatchers = Lib.ReflectionValue<object[]>(panel, "suncatchers"); // retrieve the SuncatcherData class array
						object[] pivots = Lib.ReflectionValue<object[]>(panel, "pivots"); // retrieve the SolarPivotData class array

						int suncatchersCount = suncatchers.Length;
						if (suncatchers == null || pivots == null || suncatchersCount == 0) continue;

						// instantiate our data class
						SSTUPanelData panelData = new SSTUPanelData();

						// get suncatcher transforms and the orientation of the panel surface normal
						panelData.suncatchers = new SSTUPanelData.SSTUSunCatcher[suncatchersCount];
						for (int i = 0; i < suncatchersCount; i++)
						{
							object suncatcher = suncatchers[i];
							if (Lib.IsFlight()) Lib.ReflectionValue(suncatcher, "resourceRate", 0f); // actually prevent SSTU modules from generating EC, but not in the editor
							panelData.suncatchers[i] = new SSTUPanelData.SSTUSunCatcher();
							panelData.suncatchers[i].objectRef = suncatcher; // keep a reference to the original suncatcher instance, for raycast hit acquisition
							panelData.suncatchers[i].transform = Lib.ReflectionValue<Transform>(suncatcher, "suncatcher"); // get suncatcher transform
							panelData.suncatchers[i].axis = SSTUPanelData.ParseSSTUAxis(Lib.ReflectionValue<object>(suncatcher, "suncatcherAxis")); // get suncatcher axis
						}

						// get pivot transform and the pivot axis. Only needed for single-pivot tracking panels
						// double axis panels can have 2 pivots. Its seems the suncatching one is always the second.
						// For our purpose we can just assume always perfect alignement anyway.
						// Note : some double-pivot panels seems to use a second SSTUSolarPanelDeployable instead, we don't support those.
						switch (pivots.Length)
						{
							case 0:
								trackingType = TrackingType.Fixed; break;
							case 1:
								trackingType = TrackingType.SinglePivot;
								panelData.pivot = Lib.ReflectionValue<Transform>(pivots[0], "pivot");
								panelData.pivotAxis = SSTUPanelData.ParseSSTUAxis(Lib.ReflectionValue<object>(pivots[0], "pivotRotationAxis"));
								break;
							case 2:
								trackingType = TrackingType.DoublePivot; break;
							default: continue;
						}

						panels.Add(panelData);
					}

					// disable ourselves if no panel was found
					if (panels.Count == 0) return false;

					// hide PAW status fields
					switch (panelModule.moduleName)
					{
						case "SSTUModularPart": panelModule.Fields["solarPanelStatus"].guiActive = false; break;
						case "SSTUSolarPanelDeployable": foreach (var field in panelModule.Fields) field.guiActive = false; break;
					}
					return true;
#if !DEBUG_SOLAR
				}
				catch (Exception ex)
				{
					Lib.Log("SolarPanelFixer : exception while getting SSTUModularPart/SSTUSolarPanelDeployable solar panel data : " + ex.Message);
					return false;
				}
#endif
			}

			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				double cosineFactor = 0.0;
				int suncatcherTotalCount = 0;
				foreach (SSTUPanelData panel in panels)
				{
					if (!panel.IsValid) continue;
					suncatcherTotalCount += panel.SuncatcherCount;
					for (int i = 0; i < panel.SuncatcherCount; i++)
					{
#if DEBUG_SOLAR
						SolarDebugDrawer.DebugLine(panel.SuncatcherPosition(i), panel.SuncatcherPosition(i) + panel.SuncatcherAxisVector(i), Color.yellow);
						if (trackingType == TrackingType.SinglePivot) SolarDebugDrawer.DebugLine(panel.pivot.position, panel.pivot.position + (panel.PivotAxisVector * -1f), Color.blue);
#endif

						if (!analytic) { cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.SuncatcherAxisVector(i)), 0.0); continue; }

						switch (trackingType)
						{
							case TrackingType.Fixed: cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.SuncatcherAxisVector(i)), 0.0); continue;
							case TrackingType.SinglePivot: cosineFactor += Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(sunDir, panel.PivotAxisVector))); continue;
							case TrackingType.DoublePivot: cosineFactor += 1.0; continue;
						}
					}
				}
				return cosineFactor / suncatcherTotalCount;
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludingFactor = 0.0;
				occludingPart = null;
				int suncatcherTotalCount = 0;
				foreach (SSTUPanelData panel in panels)
				{
					if (!panel.IsValid) continue;
					suncatcherTotalCount += panel.SuncatcherCount;
					for (int i = 0; i < panel.SuncatcherCount; i++)
					{
						RaycastHit raycastHit;
						if (analytic)
							Physics.Raycast(panel.SuncatcherPosition(i) + (sunDir * 0.25), sunDir, out raycastHit, 10000f);
						else
							raycastHit = panel.SuncatcherHit(i);

						if (raycastHit.collider != null)
						{
							occludingFactor += 1.0; // in case of multiple panels per part, it is perfectly valid for panels to occlude themselves so we don't do the usual check
							Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.transform.gameObject);
							if (occludingPart == null && blockingPart != null) // don't update if occlusion is from multiple parts
								occludingPart = blockingPart.partInfo.title;
						}
					}
				}
				occludingFactor = 1.0 - (occludingFactor / suncatcherTotalCount);
				if (occludingFactor < 0.01) occludingFactor = 0.0; // avoid precison issues
				return occludingFactor;
			}

			public override PanelState GetState()
			{
				switch (trackingType)
				{
					case TrackingType.Fixed: return PanelState.Static;
					case TrackingType.Unknown: return PanelState.Unknown;
				}
#if !DEBUG_SOLAR
				try
				{
#endif
					// handle solar panel variant switching in SSTUModularPart
					if (Lib.IsEditor() && panelModule.ClassName == "SSTUModularPart")
					{
						string newVariant = Lib.ReflectionValue<string>(panelModule, "currentSolar");
						if (newVariant != currentModularVariant)
						{
							currentModularVariant = newVariant;
							OnStart(false, ref fixerModule.nominalRate);
						}
					}
					// get animation state
					switch (getAnimationState())
					{
						case "STOPPED_START": return PanelState.Retracted;
						case "STOPPED_END": return PanelState.Extended;
						case "PLAYING_FORWARD": return PanelState.Extending;
						case "PLAYING_BACKWARD": return PanelState.Retracting;
					}
#if !DEBUG_SOLAR
				}
				catch { return PanelState.Unknown; }
#endif
				return PanelState.Unknown;
			}

			public override bool IsTracking => trackingType == TrackingType.SinglePivot || trackingType == TrackingType.DoublePivot;

			public override void SetTrackedBody(CelestialBody body)
			{
				Lib.ReflectionValue(solarModuleSSTU, "trackedBodyIndex", body.flightGlobalsIndex);
			}

			public override bool SupportAutomation(PanelState state) { return false; }

			public override bool SupportProtoAutomation(ProtoPartModuleSnapshot protoModule) { return false; }
		}
		#endregion

		#region ROSolar switcheable/resizeable MDSP derivative (ModuleROSolar)
		// Made by Pap for RO. Implement in-editor model switching / resizing on top of the stock module.
		// TODO: Tracking panels implemented in v1.1 (May 2020).  Need further work here to get those working?
		// Plugin is here : https://github.com/KSP-RO/ROLibrary/blob/master/Source/ROLib/Modules/ModuleROSolar.cs
		// Configs are here : https://github.com/KSP-RO/ROSolar
		// Require the following MM patch to work :
		/*
		@PART:HAS[@MODULE[ModuleROSolar]]:AFTER[zzzKerbalism] { %MODULE[SolarPanelFixer]{} }
		*/
		private class ROConfigurablePanel : StockPanel
		{
			// Note : this has been implemented in the base class (StockPanel) because
			// we have the same issue with NearFutureSolar B9PS-switching its MDSP modules.

			/*
			public override PanelState GetState()
			{
				// We set the resHandler rate to 0 in StockPanel.OnStart(), and ModuleROSolar set it back
				// to the new nominal rate after some switching/resizing has been done (see ModuleROSolar.RecalculateStats()),
				// so don't complicate things by using events and just call StockPanel.OnStart() if we detect a non-zero rate.
				if (Lib.IsEditor() && panelModule.resHandler.outputResources[0].rate != 0.0)
					OnStart(false, ref fixerModule.nominalRate);

				return base.GetState();
			}
			*/
		}

		#endregion

		#region Universal Storage 2 support (USSolarSwitch)
		private class UniversalStorage2Panel : SupportedPanel<PartModule>
		{
			private List<Transform> sunCatchers;
			private float[] chargeRates;
			private int lastSelection = -1;
			private MethodInfo deployMethod;

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{
				this.fixerModule = fixerModule;
				panelModule = targetModule;
			}

			public override string ResourceName => Lib.ReflectionValue<string>(panelModule, "resourceName");

			public override ModuleDeployableSolarPanel.PanelType Type => ModuleDeployableSolarPanel.PanelType.FLAT;

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
				// Disable the target module to stop its FixedUpdate (power generation)
				panelModule.enabled = false;

				string chargeRateStr = Lib.ReflectionValue<string>(panelModule, "chargeRate");
				if (!string.IsNullOrEmpty(chargeRateStr))
				{
					string[] rates = chargeRateStr.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
					chargeRates = new float[rates.Length];
					for (int i = 0; i < rates.Length; i++)
					{
						float.TryParse(rates[i], out chargeRates[i]);
					}
				}
				else
				{
					chargeRates = new float[] { 0f };
				}

				sunCatchers = new List<Transform>();
				string secondaryTransformName = Lib.ReflectionValue<string>(panelModule, "secondaryTransformName");
				if (!string.IsNullOrEmpty(secondaryTransformName))
				{
					Transform[] transforms = panelModule.part.FindModelTransforms(secondaryTransformName);
					if (transforms != null)
						sunCatchers.AddRange(transforms);
				}

				if (sunCatchers.Count == 0)
				{
					Lib.Log("Could not find suncatcher transform `{0}` in part `{1}`", Lib.LogLevel.Error, secondaryTransformName, panelModule.part.name);
					return false;
				}

				deployMethod = panelModule.GetType().GetMethod("DeploySolarPanels", new Type[] { typeof(bool) });

				UpdateNominalRate();
				nominalRate = fixerModule.nominalRate;

				// We don't need to zero out rate if we disabled the module, 
				// but let's do it once just in case.
				ZeroOutRate();

				return true;
			}

			private void UpdateNominalRate()
			{
				if (chargeRates == null || chargeRates.Length == 0)
				{
					fixerModule.nominalRate = 0.0;
					return;
				}

				int selection = Lib.ReflectionValue<int>(panelModule, "CurrentSelection");
				if (selection >= 0 && selection < chargeRates.Length)
				{
					fixerModule.nominalRate = chargeRates[selection];
				}
				else
				{
					fixerModule.nominalRate = 0.0;
				}
				lastSelection = selection;
			}

			private void ZeroOutRate()
			{
				if (panelModule.resHandler != null && panelModule.resHandler.outputResources.Count > 0)
				{
					foreach (var res in panelModule.resHandler.outputResources) res.rate = 0.0;
				}
			}

			public override void OnUpdate()
			{
				// Check for variant switching
				int currentSelection = Lib.ReflectionValue<int>(panelModule, "CurrentSelection");
				if (currentSelection != lastSelection)
				{
					UpdateNominalRate();
				}

				// Hide UI fields
				foreach (BaseField field in panelModule.Fields)
				{
					field.guiActive = false;
					field.guiActiveEditor = false;
				}
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				occludingPart = null;
				double totalOcclusion = 0;
				int count = 0;

				foreach (Transform t in sunCatchers)
				{
					if (t == null) continue;

					if (analytic)
					{
						RaycastHit raycastHit;
						if (Physics.Raycast(t.position + (sunDir * 0.1f), sunDir, out raycastHit, 10000f))
						{
						}
						else
						{
							totalOcclusion += 1.0;
						}
					}
					else
					{
						string tempOccludingPart = null;
						if (Visible(t, sunDir, out tempOccludingPart))
						{
							totalOcclusion += 1.0;
						}
						else
						{
							if (occludingPart == null) occludingPart = tempOccludingPart;
						}
					}
					count++;
				}

				if (count == 0) return 0.0;
				return totalOcclusion / count;
			}

			private bool Visible(Transform t, Vector3d sunDir, out string occludingPart)
			{
				occludingPart = null;
				RaycastHit hit;
				if (Physics.Raycast(t.position + (sunDir * 0.25f), sunDir, out hit, 10000f))
				{
					Part p = hit.collider.GetComponentInParent<Part>();
					if (p == panelModule.part) return true;

					if (p != null) occludingPart = p.partInfo.title;
					return false;
				}
				return true;
			}

			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				double totalCos = 0;
				int count = 0;
				foreach (Transform t in sunCatchers)
				{
					if (t == null) continue;
					double dot = Vector3.Dot(t.forward, sunDir);
					totalCos += Math.Max(0.0, dot);
					count++;
				}
				if (count == 0) return 0.0;
				return totalCos / count;
			}

			public override PanelState GetState()
			{
				bool isActive = Lib.ReflectionValue<bool>(panelModule, "IsActive");
				bool isFixed = Lib.ReflectionValue<bool>(panelModule, "IsFixed");
				bool isDeployed = Lib.ReflectionValue<bool>(panelModule, "IsDeployed");

				if (!isActive) return PanelState.Retracted;
				if (isFixed) return PanelState.Static;
				if (isDeployed) return PanelState.Extended;
				return PanelState.Retracted;
			}

			public override bool SupportAutomation(PanelState state)
			{
				bool isFixed = Lib.ReflectionValue<bool>(panelModule, "IsFixed");
				if (isFixed) return false;
				return base.SupportAutomation(state);
			}

			public override void Extend()
			{
				bool isFixed = Lib.ReflectionValue<bool>(panelModule, "IsFixed");
				if (isFixed) return;
				if (deployMethod != null) deployMethod.Invoke(panelModule, new object[] { true });
			}

			public override void Retract()
			{
				bool isFixed = Lib.ReflectionValue<bool>(panelModule, "IsFixed");
				if (isFixed) return;
				if (deployMethod != null) deployMethod.Invoke(panelModule, new object[] { false });
			}

			public override void SetDeployedStateOnLoad(PanelState state)
			{
				bool isFixed = Lib.ReflectionValue<bool>(panelModule, "IsFixed");
				if (isFixed) return;

				if (deployMethod == null)
					deployMethod = panelModule.GetType().GetMethod("DeploySolarPanels", new Type[] { typeof(bool) });

				if (deployMethod != null)
				{
					if (state == PanelState.Extended || state == PanelState.ExtendedFixed)
						deployMethod.Invoke(panelModule, new object[] { true });
					else if (state == PanelState.Retracted)
						deployMethod.Invoke(panelModule, new object[] { false });
				}
			}
		}
		#endregion
	}

	#region Utility class for drawing vectors on screen
	// Source : https://github.com/sarbian/DebugStuff/blob/master/DebugDrawer.cs
	// By Sarbian, released under MIT I think
	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	class SolarDebugDrawer : MonoBehaviour
	{
		private static readonly List<Line> lines = new List<Line>();
		private static readonly List<Point> points = new List<Point>();
		private static readonly List<Trans> transforms = new List<Trans>();
		public Material lineMaterial;

		private struct Line
		{
			public readonly Vector3 start;
			public readonly Vector3 end;
			public readonly Color color;

			public Line(Vector3 start, Vector3 end, Color color)
			{
				this.start = start;
				this.end = end;
				this.color = color;
			}
		}

		private struct Point
		{
			public readonly Vector3 pos;
			public readonly Color color;

			public Point(Vector3 pos, Color color)
			{
				this.pos = pos;
				this.color = color;
			}
		}

		private struct Trans
		{
			public readonly Vector3 pos;
			public readonly Vector3 up;
			public readonly Vector3 right;
			public readonly Vector3 forward;

			public Trans(Vector3 pos, Vector3 up, Vector3 right, Vector3 forward)
			{
				this.pos = pos;
				this.up = up;
				this.right = right;
				this.forward = forward;
			}
		}

		[Conditional("DEBUG_SOLAR")]
		public static void DebugLine(Vector3 start, Vector3 end, Color col)
		{
			lines.Add(new Line(start, end, col));
		}

		[Conditional("DEBUG_SOLAR")]
		public static void DebugPoint(Vector3 start, Color col)
		{
			points.Add(new Point(start, col));
		}

		[Conditional("DEBUG_SOLAR")]
		public static void DebugTransforms(Transform t)
		{
			transforms.Add(new Trans(t.position, t.up, t.right, t.forward));
		}

		[Conditional("DEBUG_SOLAR")]
		private void Start()
		{
			DontDestroyOnLoad(this);
			if (!lineMaterial)
			{
				Shader shader = Shader.Find("Hidden/Internal-Colored");
				lineMaterial = new Material(shader);
				lineMaterial.hideFlags = HideFlags.HideAndDontSave;
				lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
				lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
				lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
				lineMaterial.SetInt("_ZWrite", 0);
				lineMaterial.SetInt("_ZWrite", (int)UnityEngine.Rendering.CompareFunction.Always);
			}
			StartCoroutine("EndOfFrameDrawing");
		}

		private IEnumerator EndOfFrameDrawing()
		{
			UnityEngine.Debug.Log("DebugDrawer starting");
			while (true)
			{
				yield return new WaitForEndOfFrame();

				Camera cam = GetActiveCam();

				if (cam == null) continue;

				try
				{
					transform.position = Vector3.zero;

					GL.PushMatrix();
					lineMaterial.SetPass(0);

					// In a modern Unity we would use cam.projectionMatrix.decomposeProjection to get the decomposed matrix
					// and Matrix4x4.Frustum(FrustumPlanes frustumPlanes) to get a new one

					// Change the far clip plane of the projection matrix
					Matrix4x4 projectionMatrix = Matrix4x4.Perspective(cam.fieldOfView, cam.aspect, cam.nearClipPlane, float.MaxValue);
					GL.LoadProjectionMatrix(projectionMatrix);
					GL.MultMatrix(cam.worldToCameraMatrix);
					//GL.Viewport(new Rect(0, 0, Screen.width, Screen.height));

					GL.Begin(GL.LINES);

					for (int i = 0; i < lines.Count; i++)
					{
						Line line = lines[i];
						DrawLine(line.start, line.end, line.color);
					}

					for (int i = 0; i < points.Count; i++)
					{
						Point point = points[i];
						DrawPoint(point.pos, point.color);
					}

					for (int i = 0; i < transforms.Count; i++)
					{
						Trans t = transforms[i];
						DrawTransform(t.pos, t.up, t.right, t.forward);
					}
				}
				catch (Exception e)
				{
					UnityEngine.Debug.Log("EndOfFrameDrawing Exception" + e);
				}
				finally
				{
					GL.End();
					GL.PopMatrix();

					lines.Clear();
					points.Clear();
					transforms.Clear();
				}
			}
		}

		private static Camera GetActiveCam()
		{
			if (!HighLogic.fetch)
				return Camera.main;

			if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch)
				return EditorLogic.fetch.editorCamera;

			if (HighLogic.LoadedSceneIsFlight && PlanetariumCamera.fetch && FlightCamera.fetch)
				return MapView.MapIsEnabled ? PlanetariumCamera.Camera : FlightCamera.fetch.mainCamera;

			return Camera.main;
		}

		private static void DrawLine(Vector3 origin, Vector3 destination, Color color)
		{
			GL.Color(color);
			GL.Vertex(origin);
			GL.Vertex(destination);
		}

		private static void DrawRay(Vector3 origin, Vector3 direction, Color color)
		{
			GL.Color(color);
			GL.Vertex(origin);
			GL.Vertex(origin + direction);
		}

		private static void DrawTransform(Vector3 position, Vector3 up, Vector3 right, Vector3 forward, float scale = 1.0f)
		{
			DrawRay(position, up * scale, Color.green);
			DrawRay(position, right * scale, Color.red);
			DrawRay(position, forward * scale, Color.blue);
		}

		private static void DrawPoint(Vector3 position, Color color, float scale = 1.0f)
		{
			DrawRay(position + Vector3.up * (scale * 0.5f), -Vector3.up * scale, color);
			DrawRay(position + Vector3.right * (scale * 0.5f), -Vector3.right * scale, color);
			DrawRay(position + Vector3.forward * (scale * 0.5f), -Vector3.forward * scale, color);
		}
	}
	#endregion
} // KERBALISM
