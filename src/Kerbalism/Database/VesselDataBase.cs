using Flee.PublicTypes;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	// note : this class should ideally be abstract, but we need 
	// an instance of it to compile the Flee modifiers.
	public class VesselDataBase
	{
		public const string NODENAME_VESSEL = "KERBALISMVESSEL";
		public const string NODENAME_MODULE = "KERBALISMMODULE";

		public ExpressionContext ModifierContext { get; private set; }

		public virtual bool IsPersistent => true;

		public virtual IEnumerable<PartData> PartList { get; }

		public virtual VesselResHandler ResHandler { get; }

		/// <summary>habitat info</summary>
		public virtual HabitatVesselData HabitatInfo { get; }

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

		/// <summary> [environment] solar flux reflected from the nearest body</summary>
		public virtual double EnvAlbedoFlux { get; }

		/// <summary> [environment] infrared radiative flux from the nearest body</summary>
		public virtual double EnvBodyFlux  { get; }

		/// <summary> [environment] total flux at vessel position</summary>
		public virtual double EnvTotalFlux  { get; }

		/// <summary> [environment] temperature ar vessel position</summary>
		public virtual double EnvTemperature  { get; }

		/// <summary> [environment] difference between environment temperature and survival temperature</summary>// 
		public virtual double EnvTempDiff  { get; }

		/// <summary> [environment] radiation at vessel position</summary>
		public virtual double EnvRadiation  { get; }

		/// <summary> [environment] radiation effective for habitats/EVAs</summary>
		public virtual double EnvHabitatRadiation  { get; }

		/// <summary> [environment] proportion of ionizing radiation not blocked by atmosphere</summary>
		public virtual double EnvGammaTransparency  { get; }
		/// <summary>
		///  [environment] total solar flux from all stars at vessel position in W/m², include atmospheric absorption if inside an atmosphere (atmo_factor)
		/// <para/> zero when the vessel is in shadow while evaluation is non-analytic (low timewarp rates)
		/// <para/> in analytic evaluation, this include fractional sunlight factor
		/// </summary>
		public virtual double EnvSolarFluxTotal  { get; }

		/// <summary> [environment] Average time spend in sunlight, including sunlight from all suns/stars. Each sun/star influence is pondered by its flux intensity</summary>
		public virtual double EnvSunlightFactor  { get; }

		/// <summary> [environment] true if the vessel is currently in sunlight, or at least half the time when in analytic mode</summary>
		public virtual bool EnvInSunlight  { get; }

		/// <summary> [environment] true if the vessel is currently in shadow, or least 90% of the time when in analytic mode</summary>
		// this threshold is also used to ignore light coming from distant/weak stars 
		public virtual bool EnvInFullShadow  { get; }

		private PreferencesReliability PrefReliability => PreferencesReliability.Instance;
		private PreferencesScience PrefScience => PreferencesScience.Instance;
		private PreferencesComfort PrefComfort => PreferencesComfort.Instance;
		private PreferencesRadiation PrefRadiation => PreferencesRadiation.Instance;

		public VesselDataBase()
		{
			ModifierContext = new ExpressionContext(this);
			ModifierContext.Options.CaseSensitive = true;
			ModifierContext.Options.ParseCulture = System.Globalization.CultureInfo.InvariantCulture;
			ModifierContext.Imports.AddType(typeof(Math));
		}

		// put here the persistence that is common to VesselData and VesselDataShip to have
		// it transfered when creating a vessel from a shipconstruct (ie, from editor to flight)
		public void Load(ConfigNode vesselDataNode, bool isNewVessel)
		{
			VesselVirtualResource.Load(this, vesselDataNode);

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
			VesselVirtualResource.Save(this, vesselNode);
			ModuleData.SaveModuleDatas(PartList, vesselNode);
			node.AddNode(vesselNode);
		}

		// This is overridden in VesselData for vessel <> vessel persistence.
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
			if (vesselDataNode != null)
			{
				// we don't want to overwrite VesselData when loading a subassembly or when merging.
				if (isNewShip)
				{
					VesselDataShip.Instance = new VesselDataShip();
					VesselDataShip.Instance.Load(vesselDataNode, false);
				}

				// populate the dictionary of ModuleData nodes to load, to avoid doing a full loop
				// on every node for each ModuleData
				foreach (ConfigNode moduleNode in vesselDataNode.GetNode(NODENAME_MODULE).GetNodes())
				{
					int shipId = Lib.ConfigValue(moduleNode, ModuleData.VALUENAME_SHIPID, 0);
					if (shipId != 0)
						moduleDataNodes.Add(shipId, moduleNode);
				}
			}

			// instantiate all PartData/ModuleData for the ship, loading ModuleData if available.
			foreach (Part part in ship.parts)
			{
				PartData partData = new PartData(part);
				VesselDataShip.LoadedParts.Add(partData);

				for (int i = 0; i < part.Modules.Count; i++)
				{
					if (part.Modules[i] is KsmPartModule ksmPM)
					{
						if ( moduleDataNodes.TryGetValue(ksmPM.dataShipId, out ConfigNode moduleNode))
						{
							ModuleData.NewFromNode(ksmPM, partData, moduleNode);
						}
						else
						{
							ModuleData.New(ksmPM, partData, false);
						}
					}
				}
			}
		}
	}
}
