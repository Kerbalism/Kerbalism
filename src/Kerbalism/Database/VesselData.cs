using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	public class VesselData
	{
		// references
		public Guid VesselId { get; private set; }
		public Vessel Vessel { get; private set; }

		// validity
		public bool is_vessel;              // true if this is a valid vessel
		public bool is_rescue;              // true if this is a rescue mission vessel
		public bool is_eva_dead;

		/// <summary>False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue</summary>
		public bool IsValid { get; private set; }

		/// <summary>Set to true after evaluation has finished. Used to avoid triggering of events from an uninitialized status</summary>
		private bool Evaluated = false;

		// time since last update
		private double secSinceLastEval;

		#region non-evaluated non-persisted fields
		// there are probably a lot of candidates for this in the current codebase

		/// <summary>name of file being transmitted, or empty</summary>
		// TODO : transmitting is both evaluated here and set from Science.Update(), a sure sign that the handling of this is a huge mess
		public string transmitting;

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

		// other persisted fields
		public bool msg_signal;       // message flag: link status
		public bool msg_belt;         // message flag: crossing radiation belt
		public double storm_time;     // time of next storm (interplanetary CME)
		public double storm_age;      // time since last storm (interplanetary CME)
		public uint storm_state;      // 0: none, 1: inbound, 2: in progress (interplanetary CME)
		private Dictionary<string, SupplyData> supplies; // supplies data
		public List<uint> scansat_id; // used to remember scansat sensors that were disabled
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

		/// <summary> [environment] List of all stars/suns and the related data/calculations for the current vessel</summary>
		public List<SunInfo> EnvSunsInfo => sunsInfo; List<SunInfo> sunsInfo;

		public double RadiationSunShieldingFactor;

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
			// the quantization error first became noticeable, and then exceed 100%, to solve this :
			// - we switch to an analytical estimation of the sunlight/shadow period
			// - atmo_factor become an average atmospheric absorption factor over the daylight period (not the whole day)
			public static void UpdateSunsInfo(VesselData vd, Vector3d vesselPosition)
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
						// analytical estimation of the portion of orbit that was in sunlight, current limitations :
						// - the result is dependant on the vessel altitude at the time of evaluation, 
						//   consequently it gives inconsistent behavior with highly eccentric orbits
						// - this totally ignore the orbit inclinaison, polar orbits will be treated as equatorial orbits
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

		/// <summary>moist atmosphere amount</summary>
		public double Humidity => humidity; double humidity;

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
		#endregion

		public ScienceLog ScienceLog { get; } = new ScienceLog();
		public VesselCache Cache => cache;

		VesselCache cache;

		public void Initialize(Vessel v)
		{
			VesselId = Lib.VesselID(v);
			Vessel = v;

			cache = new VesselCache(v);

			EvaluateValidity(v);
			if (!IsValid) return;

			// use a random initial time to avoid having all loaded vessels being updated in the same tick
			Evaluate(true, Lib.RandomDouble());
		}

		/// <summary>Called for all vessels every FixedUpdate. </summary>
		public void Update(Vessel v)
		{
			// Acquiring the vessel reference is needed in the following case :
			// - when instantiating VesselData from DB.Load, the vessel doesn't exist yet
			// - after undocking, Vessel will be null because VesselData is not deleted on docking(we want to restore the cfg fields when the vessel will undock;
			if (Vessel == null) Initialize(v);

			EvaluateValidity(v);
		}

		private void EvaluateValidity(Vessel v)
		{
			// determine if this is a valid vessel
			is_vessel = Lib.IsVessel(v);

			// determine if this is a rescue mission vessel
			is_rescue = Misc.IsRescueMission(v);

			// dead EVA are not valid vessels
			is_eva_dead = EVA.IsDead(v);

			if (!is_vessel || is_rescue || is_eva_dead)
			{
				IsValid = false;
			}
			else
			{
				// if vessel was invalid but is now valid, immediatly update everything.
				if (!IsValid)
				{
					IsValid = true;
					Evaluate(true, Lib.RandomDouble());
				}
			}
		}

		/// <summary>
		/// Evaluate Status and Conditions. Called from Kerbalism.FixedUpdate :
		/// <para/> - for loaded vessels : every gametime second 
		/// <para/> - for unloaded vessels : at the beginning of every background update
		/// </summary>
		public void Evaluate(bool forced, double elapsedSeconds)
		{
			if (!IsValid) return;

			secSinceLastEval += elapsedSeconds;

			// don't update more than every second of game time
			if (!forced)
			{
				if (secSinceLastEval < 1.0) return;
				secSinceLastEval = 0.0;
			}

			EvaluateEnvironment(elapsedSeconds);
			EvaluateStatus();
			Evaluated = true;
		}

		public void UpdateOnVesselModified(Vessel v)
		{
			cache.Clear();
			Update(v);
			if (IsValid) EvaluateStatus();
		}

		public void UpdateOnDock()
		{
			cache.Clear();
			this.Vessel = null;
			msg_belt = false;
			msg_signal = false;
			storm_age = 0.0;
			storm_time = 0.0;
			storm_state = 0;
			supplies.Clear();
			scansat_id.Clear();
		}

		public VesselData()
		{
			IsValid = false;
			//Initialize(v);

			msg_signal = false;
			msg_belt = false;
			cfg_ec = PreferencesMessages.Instance.ec;
			cfg_supply = PreferencesMessages.Instance.supply;
			cfg_signal = PreferencesMessages.Instance.signal;
			cfg_malfunction = PreferencesMessages.Instance.malfunction;
			cfg_storm = PreferencesMessages.Instance.storm;
			cfg_script = PreferencesMessages.Instance.script;
			cfg_highlights = PreferencesBasic.Instance.highlights;
			cfg_showlink = true;
			storm_time = 0.0;
			storm_age = 0.0;
			storm_state = 0;
			computer = new Computer();
			supplies = new Dictionary<string, SupplyData>();
			scansat_id = new List<uint>();
		}

		#region persistence methods

		//TODO : I'm pretty sure this doesn't work as intended, as there is a good chance that the dictionary entry for this vessel is already set from elsewhere when this could have been called
		// In any case it should be a lot cleaner to move this in a "OnLoad" method instead of it being a ctor.
		public void Load(ConfigNode node)
		{
			msg_signal = Lib.ConfigValue(node, "msg_signal", false);
			msg_belt = Lib.ConfigValue(node, "msg_belt", false);
			cfg_ec = Lib.ConfigValue(node, "cfg_ec", PreferencesMessages.Instance.ec);
			cfg_supply = Lib.ConfigValue(node, "cfg_supply", PreferencesMessages.Instance.supply);
			cfg_signal = Lib.ConfigValue(node, "cfg_signal", PreferencesMessages.Instance.signal);
			cfg_malfunction = Lib.ConfigValue(node, "cfg_malfunction", PreferencesMessages.Instance.malfunction);
			cfg_storm = Lib.ConfigValue(node, "cfg_storm", PreferencesMessages.Instance.storm);
			cfg_script = Lib.ConfigValue(node, "cfg_script", PreferencesMessages.Instance.script);
			cfg_highlights = Lib.ConfigValue(node, "cfg_highlights", PreferencesBasic.Instance.highlights);
			cfg_showlink = Lib.ConfigValue(node, "cfg_showlink", true);
			storm_time = Lib.ConfigValue(node, "storm_time", 0.0);
			storm_age = Lib.ConfigValue(node, "storm_age", 0.0);
			storm_state = Lib.ConfigValue(node, "storm_state", 0u);

			RadiationSunShieldingFactor = Lib.ConfigValue(node, "RadiationSunShieldingFactor", 0.0);

			computer = node.HasNode("computer") ? new Computer(node.GetNode("computer")) : new Computer();

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

			if (node.HasNode("ScienceLog")) ScienceLog.Load(node.GetNode("ScienceLog"));
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
			node.AddValue("storm_time", storm_time);
			node.AddValue("storm_age", storm_age);
			node.AddValue("storm_state", storm_state);

			node.AddValue("RadiationSunShieldingFactor", RadiationSunShieldingFactor);

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

			ScienceLog.Save(node.AddNode("ScienceLog"));
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
			transmitting = Science.Transmitting(Vessel, connection.linked && connection.rate > double.Epsilon);

			// habitat data
			volume = Habitat.Tot_volume(Vessel);
			surface = Habitat.Tot_surface(Vessel);
			pressure = Habitat.Pressure(Vessel);

			evas = (uint)(Math.Max(0, ResourceCache.GetResource(Vessel, "Nitrogen").Amount - 330) / PreferencesLifeSupport.Instance.evaAtmoLoss);
			poisoning = Habitat.Poisoning(Vessel);
			humidity = Habitat.Humidity(Vessel);
			shielding = Habitat.Shielding(Vessel);
			livingSpace = Habitat.Living_space(Vessel);
			volumePerCrew = Habitat.Volume_per_crew(Vessel);
			comforts = new Comforts(Vessel, EnvLanded, crewCount > 1, connection.linked && connection.rate > double.Epsilon);

			// data about greenhouses
			greenhouses = Greenhouse.Greenhouses(Vessel);

			Drive.GetCapacity(Vessel, out drivesFreeSpace, out drivesCapacity);
		}
		#endregion

		#region environment evaluation
		private void EvaluateEnvironment(double elapsedSeconds)
		{
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
			SunInfo.UpdateSunsInfo(this, position);
			sunBodyAngle = Sim.SunBodyAngle(Vessel, position, mainSun.SunData.body);

			// temperature at vessel position
			temperature = Sim.Temperature(Vessel, position, solarFluxTotal, out albedoFlux, out bodyFlux, out totalFlux);
			tempDiff = Sim.TempDiff(EnvTemperature, Vessel.mainBody, EnvLanded);

			// radiation
			gammaTransparency = Sim.GammaTransparency(Vessel.mainBody, Vessel.altitude);

			bool new_innerBelt, new_outerBelt, new_magnetosphere;
			radiation = Radiation.Compute(Vessel, position, EnvGammaTransparency, mainSun.SunlightFactor, out blackout, out new_magnetosphere, out new_innerBelt, out new_outerBelt, out interstellar);

			if (new_innerBelt != innerBelt || new_outerBelt != outerBelt || new_magnetosphere != magnetosphere)
			{
				innerBelt = new_innerBelt;
				outerBelt = new_outerBelt;
				magnetosphere = new_magnetosphere;
				if(Evaluated) API.OnRadiationFieldChanged.Notify(Vessel, innerBelt, outerBelt, magnetosphere);
			}

			// extended atmosphere
			thermosphere = Sim.InsideThermosphere(Vessel);
			exosphere = Sim.InsideExosphere(Vessel);

			// other stuff
			gravioli = Sim.Graviolis(Vessel);
		}

		#endregion
	}
} // KERBALISM
