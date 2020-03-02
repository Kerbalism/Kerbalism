using System;

namespace KERBALISM
{
	public class CommHandlerEditorRemoteTech : CommHandlerEditor
	{
		public override void UpdateConnection(ConnectionInfoEditor connection)
		{
			connection.baseRate = 1.0;
			connection.ec = 0.0;
			connection.ec_idle = 0.0;
			int transmitterCount = 0;

			foreach (Part p in EditorLogic.fetch.ship.parts)
			{
				foreach (PartModule pm in p.Modules)
				{
					if (pm.moduleName == "ModuleRTAntennaPassive")
					{
						connection.ec_idle += 0.0005;
					}
					else if (pm.moduleName == "ModuleRTAntenna")
					{
						pm.Events["EventTransmit"].active = false;
						pm.resHandler.inputResources.Clear(); // we handle consumption by ourselves

						if (Lib.ReflectionValue<bool>(pm, "IsRTActive"))
						{
							connection.baseRate *= (Lib.ReflectionValue<float>(pm, "RTPacketSize") / Lib.ReflectionValue<float>(pm, "RTPacketInterval"));
							connection.ec += RemoteTech.GetModuleRTAntennaConsumption(pm);
							transmitterCount++;
						}
					}
				}
			}

			if (transmitterCount > 1)
				connection.baseRate = Math.Pow(connection.baseRate, 1.0 / transmitterCount);
			else if (transmitterCount == 0)
				connection.baseRate = 0.0;

			connection.ec_idle *= Settings.TransmitterPassiveEcFactor; // apply passive factor to "internal" antennas always-consumed rate
			connection.ec_idle += connection.ec * Settings.TransmitterPassiveEcFactor; // add "transmit" antennas always-consumed rate
			connection.ec *= Settings.TransmitterActiveEcFactor; // adjust "transmit" antennas transmit-only rate by the factor
		}

	}
}
