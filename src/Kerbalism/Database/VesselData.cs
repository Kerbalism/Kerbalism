using System;
using System.Collections.Generic;


namespace KERBALISM
{
	public class VesselData
	{
		// references
		public Guid VesselId { get; private set; }
		public Vessel Vessel { get; private set; }

		// validity
		/// <summary> True if the vessel exists in FlightGlobals. will be false in the editor</summary>
		public bool ExistsInFlight { get; private set; }
		public bool is_vessel;              // true if this is a valid vessel
		public bool is_rescue;              // true if this is a rescue mission vessel
		public bool is_eva_dead;

		/// <summary>False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue</summary>
		public bool IsSimulated { get; private set; }

		/// <summary>Set to true after evaluation has finished. Used to avoid triggering of events from an uninitialized status</summary>
		private bool Evaluated = false;

		// time since last update
		private double secSinceLastEval;


		#region non-evaluated non-persisted fields
		// there are probably a lot of candidates for this in the current codebase

		/// <summary>name of last file being transmitted, or empty if nothing is being transmitted</summary>
		public List<File> filesTransmitted;

		#endregion

		#region non-evaluated persisted fields
		// user defined persisted fields
		public bool cfg_ec;           // enable/disable message: ec level
		public bool cfg_supply;       // enable/disable message: supplies level
		public bool cfg_signal;       // enable/disable message: link status
		public bool cfg_malfunction;  // enable/disable message: malfunctions
		public bool cfg_storm;        // enable/disable message: storms
		public bool cfg_script;       // enable/disable message: scripts
		public bool cfg_highlights;   // show/hide malfunction highlights
		public bool cfg_showlink;     // show/hide link line
		public Computer computer;     // store scripts
		public bool deviceTransmit;   // vessel wide automation : enable/disable data transmission

		// other persisted fields
		private List<ResourceUpdateDelegate> resourceUpdateDelegates = null; // all part modules that have a ResourceUpdate method
		private Dictionary<uint, PartData> parts; // all parts by flightID
		public Dictionary<uint, PartData>.ValueCollection PartDatas => parts.Values;
		public PartData GetPartData(uint flightID)
		{
			PartData pd;
			// in some cases (KIS added parts), we might try to get partdata before it is added by part-adding events
			// so we implement a fallback here
			if (!parts.TryGetValue(flightID, out pd))
			{
				foreach (Part p in Vessel.parts)
				{
					if (p.flightID == flightID)
					{
						pd = new PartData(p);
						parts.Add(flightID, pd);
						Lib.LogDebug("VesselData : newly created part '{0}' added to vessel '{1}'", p.partInfo.title, Vessel.vesselName);
					}
				}
			}
			return pd;
		}

		public bool msg_signal;       // message flag: link status
		public bool msg_belt;         // message flag: crossing radiation belt
		public StormData stormData;
		private Dictionary<string, SupplyData> supplies; // supplies data
		public List<uint> scansat_id; // used to remember scansat sensors that were disabled
		public double scienceTransmitted;
		#endregion

		#region evaluated environment properties
		// Things like vessel situation, sunlight, temperature, radiation, 

		/// <summary>
		/// [environment] true when timewarping faster at 10000x or faster. When true, some fields are updated more frequently
		/// and their evaluation is changed to an analytic, timestep-independant and vessel-position-independant mode.
		/// </summary>
		public bool EnvIsAnalytic => isAnalytic; bool isAnalytic;

		/// <summary> [environment] true if inside ocean</summary>
		public bool EnvUnderwater => underwater; bool underwater;

		/// <summary> [environment] true if inside breathable atmosphere</summary>
		public bool EnvBreathable => breathable; bool breathable;

		/// <summary> [environment] true if on the surface of a body</summary>
		public bool EnvLanded => landed; bool landed;

		/// <summary> Is the vessel inside an atmosphere ?</summary>
		public bool EnvInAtmosphere => inAtmosphere; bool inAtmosphere;

		/// <summary> [environment] true if in zero g</summary>
		public bool EnvZeroG => zeroG; bool zeroG;

		/// <summary> [environment] solar flux reflected from the nearest body</summary>
		public double EnvAlbedoFlux => albedoFlux; double albedoFlux;

		/// <summary> [environment] infrared radiative flux from the nearest body</summary>
		public double EnvBodyFlux => bodyFlux; double bodyFlux;

		/// <summary> [environment] total flux at vessel position</summary>
		public double EnvTotalFlux => totalFlux; double totalFlux;

		/// <summary> [environment] temperature ar vessel position</summary>
		public double EnvTemperature => temperature; double temperature;

		/// <summary> [environment] difference between environment temperature and survival temperature</summary>// 
		public double EnvTempDiff => tempDiff; double tempDiff;

		/// <summary> [environment] radiation at vessel position</summary>
		public double EnvRadiation => radiation; double radiation;

