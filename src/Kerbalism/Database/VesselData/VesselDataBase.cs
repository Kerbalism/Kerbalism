using Flee.PublicTypes;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	// note : this class should ideally be abstract, but we need 
	// an instance of it to compile the Flee modifiers.
	public partial class VesselDataBase
	{
		public const string NODENAME_VESSEL = "KERBALISM_VESSEL";
		public const string NODENAME_MODULE = "MODULES";

		#region BASE FIELDS/PROPERTIES

		public ExpressionContext ModifierContext { get; private set; }

		public VesselProcessCollection VesselProcesses => vesselProcesses; VesselProcessCollection vesselProcesses;

		/// <summary>habitat info</summary>
		public HabitatVesselData Habitat => habitatData; HabitatVesselData habitatData;

		

		// the following is to provide Flee modifiers access, it isn't functionally needed otherwise
		private PreferencesReliability PrefReliability => PreferencesReliability.Instance;
		private PreferencesScience PrefScience => PreferencesScience.Instance;
		private PreferencesComfort PrefComfort => PreferencesComfort.Instance;
		private PreferencesRadiation PrefRadiation => PreferencesRadiation.Instance;

		#endregion

		#region VIRTUAL PROPERTIES

		public virtual bool LoadedOrEditor => true;

		public virtual bool IsEVA => false;

		public virtual string VesselName => string.Empty;

		public virtual bool IsPersistent => true;

		public virtual PartDataCollectionBase Parts { get; }

		public virtual VesselResHandler ResHandler { get; }

		public virtual IConnectionInfo ConnectionInfo { get; }

		public virtual CelestialBody MainBody { get; }

		public virtual double Altitude { get; }

		public virtual double Latitude { get; }

		public virtual double Longitude { get; }

		/// <summary>number of crew on the vessel</summary>
		public virtual int CrewCount { get; }

		/// <summary>crew capacity of the vessel</summary>
		public virtual int CrewCapacity { get; }

		/// <summary> [environment] true if inside ocean</summary>
		public virtual bool EnvUnderwater { get; }

		/// <summary> [environment] true if on the surface of a body</summary>
		public virtual bool EnvLanded { get; }

		/// <summary> current atmospheric pressure in atm</summary>
		public virtual double EnvStaticPressure { get; }

		/// <summary> Is the vessel inside an atmosphere ?</summary>
		public virtual bool EnvInAtmosphere { get; }

		/// <summary> Is the vessel inside a breatheable atmosphere ?</summary>
		public virtual bool EnvInOxygenAtmosphere { get; }

		/// <summary> Is the vessel inside a breatheable atmosphere and at acceptable pressure conditions ?</summary>
		public virtual bool EnvInBreathableAtmosphere { get; }

		/// <summary> [environment] true if in zero g</summary>
		public virtual bool EnvZeroG { get; }

		/// <summary> [environment] temperature ar vessel position</summary>
		public virtual double EnvTemperature  { get; }

		/// <summary> [environment] difference between environment temperature and survival temperature</summary>// 
		public virtual double EnvTempDiff  { get; }

		/// <summary> [environment] radiation at vessel position</summary>
		public virtual double EnvRadiation  { get; }

		/// <summary> [environment] radiation effective for habitats/EVAs</summary>
		public virtual double EnvHabitatRadiation  { get; }

		public bool EnvStorm => EnvStormRadiation > 0.0;

		public virtual double EnvStormRadiation { get; }

		/// <summary> [environment] proportion of ionizing radiation not blocked by atmosphere</summary>
		public virtual double EnvGammaTransparency  { get; }

		/// <summary> total irradiance from all sources (W/m²) at vessel position</summary>
		public double IrradianceTotal => irradianceTotal; protected double irradianceTotal;

		/// <summary> star(s) irradiance (W/m²) reflected by the nearest body (and it's parent planet if it's a moon)</summary>
		public double IrradianceAlbedo => irradianceAlbedo; protected double irradianceAlbedo;

		/// <summary> thermal irradiance (W/m²) from the nearest body (and it's parent planet if it's a moon), induced by the star(s) heating effect on the body </summary>
		public double IrradianceBodiesEmissive => irradianceBodiesEmissive; protected double irradianceBodiesEmissive;

		/// <summary> thermal irradiance (W/m²) from the nearest body (and it's parent planet if it's a moon), induced by the body own intrinsic sources </summary>
		public double IrradianceBodiesCore => irradianceBodiesCore; protected double irradianceBodiesCore;

		/// <summary> direct star(s) irradiance (W/m²) from all stars at vessel position, include atmospheric absorption if inside an atmosphere </summary>
		public double IrradianceStarTotal => irradianceStarTotal; protected double irradianceStarTotal;

		/// <summary> List of all stars/suns and the related data/calculations for the current vessel</summary>
		public StarFlux[] StarsIrradiance => starsIrradiance; protected StarFlux[] starsIrradiance;

		/// <summary> Star that send the highest nominal flux (in W/m²) at the vessel position (ignoring occlusion / atmo absorbtion)</summary>
		public StarFlux MainStar => mainStar; protected StarFlux mainStar;

		/// <summary> Nomalized direction vector to the main star</summary>
		public Vector3d MainStarDirection => MainStar.direction;

		/// <summary> % of time spent in the main star direct light (for the current environment update)</summary>
		public double MainStarSunlightFactor => MainStar.sunlightFactor;

		/// <summary> True if at least half of the current update was spent in the direct light of the main star</summary>
		public bool InSunlight => MainStar.sunlightFactor > 0.45;

		/// <summary> True if less than 10% of the current update was spent in the direct light of the main star</summary>
		public bool InFullShadow => MainStar.sunlightFactor < 0.1;

		#endregion

		#region LIFECYCLE

		public VesselDataBase()
		{
			ModifierContext = new ExpressionContext(this);
			ModifierContext.Options.CaseSensitive = true;
			ModifierContext.Options.ParseCulture = System.Globalization.CultureInfo.InvariantCulture;
			ModifierContext.Imports.AddType(typeof(Math));

			vesselProcesses = new VesselProcessCollection();
			habitatData = new HabitatVesselData();
			starsIrradiance = StarFlux.StarArrayFactory();
		}

		// put here the persistence that is common to VesselData and VesselDataShip to have
		// it transfered when creating a vessel from a shipconstruct (ie, from editor to flight)
		public void Load(ConfigNode vesselDataNode, bool isNewVessel)
		{
			VesselProcesses.Load(vesselDataNode);

			if (!isNewVessel)
			{
				OnLoad(vesselDataNode);
			}
		}

		public void Save(ConfigNode node)
		{
			if (!IsPersistent)
				return;

			ConfigNode vesselNode = new ConfigNode(NODENAME_VESSEL);
			OnSave(vesselNode);
			VesselProcesses.Save(vesselNode);
			Parts.Save(vesselNode);
			ModuleData.SaveModuleDatas(Parts, vesselNode);
			node.AddNode(vesselNode);
		}

		// This is overridden in VesselData for vessel <--> vessel persistence.
		// It can't be used in VesselDataShip, as we don't call it when instantiating
		// a vessel from a shipconstruct
		protected virtual void OnLoad(ConfigNode node) { }
		protected virtual void OnSave(ConfigNode node) { }

		// LoadShipConstruct is a constructor for VesselDataShip, and is responsible for
		// instantiating the PartData/ModuleData objects. This differs a lot from the flight
		// VesselData objects instantiation / loading, so while the data structure is the same
		// the handling is completely different. Ideally, we should use common methods but the
		// hacky nature of forcing our data into the stock ShipConstruct persistence, as well
		// as the difficulty of keeping our editor data synchronized severly limit the options.
		private static Dictionary<int, ConfigNode> moduleDataNodes = new Dictionary<int, ConfigNode>();
		public static void LoadShipConstruct(ShipConstruct ship, ConfigNode vesselDataNode, bool isNewShip)
		{
			Lib.LogDebug($"Loading VesselDataShip for shipconstruct {ship.shipName}");
			moduleDataNodes.Clear();

			List<PartData> thisShipParts = new List<PartData>(ship.parts.Count);

			if (vesselDataNode != null)
			{
				// we don't want to overwrite VesselData when loading a subassembly or when merging.
				if (isNewShip)
					VesselDataShip.Instance = new VesselDataShip();

				// we need to instantiate the PartDatas before Load() is called
				foreach (Part part in ship.parts)
				{
					PartData partData = new PartData(VesselDataShip.Instance, part);
					VesselDataShip.ShipParts.Add(partData);
					thisShipParts.Add(partData);
				}

				VesselDataShip.Instance.Parts.Load(vesselDataNode);

				if (isNewShip)
					VesselDataShip.Instance.Load(vesselDataNode, false);

				// populate the dictionary of ModuleData nodes to load, to avoid doing a full loop
				// on every node for each ModuleData
				foreach (ConfigNode moduleNode in vesselDataNode.GetNode(NODENAME_MODULE).GetNodes())
				{
					int shipId = Lib.ConfigValue(moduleNode, ModuleData.VALUENAME_SHIPID, 0);
					if (shipId != 0)
						moduleDataNodes.Add(shipId, moduleNode);
				}
			}
			else
			{
				foreach (Part part in ship.parts)
				{
					PartData partData = new PartData(VesselDataShip.Instance, part);
					VesselDataShip.ShipParts.Add(partData);
					thisShipParts.Add(partData);
				}
			}

			// instantiate all ModuleData for the ship, loading ModuleData if available.
			foreach (PartData partData in thisShipParts)
			{
				for (int i = 0; i < partData.LoadedPart.Modules.Count; i++)
				{
					if (partData.LoadedPart.Modules[i] is KsmPartModule ksmPM)
					{
						if ( moduleDataNodes.TryGetValue(ksmPM.dataShipId, out ConfigNode moduleNode))
						{
							ModuleData.NewFromNode(ksmPM, i, partData, moduleNode);
						}
						else
						{
							ModuleData.New(ksmPM, i, partData, false);
						}
					}
				}
			}
		}

		#endregion

		#region EVALUATION

		public void ModuleDataUpdate()
		{
			habitatData.ResetBeforeModulesUpdate(this);

			foreach (PartData partData in Parts)
			{
				foreach (ModuleData moduleData in partData.modules)
				{
					moduleData.VesselDataUpdate();
				}
			}

			habitatData.EvaluateAfterModuleUpdate(this);
			vesselProcesses.EvaluateAfterModuleUpdate(this);
		}

		public void ProcessSimStep(SimStep step)
		{
			irradianceBodiesCore = step.bodiesCoreIrradiance;

			double directRawFluxTotal = 0.0;
			irradianceStarTotal = 0.0;
			mainStar = starsIrradiance[0];
			irradianceAlbedo = 0.0;
			irradianceBodiesEmissive = 0.0;

			for (int i = 0; i < starsIrradiance.Length; i++)
			{
				starsIrradiance[i] = step.starFluxes[i];
				StarFlux starFlux = starsIrradiance[i];

				irradianceStarTotal += starFlux.directFlux;
				directRawFluxTotal += starFlux.directRawFlux;
				irradianceAlbedo += starFlux.bodiesAlbedoFlux;
				irradianceBodiesEmissive += starFlux.bodiesEmissiveFlux;

				starFlux.sunlightFactor = starFlux.directFlux > 0.0 ? 1.0 : 0.0;

				if (mainStar.directFlux < starFlux.directFlux)
					mainStar = starFlux;
			}

			foreach (StarFlux vesselStarFlux in starsIrradiance)
			{
				vesselStarFlux.directRawFluxProportion = vesselStarFlux.directRawFlux / directRawFluxTotal;
			}

			irradianceTotal = irradianceStarTotal + irradianceAlbedo + irradianceBodiesEmissive + irradianceBodiesCore;
		}

		#endregion
	}
}
