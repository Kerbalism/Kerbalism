using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
		/// <summary>Main PAW info label</summary>
		[KSPField(guiActive = true, guiActiveEditor = false, guiName = "Solar panel")]
		public string panelStatus = string.Empty;

		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Solar panel output")]
		[UI_Toggle(enabledText = "<color=#00ff00>simulated</color>", disabledText = "<color=#ffff00>ignored</color>")]
		public bool editorEnabled = true;

		/// <summary>nominal rate at 1 UA (Kerbin distance from the sun)</summary>
		[KSPField(isPersistant = true)]
		public double nominalRate = 10.0; // doing this on the purpose of not breaking existing saves

		/// <summary>aggregate efficiency factor for angle exposure losses and occlusion from parts</summary>
		[KSPField(isPersistant = true)]
		public double persistentFactor = 1.0; // doing this on the purpose of not breaking existing saves

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
		[KSPField]
		public FloatCurve timeEfficCurve;

		/// <summary>UT of part creation in flight, used to evaluate the timeEfficCurve</summary>
		[KSPField(isPersistant = true)]
		public double launchUT = -1.0;

		/// <summary>internal object for handling the various hacks depending on the target solar panel module</summary>
		public SupportedPanel SolarPanel { get; private set; }

		/// <summary>current state of the module</summary>
		public bool isInitialized = false;

		/// <summary>for tracking analytic mode changes and ui updating</summary>
		private bool analyticSunlight;

		// The following fields are local to FixedUpdate() but are shared for status string updates in Update()
		// Their value can be inconsistent, don't rely on them for anything else
		private double currentOutput;
		private double exposureFactor;
		private double wearFactor;
		private ExposureState exposureState;
		private string mainOccludingPart;
		private string rateFormat;

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

		[KSPEvent(active = true, guiActive = true, guiName = "Select tracked star")]
		public void ManualTracking()
		{
			// Assemble the buttons
			DialogGUIBase[] options = new DialogGUIBase[Sim.suns.Count + 1];
			options[0] = new DialogGUIButton("Automatic", () => { manualTracking = false; }, true);
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
				"SelectTrackingBody",
				"Select the star you want to track with this solar panel.",
				"Select tracked star",
				UISkinManager.GetSkin("MainMenuSkin"),
				options), false, UISkinManager.GetSkin("MainMenuSkin"));
		}

		public override void OnLoad(ConfigNode node)
		{
			if (SolarPanel == null && !GetSolarPanelModule())
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

			// not sure why (I guess because of the KSPField attribute), but timeEfficCurve is instanciated with 0 keys by something instead of being null
			if (timeEfficCurve == null || timeEfficCurve.Curve.keys.Length == 0)
				timeEfficCurve = SolarPanel.GetTimeCurve();

			if (Lib.IsFlight() && launchUT < 0.0)
				launchUT = Planetarium.GetUniversalTime();

			// setup star selection GUI
			Events["ManualTracking"].active = Sim.suns.Count > 1 && SolarPanel.IsTracking;
			Events["ManualTracking"].guiActive = state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static;

			// setup target module animation for custom star tracking
			SolarPanel.SetTrackedBody(FlightGlobals.Bodies[trackedSunIndex]);

			// set how many decimal points are needed to show the panel Ec output in the UI
			if (nominalRate < 0.1) rateFormat = "F4";
			else if (nominalRate < 1.0) rateFormat = "F3";
			else if (nominalRate < 10.0) rateFormat = "F2";
			else rateFormat = "F1";
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
			if (!vd.IsValid) return;

			// calculate average exposure over a full day when landed, will be used for panel background processing
			node.SetValue("persistentFactor", GetAnalyticalCosineFactorLanded(vd));
		}

		public void Update()
		{
			// sanity check
			if (SolarPanel == null) return;

			// call Update specfic handling, if any
			SolarPanel.OnUpdate();

			// Do nothing else in the editor
			if (Lib.IsEditor()) return;

			// Update tracked body selection button (Kopernicus multi-star support)
			if (Events["ManualTracking"].active && (state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static))
			{
				Events["ManualTracking"].guiActive = true;
				Events["ManualTracking"].guiName = Lib.BuildString("Tracked star ", manualTracking ? ": " : "[Auto] : ", FlightGlobals.Bodies[trackedSunIndex].bodyDisplayName.Replace("^N", ""));
			}
			else
			{
				Events["ManualTracking"].guiActive = false;
			}

			// Update main status field visibility
			if (state == PanelState.Failure || state == PanelState.Unknown)
				Fields["panelStatus"].guiActive = false;
			else
				Fields["panelStatus"].guiActive = true;

			// Update main status field text
			switch (exposureState)
			{
				case ExposureState.InShadow:
					panelStatus = "<color=#ff2222>in shadow</color>";
					if (currentOutput > 0.001) panelStatus = Lib.BuildString(currentOutput.ToString(rateFormat), " EC/s, ", panelStatus);
					break;
				case ExposureState.OccludedTerrain:
					panelStatus = "<color=#ff2222>occluded by terrain</color>";
					if (currentOutput > 0.001) panelStatus = Lib.BuildString(currentOutput.ToString(rateFormat), " EC/s, ", panelStatus);
					break;
				case ExposureState.OccludedPart:
					panelStatus = Lib.BuildString("<color=#ff2222>occluded by ", mainOccludingPart, "</color>");
					if (currentOutput > 0.001) panelStatus = Lib.BuildString(currentOutput.ToString(rateFormat), " EC/s, ", panelStatus);
					break;
				case ExposureState.BadOrientation:
					panelStatus = "<color=#ff2222>bad orientation</color>";
					if (currentOutput > 0.001) panelStatus = Lib.BuildString(currentOutput.ToString(rateFormat), " EC/s, ", panelStatus);
					break;
				case ExposureState.Disabled:
					switch (state)
					{
						case PanelState.Retracted: panelStatus = "retracted"; break;
						case PanelState.Extending: panelStatus = "extending"; break;
						case PanelState.Retracting: panelStatus = "retracting"; break;
						case PanelState.Broken: panelStatus = "broken"; break;
						case PanelState.Failure: panelStatus = "failure"; break;
						case PanelState.Unknown: panelStatus = "invalid state"; break;
					}
					break;
				case ExposureState.Exposed:
					StringBuilder sb = new StringBuilder(256);
					sb.Append(currentOutput.ToString(rateFormat));
					sb.Append(" EC/s");
					if (analyticSunlight)
					{
						sb.Append(", analytic ");
						sb.Append(persistentFactor.ToString("P0"));
					}
					else
					{
						sb.Append(", exposure ");
						sb.Append(exposureFactor.ToString("P0"));
					}
					if (wearFactor < 1.0)
					{
						sb.Append(", wear : ");
						sb.Append((1.0 - wearFactor).ToString("P0"));
					}
					panelStatus = sb.ToString();
					break;
			}
		}

		public void FixedUpdate()
		{
			// sanity check
			if (SolarPanel == null) return;

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
				return;
			}

			// do nothing else in editor
			if (Lib.IsEditor()) return;

			// get vessel data from cache
			VesselData vd = vessel.KerbalismData();

			// do nothing if vessel is invalid
			if (!vd.IsValid) return;

			// Update tracked sun in auto mode
			if (!manualTracking && trackedSunIndex != vd.EnvMainSun.SunData.bodyIndex)
			{
				trackedSunIndex = vd.EnvMainSun.SunData.bodyIndex;
				SolarPanel.SetTrackedBody(vd.EnvMainSun.SunData.body);
			}

			VesselData.SunInfo trackedSunInfo = vd.EnvSunsInfo.Find(p => p.SunData.bodyIndex == trackedSunIndex);

			if (trackedSunInfo.SunlightFactor == 0.0)
				exposureState = ExposureState.InShadow;
			else
				exposureState = ExposureState.Exposed;