		/// <summary> [environment] radiation effective for habitats/EVAs</summary>
		public double EnvHabitatRadiation => shieldedRadiation; double shieldedRadiation;

		/// <summary> [environment] true if vessel is inside a magnetopause (except the heliosphere)</summary>
		public bool EnvMagnetosphere => magnetosphere; bool magnetosphere;

		/// <summary> [environment] true if vessel is inside a radiation belt</summary>
		public bool EnvInnerBelt => innerBelt; bool innerBelt;

		/// <summary> [environment] true if vessel is inside a radiation belt</summary>
		public bool EnvOuterBelt => outerBelt; bool outerBelt;

		/// <summary> [environment] true if vessel is outside sun magnetopause</summary>
		public bool EnvInterstellar => interstellar; bool interstellar;

		/// <summary> [environment] true if the vessel is inside a magnetopause (except the sun) and under storm</summary>
		public bool EnvBlackout => blackout; bool blackout;

		/// <summary> [environment] true if vessel is inside thermosphere</summary>
		public bool EnvThermosphere => thermosphere; bool thermosphere;

		/// <summary> [environment] true if vessel is inside exosphere</summary>
		public bool EnvExosphere => exosphere; bool exosphere;

		/// <summary> [environment] true if vessel is inside exosphere</summary>
		public bool EnvStorm => inStorm; bool inStorm;

		/// <summary> [environment] proportion of ionizing radiation not blocked by atmosphere</summary>
		public double EnvGammaTransparency => gammaTransparency; double gammaTransparency;

		/// <summary> [environment] gravitation gauge particles detected (joke)</summary>
		public double EnvGravioli => gravioli; double gravioli;

		/// <summary> [environment] Bodies whose apparent diameter from the vessel POV is greater than ~10 arcmin (~0.003 radians)</summary>
		// real apparent diameters at earth : sun/moon =~ 30 arcmin, Venus =~ 1 arcmin
		public List<CelestialBody> EnvVisibleBodies => visibleBodies; List<CelestialBody> visibleBodies;

		/// <summary> [environment] Sun that send the highest nominal solar flux (in W/m²) at vessel position</summary>
		public SunInfo EnvMainSun => mainSun; SunInfo mainSun;

		/// <summary> [environment] Angle of the main sun on the surface at vessel position</summary>
		public double EnvSunBodyAngle => sunBodyAngle; double sunBodyAngle;

		/// <summary>
		///  [environment] total solar flux from all stars at vessel position in W/m², include atmospheric absorption if inside an atmosphere (atmo_factor)
		/// <para/> zero when the vessel is in shadow while evaluation is non-analytic (low timewarp rates)
		/// <para/> in analytic evaluation, this include fractional sunlight factor
		/// </summary>
		public double EnvSolarFluxTotal => solarFluxTotal; double solarFluxTotal;

		/// <summary> similar to solar flux total but doesn't account for atmo absorbtion nor occlusion</summary>
		private double rawSolarFluxTotal;

		/// <summary> [environment] Average time spend in sunlight, including sunlight from all suns/stars. Each sun/star influence is pondered by its flux intensity</summary>
		public double EnvSunlightFactor => sunlightFactor; double sunlightFactor;

		/// <summary> [environment] true if the vessel is currently in sunlight, or at least half the time when in analytic mode</summary>
		public bool EnvInSunlight => sunlightFactor > 0.49;

		/// <summary> [environment] true if the vessel is currently in shadow, or least 90% of the time when in analytic mode</summary>
		// this threshold is also used to ignore light coming from distant/weak stars 
		public bool EnvInFullShadow => sunlightFactor < 0.1;

		/// <summary> List of all habitats and their relevant sun shielding parts </summary>
		public VesselHabitatInfo EnvHabitatInfo => habitatInfo; VesselHabitatInfo habitatInfo;

		/// <summary> [environment] List of all stars/suns and the related data/calculations for the current vessel</summary>
		public List<SunInfo> EnvSunsInfo => sunsInfo; List<SunInfo> sunsInfo;

		public VesselSituations VesselSituations => vesselSituations; VesselSituations vesselSituations;

		public class SunInfo
		{
			/// <summary> reference to the sun/star</summary>
			public Sim.SunData SunData => sunData; Sim.SunData sunData;

			/// <summary> normalized vector from vessel to sun</summary>
			public Vector3d Direction => direction; Vector3d direction;

			/// <summary> distance from vessel to sun surface</summary>
			public double Distance => distance; double distance;

			/// <summary>
			/// return 1.0 when the vessel is in direct sunlight, 0.0 when in shadow
			/// <para/> in analytic evaluation, this is a scalar of representing the fraction of time spent in sunlight
			/// </summary>
			// current limitations :
			// - the result is dependant on the vessel altitude at the time of evaluation, 
			//   consequently it gives inconsistent behavior with highly eccentric orbits
			// - this totally ignore the orbit inclinaison, polar orbits will be treated as equatorial orbits
			public double SunlightFactor => sunlightFactor; double sunlightFactor;

