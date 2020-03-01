using System.Collections.Generic;

namespace KERBALISM
{

	public sealed class ConnectionInfoEditor
	{
		// ====================================================================
		// VALUES TO SET BY KERBALISM (API usage)
		// ====================================================================

		/// <summary> min distance between the vessel and the DSN </summary>
		public double minDsnDistance = 0.0;

		/// <summary> max distance between the vessel and the DSN </summary>
		public double maxDsnDistance = 0.0;

		/// <summary> DSN level </summary>
		public int dsnLevel = 0;

		// ====================================================================
		// VALUES TO SET FOR KERBALISM (API usage)
		// ====================================================================

		/// <summary>
		/// ec cost while transmitting at baseRate
		/// <para/> Note: ec_idle is substracted from ec in Science.Update(), it's silly but don't change it as this is what is expected from the RealAntenna API handler
		/// </summary>
		public double ec = 0.0;

		/// <summary> ec cost while not transmitting </summary>
		public double ec_idle = 0.0;

		/// <summary> maximum data rate </summary>
		public double baseRate = 0.0;

		/// <summary> nominal power (exprimed as a range in meters)</summary>
		public double basePower = 0.0;

		/// <summary> max link distance at the provided dsnLevel</summary>
		public double maxRange = 0.0;

		/// <summary> link quality at minDsnDistance, any value from 0-1</summary>
		public double minDistanceStrength = 0.0;

		/// <summary> link quality at maxDsnDistance, any value from 0-1</summary>
		public double maxDistanceStrength = 0.0;

		/// <summary> science data rate at minDsnDistance, in MB/s</summary>
		public double minDistanceRate = 0.0;

		/// <summary> science data rate at maxDsnDistance, in MB/s</summary>
		public double maxDistanceRate = 0.0;
	}
}
