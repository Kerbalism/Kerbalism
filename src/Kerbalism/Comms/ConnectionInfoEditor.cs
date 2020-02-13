using CommNet;
using KERBALISM.Planner;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ConnectionInfoEditor : ConnectionInfo
	{
		public ConnectionInfoEditor(List<Part> parts, EnvironmentAnalyzer environment)
		{
			rate = 1.0;
			int transmitterCount = 0;
			double ec_transmitter = 0;

			foreach (Part p in parts)
			{
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled)
						continue;

					// RemoteTech enabled, passive's don't count
					if (m.moduleName == "ModuleRTAntenna")
					{
						linked = true;
						ec = m.resHandler.inputResources.Find(r => r.name == "ElectricCharge").rate;
					}
					else if (m is ModuleDataTransmitter mdt)
					{
						if (!mdt.CanComm())
							continue;

						// do not include internal data rate, ec cost only
						if (mdt.antennaType == AntennaType.INTERNAL)
						{
							ec += mdt.DataResourceCost * mdt.DataRate;
						}
						else
						{
							rate *= mdt.DataRate;
							transmitterCount++;
							ec_transmitter += mdt.DataResourceCost * mdt.DataRate;
						}
					}
				}
			}

			if (transmitterCount > 1)
				rate = Math.Pow(rate, 1.0 / transmitterCount);
			else if (transmitterCount == 0)
				rate = 0;

			ec = ec_transmitter * Settings.TransmitterActiveEcFactor * Settings.TransmitterActiveEcFactor;
			ec_idle = ec_transmitter * Settings.TransmitterPassiveEcFactor * Settings.TransmitterPassiveEcFactor;

		}
	}
}
