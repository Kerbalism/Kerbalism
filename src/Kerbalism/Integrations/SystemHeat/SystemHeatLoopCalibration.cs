using System.Globalization;

namespace KERBALISM
{
	/// <summary>
	/// Per-loop calibration of the background SystemHeat reconstruction, captured on the first
	/// background step after a vessel is unloaded and persisted in VesselData.
	///
	/// residualKw = (net loop flux SystemHeat persisted at unload) - (net flux reconstructed from proto at
	/// the same temperature). Everything the proto reconstruction can't see (unpatched radiators, atmosphere
	/// convection, third-party heat modules...) ends up here, so the first background step reproduces the
	/// live balance exactly and later steps only react to modeled changes (automation, failures).
	/// </summary>
	public sealed class SystemHeatLoopCalibration
	{
		/// <summary>Observed minus reconstructed net loop flux, kW. Negative = unmodeled cooling.</summary>
		public float residualKw;

		/// <summary>Loop temperature the residual was measured at, K. Unmodeled cooling scales with T / reference.</summary>
		public float referenceTemperatureK;

		public SystemHeatLoopCalibration() { }

		public SystemHeatLoopCalibration(float residualKw, float referenceTemperatureK)
		{
			this.residualKw = residualKw;
			this.referenceTemperatureK = referenceTemperatureK;
		}

		public string Serialize()
		{
			return residualKw.ToString("R", CultureInfo.InvariantCulture)
				+ ","
				+ referenceTemperatureK.ToString("R", CultureInfo.InvariantCulture);
		}

		public static bool TryDeserialize(string value, out SystemHeatLoopCalibration calibration)
		{
			calibration = null;
			if (string.IsNullOrEmpty(value))
				return false;

			string[] parts = value.Split(',');
			if (parts.Length != 2
				|| !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float residual)
				|| !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float reference)
				|| float.IsNaN(residual) || float.IsInfinity(residual)
				|| float.IsNaN(reference) || float.IsInfinity(reference))
				return false;

			calibration = new SystemHeatLoopCalibration(residual, reference);
			return true;
		}
	}
}
