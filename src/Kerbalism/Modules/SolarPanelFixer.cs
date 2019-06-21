using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace KERBALISM
{
	// TODO : SolarPanelFixer features that require testing :
	// - fully untested : time efficiency curve (must try with a stock panel defined curve, and a SolarPanelFixer defined curve)
	// - background update : must check that the output is consistent with what we get when loaded (should be but checking can't hurt)
	//   (note : only test it with equatorial circular orbits, other orbits will give inconsistent output due to sunlight evaluation algorithm limitations)

	// TODO : SolarPanelFixer missing features :
	// - (critical) reliability support
	// - (critical) automation support
	// - SSTU module support

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
	public sealed class SolarPanelFixer : PartModule
	{
		/// <summary>Main PAW info label</summary>
		[KSPField(guiActive = true, guiActiveEditor = false, guiName = "Solar panel")]
		public string panelStatus = string.Empty;

		/// <summary>solar flux PAW info, maybe this can be disabled for PAW decluttering</summary>
		[KSPField(guiActive = false, guiActiveEditor = false, advancedTweakable = false, guiName = "Solar flux", guiUnits = " W/m²", guiFormat = "F1")]
		public double solarFlux = 0.0;

		/// <summary>current output rate, updated only for loaded vessels</summary>
		[KSPField(guiActive = false, guiActiveEditor = false, guiName = "Panel output", guiUnits = " EC/s", guiFormat = "F1")]
		public double currentOutput = 0.0;

		/// <summary>nominal rate at 1 UA (Kerbin distance from the sun)</summary>
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Panel nominal output", guiUnits = " EC/s", guiFormat = "F1")]
		public double nominalRate = 10.0;

		/// <summary>aggregate efficiency factor for angle exposure losses and occlusion from parts</summary>
		[KSPField(isPersistant = true)]
		public double persistentFactor = 1.0;

		[KSPField(isPersistant = true)]
		public PanelState state;

		// this is used for not breaking existing saves
		public bool isInitialized = false;

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

		/// <summary>is this panel either in shadow or by occluded by the terrain / a scene object</summary>
		private double occludedFactor;

		/// <summary>for UI state updating</summary>
		private bool analyticSunlight;

		public enum PanelState
		{
			Unknown = 0,
			Retracted,
			Extending,
			Extended,
			Retracting,
			Static,
			Broken,
			Failure
		}

		public bool GetSolarPanelModule()
		{
			// find the module based on explicitely supported modules
			foreach (PartModule pm in part.Modules)
			{
				// stock module and derivatives
				if (pm is ModuleDeployableSolarPanel)
					SolarPanel = new StockPanel();


				// other supported modules
				switch (pm.moduleName)
				{
					case "ModuleCurvedSolarPanel": SolarPanel = new NFSCurvedPanel(); break;
					case "SSTUSolarPanelStatic": break;
					case "SSTUSolarPanelDeployable": break;
					case "SSTUModularPart": break;
				}

				if (SolarPanel != null)
				{
					SolarPanel.OnLoad(pm);
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

		public override void OnLoad(ConfigNode node)
		{
			if (SolarPanel == null && !GetSolarPanelModule())
				return;

			// apply states changes we have done trough automation
			if ((state == PanelState.Retracted || state == PanelState.Extended) && state != SolarPanel.GetState())
			{
				SolarPanel.SetState(state);
			}
				
		}

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			// TODO : does this actually work ?
			if (Lib.DisableScenario(this)) return;

			if (SolarPanel == null && !GetSolarPanelModule())
			{
				isInitialized = true;
				return;
			}

			double newNominalRate = SolarPanel.OnStart(isInitialized);
			if (newNominalRate > 0.0)
				nominalRate = newNominalRate;

			isInitialized = true;

			// not sure why (I guess because of the KSPField attribute), but timeEfficCurve is instanciated with 0 keys by something instead of being null
			if (timeEfficCurve == null || timeEfficCurve.Curve.keys.Length == 0)
			{
				timeEfficCurve = SolarPanel.GetTimeCurve();
				if (Lib.IsFlight() && launchUT < 0.0)
					launchUT = Planetarium.GetUniversalTime();
			}
		}

		public override void OnSave(ConfigNode node)
		{
			// vessel can be null in OnSave (ex : on vessel creation)
			if (!Lib.IsFlight()
				|| vessel == null
				|| !isInitialized
				|| SolarPanel == null
				|| !Lib.Landed(vessel))
				return;

			// get vessel data from cache
			Vessel_info info = Cache.VesselInfo(vessel);

			// do nothing if vessel is invalid
			if (!info.is_valid) return;

			// calculate average exposure over a full day when landed, will be used for panel background processing
			persistentFactor = GetAnalyticalCosineFactorLanded(info.sun_dir);
		}

		public void Update()
		{
			// sanity check
			if (SolarPanel == null) return;

			// call Update specfic handling, if any
			SolarPanel.OnUpdate();

			// do nothing else in editor
			if (Lib.IsEditor()) return;

			// update ui
			if (currentOutput > 0.0)
			{
				Fields["solarFlux"].guiActive = true;
				Fields["currentOutput"].guiActive = true;
				Fields["solarFlux"].guiName = analyticSunlight ? "Analytic solar flux" : "Solar flux";
			}
			else
			{
				Fields["solarFlux"].guiActive = false;
				Fields["currentOutput"].guiActive = false;
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
				if (Lib.IsEditor() && (newState == PanelState.Extended || newState == PanelState.Retracted))
					Lib.RefreshPlanner();
			}

			if (!(state == PanelState.Extended || state == PanelState.Static))
			{
				switch (state)
				{
					case PanelState.Retracted:	panelStatus = "retracted";	break;
					case PanelState.Extending:	panelStatus = "extending";	break;
					case PanelState.Retracting: panelStatus = "retracting"; break;
					case PanelState.Broken:		panelStatus = "broken";		break;
					case PanelState.Failure:	panelStatus = "failure";	break;
					case PanelState.Unknown:	panelStatus = "invalid state"; break;
				}
				currentOutput = 0.0;
				return;
			}

			// do nothing else in editor
			if (Lib.IsEditor()) return;

			// get vessel data from cache
			Vessel_info info = Cache.VesselInfo(vessel);

			// do nothing if vessel is invalid
			if (!info.is_valid) return;

#if DEBUG
			Vector3d SunDir = info.sun_dir;

			// flight view sun dir
			DebugDrawer.DebugLine(vessel.transform.position, vessel.transform.position + (SunDir * 100.0), Color.red);

			// GetAnalyticalCosineFactorLanded() map view debugging
			Vector3d sunCircle = Vector3d.Cross(Vector3d.left, SunDir);
			Quaternion qa = Quaternion.AngleAxis(45, sunCircle);
			LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), sunCircle, 500f, Color.red);
			LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), SunDir, 500f, Color.yellow);
			for (int i = 0; i < 7; i++)
			{
				SunDir = qa * SunDir;
				LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), SunDir, 500f, Color.green);
			}
