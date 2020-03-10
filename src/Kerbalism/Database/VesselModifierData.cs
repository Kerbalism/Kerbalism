using Flee.PublicTypes;
using System;

namespace KERBALISM
{
	public class VesselModifierData
	{
		public ExpressionContext ModifierContext { get; private set; }

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

		public VesselModifierData()
		{
			ModifierContext = new ExpressionContext(this);
			ModifierContext.Options.CaseSensitive = true;
			ModifierContext.Options.ParseCulture = System.Globalization.CultureInfo.InvariantCulture;
			ModifierContext.Imports.AddType(typeof(Math));
		}
	}
}