			/// <summary>
			/// solar flux at vessel position in W/m², include atmospheric absorption if inside an atmosphere (atmo_factor)
			/// <para/> zero when the vessel is in shadow while evaluation is non-analytic (low timewarp rates)
			/// <para/> in analytic evaluation, this include fractional sunlight / atmo absorbtion
			/// </summary>
			public double SolarFlux => solarFlux; double solarFlux;

			/// <summary>
			/// scalar for solar flux absorbtion by atmosphere at vessel position, not meant to be used directly (use solar_flux instead)
			/// <para/> if integrated over orbit (analytic evaluation), average atmospheric absorption factor over the daylight period (not the whole day)
			/// </summary>
			public double AtmoFactor => atmoFactor; double atmoFactor;

			/// <summary> proportion of this sun flux in the total flux at the vessel position (ignoring atmoshere and occlusion) </summary>
			public double FluxProportion => fluxProportion; double fluxProportion;

			/// <summary> similar to solar flux but doesn't account for atmo absorbtion nor occlusion</summary>
			private double rawSolarFlux;

			public SunInfo(Sim.SunData sunData)
			{
				this.sunData = sunData;
			}

			/// <summary>
			/// Update the 'sunsInfo' list and the 'mainSun', 'solarFluxTotal' variables.
			/// Uses discrete or analytic (for high timewarp speeds) evaluation methods based on the isAnalytic bool.
			/// Require the 'visibleBodies' variable to be set.
			/// </summary>
			// at the two highest timewarp speed, the number of sun visibility samples drop to the point that
			// the quantization error first became noticeable, and then exceed 100%, to solve this:
			// - we switch to an analytical estimation of the sunlight/shadow period
			// - atmo_factor become an average atmospheric absorption factor over the daylight period (not the whole day)
			public static void UpdateSunsInfo(VesselData vd, Vector3d vesselPosition, double elapsedSeconds)
			{
				Vessel v = vd.Vessel;
				double lastSolarFlux = 0.0;

				vd.sunsInfo = new List<SunInfo>(Sim.suns.Count);
				vd.solarFluxTotal = 0.0;
				vd.rawSolarFluxTotal = 0.0;

				foreach (Sim.SunData sunData in Sim.suns)
				{
					SunInfo sunInfo = new SunInfo(sunData);

					if (vd.isAnalytic)
					{
						// get sun direction and distance
						Lib.DirectionAndDistance(vesselPosition, sunInfo.sunData.body, out sunInfo.direction, out sunInfo.distance);
						// analytical estimation of the portion of orbit that was in sunlight.
						// it has some limitations, see the comments on Sim.ShadowPeriod

						if (Settings.UseSamplingSunFactor)
							// sampling estimation of the portion of orbit that is in sunlight
							// until we will calculate again
							sunInfo.sunlightFactor = Sim.SampleSunFactor(v, elapsedSeconds);

						else
							// analytical estimation of the portion of orbit that was in sunlight.
							// it has some limitations, see the comments on Sim.ShadowPeriod
							sunInfo.sunlightFactor = 1.0 - Sim.ShadowPeriod(v) / Sim.OrbitalPeriod(v);


						// get atmospheric absorbtion
						// for atmospheric bodies whose rotation period is less than 120 hours,
						// determine analytic atmospheric absorption over a single body revolution instead
						// of using a discrete value that would be unreliable at large timesteps :
						if (vd.inAtmosphere)
							sunInfo.atmoFactor = Sim.AtmosphereFactorAnalytic(v.mainBody, vesselPosition, sunInfo.direction);
						else
							sunInfo.atmoFactor = 1.0;
					}
					else
					{
						// determine if in sunlight, calculate sun direction and distance
						sunInfo.sunlightFactor = Sim.IsBodyVisible(v, vesselPosition, sunData.body, vd.visibleBodies, out sunInfo.direction, out sunInfo.distance) ? 1.0 : 0.0;
						// get atmospheric absorbtion
						sunInfo.atmoFactor = Sim.AtmosphereFactor(v.mainBody, vesselPosition, sunInfo.direction);
					}

					// get resulting solar flux in W/m²
					sunInfo.rawSolarFlux = sunInfo.sunData.SolarFlux(sunInfo.distance);
					sunInfo.solarFlux = sunInfo.rawSolarFlux * sunInfo.sunlightFactor * sunInfo.atmoFactor;
					// increment total flux from all stars
					vd.rawSolarFluxTotal += sunInfo.rawSolarFlux;
					vd.solarFluxTotal += sunInfo.solarFlux;
					// add the star to the list
					vd.sunsInfo.Add(sunInfo);
					// the most powerful star will be our "default" sun. Uses raw flux before atmo / sunlight factor
					if (sunInfo.rawSolarFlux > lastSolarFlux)
					{
						lastSolarFlux = sunInfo.rawSolarFlux;
						vd.mainSun = sunInfo;
					}
				}

				vd.sunlightFactor = 0.0;
				foreach (SunInfo sunInfo in vd.sunsInfo)
				{
					sunInfo.fluxProportion = sunInfo.rawSolarFlux / vd.rawSolarFluxTotal;
					vd.sunlightFactor += sunInfo.SunlightFactor * sunInfo.fluxProportion;
				}
				// avoid rounding errors
				if (vd.sunlightFactor > 0.99) vd.sunlightFactor = 1.0;
			}
		}
		#endregion