#if DEBUG_SOLAR
			Vector3d sunDirDebug = trackedSunInfo.Direction;

			// flight view sun dir
			SolarDebugDrawer.DebugLine(vessel.transform.position, vessel.transform.position + (sunDirDebug * 100.0), Color.red);

			// GetAnalyticalCosineFactorLanded() map view debugging
			Vector3d sunCircle = Vector3d.Cross(Vector3d.left, sunDirDebug);
			Quaternion qa = Quaternion.AngleAxis(45, sunCircle);
			LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), sunCircle, 500f, Color.red);
			LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), sunDirDebug, 500f, Color.yellow);
			for (int i = 0; i < 7; i++)
			{
				sunDirDebug = qa * sunDirDebug;
				LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), sunDirDebug, 500f, Color.green);
			}
#endif

			if (vd.EnvIsAnalytic)
			{
				// if we are switching to analytic mode and the vessel is landed, get an average exposure over a day
				// TODO : maybe check the rotation speed of the body, this might be inaccurate for tidally-locked bodies (test on the mun ?)
				if (!analyticSunlight && Lib.Landed(vessel)) persistentFactor = GetAnalyticalCosineFactorLanded(vd);
				analyticSunlight = true;
			}
			else
			{
				analyticSunlight = false;
			}

			// cosine / occlusion factor isn't updated when in analyticalSunlight / unloaded states :
			// - evaluting sun_dir / vessel orientation gives random results resulting in inaccurate behavior / random EC rates
			// - using the last calculated factor is a satisfactory simulation of a sun relative vessel attitude keeping behavior
			//   without all the complexity of actually doing it
			if (analyticSunlight)
			{
				exposureFactor = persistentFactor;
			}
			else
			{
				// reset factors
				persistentFactor = 0.0;
				exposureFactor = 0.0;

				// iterate over all stars, compute the exposure factor
				foreach (VesselData.SunInfo sunInfo in vd.EnvSunsInfo)
				{
					double sunCosineFactor = 0.0;
					double sunOccludedFactor = 0.0;
					string occludingPart = null;

					// Get the cosine factor (alignement between the sun and the panel surface)
					sunCosineFactor = SolarPanel.GetCosineFactor(sunInfo.Direction);

					if (sunCosineFactor == 0.0)
					{
						// If this is the tracked sun and the panel is not oriented toward the sun, update the gui info string.
						if (sunInfo == trackedSunInfo)
							exposureState = ExposureState.BadOrientation;
					}
					else
					{
						// The panel is oriented toward the sun, do a physic raycast to check occlusion from parts, terrain, buildings...
						sunOccludedFactor = SolarPanel.GetOccludedFactor(sunInfo.Direction, out occludingPart, sunInfo != trackedSunInfo);

						// If this is the tracked sun and the panel is occluded, update the gui info string. 
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

					// Compute final aggregate exposure factor
					double sunExposureFactor = sunCosineFactor * sunOccludedFactor * sunInfo.FluxProportion;

					// Add the final factor to the saved exposure factor to be used in analytical / unloaded states.
					// If occlusion is from the scene, not a part (terrain, building...) don't save the occlusion factor,
					// as occlusion from the terrain and static objects is too variable over time.
					if (occludingPart != null)
						persistentFactor += sunExposureFactor;
					else
						persistentFactor += sunCosineFactor * sunInfo.FluxProportion;

					// Only apply the exposure factor if not in shadow (body occlusion check)
					if (sunInfo.SunlightFactor == 1.0) exposureFactor += sunExposureFactor;
					else if (sunInfo == trackedSunInfo) exposureState = ExposureState.InShadow;
				}
			}

			// get solar flux and deduce a scalar based on nominal flux at 1AU
			// - this include atmospheric absorption if inside an atmosphere
			// - at high timewarps speeds, atmospheric absorption is analytical (integrated over a full revolution)
			double distanceFactor = vd.EnvSolarFluxTotal / Sim.SolarFluxAtHome;

			// get wear factor (time based output degradation)
			wearFactor = 1.0;
			if (timeEfficCurve != null && timeEfficCurve.Curve.keys.Length > 1)
				wearFactor = Lib.Clamp(timeEfficCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0)), 0.0, 1.0);

			// get final output rate in EC/s
			currentOutput = nominalRate * wearFactor * distanceFactor * exposureFactor;

			// ignore very small outputs
			if (currentOutput < 1e-10)
			{
				currentOutput = 0.0;
				return;
			}

			// get resource handler
			ResourceInfo ec = ResourceCache.GetResource(vessel, "ElectricCharge");

			// produce EC
			ec.Produce(currentOutput * Kerbalism.elapsed_s, "solar panel");
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, SolarPanelFixer prefab, VesselData vd, ResourceInfo ec, double elapsed_s)
		{
			// this is ugly spaghetti code but initializing the prefab at loading time is messy because the targeted solar panel module may not be loaded yet
			if (!prefab.isInitialized) prefab.OnStart(StartState.None);

			string state = Lib.Proto.GetString(m, "state");
			if (!(state == "Static" || state == "Extended" || state == "ExtendedFixed"))
				return;

			// We don't recalculate panel orientation factor for unloaded vessels :
			// - this ensure output consistency and prevent timestep-dependant fluctuations
			// - the player has no way to keep an optimal attitude while unloaded
			// - it's a good way of simulating sun-relative attitude keeping 
			// - it's fast and easy
			double efficiencyFactor = Lib.Proto.GetDouble(m, "persistentFactor");

			// calculate normalized solar flux factor
			// - this include atmospheric absorption if inside an atmosphere
			// - this is zero when the vessel is in shadow when evaluation is non-analytic (low timewarp rates)
			// - if integrated over orbit (analytic evaluation), this include fractional sunlight / atmo absorbtion
			efficiencyFactor *= vd.EnvSolarFluxTotal / Sim.SolarFluxAtHome;

			// get wear factor (output degradation with time)
			if (prefab.timeEfficCurve != null && prefab.timeEfficCurve.Curve.keys.Length > 1)
			{
				double launchUT = Lib.Proto.GetDouble(m, "launchUT");
				efficiencyFactor *= Lib.Clamp(prefab.timeEfficCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0)), 0.0, 1.0);
			}

			// get nominal panel charge rate at 1 AU
			// don't use the prefab value as some modules that does dynamic switching (SSTU) may have changed it
			double nominalRate = Lib.Proto.GetDouble(m, "nominalRate");

			// calculate output
			double output = nominalRate * efficiencyFactor;

			// produce EC
			ec.Produce(output * elapsed_s, "solar panel");
		}
		#endregion

		#region Other methods
		public bool GetSolarPanelModule()
		{
			// find the module based on explicitely supported modules
			foreach (PartModule pm in part.Modules)
			{
				// mod supported modules
				switch (pm.moduleName)
				{
					case "ModuleCurvedSolarPanel": SolarPanel = new NFSCurvedPanel(); break;
					case "SSTUSolarPanelStatic": SolarPanel = new SSTUStaticPanel();  break;
					case "SSTUSolarPanelDeployable": SolarPanel = new SSTUVeryComplexPanel(); break;
					case "SSTUModularPart": SolarPanel = new SSTUVeryComplexPanel(); break;
					case "KopernicusSolarPanel":
						Lib.Log("WARNING : Part '" + part.partInfo.title + "' use the KopernicusSolarPanel module, please remove it from your config. Kerbalism has it's own support for Kopernicus");
						continue;
				}

				// stock module and derivatives
				if (pm is ModuleDeployableSolarPanel)
					SolarPanel = new StockPanel();

				if (SolarPanel != null)
				{
					SolarPanel.OnLoad(this, pm);
					break;
				}
			}

			if (SolarPanel == null)
			{
				Lib.Log("WARNING : Could not find a supported solar panel module, disabling SolarPanelFixer module...");
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

		private double GetAnalyticalCosineFactorLanded(VesselData vd)
		{
			double finalFactor = 0.0;
			foreach (VesselData.SunInfo sun in vd.EnvSunsInfo)
			{
				Vector3d sunDir = sun.Direction;
				// get a rotation of 45° perpendicular to the sun direction
				Quaternion sunRot = Quaternion.AngleAxis(45, Vector3d.Cross(Vector3d.left, sunDir));

				double factor = 0.0;
				string occluding;
				for (int i = 0; i < 8; i++)
				{
					sunDir = sunRot * sunDir;
					factor += SolarPanel.GetCosineFactor(sunDir, true);
					factor += SolarPanel.GetOccludedFactor(sunDir, out occluding, true);
				}
				factor /= 16.0;
				finalFactor += factor * sun.FluxProportion;
			}
			return finalFactor;
		}
		#endregion

		#region Abstract class for common interaction with supported PartModules
		public abstract class SupportedPanel 
		{
			/// <summary>Reference to the SolarPanelFixer, must be set from OnLoad</summary>
			protected SolarPanelFixer fixerModule;

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

			/// <summary>Kopernicus stars support : must set the animation tracked body</summary>
			public virtual void SetTrackedBody(CelestialBody body) { }

			/// <summary>Reliability : specific hacks for the target module that must be applied when the panel is disabled by a failure</summary>
			public virtual void Break(bool isBroken) { }

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
		}
		#endregion

		#region Stock module support (ModuleDeployableSolarPanel)
		// stock solar panel module support
		// - we don't support the temperatureEfficCurve
		// - we override the stock UI
		// - we still reuse most of the stock calculations
		// - we let the module fixedupdate/update handle animations/suncatching
		// - we prevent stock EC generation by reseting the reshandler rate
		// - we don't support cylindrical/spherical panel types
		private class StockPanel : SupportedPanel<ModuleDeployableSolarPanel>
		{
			private Transform sunCatcherPosition;   // middle point of the panel surface (usually). Use only position, panel surface direction depend on the pivot transform, even for static panels.
			private Transform sunCatcherPivot;      // If it's a tracking panel, "up" is the pivot axis and "position" is the pivot position. In any case "forward" is the panel surface normal.

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{
				this.fixerModule = fixerModule;
				panelModule = (ModuleDeployableSolarPanel)targetModule;
			}

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
				// hide stock ui
				foreach (BaseField field in panelModule.Fields)
					field.guiActive = false;

				if (sunCatcherPivot == null)
					sunCatcherPivot = panelModule.part.FindModelComponent<Transform>(panelModule.pivotName);
				if (sunCatcherPosition == null)
					sunCatcherPosition = panelModule.part.FindModelTransform(panelModule.secondaryTransformName);

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

				if (panelModule.timeEfficCurve != null && panelModule.timeEfficCurve.Curve.keys.Length > 1)
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
							return Math.Max(Vector3d.Dot(sunDir, panelModule.trackingDotTransform.forward), 0.0);

						if (panelModule.isTracking)
							return Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(sunDir, sunCatcherPivot.up)));
						else
							return Math.Max(Vector3d.Dot(sunDir, sunCatcherPivot.forward), 0.0);

					case ModuleDeployableSolarPanel.PanelType.CYLINDRICAL:
						return Math.Max((1.0 - Math.Abs(Vector3d.Dot(sunDir, panelModule.trackingDotTransform.forward))) * (1.0 / Math.PI), 0.0);
					case ModuleDeployableSolarPanel.PanelType.SPHERICAL:
						return 0.25;
					default:
						return 0.0;
				}
			}

			public override PanelState GetState()
			{
				if (!panelModule.isTracking)
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
			private enum TrackingType {Unknown = 0, Fixed, SinglePivot, DoublePivot }
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

				public enum Axis {XPlus, XNeg, YPlus, YNeg, ZPlus, ZNeg}
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
						case "SSTUSolarPanelDeployable": foreach(var field in panelModule.Fields) field.guiActive = false; break;
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
							case TrackingType.Fixed:		cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.SuncatcherAxisVector(i)), 0.0); continue;
							case TrackingType.SinglePivot:	cosineFactor += Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(sunDir, panel.PivotAxisVector))); continue;
							case TrackingType.DoublePivot:	cosineFactor += 1.0; continue;
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
