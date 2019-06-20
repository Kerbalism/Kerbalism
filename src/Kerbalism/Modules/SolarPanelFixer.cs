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

		/// <summary>should the panel generate EC at all : false if broken, not deployed, etc...</summary>
		[KSPField(isPersistant = true)]
		public bool canRun = true;

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
		private SupportedPanel solarPanel;

		/// <summary>used for checking prefab state</summary>
		public bool IsInitialized { get; private set; }

		/// <summary>is this panel either in shadow or by occluded by the terrain / a scene object</summary>
		private double occludedFactor;

		bool analyticSunlight;

		public override void OnStart(StartState state)
		{
			IsInitialized = true;

			// don't break tutorial scenarios
			// TODO : does this actually work ?
			if (Lib.DisableScenario(this)) return;

			// find the module based on explicitely supported modules
			foreach (PartModule pm in part.Modules)
			{
				// stock module and derivatives
				if (pm is ModuleDeployableSolarPanel) // stock module and derivatives
				{
					solarPanel = new StockPanel();
				}

				// other supported modules
				switch (pm.moduleName)
				{
					case "ModuleCurvedSolarPanel": solarPanel = new NFSCurvedPanel(); break;
					case "SSTUSolarPanelStatic": break;
					case "SSTUSolarPanelDeployable": break;
					case "SSTUModularPart": break;
				}

				if (solarPanel != null)
				{
					nominalRate = solarPanel.OnStart(this, pm);
					break;
				}
			}

			if (solarPanel == null)
			{
				Lib.Log("WARNING : Could not find a supported solar panel module, disabling SolarPanelFixer module...");
				enabled = isEnabled = moduleIsEnabled = false;
				return;
			}

			// not sure why (I guess because of the KSPField attribute), but timeEfficCurve is instanciated with 0 keys by something instead of being null
			if (timeEfficCurve == null || timeEfficCurve.Curve.keys.Length == 0)
			{
				timeEfficCurve = solarPanel.GetTimeCurve();
				if (Lib.IsFlight() && launchUT < 0.0)
					launchUT = Planetarium.GetUniversalTime();
			}
		}

		public override void OnSave(ConfigNode node)
		{
			// vessel can be null in OnSave (ex : on vessel creation)
			if (!Lib.IsFlight()
				|| vessel == null
				|| !IsInitialized
				|| solarPanel == null
				|| !Lib.Landed(vessel))
				return;

			// get vessel data from cache
			Vessel_info info = Cache.VesselInfo(vessel);

			// do nothing if vessel is invalid
			if (!info.is_valid) return;

			persistentFactor = GetAnalyticalCosineFactorLanded(info.sun_dir);
		}

		public void Update()
		{
			// sanity check
			if (solarPanel == null) return;

			// call Update specfic handling, if any
			solarPanel.OnUpdate();

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

		private double GetAnalyticalCosineFactorLanded(Vector3d sunDir)
		{
			Quaternion sunRot = Quaternion.AngleAxis(45, Vector3d.Cross(Vector3d.left, sunDir));

			double factor = 0.0;
			string occluding;
			for (int i = 0; i < 8; i++)
			{
				sunDir = sunRot * sunDir;
				factor += solarPanel.GetCosineFactor(sunDir, true);
				factor += solarPanel.GetOccludedFactor(sunDir, out occluding, true);
			}
			return factor /= 16.0;
		}

		public void FixedUpdate()
		{
			// sanity check
			if (solarPanel == null) return;

			// can't produce anything if not deployed, broken, etc
			if (canRun != solarPanel.CanRun(out panelStatus))
			{
				canRun = !canRun;
				if (Lib.IsEditor()) Lib.RefreshPlanner();
			}

			if (!canRun)
			{
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
				panelStatus = "<color=#ff2222>in shadow</color>";
				occludedFactor = 0.0;
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
			// local occlusion from physic raycasts is also problematic :
			// - occlusion from parts can be considered as a permanent factor
			// - occlusion from the terrain and static object can't be reliably evaluated at high timewarp speeds
			// - in analyticalSunlight / unloaded states, we only apply the occlusion from parts
			// - this ensure occlusion output factor is never lower than it should be
			double cosineFactor = 1.0;
			if (!analyticSunlight)
			{
				cosineFactor = solarPanel.GetCosineFactor(info.sun_dir);
				if (cosineFactor > 0.0)
				{
					occludedFactor = solarPanel.GetOccludedFactor(info.sun_dir, out panelStatus);
					if (panelStatus != null)
					{
						persistentFactor = cosineFactor * occludedFactor;
						if (occludedFactor == 0.0)
						{
							currentOutput = 0.0;
							panelStatus = Lib.BuildString("<color=#ff2222>occluded by ", panelStatus, "</color>");
							return;
						}
					}
					else
					{
						persistentFactor = cosineFactor;
						if (occludedFactor == 0.0)
						{
							currentOutput = 0.0;
							panelStatus = "<color=#ff2222>occluded by terrain</color>";
							return;
						}
					}
				}
				else
				{
					persistentFactor = 0.0;
					currentOutput = 0.0;
					panelStatus = "<color=#ff2222>not in sunlight</color>";
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
				currentOutput = nominalRate * fluxFactor * wearFactor * persistentFactor;
			else
				currentOutput = nominalRate * fluxFactor * wearFactor * cosineFactor * occludedFactor;

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
			if (!prefab.IsInitialized) prefab.OnStart(StartState.None);

			if (!Lib.Proto.GetBool(m, "canRun"))
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

		private abstract class SupportedPanel
		{
			public abstract double OnStart(SolarPanelFixer warpFixer, PartModule targetModule);
			public abstract bool CanRun(out string issueInfo);
			public abstract double GetOccludedFactor(Vector3d sunDir, out string occludingObject, bool analytic = false);
			public abstract double GetCosineFactor(Vector3d sunDir, bool analytic = false);
			public virtual void OnUpdate() { }
			public virtual FloatCurve GetTimeCurve() { return new FloatCurve(new Keyframe[] { new Keyframe(0f, 1f) }); }
		}

		private abstract class SupportedPanel<T> : SupportedPanel where T : PartModule
		{
			public T panelModule;
		}

		// stock solar panel module support
		// - we don't support the temperatureEfficCurve
		// - we override the stock UI
		// - we still reuse most of the stock calculations
		private class StockPanel : SupportedPanel<ModuleDeployableSolarPanel>
		{
			private Transform sunCatcher;   // suncatcher transform
			private Transform pivot;        // pivot transform (if it's a tracking panel)

			public static ModuleDeployableSolarPanel modulePrefab;

			public override double OnStart(SolarPanelFixer warpFixer, PartModule targetModule)
			{
				panelModule = (ModuleDeployableSolarPanel)targetModule;

				if (modulePrefab == null)
					modulePrefab = (ModuleDeployableSolarPanel)panelModule.part.partInfo.partPrefab.Modules[panelModule.part.Modules.IndexOf(panelModule)];

				double output_rate;

				// store rate, but avoid rate lost due to this being called multiple times in the editor
				if (warpFixer.nominalRate > 0.0)
					output_rate = warpFixer.nominalRate;
				else
					output_rate = panelModule.resHandler.outputResources[0].rate;

				// reset rate
				// - This can break mods that evaluate solar panel output for a reason or another (eg: AmpYear, BonVoyage).
				//   We fix that by exploiting the fact that resHandler was introduced in KSP recently, and most of
				//   these mods weren't updated to reflect the changes or are not aware of them, and are still reading
				//   chargeRate. However the stock solar panel ignore chargeRate value during FixedUpdate.
				//   So we only reset resHandler rate.
				panelModule.resHandler.outputResources[0].rate = 0.0f;

				// hide stock ui
				foreach (BaseField field in panelModule.Fields)
				{
					field.guiActive = false;
				}

				return output_rate;
			}

			public override FloatCurve GetTimeCurve()
			{
				// akwardness award : stock timeEfficCurve use 24 hours days (1/(24*60/60)) as unit for the curve keys
				// we convert that to hours
				if (panelModule.timeEfficCurve != null && panelModule.timeEfficCurve.Curve.keys.Length > 1)
				{
					FloatCurve timeCurve = new FloatCurve();
					foreach (Keyframe key in panelModule.timeEfficCurve.Curve.keys)
						timeCurve.Add(key.time * 24f, key.value);
					return timeCurve;
				}
				return base.GetTimeCurve();
			}

			public override bool CanRun(out string issueInfo)
			{
				if (!panelModule.moduleIsEnabled || panelModule.deployState == ModuleDeployablePart.DeployState.BROKEN)
				{
					issueInfo = "<color=#ff2222>broken</color>";
					return false;
				}

				if (panelModule.deployState != ModuleDeployablePart.DeployState.EXTENDED)
				{
					issueInfo = "disabled";
					return false;
				}

				issueInfo = null;
				return true;
			}

			// detect occlusion from the scene colliders using the stock module physics raycast.
			// Note that this cover everything from parts to buildings, terrain and bodies,
			// and consequently can be redundant with our own cosineFactor evaluation
			public override double GetOccludedFactor(Vector3d sunDir, out string occludingObject, bool analytic = false)
			{
				double occludingFactor = 1.0;
				occludingObject = null;
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

						occludingObject = blockingPart.partInfo.title;
					}
					occludingFactor = 0.0;
				}
				return occludingFactor;
			}


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

			public override double OnStart(SolarPanelFixer warpFixer, PartModule targetModule)
			{
				// get the module
				panelModule = targetModule;

				// get a delegate for Update() method (avoid performance penality of reflection)
				panelModuleUpdate = (Action)Delegate.CreateDelegate(typeof(Action), panelModule, "Update");

				// since we are disabling the MonoBehavior, ensure the module Start() has been called
				Lib.ReflectionCall(panelModule, "Start");

				// get values from module
				deployable = Lib.ReflectionValue<bool>(panelModule, "Deployable");
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

			public override bool CanRun(out string issueInfo)
			{
				string state = Lib.ReflectionValue<string>(panelModule, "SavedState");
				//ModuleDeployablePart.DeployState state = (ModuleDeployablePart.DeployState)Enum.Parse(typeof(ModuleDeployablePart.DeployState), Lib.ReflectionValue<string>(panelModule, "SavedState"));

				if (!panelModule.moduleIsEnabled || state == "BROKEN")
				{
					issueInfo = "<color=#ff2222>broken</color>";
					return false;
				}

				if (deployable && state != "EXTENDED")
				{
					issueInfo = "disabled";
					return false;
				}

				issueInfo = null;
				return true;
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingObject, bool analytic = false)
			{
				double occludedFactor = 1.0;
				occludingObject = null;

				RaycastHit raycastHit;
				foreach (Transform panel in sunCatchers)
				{
					if (Physics.Raycast(panel.position, sunDir, out raycastHit, 10000f))
					{

						if (occludingObject == null && raycastHit.collider != null)
						{
							Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.transform.gameObject);
							if (blockingPart != null)
							{
								// avoid panels from occluding themselves
								if (blockingPart == panelModule.part)
									continue;

								occludingObject = blockingPart.partInfo.title;
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
		}

		private class SSTUStaticPanel : SupportedPanel<PartModule>
		{
			public override bool CanRun(out string issueInfo)
			{
				throw new NotImplementedException();
			}

			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				throw new NotImplementedException();
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingObject, bool analytic = false)
			{
				throw new NotImplementedException();
			}

			public override double OnStart(SolarPanelFixer warpFixer, PartModule panelModule)
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