		#region evaluated vessel state information properties
		// things like
		// TODO : change all those fields to { get; private set; } properties
		/// <summary>number of crew on the vessel</summary>
		public int CrewCount => crewCount; int crewCount;

		/// <summary>crew capacity of the vessel</summary>
		public int CrewCapacity => crewCapacity; int crewCapacity;

		/// <summary>true if at least a component has malfunctioned or had a critical failure</summary>
		public bool Malfunction => malfunction; bool malfunction;

		/// <summary>true if at least a component had a critical failure</summary>
		public bool Critical => critical; bool critical;

		/// <summary>connection info</summary>
		public ConnectionInfo Connection => connection; ConnectionInfo connection;

		/// <summary>enabled volume in m^3</summary>
		public double Volume => volume; double volume;

		/// <summary>enabled surface in m^2</summary> 
		public double Surface => surface; double surface;

		/// <summary>normalized pressure</summary>
		public double Pressure => pressure; double pressure;

		/// <summary>number of EVA's using available Nitrogen</summary>
		public uint Evas => evas; uint evas;

		/// <summary>waste atmosphere amount versus total atmosphere amount</summary>
		public double Poisoning => poisoning; double poisoning;

		/// <summary>shielding level</summary>
		public double Shielding => shielding; double shielding;

		/// <summary>living space factor</summary>
		public double LivingSpace => livingSpace; double livingSpace;

		/// <summary>Available volume per crew</summary>
		public double VolumePerCrew => volumePerCrew; double volumePerCrew;

		/// <summary>comfort info</summary>
		public Comforts Comforts => comforts; Comforts comforts;

		/// <summary>some data about greenhouses</summary>
		public List<Greenhouse.Data> Greenhouses => greenhouses; List<Greenhouse.Data> greenhouses;

		/// <summary>true if vessel is powered</summary>
		public bool Powered => powered; bool powered;

		/// <summary>free data storage available data capacity of all public drives</summary>
		public double DrivesFreeSpace => drivesFreeSpace; double drivesFreeSpace = 0.0;

		/// <summary>data capacity of all public drives</summary>
		public double DrivesCapacity => drivesCapacity; double drivesCapacity = 0.0;

		/// <summary>evaluated on loaded vessels based on the data pushed by SolarPanelFixer. This doesn't change for unloaded vessel, so the value is persisted</summary>
		public double SolarPanelsAverageExposure => solarPanelsAverageExposure; double solarPanelsAverageExposure = -1.0;
		private List<double> solarPanelsExposure = new List<double>(); // values are added by SolarPanelFixer, then cleared by VesselData once solarPanelsAverageExposure has been computed
		public void SaveSolarPanelExposure(double exposure) => solarPanelsExposure.Add(exposure); // meant to be called by SolarPanelFixer

		private List<ReliabilityInfo> reliabilityStatus;
		public List<ReliabilityInfo> ReliabilityStatus()
		{
			if (reliabilityStatus != null) return reliabilityStatus;
			reliabilityStatus = ReliabilityInfo.BuildList(Vessel);
			return reliabilityStatus;
		}

		public void ResetReliabilityStatus()
		{
			reliabilityStatus = null;
		}

		#endregion

		#region core update handling

		/// <summary> Garanteed to be called for every VesselData in DB before any other method (FixedUpdate/Evaluate) is called </summary>
		public void EarlyUpdate()
		{
			ExistsInFlight = false;
		}

		/// <summary>Called every FixedUpdate for all existing flightglobal vessels </summary>
		public void Update(Vessel v)
		{
			bool isInit = Vessel == null; // debug

			Vessel = v;
			ExistsInFlight = true;

			if (!ExistsInFlight || !CheckIfSimulated())
			{
				IsSimulated = false;
			}
			else
			{
				// if vessel wasn't simulated previously : update everything immediately.
				if (!IsSimulated)
				{
					Lib.LogDebug("VesselData : id '" + VesselId + "' (" + Vessel.vesselName + ") is now simulated (wasn't previously)");
					IsSimulated = true;
					Evaluate(true, Lib.RandomDouble());
				}
			}

			if (isInit)
			{
				Lib.LogDebug("Init complete : IsSimulated={3}, is_vessel={0}, is_rescue={1}, is_eva_dead={2} ({4})", is_vessel, is_rescue, is_eva_dead, IsSimulated, Vessel.vesselName);
			}
		}

