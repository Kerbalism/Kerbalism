using Flee.PublicTypes;
using System;
using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
    public partial class VesselData : VesselDataBase
	{
		public List<Step> subSteps = new List<Step>();
		private SimVessel simVessel;
		

		#region FIELDS/PROPERTIES : CORE STATE AND SUBSYSTEMS

		/// <summary>
		/// reference to the KSP Vessel object
		/// </summary>
		public Vessel Vessel { get; private set; }

		/// <summary>
		/// Guid of the vessel, match the KSP affected Guid
		/// </summary>
		public Guid VesselId { get; private set; }

		/// <summary>
		/// convenience property
		/// </summary>
		public override string VesselName => Vessel?.vesselName;

        /// <summary>
		/// False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue
		/// </summary>
        public bool IsSimulated { get; private set; }

		// those are the various component of the IsSimulated check
		private bool isVessel;  // true if the vessel is not dead and if the vessel type is right
		private bool isRescue;  // true if this is a not yet loaded rescue mission vessel
		private bool isEvaDead; // true if this is an EVA kerbal that we killed

		/// <summary>
		/// True if the vessel exists in FlightGlobals. Will be false in the editor
		/// </summary>
		// TODO : unused, can probably be removed alongside the EarlyUpdate() thing
		// this was used to avoid saving vesseldatas for vessels that don't exist anymore, but now we do that
		// by only saving vessels that exist in HighLogic.CurrentGame.flightState.protoVessels
		private bool existsInFlight;

		public override bool LoadedOrEditor => Vessel.loaded;

		public override bool IsEVA => Vessel.isEVA;

		/// <summary>
		/// Time elapsed since last evaluation
		/// </summary>
		private double secSinceLastEval;

		/// <summary>
		/// Resource handler for this vessel. <br/>
		/// This can be null, or not properly initialized while VesselData.IsSimulated is false. <br/>
		/// Do not use it from PartModule.Start() / PartModule.OnStart(), and check VesselData.IsSimulated before using it from Update() / FixedUpdate()
		/// </summary>
		public override VesselResHandler ResHandler => resHandler; VesselResHandler resHandler;

		/// <summary>
		/// Comms handler for this vessel, evaluate and expose data about the vessel antennas and comm link
		/// </summary>
		public CommHandler CommHandler { get; private set; }

		/// <summary>
		/// List/Dictionary of all the vessel PartData, and their ModuleData
		/// </summary>
		public override PartDataCollectionBase Parts => VesselParts;

		/// <summary>
		/// Base class implementation for the PartData list.
		/// Prefer using the Parts property unless you are doing something that must be flight/editor agnostic.
		/// </summary>
		public PartDataCollectionVessel VesselParts { get; private set; }

		/// <summary>
		/// all part modules that have a ResourceUpdate method
		/// </summary>
		public List<ResourceUpdateDelegate> resourceUpdateDelegates = null;

        /// <summary>
        /// List of files being transmitted, or empty if nothing is being transmitted <br/>
        /// Note that the transmit rates stored in the File objects can be unreliable, do not use it apart from UI purposes
        /// </summary>
        public List<File> filesTransmitted;

		#endregion

		#region FIELDS/PROPERTIES : PERSISTED STATE

		// user defined persisted fields
		public bool cfg_ec;           // enable/disable message: ec level
        public bool cfg_supply;       // enable/disable message: supplies level
        public bool cfg_signal;       // enable/disable message: link status
        public bool cfg_malfunction;  // enable/disable message: malfunctions
        public bool cfg_storm;        // enable/disable message: storms
        public bool cfg_script;       // enable/disable message: scripts
        public bool cfg_highlights;   // show/hide malfunction highlights
        public bool cfg_showlink;     // show/hide link line
        public bool cfg_show;         // show/hide vessel in monitor
        public Computer computer;     // store scripts

        // vessel wide toggles
        public bool deviceTransmit;   // vessel wide automation : enable/disable data transmission

        // other persisted fields
        public bool msg_signal;       // message flag: link status
        public bool msg_belt;         // message flag: crossing radiation belt
        public StormData stormData;   // store state of current/next solar storm
        public Dictionary<string, Supply.SupplyState> supplies; // supplies state data
        public List<uint> scansat_id; // used to remember scansat sensors that were disabled
        public double scienceTransmitted; // how much science points has this vessel earned trough transmission

		// persist that so we don't have to do an expensive check every time
        public bool IsSerenityGroundController => isSerenityGroundController; bool isSerenityGroundController;

		#endregion

		#region FIELDS/PROPERTIES : EVALUATED VESSEL ENVIRONMENT

		// Things like vessel situation, sunlight, temperature, radiation, 

		/// <summary>
		/// [environment] true when timewarping faster at 10000x or faster. When true, some fields are updated more frequently
		/// and their evaluation is changed to an analytic, timestep-independant and vessel-position-independant mode.
		/// </summary>
		public bool EnvIsAnalytic => isAnalytic; bool isAnalytic;

		public override CelestialBody MainBody => mainBody; CelestialBody mainBody;

		public override double Altitude => altitude; double altitude;

		/// <summary> [environment] true if inside ocean</summary>
		public override bool EnvUnderwater => underwater; bool underwater;

        /// <summary> [environment] true if on the surface of a body</summary>
        public override bool EnvLanded => landed; bool landed;

        /// <summary> current atmospheric pressure in atm</summary>
        public override double EnvStaticPressure => envStaticPressure; double envStaticPressure;

        /// <summary> Is the vessel inside an atmosphere ?</summary>
        public override bool EnvInAtmosphere => inAtmosphere; bool inAtmosphere;

        /// <summary> Is the vessel inside a breatheable atmosphere ?</summary>
        public override bool EnvInOxygenAtmosphere => inOxygenAtmosphere; bool inOxygenAtmosphere;

        /// <summary> Is the vessel inside a breatheable atmosphere and at acceptable pressure conditions ?</summary>
        public override bool EnvInBreathableAtmosphere => inBreathableAtmosphere; bool inBreathableAtmosphere;

        /// <summary> [environment] true if in zero g</summary>
        public override bool EnvZeroG => zeroG; bool zeroG;

        /// <summary> [environment] solar flux reflected from the nearest body</summary>
        public override double EnvAlbedoFlux => albedoFlux; double albedoFlux;

        /// <summary> [environment] infrared radiative flux from the nearest body</summary>
        public override double EnvBodyFlux => bodyFlux; double bodyFlux;

        /// <summary> [environment] total flux at vessel position</summary>
        public override double EnvTotalFlux => totalFlux; double totalFlux;

        /// <summary> [environment] temperature ar vessel position</summary>
        public override double EnvTemperature => temperature; double temperature;

        /// <summary> [environment] difference between environment temperature and survival temperature</summary>// 
        public override double EnvTempDiff => tempDiff; double tempDiff;

        /// <summary> [environment] radiation at vessel position</summary>
        public override double EnvRadiation => radiation; double radiation;

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

		/// <summary> [environment] true if vessel currently experienced a solar storm</summary>
		public override double EnvStormRadiation => stormRadiation; public double stormRadiation;

		/// <summary> [environment] proportion of ionizing radiation not blocked by atmosphere</summary>
		public override double EnvGammaTransparency => gammaTransparency; double gammaTransparency;

        /// <summary> [environment] gravitation gauge particles detected (joke)</summary>
        public double EnvGravioli => gravioli; double gravioli;

        /// <summary> [environment] Bodies whose apparent diameter from the vessel POV is greater than ~10 arcmin (~0.003 radians)</summary>
        // real apparent diameters at earth : sun/moon =~ 30 arcmin, Venus =~ 1 arcmin
        public CelestialBody[] VisibleBodies => visibleBodies; CelestialBody[] visibleBodies;

        /// <summary> Star that send the highest nominal flux (in W/m²) at the vessel position</summary>
        public StarFlux MainStar => mainStar; StarFlux mainStar;

		/// <summary> Normalized direction vector to the main star</summary>
		public override Vector3d MainStarDirection => mainStar.direction;

		/// <summary> Proportion of current update duration spent in the direct light of the main star</summary>
		public override double MainStarSunlightFactor => mainStar.sunlightFactor;

		/// <summary> True if at least half of the current update was spent in the direct light of the main star</summary>
		public override bool InSunlight => mainStar.sunlightFactor > 0.45;

		/// <summary> True if less than 10% of the current update was spent in the direct light of the main star</summary>
		public override bool InFullShadow => mainStar.sunlightFactor < 0.1;

		/// <summary> Angle of the main sun on the body surface over the vessel position</summary>
		public double MainStarBodyAngle => sunBodyAngle; double sunBodyAngle;

		/// <summary> Sum of the flux from all stars at vessel position in W/m² </summary>
		public override double DirectStarFluxTotal => directStarFluxTotal; double directStarFluxTotal;

		public double BodiesCoreIrradiance => bodiesCoreIrradiance; double bodiesCoreIrradiance;

		/// <summary> List of all stars/suns and the related data/calculations for the current vessel</summary>
		public StarFlux[] StarsIrradiance => starsIrradiance; StarFlux[] starsIrradiance;

        public VesselSituations VesselSituations => vesselSituations; VesselSituations vesselSituations;

		#endregion

		#region FIELDS/PROPERTIES : EVALUATED VESSEL STATE

		/// <summary>number of crew on the vessel</summary>
		public override int CrewCount => crewCount; int crewCount;

        /// <summary>crew capacity of the vessel</summary>
        public override int CrewCapacity => crewCapacity; int crewCapacity;

        /// <summary>connection info</summary>
        public ConnectionInfo Connection => connection; ConnectionInfo connection;

		public override IConnectionInfo ConnectionInfo => connection;

        /// <summary>some data about greenhouses</summary>
        public List<Greenhouse.Data> Greenhouses => greenhouses; List<Greenhouse.Data> greenhouses;

		

        /// <summary>true if all command modules are hibernating (limited control and no transmission)</summary>
        public bool Hibernating { get; private set; }
        public bool hasNonHibernatingCommandModules = false;

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

		/// <summary>true if at least a component has malfunctioned or had a critical failure</summary>
		public bool Malfunction => malfunction; bool malfunction;

		/// <summary>true if at least a component had a critical failure</summary>
		public bool Critical => critical; bool critical;

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

		#region INSTANTIATION AND PERSISTANCE

		public static bool VesselNeedVesselData(ProtoVessel pv)
		{
			if (pv.vesselRef == null)
			{
				// flags have an empty Guid
				if (pv.vesselID == Guid.Empty)
					return false;

				// exclude unloaded asteroids
				if (pv.protoPartSnapshots.Count == 1 && pv.protoPartSnapshots[0].partName == "PotatoRoid")
					return false;
			}
			else
			{
				// flags have an empty Guid
				if (pv.vesselRef.id == Guid.Empty)
					return false;

				// exclude unloaded asteroids
				if (!pv.vesselRef.loaded
					&& pv.protoPartSnapshots.Count == 1 && pv.protoPartSnapshots[0].partName == "PotatoRoid")
					return false;
			}

			return true;
		}

		public VesselData(Vessel vessel, ConfigNode kerbalismDataNode, VesselDataShip shipVd)
		{
			existsInFlight = true;  // vessel exists
			IsSimulated = false;    // will be evaluated in next fixedupdate

			Vessel = vessel;
			VesselId = Vessel.id;

			VesselParts = new PartDataCollectionVessel(this, (PartDataCollectionShip)shipVd.Parts);
			resHandler = shipVd.ResHandler;
			resHandler.ConvertShipHandlerToVesselHandler();

			//Parts.Load(kerbalismDataNode); // don't load parts, they already have been loaded when the ship was instantiated
			Load(kerbalismDataNode, true);

			SetPersistedFieldsDefaults(vessel.protoVessel);
			SetInstantiateDefaults(vessel.protoVessel);
		}

		/// <summary> This ctor is to be used for newly created vessels, either from ship construction or for  </summary>
		public VesselData(Vessel vessel, List<PartData> partDatas = null)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Ctor");

			existsInFlight = true;  // vessel exists
			IsSimulated = false;    // will be evaluated in next fixedupdate

			Vessel = vessel;
			VesselId = Vessel.id;

			if (Vessel.loaded)
			{
				if (partDatas == null)
					VesselParts = new PartDataCollectionVessel(this, Vessel);
				else
					VesselParts = new PartDataCollectionVessel(this, partDatas);

				resHandler = new VesselResHandler(Vessel, VesselResHandler.VesselState.Loaded);
			}
			else
			{
				// vessels can be created unloaded, asteroids for example
				if (partDatas == null)
					VesselParts = new PartDataCollectionVessel(this, Vessel.protoVessel, null);
				else
					VesselParts = new PartDataCollectionVessel(this, partDatas);

				resHandler = new VesselResHandler(Vessel.protoVessel, VesselResHandler.VesselState.Unloaded);
			}

			SetPersistedFieldsDefaults(vessel.protoVessel);
			SetInstantiateDefaults(vessel.protoVessel);

			Lib.LogDebug("VesselData ctor (new vessel) : id '" + VesselId + "' (" + Vessel.vesselName + "), part count : " + Parts.Count);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// This ctor is meant to be used in OnLoad only, but can be used as a fallback
		/// with a null ConfigNode to create VesselData from a protovessel. 
		/// The Vessel reference will be acquired in the first fixedupdate
		/// </summary>
		public VesselData(ProtoVessel protoVessel, ConfigNode topnode)
		{
			ConfigNode vesselDataNode = topnode?.GetNode(NODENAME_VESSEL);

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Ctor");
			existsInFlight = false;
			IsSimulated = false;

			VesselId = protoVessel.vesselID;

			if (vesselDataNode == null)
			{
				VesselParts = new PartDataCollectionVessel(this, protoVessel, null);
				resHandler = new VesselResHandler(protoVessel, VesselResHandler.VesselState.Unloaded);
				SetPersistedFieldsDefaults(protoVessel);
				Lib.LogDebug("VesselData ctor (created from unsaved protovessel) : id '" + VesselId + "' (" + protoVessel.vesselName + "), part count : " + Parts.Count);
			}
			else
			{
				Lib.LogDebug("VesselData ctor (loading from database) : id '" + VesselId + "' (" + protoVessel.vesselName + ")...");
				VesselParts = new PartDataCollectionVessel(this, protoVessel, vesselDataNode);
				resHandler = new VesselResHandler(protoVessel, VesselResHandler.VesselState.Unloaded);
				Parts.Load(vesselDataNode);
				Load(vesselDataNode, false);
				Lib.LogDebug("VesselData ctor (loaded from database) : id '" + VesselId + "' (" + protoVessel.vesselName + "), part count : " + Parts.Count);
			}

			SetInstantiateDefaults(protoVessel);

			UnityEngine.Profiling.Profiler.EndSample();
		}

		private void SetPersistedFieldsDefaults(ProtoVessel pv)
		{
			msg_signal = false;
			msg_belt = false;
			cfg_ec = PreferencesMessages.Instance.ec;
			cfg_supply = PreferencesMessages.Instance.supply;
			cfg_signal = PreferencesMessages.Instance.signal;
			cfg_malfunction = PreferencesMessages.Instance.malfunction;
			cfg_storm = Features.Radiation && PreferencesMessages.Instance.storm && Lib.CrewCount(pv) > 0;
			cfg_script = PreferencesMessages.Instance.script;
			cfg_highlights = PreferencesReliability.Instance.highlights;
			cfg_showlink = true;
			cfg_show = true;
			deviceTransmit = true;
			// note : we check that at vessel creation and persist it, as the vesselType can be changed by the player
			isSerenityGroundController = pv.vesselType == VesselType.DeployedScienceController;
			stormData = new StormData(null);
			computer = new Computer(null);
			scansat_id = new List<uint>();
		}

		private void SetInstantiateDefaults(ProtoVessel protoVessel)
		{
			simVessel = new SimVessel();
			starsIrradiance = StarFlux.StarArrayFactory();
			filesTransmitted = new List<File>();
			vesselSituations = new VesselSituations(this);
			connection = new ConnectionInfo();
			CommHandler = CommHandler.GetHandler(this, isSerenityGroundController);
			supplies = Supply.CreateStateDictionary(resHandler);
		}

		protected override void OnLoad(ConfigNode node)
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
			cfg_show = Lib.ConfigValue(node, "cfg_show", true);

			deviceTransmit = Lib.ConfigValue(node, "deviceTransmit", true);

			isSerenityGroundController = Lib.ConfigValue(node, "isSerenityGroundController", false);

			solarPanelsAverageExposure = Lib.ConfigValue(node, "solarPanelsAverageExposure", -1.0);
			scienceTransmitted = Lib.ConfigValue(node, "scienceTransmitted", 0.0);

			

			stormData = new StormData(node.GetNode("StormData"));
			computer = new Computer(node.GetNode("computer"));

			scansat_id = new List<uint>();
			foreach (string s in node.GetValues("scansat_id"))
			{
				scansat_id.Add(Lib.Parse.ToUInt(s));
			}
		}

		protected override void OnSave(ConfigNode node)
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
			node.AddValue("cfg_show", cfg_show);

			node.AddValue("deviceTransmit", deviceTransmit);

			node.AddValue("isSerenityGroundController", isSerenityGroundController);

			node.AddValue("solarPanelsAverageExposure", solarPanelsAverageExposure);
			node.AddValue("scienceTransmitted", scienceTransmitted);

			stormData.Save(node.AddNode("StormData"));
			computer.Save(node.AddNode("computer"));

			foreach (uint id in scansat_id)
			{
				node.AddValue("scansat_id", id.ToString());
			}

			if (Vessel != null)
				Lib.LogDebug("VesselData saved for vessel " + Vessel.vesselName);
			else
				Lib.LogDebug("VesselData saved for vessel (Vessel is null)");

		}

		#endregion

		#region UPDATE

		/// <summary> Garanteed to be called for every VesselData in DB before any other method (Update/Evaluate) is called </summary>
		public void EarlyUpdate()
		{
			existsInFlight = false;

			/// This is just to stop the compiler complaining, I'm still not sure about suppressing existsInFlight
			if (existsInFlight)
				existsInFlight = true;
		}

		/// <summary> Must be called every FixedUpdate for all existing flightglobal vessels </summary>
		public void OnFixedUpdate(Vessel v)
		{
			bool isInit = Vessel == null; // debug

			Vessel = v;
			existsInFlight = true;

			if (!CheckIfSimulated(out bool rescueJustLoaded))
			{
				IsSimulated = false;
			}
			else
			{
				// if vessel wasn't simulated previously : update everything immediately.
				if (!IsSimulated)
				{
					secSinceLastEval = 0.0;
					Evaluate(true, Kerbalism.elapsed_s);
					// set the flag after evaluation, allow to know if this is the first evaluation from Evaluate()
					IsSimulated = true; 
					Lib.LogDebug($"{Vessel.vesselName} is now simulated");
				}
			}

			if (rescueJustLoaded)
			{
				Lib.LogDebug($"Rescue vessel {Vessel.vesselName} discovered, granting resources and enabling processing");
				OnRescueVesselLoaded();
			}

			if (isInit)
			{
				Lib.LogDebug("Init complete : IsSimulated={3}, is_vessel={0}, is_rescue={1}, is_eva_dead={2} ({4})", Lib.LogLevel.Message, isVessel, isRescue, isEvaDead, IsSimulated, Vessel.vesselName);
			}
		}

		private bool CheckIfSimulated(out bool rescueJustLoaded)
		{
			// determine if this is a valid vessel
			isVessel = Lib.IsVessel(Vessel);

			// determine if this is a rescue mission vessel
			isRescue = CheckRescueStatus(Vessel, out rescueJustLoaded);

			// dead EVA are not valid vessels
			isEvaDead = EVA.IsDead(Vessel);

			return isVessel && !isRescue && !isEvaDead;
		}

		#endregion

		#region EVALUATION

		/// <summary>
		/// Evaluate status and environment. Called from Kerbalism.FixedUpdate :
		/// <para/> - for loaded vessels : every gametime second 
		/// <para/> - for unloaded vessels : at the beginning of every background update
		/// </summary>
		public void Evaluate(bool forced, double elapsedSeconds)
		{
			secSinceLastEval += elapsedSeconds;

			PartCache.Update(this);

			// don't update things that don't change often more than every second of game time
			if (forced || secSinceLastEval > 1.0)
			{
				EnvironmentUpdate(secSinceLastEval);
				StateUpdate();
				ModuleDataUpdate();
				secSinceLastEval = 0.0;
			}

			FixedUpdate(elapsedSeconds);
		}

		private int partToUpdate = 1;
		private bool modulesStarted = false;
		private void FixedUpdate(double elapsedSec)
		{
			

			// On loaded vessels, don't call this before the loaded part / modules 
			// references have been set (happen in the Part.Start() prefix, usually called)
			if (LoadedOrEditor)
			{
				if (Parts[0].LoadedPart == null)
				{
					Lib.LogDebug($"Skipping loaded vessel part update (part references not set yet) on {VesselName}");
					return;
				}
			}
			else if (!modulesStarted)
			{
				modulesStarted = true;
				foreach (PartData part in Parts)
				{
					foreach (ModuleData module in part.modules)
					{
						Lib.LogDebug($"Starting {module.GetType().Name} on {VesselName}");
						module.OnStart();
					}
				}
			}
				
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.PartRadiationUpdate");

			for (int i = 0; i < Parts.Count; i++)
			{
				foreach (ModuleData module in Parts[i].modules)
				{
					module.OnFixedUpdate(elapsedSec);
				}

				PartRadiationData radiationData = Parts[i].radiationData;
				radiationData.elapsedSecSinceLastUpdate += elapsedSec;

				if (i == partToUpdate)
				{
					radiationData.Update();

				}
			}

			partToUpdate = (partToUpdate + 1) % Parts.Count;

			UnityEngine.Profiling.Profiler.EndSample();
		}
		
		private void StateUpdate()
        {
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.StateUpdate");
            // determine if there is enough EC for a powered state
            powered = Lib.IsPowered(Vessel, ResHandler.ElectricCharge);

            // calculate crew info for the vessel
            crewCount = Lib.CrewCount(Vessel);
            crewCapacity = Lib.CrewCapacity(Vessel);

            // malfunction stuff
            malfunction = Reliability.HasMalfunction(Vessel);
            critical = Reliability.HasCriticalFailure(Vessel);

            // communications info
            CommHandler.UpdateConnection(connection);

            // check ModuleCommand hibernation
            if (isSerenityGroundController)
                hasNonHibernatingCommandModules = true;

            if (Hibernating != !hasNonHibernatingCommandModules)
            {
                Hibernating = !hasNonHibernatingCommandModules;
                if (!Hibernating)
                    deviceTransmit = true;
            }

            // this flag will be set by the ModuleCommand harmony patches / background update
            hasNonHibernatingCommandModules = false;

            if (Hibernating)
                deviceTransmit = false;

			// data about greenhouses
			greenhouses = Greenhouse.Greenhouses(Vessel);

            DriveData.GetCapacity(this, out drivesFreeSpace, out drivesCapacity);

            // solar panels data
            if (Vessel.loaded)
            {
                solarPanelsAverageExposure = SolarPanelFixer.GetSolarPanelsAverageExposure(solarPanelsExposure);
                solarPanelsExposure.Clear();
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }

        private void EnvironmentUpdate(double elapsedSec)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EnvironmentUpdate");
            isAnalytic = elapsedSec > SubStepSim.interval * 2.0;

			// Those must be evaluated before the Sim / StepSim is evaluated
			Vector3d position = Lib.VesselPosition(Vessel);
			landed = Lib.Landed(Vessel);
			altitude = Vessel.altitude;
			mainBody = Vessel.mainBody;
				
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.ProcessStep");


			int subStepCount = subSteps.Count;
			if (isAnalytic && subStepCount > 0)
			{
				// Reset stars
				for (int i = 0; i < starsIrradiance.Length; i++)
					starsIrradiance[i].Reset();

				double directRawFluxTotal = 0.0;
				bodiesCoreIrradiance = 0.0;

				// take the "average" as the current step
				Step currentStep = subSteps[subStepCount / 2];
				for (int i = 0; i < starsIrradiance.Length; i++)
				{
					StarFlux vesselStarFlux = starsIrradiance[i];
					vesselStarFlux.direction = currentStep.starFluxes[i].direction;
					vesselStarFlux.distance = currentStep.starFluxes[i].distance;
				}

				int starCount = subSteps[0].starFluxes.Length;
				for (int k = 0; k < subStepCount; k++)
				{
					bodiesCoreIrradiance += subSteps[k].bodiesCoreFlux;

					for (int i = 0; i < starCount; i++)
					{
						StarFlux stepStarFlux = subSteps[k].starFluxes[i];
						StarFlux vesselStarFlux = starsIrradiance[i];

						vesselStarFlux.directFlux += stepStarFlux.directFlux;
						vesselStarFlux.directRawFlux += stepStarFlux.directRawFlux;
						vesselStarFlux.bodiesAlbedoFlux += stepStarFlux.bodiesAlbedoFlux;
						vesselStarFlux.bodiesEmissiveFlux += stepStarFlux.bodiesEmissiveFlux;
						directRawFluxTotal += stepStarFlux.directRawFlux;

						if (vesselStarFlux.directFlux > 0.0)
							vesselStarFlux.sunlightFactor += 1.0;
					}

					subSteps[k].ReleaseToWorkerPool();
				}

				subSteps.Clear();

				double subStepCountD = subStepCount;
				bodiesCoreIrradiance /= subStepCountD;
				directStarFluxTotal = 0.0;
				mainStar = starsIrradiance[0];
				albedoFlux = 0.0;
				bodyFlux = 0.0;

				for (int i = 0; i < starsIrradiance.Length; i++)
				{
					StarFlux vesselStarFlux = starsIrradiance[i];
					vesselStarFlux.directRawFluxProportion = vesselStarFlux.directRawFlux / directRawFluxTotal;
					vesselStarFlux.directFlux /= subStepCountD;
					directStarFluxTotal += vesselStarFlux.directFlux;
					vesselStarFlux.directRawFlux /= subStepCountD;
					vesselStarFlux.bodiesAlbedoFlux /= subStepCountD;
					vesselStarFlux.bodiesEmissiveFlux /= subStepCountD;
					vesselStarFlux.sunlightFactor /= subStepCountD;

					albedoFlux += vesselStarFlux.bodiesAlbedoFlux;
					bodyFlux += vesselStarFlux.bodiesEmissiveFlux;

					if (mainStar.directFlux < vesselStarFlux.directFlux)
						mainStar = vesselStarFlux;
				}
			}
			else
			{
				subSteps.Clear();

				simVessel.UpdatePosition(this, position);
				Step step = new Step();
				step.Init(simVessel);
				step.Evaluate();

				bodiesCoreIrradiance = step.bodiesCoreFlux;

				double directRawFluxTotal = 0.0;
				directStarFluxTotal = 0.0;
				mainStar = starsIrradiance[0];
				albedoFlux = 0.0;
				bodyFlux = 0.0;

				for (int i = 0; i < starsIrradiance.Length; i++)
				{
					starsIrradiance[i] = step.starFluxes[i];
					StarFlux starFlux = starsIrradiance[i];

					directStarFluxTotal += starFlux.directFlux;
					directRawFluxTotal += starFlux.directRawFlux;
					albedoFlux += starFlux.bodiesAlbedoFlux;
					bodyFlux += starFlux.bodiesEmissiveFlux;

					starFlux.sunlightFactor = starFlux.directFlux > 0.0 ? 1.0 : 0.0;

					if (mainStar.directFlux < starFlux.directFlux)
						mainStar = starFlux;
				}

				foreach (StarFlux vesselStarFlux in starsIrradiance)
				{
					vesselStarFlux.directRawFluxProportion = vesselStarFlux.directRawFlux / directRawFluxTotal;
				}
			}

			UnityEngine.Profiling.Profiler.EndSample();


			// situation
			underwater = Sim.Underwater(Vessel);
            envStaticPressure = Sim.StaticPressureAtm(Vessel);
            inAtmosphere = Vessel.mainBody.atmosphere && Vessel.altitude < Vessel.mainBody.atmosphereDepth;
            inOxygenAtmosphere = Sim.InBreathableAtmosphere(Vessel, inAtmosphere, underwater);
            inBreathableAtmosphere = inOxygenAtmosphere && envStaticPressure > Settings.PressureThreshold;

            zeroG = !EnvLanded && !inAtmosphere;

			visibleBodies = Sim.GetLargeBodies(position).ToArray();
            sunBodyAngle = Sim.SunBodyAngle(Vessel, position, mainStar.Star.body);

            // temperature at vessel position
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EnvTemperature");
            temperature = Sim.Temperature(bodiesCoreIrradiance, starsIrradiance);
            tempDiff = Sim.TempDiff(temperature, Vessel.mainBody, EnvLanded);
            UnityEngine.Profiling.Profiler.EndSample();

            // radiation
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EnvRadiation");
            gammaTransparency = Sim.GammaTransparency(Vessel.mainBody, Vessel.altitude);

            bool new_innerBelt, new_outerBelt, new_magnetosphere;
            radiation = Radiation.Compute(Vessel, position, EnvGammaTransparency, mainStar.sunlightFactor, out blackout, out new_magnetosphere, out new_innerBelt, out new_outerBelt, out interstellar);

            if (new_innerBelt != innerBelt || new_outerBelt != outerBelt || new_magnetosphere != magnetosphere)
            {
                innerBelt = new_innerBelt;
                outerBelt = new_outerBelt;
                magnetosphere = new_magnetosphere;
                if (IsSimulated) API.OnRadiationFieldChanged.Notify(Vessel, innerBelt, outerBelt, magnetosphere);
            }
            

            thermosphere = Sim.InsideThermosphere(Vessel);
            exosphere = Sim.InsideExosphere(Vessel);
            if (Storm.InProgress(Vessel))
			{
				double sunActivity = Radiation.Info(mainStar.Star.body).SolarActivity(false) / 2.0;
				stormRadiation = PreferencesRadiation.Instance.StormRadiation * mainStar.sunlightFactor * (sunActivity + 0.5);
			}
			else
			{
				stormRadiation = 0.0;
			}

			UnityEngine.Profiling.Profiler.EndSample();

			vesselSituations.Update();

            // other stuff
            gravioli = Sim.Graviolis(Vessel);
            UnityEngine.Profiling.Profiler.EndSample();
        }

		#endregion

		#region EVENTS

		private static List<PartData> transferredParts = new List<PartData>();

		/// <summary>
		/// Called from Callbacks, just after a part has been decoupled (undocking) or detached (usually a joint failure)
		/// At this point, the new Vessel object has been created by KSP and should be fully initialized.
		/// </summary>
		public static void OnDecoupleOrUndock(Vessel oldVessel, Vessel newVessel)
		{
			Lib.LogDebug("Decoupling vessel '{0}' from vessel '{1}'", Lib.LogLevel.Message, newVessel.vesselName, oldVessel.vesselName);

			if (!oldVessel.TryGetVesselDataTemp(out VesselData oldVD))
				return;

			if (newVessel.TryGetVesselData(out VesselData newVD))
			{
				Lib.LogDebugStack($"Decoupled/Undocked vessel {newVessel.vesselName} exists already, can't transfer partdatas !", Lib.LogLevel.Error);
				return;
			}

			transferredParts.Clear();
			foreach (Part part in newVessel.Parts)
			{
				// for all parts in the new vessel, move the corresponding partdata from the old vessel to the new vessel
				if (oldVD.VesselParts.TryGet(part.flightID, out PartData pd))
				{
					transferredParts.Add(pd);
					oldVD.VesselParts.Remove(pd);
				}
			}

			oldVD.OnVesselWasModified();

			newVD = new VesselData(newVessel, transferredParts);
			transferredParts.Clear();

			DB.AddNewVesselData(newVD);

			Lib.LogDebug("Decoupling complete for new vessel, vd.partcount={1}, v.partcount={2} ({0})", Lib.LogLevel.Message, newVessel.vesselName, newVD.Parts.Count, newVessel.parts.Count);
			Lib.LogDebug("Decoupling complete for old vessel, vd.partcount={1}, v.partcount={2} ({0})", Lib.LogLevel.Message, oldVessel.vesselName, oldVD.Parts.Count, oldVessel.parts.Count);
		}

		/// <summary>
		/// Called from Callbacks, just after a part has been coupled (docking, KIS attached part...)
		/// </summary>
		public static void OnPartCouple(GameEvents.FromToAction<Part, Part> data)
		{
			Lib.LogDebug("Coupling part '{0}' from vessel '{1}' to vessel '{2}'", Lib.LogLevel.Message, data.from.partInfo.title, data.from.vessel.vesselName, data.to.vessel.vesselName);

			Vessel fromVessel = data.from.vessel;
			Vessel toVessel = data.to.vessel;

			fromVessel.TryGetVesselDataTemp(out VesselData fromVD);
			toVessel.TryGetVesselDataTemp(out VesselData toVD);

			// GameEvents.onPartCouple may be fired by mods (KIS) that add new parts to an existing vessel
			// In the case of KIS, the part vessel is already set to the destination vessel when the event is fired
			// so we just add the part.
			if (fromVessel == toVessel)
			{
				if (!toVD.VesselParts.Contains(data.from.flightID))
				{
					toVD.VesselParts.Add(data.from);
					Lib.LogDebug("VesselData : newly created part '{0}' added to vessel '{1}'", Lib.LogLevel.Message, data.from.partInfo.title, data.to.vessel.vesselName);
				}
				return;
			}

			// transfer all partdata of the docking vessel to the docked to vessel
			toVD.VesselParts.TransferFrom(fromVD.VesselParts);

			// reset a few things on the docked to vessel
			toVD.scansat_id.Clear();
			toVD.OnVesselWasModified();

			Lib.LogDebug("Coupling complete to   vessel, vd.partcount={1}, v.partcount={2} ({0})", Lib.LogLevel.Message, toVessel.vesselName, toVD.Parts.Count, toVessel.parts.Count);
			Lib.LogDebug("Coupling complete from vessel, vd.partcount={1}, v.partcount={2} ({0})", Lib.LogLevel.Message, fromVessel.vesselName, fromVD.Parts.Count, fromVessel.parts.Count);
		}

		/// <summary>
		/// Called from Callbacks, just after a part has been coupled (docking, KIS attached part...)
		/// </summary>
		public static void OnPartWillDie(Part part)
		{
			if (!part.vessel.TryGetVesselDataTemp(out VesselData vd))
				return;

			vd.OnPartWillDie(part.flightID);

			vd.OnVesselWasModified();
			Lib.LogDebug("Removing dead part, vd.partcount={0}, v.partcount={1} (part '{2}' in vessel '{3}')", Lib.LogLevel.Message, vd.Parts.Count, part.vessel.parts.Count, part.partInfo.title, part.vessel.vesselName);
		}

		private void OnPartWillDie(uint flightId)
		{
			VesselParts[flightId].PartWillDie();
			VesselParts.Remove(flightId);
			OnVesselWasModified();
		}

		// note : we currently have no way of detecting 100% of cases 
		// where an unloaded vessel is destroyed,
		public void OnVesselWillDie()
		{
			resourceUpdateDelegates = null;
			VesselParts.OnAllPartsWillDie();
			CommHandler.ResetPartTransmitters();
		}

		public void OnVesselWasModified()
		{
			if (!IsSimulated)
				return;

			resourceUpdateDelegates = null;
			CommHandler.ResetPartTransmitters();
			ResetReliabilityStatus();
			StateUpdate(); // TODO : wthis isn't good, we need to decouple "pure update" code from the "vessel state analysis" code

			Lib.LogDebug("VesselData updated on vessel modified event ({0})", Lib.LogLevel.Message, Vessel.vesselName);
		}

		#endregion
	}
} // KERBALISM
