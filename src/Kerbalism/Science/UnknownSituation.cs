namespace KERBALISM
{
	/// <summary>
	/// Situation for subjects whose body token is not a real CelestialBody
	/// (DMOS asteroid science: "AsteroidSrfLandedCarbonaceous1234567", #885).
	/// </summary>
	public sealed class UnknownSituation : Situation
	{
		/// <summary> Reserved body index so unknown subjects do not collide with FlightGlobals bodies. </summary>
		public const int UnknownBodyIndex = ushort.MaxValue;

		private readonly string unknownBodyName;
		private readonly string unknownBiomeName;

		public UnknownSituation(string bodyName, ScienceSituation situation, string biomeName = null)
			: base(situation, FieldsToId(UnknownBodyIndex, situation, -1), null)
		{
			unknownBodyName = bodyName ?? string.Empty;
			unknownBiomeName = biomeName ?? string.Empty;
		}

		public override string BodyTitle => unknownBodyName;

		public override string BodyName => unknownBodyName;

		public override string BiomeTitle => unknownBiomeName;

		public override string BiomeName =>
			string.IsNullOrEmpty(unknownBiomeName)
				? string.Empty
				: unknownBiomeName.Replace(" ", string.Empty);

		public override double SituationMultiplier => 1.0;

		public override string GetTitleForExperiment(ExperimentInfo expInfo)
		{
			if (!string.IsNullOrEmpty(unknownBiomeName))
				return Lib.BuildString(BodyTitle, " ", ScienceSituationTitle, " ", BiomeTitle);
			return Lib.BuildString(BodyTitle, " ", ScienceSituationTitle);
		}

		public override string GetStockIdForExperiment(ExperimentInfo expInfo)
		{
			return Lib.BuildString(BodyName, StockScienceSituationName, BiomeName);
		}
	}
}