		private bool CheckIfSimulated()
		{
			// determine if this is a valid vessel
			is_vessel = Lib.IsVessel(Vessel);

			// determine if this is a rescue mission vessel
			is_rescue = Misc.IsRescueMission(Vessel);

			// dead EVA are not valid vessels
			is_eva_dead = EVA.IsDead(Vessel);

			return is_vessel && !is_rescue && !is_eva_dead;
		}

		/// <summary>
		/// Evaluate Status and Conditions. Called from Kerbalism.FixedUpdate :
		/// <para/> - for loaded vessels : every gametime second 
		/// <para/> - for unloaded vessels : at the beginning of every background update
		/// </summary>
		public void Evaluate(bool forced, double elapsedSeconds)
		{
			if (!IsSimulated) return;

			secSinceLastEval += elapsedSeconds;

			// don't update more than every second of game time
			if (!forced && secSinceLastEval < 1.0)
				return;

			EvaluateEnvironment(secSinceLastEval);
			EvaluateStatus();
			secSinceLastEval = 0.0;
			Evaluated = true;
		}

		/// <summary>
		/// Call ResourceUpdate on all part modules that have that method
		/// </summary>
		public void ResourceUpdate(VesselResources resources, double elapsed_s)
		{
			// only do this for loaded vessels. unloaded vessels will be handled in Background.cs
			if (!Vessel.loaded) return;

			if(resourceUpdateDelegates == null)
			{
				resourceUpdateDelegates = new List<ResourceUpdateDelegate>();
				foreach(var part in Vessel.parts)
				{
					foreach(var module in part.Modules)
					{
						if (!module.isEnabled) continue;
						var resourceUpdateDelegate = ResourceUpdateDelegate.Instance(module);
						if (resourceUpdateDelegate != null) resourceUpdateDelegates.Add(resourceUpdateDelegate);
					}
				}
			}

			if (resourceUpdateDelegates.Count == 0) return;

			List<ResourceInfo> allResources = resources.GetAllResources(Vessel); // there might be some performance to be gained by caching the list of all resource

			Dictionary<string, double> availableResources = new Dictionary<string, double>();
			foreach (var ri in allResources)
				availableResources[ri.ResourceName] = ri.Amount;
			List<KeyValuePair<string, double>> resourceChangeRequests = new List<KeyValuePair<string, double>>();

			foreach(var resourceUpdateDelegate in resourceUpdateDelegates)
			{
				resourceChangeRequests.Clear();
				string title = resourceUpdateDelegate.invoke(availableResources, resourceChangeRequests);
				foreach (var rc in resourceChangeRequests)
				{
					if (rc.Value > 0) resources.Produce(Vessel, rc.Key, rc.Value * elapsed_s, title);
					if (rc.Value < 0) resources.Consume(Vessel, rc.Key, -rc.Value * elapsed_s, title);
				}
			}
		}

		#endregion

		#region events handling

		public void UpdateOnVesselModified()
		{
			if (!IsSimulated)
				return;

			resourceUpdateDelegates = null;
			ResetReliabilityStatus();
			habitatInfo = new VesselHabitatInfo(null);
			EvaluateStatus();

			Lib.LogDebug("VesselData updated on vessel modified event ({0})", Vessel.vesselName);
		}

		/// <summary> Called by GameEvents.onVesselsUndocking, just after 2 vessels have undocked </summary>
		internal static void OnDecoupleOrUndock(Vessel oldVessel, Vessel newVessel)
		{
			Lib.LogDebug("Decoupling vessel '{0}' from vessel '{1}'", newVessel.vesselName, oldVessel.vesselName);

			VesselData oldVD = oldVessel.KerbalismData();
			VesselData newVD = newVessel.KerbalismData();

			// remove all partdata on the new vessel
			newVD.parts.Clear();

			foreach (Part part in newVessel.Parts)
			{
				PartData pd;
				// for all parts in the new vessel, move the corresponding partdata from the old vessel to the new vessel
				if (oldVD.parts.TryGetValue(part.flightID, out pd))
				{
					newVD.parts.Add(part.flightID, pd);
					oldVD.parts.Remove(part.flightID);
				}
			}

			newVD.UpdateOnVesselModified();
			oldVD.UpdateOnVesselModified();

			Lib.LogDebug("Decoupling complete for new vessel, vd.partcount={1}, v.partcount={2} ({0})", newVessel.vesselName, newVD.parts.Count, newVessel.parts.Count);
			Lib.LogDebug("Decoupling complete for old vessel, vd.partcount={1}, v.partcount={2} ({0})", oldVessel.vesselName, oldVD.parts.Count, oldVessel.parts.Count);
		}

