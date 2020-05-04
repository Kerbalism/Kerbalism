using System;
using System.Collections.Generic;
using System.Linq;

namespace KERBALISM
{
	public class CommHandlerEditorCommNet : CommHandlerEditor
	{
		private List<ModuleDataTransmitter> loadedTransmitters = new List<ModuleDataTransmitter>();

		public override void UpdateConnection(ConnectionInfoEditor connection)
		{
			loadedTransmitters.Clear();
			GetTransmittersLoaded();

			float rangeModifier = HighLogic.CurrentGame.Parameters.CustomParams<CommNet.CommNetParams>().rangeModifier;

			connection.baseRate = 1.0;
			connection.ec_idle = 0.0;
			connection.ec = 0.0;
			int transmitterCount = 0;
			double strongestPower = 0.0;
			double strongestConbinablePower = 0.0;
			double allCombinablePower = 0.0;
			double x = 0.0;
			double y = 0.0;
			double averageWeightedCombinabilityExponent;

			foreach (ModuleDataTransmitter mdt in loadedTransmitters)
			{
				// CanComm method : check if module has moduleIsEnabled = false or is broken or not deployed
				if (!mdt.isEnabled || !mdt.CanComm())
					continue;

				// do not include internal data rate, ec cost only
				if (mdt.antennaType == AntennaType.INTERNAL)
				{
					connection.ec_idle += mdt.DataResourceCost * mdt.DataRate;
				}
				else
				{
					connection.baseRate *= mdt.DataRate;
					connection.ec += mdt.DataResourceCost * mdt.DataRate;
					transmitterCount++;
				}

				if (mdt.antennaPower > strongestPower)
					strongestPower = mdt.antennaPower;

				if (mdt.antennaCombinable)
				{
					double antennaPower = mdt.antennaPower * rangeModifier;

					x += antennaPower * mdt.antennaCombinableExponent;
					y += antennaPower;

					allCombinablePower += antennaPower;
					if (antennaPower > strongestConbinablePower)
					{
						strongestConbinablePower = antennaPower;
					}
				}
			}

			if (loadedTransmitters.Count == 1)
				averageWeightedCombinabilityExponent = loadedTransmitters[0].antennaCombinableExponent;
			else
				averageWeightedCombinabilityExponent = x / y;

			double combinablePower = 0.0;
			if (strongestConbinablePower > 0.0)
				combinablePower = strongestConbinablePower * Math.Pow(allCombinablePower / strongestConbinablePower, averageWeightedCombinabilityExponent);

			connection.basePower = Math.Max(combinablePower, strongestPower);

			double dsnPower = GameVariables.Instance.GetDSNRange(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation));
			dsnPower *= HighLogic.CurrentGame.Parameters.CustomParams<CommNet.CommNetParams>().DSNModifier;

			connection.maxRange = Math.Sqrt(connection.basePower * dsnPower);

			connection.minDistanceStrength = Sim.SignalStrength(connection.maxRange, connection.minDsnDistance);
			connection.maxDistanceStrength = Sim.SignalStrength(connection.maxRange, connection.maxDsnDistance);

			if (transmitterCount > 1)
				connection.baseRate = Math.Pow(connection.baseRate, 1.0 / transmitterCount);
			else if (transmitterCount == 0)
				connection.baseRate = 0.0;

			connection.minDistanceRate = connection.baseRate * Math.Pow(connection.minDistanceStrength, Sim.DataRateDampingExponent);
			connection.maxDistanceRate = connection.baseRate * Math.Pow(connection.maxDistanceStrength, Sim.DataRateDampingExponent);

			// set minimal data rate to what is defined in Settings (1 bit/s by default) 
			if (connection.minDistanceRate > 0.0 && connection.minDistanceRate * Lib.bitsPerMB < Settings.DataRateMinimumBitsPerSecond)
				connection.minDistanceRate = Settings.DataRateMinimumBitsPerSecond / Lib.bitsPerMB;

			// set minimal data rate to what is defined in Settings (1 bit/s by default) 
			if (connection.maxDistanceRate > 0.0 && connection.maxDistanceRate * Lib.bitsPerMB < Settings.DataRateMinimumBitsPerSecond)
				connection.maxDistanceRate = Settings.DataRateMinimumBitsPerSecond / Lib.bitsPerMB;

			// when transmitting, transmitters need more EC for the signal amplifiers.
			// while not transmitting, transmitters only use 10-20% of that
			// Note : ec_idle is substracted from ec before consumption in Science.Update().
			// Didn't change that as this is what is expected by the RealAntenna API handler
			connection.ec_idle *= Settings.TransmitterPassiveEcFactor; // apply passive factor to "internal" antennas always-consumed rate
			connection.ec_idle += connection.ec * Settings.TransmitterPassiveEcFactor; // add "transmit" antennas always-consumed rate
			connection.ec *= Settings.TransmitterActiveEcFactor; // adjust "transmit" antennas transmit-only rate by the factor

		}

		private void GetTransmittersLoaded()
		{
			foreach (Part p in EditorLogic.fetch.ship.parts)
			{
				foreach (PartModule pm in p.Modules)
				{
					if (pm is ModuleDataTransmitter mdt)
					{
						// Disable all stock buttons
						mdt.Events["TransmitIncompleteToggle"].active = false;
						mdt.Events["StartTransmission"].active = false;
						mdt.Events["StopTransmission"].active = false;
						mdt.Actions["StartTransmissionAction"].active = false;

						loadedTransmitters.Add(mdt);
					}
				}
			}
		}
	}
}