#endif

			// don't produce EC if in shadow, but don't reset cosineFactor
			if (info.sunlight == 0.0)
			{
				currentOutput = 0.0;
				solarFlux = 0.0;
				occludedFactor = 0.0;
				panelStatus = "<color=#ff2222>in shadow</color>";
				return;
			}

			if (info.sunlight < 1.0)
			{
				if (!analyticSunlight && vessel.Landed)
				{
					persistentFactor = GetAnalyticalCosineFactorLanded(info.sun_dir);
				}
				analyticSunlight = true;
			}
			else
			{
				analyticSunlight = false;
			}

			// cosine factor isn't updated when in analyticalSunlight / unloaded states :
			// - evaluting sun_dir / vessel orientation gives random results resulting in inaccurate behavior / random EC rates
			// - using the last calculated factor is a satisfactory simulation of a sun relative vessel attitude keeping behavior
			//   without all the complexity of actually doing it
			double cosineFactor = 1.0;
			if (!analyticSunlight)
			{
				// get the cosine factor
				cosineFactor = SolarPanel.GetCosineFactor(info.sun_dir);
				if (cosineFactor > 0.0)
				{
					// the panel is oriented toward the sun
					// now do a physic raycast to check occlusion from parts, terrain, buildings...
					occludedFactor = SolarPanel.GetOccludedFactor(info.sun_dir, out panelStatus);
					
					if (panelStatus != null)
					{
						// if there is occlusion from a part ("out string occludingPart" not null)
						// we save this occlusion factor to account for it in analyticalSunlight / unloaded states, 
						persistentFactor = cosineFactor * occludedFactor;
						if (occludedFactor == 0.0)
						{
							// if we are totally occluded, do nothing else
							currentOutput = 0.0;
							panelStatus = Lib.BuildString("<color=#ff2222>occluded by ", panelStatus, "</color>");
							return;
						}
					}
					else
					{
						// if there is no occlusion, or if occlusion is from the rest of the scene (terrain, building, not a part)
						// don't save the occlusion factor, as occlusion from the terrain and static objects is very variable, we won't use it in analyticalSunlight / unloaded states, 
						persistentFactor = cosineFactor;
						if (occludedFactor == 0.0)
						{
							// if we are totally occluded, do nothing else
							currentOutput = 0.0;
							panelStatus = "<color=#ff2222>occluded by terrain</color>";
							return;
						}
					}
				}
				else
				{
					// the panel is not oriented toward the sun, reset everything and abort
					persistentFactor = 0.0;
					currentOutput = 0.0;
					panelStatus = "<color=#ff2222>bad orientation</color>";
					return;
				}
			}

			// get wear factor (time based output degradation)
			double wearFactor = 1.0;
			if (timeEfficCurve != null && timeEfficCurve.Curve.keys.Length > 1)
			{
				wearFactor = timeEfficCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0));
			}

			// get solar flux and deduce a scalar based on nominal flux at 1AU
			// - this include atmospheric absorption if inside an atmosphere
			// - at high timewarps speeds, atmospheric absorption is analytical (integrated over a full revolution)
			solarFlux = info.solar_flux;
			double fluxFactor = solarFlux / Sim.SolarFluxAtHome();

			if (analyticSunlight)
				currentOutput = nominalRate * wearFactor * fluxFactor * persistentFactor;
			else
				currentOutput = nominalRate * wearFactor * fluxFactor * cosineFactor * occludedFactor;

			// get resource handler
			Resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

			// produce EC
			ec.Produce(currentOutput * Kerbalism.elapsed_s, "panel");

			// build status string
			if (wearFactor < 1.0)
			{
				if (analyticSunlight)
					panelStatus = Lib.BuildString("analytic exposure ", (persistentFactor * fluxFactor).ToString("P0"), ", wear : ", (1.0 - wearFactor).ToString("P0"));
				else
					panelStatus = Lib.BuildString("exposure ", persistentFactor.ToString("P0"), ", wear : ", (1.0 - wearFactor).ToString("P0"));
			}
			else
			{
				if (analyticSunlight)
					panelStatus = Lib.BuildString("analytic exposure ", (persistentFactor * fluxFactor).ToString("P0"));
				else
					panelStatus = Lib.BuildString("exposure ", persistentFactor.ToString("P0"));
			}

		}

		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, SolarPanelFixer prefab, Vessel_info vi, Resource_info ec, double elapsed_s)
		{
			// this is ugly spaghetti code but initializing the prefab at loading time is messy
			if (!prefab.isInitialized) prefab.OnStart(StartState.None);

			string state = Lib.Proto.GetString(m, "state");
			if (!(state == "Static" || state == "Deployed"))
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
			efficiencyFactor *= vi.solar_flux / Sim.SolarFluxAtHome();

			// get wear factor (output degradation with time)
			if (prefab.timeEfficCurve != null && prefab.timeEfficCurve.Curve.keys.Length > 1)
			{
				double launchUT = Lib.Proto.GetDouble(m, "launchUT");
				efficiencyFactor *= prefab.timeEfficCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0));
			}

			// get nominal panel charge rate at 1 AU
			// don't use the prefab value as some modules that does dynamic switching (SSTU) may have changed it
			double nominalRate = Lib.Proto.GetDouble(m, "nominalRate");

			// calculate output
			double output = nominalRate * efficiencyFactor;

			// produce EC
			ec.Produce(output * elapsed_s, "panel");
		}

		private static PanelState GetProtoState(ProtoPartModuleSnapshot protoModule)
		{
			return (PanelState)Enum.Parse(typeof(PanelState), Lib.Proto.GetString(protoModule, "state"));
		}

		private static void SetProtoState(ProtoPartModuleSnapshot protoModule, PanelState newState)
		{
			Lib.Proto.Set(protoModule, "state", newState.ToString());
		}

		public static void ProtoToggleState(ProtoPartModuleSnapshot protoModule, PanelState currentState)
		{
			switch (currentState)
			{
				case PanelState.Retracted: SetProtoState(protoModule, PanelState.Extended); return;
				case PanelState.Extended: SetProtoState(protoModule, PanelState.Retracted); return;
			}
		}

		public void ToggleState()
		{
			SolarPanel.ToggleState(state);
		}

		private double GetAnalyticalCosineFactorLanded(Vector3d sunDir)
		{
			Quaternion sunRot = Quaternion.AngleAxis(45, Vector3d.Cross(Vector3d.left, sunDir));

			double factor = 0.0;
			string occluding;
			for (int i = 0; i < 8; i++)
			{
				sunDir = sunRot * sunDir;
				factor += SolarPanel.GetCosineFactor(sunDir, true);
				factor += SolarPanel.GetOccludedFactor(sunDir, out occluding, true);
			}
			return factor /= 16.0;
		}

		public abstract class SupportedPanel 
		{

			/// <summary>
			/// Will be called by the SolarPanelFixer OnLoad, must set the partmodule reference.
			/// If the panel is deployable, GetState() must be able to return the correct state after this has been called
			/// </summary>
			public abstract void OnLoad(PartModule targetModule);

			/// <summary>
			/// Main inititalization method called from OnStart, every hack we do must be done here
			/// </summary>
			/// <param name="initialized">will be true is the method has already been called for this module (OnStart can be called multiple times in the editor)</param>
			/// <returns>nominal rate at 1AU</returns>
			public abstract double OnStart(bool initialized);

			/// <summary>Must return a [0;1] scalar evaluating the local occlusion factor (usually with a physic raycast already done by the target module)</summary>
			/// <param name="occludingPart">if the occluding object is a part, name of the part. MUST return null in all other cases.</param>
			/// <param name="analytic">if true, the returned scalar must account for the given sunDir, so we can't rely on the target module raycast</param>
			public abstract double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false);

			/// <summary>Must return a [0;1] scalar evaluating the angle of the given sunDir on the panel surface (usually a dot product clamped to [0;1])</summary>
			/// <param name="analytic">if true and the panel is orientable, the returned scalar must be the best possible output (must use the rotation around the pivot)</param>
			public abstract double GetCosineFactor(Vector3d sunDir, bool analytic = false);

			/// <summary>must return the state of the panel, must be able to work before GetNominalRateOnStart has been called</summary>
			public abstract PanelState GetState();

			/// <summary>Can be overridden if the target module implement a time efficiency curve. Keys are in hours.</summary>
			public virtual FloatCurve GetTimeCurve() { return new FloatCurve(new Keyframe[] { new Keyframe(0f, 1f) }); }

			public virtual bool SupportAutomation() { return false; }

			/// <summary>Called at Update(), can contain target module specific hacks</summary>
			public virtual void OnUpdate() { }

			/// <summary>if the panel is extendable, must be implemented for automation support</summary>
			public virtual void Extend() { }

			/// <summary>if the panel is retractable, must be implemented for automation support</summary>
			public virtual void Retract() { }

			/// <summary>if the panel is extendable/retractable, must be implemented for automation support</summary>
			public virtual void SetState(PanelState state) { }

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

		// stock solar panel module support
		// - we don't support the temperatureEfficCurve
		// - we override the stock UI
		// - we still reuse most of the stock calculations
		// - we let the module fixedupdate/update handle animations/suncatching
		// - we prevent stock EC generation by reseting the reshandler rate
		private class StockPanel : SupportedPanel<ModuleDeployableSolarPanel>
		{
			private Transform sunCatcher;   // suncatcher transform
			private Transform pivot;        // pivot transform (if it's a tracking panel)

			public override void OnLoad(PartModule targetModule)
			{
				panelModule = (ModuleDeployableSolarPanel)targetModule;
			}

			public override double OnStart(bool initialized)
			{
				// hide stock ui
				foreach (BaseField field in panelModule.Fields)
				{
					field.guiActive = false;
				}

				// avoid rate lost due to OnStart being called multiple times in the editor
				if (initialized)
					return -1.0;

				double output_rate = panelModule.resHandler.outputResources[0].rate;
				// reset target module rate
				// - This can break mods that evaluate solar panel output for a reason or another (eg: AmpYear, BonVoyage).
				//   We fix that by exploiting the fact that resHandler was introduced in KSP recently, and most of
				//   these mods weren't updated to reflect the changes or are not aware of them, and are still reading
				//   chargeRate. However the stock solar panel ignore chargeRate value during FixedUpdate.
				//   So we only reset resHandler rate.
				panelModule.resHandler.outputResources[0].rate = 0.0f;

				return output_rate;
			}

			// akwardness award : stock timeEfficCurve use 24 hours days (1/(24*60/60)) as unit for the curve keys, we convert that to hours
			public override FloatCurve GetTimeCurve()
			{

				if (panelModule.timeEfficCurve != null && panelModule.timeEfficCurve.Curve.keys.Length > 1)
				{
					FloatCurve timeCurve = new FloatCurve();
					foreach (Keyframe key in panelModule.timeEfficCurve.Curve.keys)
						timeCurve.Add(key.time * 24f, key.value);
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
					if (sunCatcher == null)
						sunCatcher = panelModule.part.FindModelTransform(panelModule.secondaryTransformName);

					Physics.Raycast(sunCatcher.position, sunDir, out raycastHit, 10000f);
				}
				else
				{
					raycastHit = panelModule.hit;
				}

				if (raycastHit.collider != null)
				{
					Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.transform.gameObject);
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
#if !DEBUG
				if (!analytic)
					return Math.Max(Vector3d.Dot(sunDir, panelModule.trackingDotTransform.forward), 0.0);
#endif
				if (panelModule.isTracking && pivot == null)
					pivot = panelModule.part.FindModelComponent<Transform>(panelModule.pivotName);

				if (sunCatcher == null)
					sunCatcher = panelModule.part.FindModelTransform(panelModule.secondaryTransformName);

#if DEBUG
				DebugDrawer.DebugLine(sunCatcher.position, sunCatcher.position + sunCatcher.forward, Color.yellow);
				if (panelModule.isTracking) DebugDrawer.DebugLine(pivot.position, pivot.position + (pivot.up * -1f), Color.blue);
#endif

				if (panelModule.isTracking)
					return Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(sunDir, pivot.up)));
				else
					return Math.Max(Vector3d.Dot(sunDir, sunCatcher.forward), 0.0);
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
						if (!panelModule.retractable) return PanelState.Static;
						return PanelState.Extended;
					case ModuleDeployablePart.DeployState.RETRACTED: return PanelState.Retracted;
					case ModuleDeployablePart.DeployState.RETRACTING: return PanelState.Retracting;
					case ModuleDeployablePart.DeployState.EXTENDING: return PanelState.Extending;
					case ModuleDeployablePart.DeployState.BROKEN: return PanelState.Broken;
				}
				return PanelState.Unknown;
			}

			public override bool SupportAutomation() { return panelModule.useAnimation ? true : false; }

			public override void SetState(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted:
						panelModule.deployState = ModuleDeployablePart.DeployState.RETRACTED;
						break;
					case PanelState.Extended:
						panelModule.deployState = ModuleDeployablePart.DeployState.EXTENDED;
						break;
				}
			}

			public override void Extend() { panelModule.Extend(); }

			public override void Retract() { panelModule.Retract(); }
		}

		// Near future solar curved panel support
		// - We prevent the NFS module from running (disabled at MonoBehavior level)
		// - We replicate the behavior of its FixedUpdate()
		// - We call its Update() method but we disable the KSPFields UI visibility.
		private class NFSCurvedPanel : SupportedPanel<PartModule>
		{
			private Transform[] sunCatchers;    // model transforms named after the "PanelTransformName" field
			private bool deployable;            // "Deployable" field
			private Action panelModuleUpdate;   // delegate for the module Update() method

			public override void OnLoad(PartModule targetModule)
			{
				panelModule = targetModule;
				deployable = Lib.ReflectionValue<bool>(panelModule, "Deployable");
			}

			public override double OnStart(bool initialized)
			{
				// get a delegate for Update() method (avoid performance penality of reflection)
				panelModuleUpdate = (Action)Delegate.CreateDelegate(typeof(Action), panelModule, "Update");

				// since we are disabling the MonoBehavior, ensure the module Start() has been called
				Lib.ReflectionCall(panelModule, "Start");

				// get transform name from module
				string transform_name = Lib.ReflectionValue<string>(panelModule, "PanelTransformName");

				// get panel components
				sunCatchers = panelModule.part.FindModelTransforms(transform_name);
				if (sunCatchers.Length == 0)
					return 0.0;

				// disable the module at the Unity level, we will handle its updates manually
				panelModule.enabled = false;

				// return panel nominal rate
				return Lib.ReflectionValue<float>(panelModule, "TotalEnergyRate");
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludedFactor = 1.0;
				occludingPart = null;

				RaycastHit raycastHit;
				foreach (Transform panel in sunCatchers)
				{
					if (Physics.Raycast(panel.position, sunDir, out raycastHit, 10000f))
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
#if DEBUG
					DebugDrawer.DebugLine(panel.position, panel.position + panel.forward, Color.yellow);
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
					return PanelState.Unknown;

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

			public override bool SupportAutomation() { return deployable; }

			public override void SetState(PanelState state)
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
		}

		private class SSTUStaticPanel : SupportedPanel<PartModule>
		{
			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				throw new NotImplementedException();
			}

			public override double OnStart(bool initialized)
			{
				throw new NotImplementedException();
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingObject, bool analytic = false)
			{
				throw new NotImplementedException();
			}

			public override PanelState GetState()
			{
				throw new NotImplementedException();
			}

			public override void OnLoad(PartModule targetModule)
			{
				throw new NotImplementedException();
			}
		}

	}

	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	class DebugDrawer : MonoBehaviour
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

		public static void DebugLine(Vector3 start, Vector3 end, Color col)
		{
			lines.Add(new Line(start, end, col));
		}

		public static void DebugPoint(Vector3 start, Color col)
		{
			points.Add(new Point(start, col));
		}

		public static void DebugTransforms(Transform t)
		{
			transforms.Add(new Trans(t.position, t.up, t.right, t.forward));
		}

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
			Debug.Log("DebugDrawer starting");
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
					Debug.Log("EndOfFrameDrawing Exception" + e);
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
} // KERBALISM