		// This is for mods (KIS), won't be used in a stock game (the docking is handled in the OnDock method
		internal static void OnPartCouple(GameEvents.FromToAction<Part, Part> data)
		{
			Lib.LogDebug("Coupling part '{0}' from vessel '{1}' to vessel '{2}'", data.from.partInfo.title, data.from.vessel.vesselName, data.to.vessel.vesselName);

			Vessel fromVessel = data.from.vessel;
			Vessel toVessel = data.to.vessel;

			VesselData fromVD = fromVessel.KerbalismData();
			VesselData toVD = toVessel.KerbalismData();

			// GameEvents.onPartCouple may be fired by mods (KIS) that add new parts to an existing vessel
			// In the case of KIS, the part vessel is already set to the destination vessel when the event is fired
			// so we just add the part.
			if (fromVD == toVD)
			{
				if (!toVD.parts.ContainsKey(data.from.flightID))
				{
					toVD.parts.Add(data.from.flightID, new PartData(data.from));
					Lib.LogDebug("VesselData : newly created part '{0}' added to vessel '{1}'", data.from.partInfo.title, data.to.vessel.vesselName);
				}
				return;
			}

			// add all partdata of the docking vessel to the docked to vessel
			foreach (PartData partData in fromVD.parts.Values)
			{
				toVD.parts.Add(partData.FlightId, partData);
			}
			// remove all partdata from the docking vessel
			fromVD.parts.Clear();

			// reset a few things on the docked to vessel
			toVD.supplies.Clear();
			toVD.scansat_id.Clear();
			toVD.UpdateOnVesselModified();

			Lib.LogDebug("Coupling complete to   vessel, vd.partcount={1}, v.partcount={2} ({0})", toVessel.vesselName, toVD.parts.Count, toVessel.parts.Count);
			Lib.LogDebug("Coupling complete from vessel, vd.partcount={1}, v.partcount={2} ({0})", fromVessel.vesselName, fromVD.parts.Count, fromVessel.parts.Count);
		}

		internal static void OnPartWillDie(Part part)
		{
			VesselData vd = part.vessel.KerbalismData();
			vd.parts.Remove(part.flightID);
			vd.UpdateOnVesselModified();
			Lib.LogDebug("Removing dead part, vd.partcount={0}, v.partcount={1} (part '{2}' in vessel '{3}')", vd.parts.Count, part.vessel.parts.Count, part.partInfo.title, part.vessel.vesselName);
		}

		#endregion

		#region ctor / init / persistence

		/// <summary> This ctor is to be used for newly created vessels </summary>
		public VesselData(Vessel vessel)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Ctor");

			ExistsInFlight = true;	// vessel exists
			IsSimulated = false;	// will be evaluated in next fixedupdate

			Vessel = vessel;
			VesselId = Vessel.id;
			
			parts = new Dictionary<uint, PartData>();
			if (Vessel.loaded)
				foreach (Part part in Vessel.Parts)
					parts.Add(part.flightID, new PartData(part));
			else
				// vessels can be created unloaded, asteroids for example
				foreach (ProtoPartSnapshot protopart in Vessel.protoVessel.protoPartSnapshots)
					parts.Add(protopart.flightID, new PartData(protopart));


			FieldsDefaultInit(vessel.protoVessel);

			Lib.LogDebug("VesselData ctor (new vessel) : id '" + VesselId + "' (" + Vessel.vesselName + "), part count : " + parts.Count);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// This ctor is meant to be used in OnLoad only, but can be used as a fallback
		/// with a null ConfigNode to create VesselData from a protovessel. 
		/// The Vessel reference will be acquired in the next fixedupdate
		/// </summary>
		public VesselData(ProtoVessel protoVessel, ConfigNode node)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Ctor");
			ExistsInFlight = false;
			IsSimulated = false;

			VesselId = protoVessel.vesselID;

			parts = new Dictionary<uint, PartData>();
			foreach (ProtoPartSnapshot protopart in protoVessel.protoPartSnapshots)
				parts.Add(protopart.flightID, new PartData(protopart));

			if (node == null)
			{
				FieldsDefaultInit(protoVessel);
				Lib.LogDebug("VesselData ctor (created from protovessel) : id '" + VesselId + "' (" + protoVessel.vesselName + "), part count : " + parts.Count);
			}
			else
			{
				Load(node);
				Lib.LogDebug("VesselData ctor (loaded from database) : id '" + VesselId + "' (" + protoVessel.vesselName + "), part count : " + parts.Count);
			}
			UnityEngine.Profiling.Profiler.EndSample();
		}

		private void FieldsDefaultInit(ProtoVessel pv)
		{
			msg_signal = false;
			msg_belt = false;
			cfg_ec = PreferencesMessages.Instance.ec;
			cfg_supply = PreferencesMessages.Instance.supply;
			cfg_signal = PreferencesMessages.Instance.signal;
			cfg_malfunction = PreferencesMessages.Instance.malfunction;
			cfg_storm = Features.SpaceWeather && PreferencesMessages.Instance.storm && Lib.CrewCount(pv) > 0;
			cfg_script = PreferencesMessages.Instance.script;
			cfg_highlights = PreferencesReliability.Instance.highlights;
			cfg_showlink = true;
			deviceTransmit = true;

			stormData = new StormData(null);
			habitatInfo = new VesselHabitatInfo(null);
			computer = new Computer(null);
			supplies = new Dictionary<string, SupplyData>();
			scansat_id = new List<uint>();
			filesTransmitted = new List<File>();
			vesselSituations = new VesselSituations(this);
		}

		private void Load(ConfigNode node)
		{
			msg_signal = Lib.ConfigValue(node, "msg_signal", false);
			msg_belt = Lib.ConfigValue(node, "msg_belt", false);
			cfg_ec = Lib.ConfigValue(node, "cfg_ec", PreferencesMessages.Instance.ec);
			cfg_supply = Lib.ConfigValue(node, "cfg_supply", PreferencesMessages.Instance.supply);
			cfg_signal = Lib.ConfigValue(node, "cfg_signal", PreferencesMessages.Instance.signal);
			cfg_malfunction = Lib.ConfigValue(node, "cfg_malfunction", PreferencesMessages.Instance.malfunction);
			cfg_storm = Lib.ConfigValue(node, "cfg_storm", PreferencesMessages.Instance.storm);
			cfg_script = Lib.ConfigValue(node, "cfg_script", PreferencesMessages.Instance.script);
			cfg_highlights = Lib.ConfigValue(node, "cfg_highlights", PreferencesReliability.Instance.highlights);
			cfg_showlink = Lib.ConfigValue(node, "cfg_showlink", true);

			deviceTransmit = Lib.ConfigValue(node, "deviceTransmit", true);

			solarPanelsAverageExposure = Lib.ConfigValue(node, "solarPanelsAverageExposure", -1.0);
			scienceTransmitted = Lib.ConfigValue(node, "scienceTransmitted", 0.0);

			stormData = new StormData(node.GetNode("StormData"));
			habitatInfo = new VesselHabitatInfo(node.GetNode("SunShielding"));
			computer = new Computer(node.GetNode("computer"));

			supplies = new Dictionary<string, SupplyData>();
			foreach (var supply_node in node.GetNode("supplies").GetNodes())
			{
				supplies.Add(DB.From_safe_key(supply_node.name), new SupplyData(supply_node));
			}

			scansat_id = new List<uint>();
			foreach (string s in node.GetValues("scansat_id"))
			{
				scansat_id.Add(Lib.Parse.ToUInt(s));
			}

			ConfigNode partsNode = new ConfigNode();
			if (node.TryGetNode("parts", ref partsNode))
			{
				foreach (ConfigNode partDataNode in partsNode.GetNodes())
				{
					PartData partData;
					if (parts.TryGetValue(Lib.Parse.ToUInt(partDataNode.name), out partData))
						partData.Load(partDataNode);
				}
			}

			filesTransmitted = new List<File>();
			vesselSituations = new VesselSituations(this);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("msg_signal", msg_signal);
			node.AddValue("msg_belt", msg_belt);
			node.AddValue("cfg_ec", cfg_ec);
			node.AddValue("cfg_supply", cfg_supply);
			node.AddValue("cfg_signal", cfg_signal);
			node.AddValue("cfg_malfunction", cfg_malfunction);
			node.AddValue("cfg_storm", cfg_storm);
			node.AddValue("cfg_script", cfg_script);
			node.AddValue("cfg_highlights", cfg_highlights);
			node.AddValue("cfg_showlink", cfg_showlink);

			node.AddValue("deviceTransmit", deviceTransmit);

			node.AddValue("solarPanelsAverageExposure", solarPanelsAverageExposure);
			node.AddValue("scienceTransmitted", scienceTransmitted);

			stormData.Save(node.AddNode("StormData"));
			computer.Save(node.AddNode("computer"));

			var supplies_node = node.AddNode("supplies");
			foreach (var p in supplies)
			{
				p.Value.Save(supplies_node.AddNode(DB.To_safe_key(p.Key)));
			}

			foreach (uint id in scansat_id)
			{
				node.AddValue("scansat_id", id.ToString());
			}

			EnvHabitatInfo.Save(node.AddNode("SunShielding"));

			ConfigNode partsNode = node.AddNode("parts");
			foreach (PartData partData in parts.Values)
			{
				// currently we only use partdata for drives, so optimize it a bit
				if (partData.Drive != null)
				{
					ConfigNode partNode = partsNode.AddNode(partData.FlightId.ToString());
					partData.Save(partNode);
				}
			}

			if (Vessel != null)
				Lib.LogDebug("VesselData saved for vessel " + Vessel.vesselName);
			else
				Lib.LogDebug("VesselData saved for vessel (Vessel is null)");

		}

		#endregion

		public SupplyData Supply(string name)
		{
			if (!supplies.ContainsKey(name))
			{
				supplies.Add(name, new SupplyData());
			}
			return supplies[name];
		}

		#region vessel state evaluation
		private void EvaluateStatus()
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EvaluateStatus");
			// determine if there is enough EC for a powered state
			powered = Lib.IsPowered(Vessel);

			// calculate crew info for the vessel
			crewCount = Lib.CrewCount(Vessel);
			crewCapacity = Lib.CrewCapacity(Vessel);

			// malfunction stuff
			malfunction = Reliability.HasMalfunction(Vessel);
			critical = Reliability.HasCriticalFailure(Vessel);

			// communications info
			connection = ConnectionInfo.Update(Vessel, powered, EnvBlackout);

			// habitat data
			habitatInfo.Update(Vessel);
			volume = Habitat.Tot_volume(Vessel);
			surface = Habitat.Tot_surface(Vessel);
			pressure = Math.Min(Habitat.Pressure(Vessel), habitatInfo.MaxPressure);

			evas = (uint)(Math.Max(0, ResourceCache.GetResource(Vessel, "Nitrogen").Amount - 330) / Settings.LifeSupportAtmoLoss);
			poisoning = Habitat.Poisoning(Vessel);
			shielding = Habitat.Shielding(Vessel);
			livingSpace = Habitat.Living_space(Vessel);
			volumePerCrew = Habitat.Volume_per_crew(Vessel);
			comforts = new Comforts(Vessel, EnvLanded, crewCount > 1, connection.linked && connection.rate > double.Epsilon);

			// data about greenhouses
			greenhouses = Greenhouse.Greenhouses(Vessel);

			Drive.GetCapacity(this, out drivesFreeSpace, out drivesCapacity);

			// solar panels data
			if (Vessel.loaded)
			{
				solarPanelsAverageExposure = SolarPanelFixer.GetSolarPanelsAverageExposure(solarPanelsExposure);
				solarPanelsExposure.Clear();
			}
			UnityEngine.Profiling.Profiler.EndSample();
		}
		#endregion

		#region environment evaluation
		private void EvaluateEnvironment(double elapsedSeconds)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EvaluateStatus");
			// we use analytic mode if more than 2 minutes of game time has passed since last evaluation (~ x6000 timewarp speed)
			isAnalytic = elapsedSeconds > 120.0;

			// get vessel position
			Vector3d position = Lib.VesselPosition(Vessel);

			// this should never happen again
			if (Vector3d.Distance(position, Vessel.mainBody.position) < 1.0)
			{
				throw new Exception("Shit hit the fan for vessel " + Vessel.vesselName);
			}

			// situation
			underwater = Sim.Underwater(Vessel);
			breathable = Sim.Breathable(Vessel, EnvUnderwater);
			landed = Lib.Landed(Vessel);
			
			inAtmosphere = Vessel.mainBody.atmosphere && Vessel.altitude < Vessel.mainBody.atmosphereDepth;
			zeroG = !EnvLanded && !inAtmosphere;

			visibleBodies = Sim.GetLargeBodies(position);

			// get solar info (with multiple stars / Kopernicus support)
			// get the 'visibleBodies' and 'sunsInfo' lists, the 'mainSun', 'solarFluxTotal' variables.
			// require the situation variables to be evaluated first
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Sunlight");
			SunInfo.UpdateSunsInfo(this, position, elapsedSeconds);
			UnityEngine.Profiling.Profiler.EndSample();
			sunBodyAngle = Sim.SunBodyAngle(Vessel, position, mainSun.SunData.body);

			// temperature at vessel position
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Temperature");
			temperature = Sim.Temperature(Vessel, position, solarFluxTotal, out albedoFlux, out bodyFlux, out totalFlux);
			tempDiff = Sim.TempDiff(EnvTemperature, Vessel.mainBody, EnvLanded);
			UnityEngine.Profiling.Profiler.EndSample();

			// radiation
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Radiation");
			gammaTransparency = Sim.GammaTransparency(Vessel.mainBody, Vessel.altitude);

			bool new_innerBelt, new_outerBelt, new_magnetosphere;
			radiation = Radiation.Compute(Vessel, position, EnvGammaTransparency, mainSun.SunlightFactor, out blackout, out new_magnetosphere, out new_innerBelt, out new_outerBelt, out interstellar, out shieldedRadiation);

			if (new_innerBelt != innerBelt || new_outerBelt != outerBelt || new_magnetosphere != magnetosphere)
			{
				innerBelt = new_innerBelt;
				outerBelt = new_outerBelt;
				magnetosphere = new_magnetosphere;
				if(Evaluated) API.OnRadiationFieldChanged.Notify(Vessel, innerBelt, outerBelt, magnetosphere);
			}
			UnityEngine.Profiling.Profiler.EndSample();

			thermosphere = Sim.InsideThermosphere(Vessel);
			exosphere = Sim.InsideExosphere(Vessel);
			inStorm = Storm.InProgress(Vessel);
			vesselSituations.Update();

			// other stuff
			gravioli = Sim.Graviolis(Vessel);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		#endregion
	}
} // KERBALISM
