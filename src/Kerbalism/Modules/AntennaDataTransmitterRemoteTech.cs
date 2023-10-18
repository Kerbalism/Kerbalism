using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Helper module to simply display some information about transmitting data with Remote Tech in the KSP UI, only gets used if RemoteTech is installed
	/// </summary>
	public class AntennaDataTransmitterRemoteTech : PartModule
	{
		// TODO: Add some localization support, haven't look into that yet...
		const string MODULE_DISPLAY_NAME = "Antenna Data Transmitter";

		[KSPField]
		public float
			packetInterval = 0.0f,
			packetSize = 0.0f,
			packetResourceCost = 0.0f,
			energyCost = 0.0f;

		public override string GetModuleDisplayName()
		{
			return MODULE_DISPLAY_NAME;
		}

		public override string GetInfo()
		{
			Specifics specs = new Specifics();

			// Since the Module Manager only add this PartModule to Parts that have ModuleRTAntenna (in RemoteTech.cfg), we can be sure
			// we find that Part Module here. This is only called in the loading screen and will only run for players that has RemoteTech
			// enabled.
			foreach (var pm in this.part.Modules)
			{
				if(pm.moduleName == "ModuleRTAntenna")
				{
					var packetInterval = pm.Fields.GetValue("RTPacketInterval");
					var packetSize = pm.Fields.GetValue("RTPacketSize");
					var packetResourceCost = pm.Fields.GetValue("RTPacketResourceCost");
					// if the RemoteTech user has changed the EnergyMultiplier value in their RemoteTech settings this will
					// show the wrong value, I have no easy fix that doesn't include loading in their assembly...
					var energyCost = pm.Fields.GetValue("EnergyCost");

					if(packetSize != null && packetInterval != null && packetResourceCost != null && energyCost != null)
					{
						// We save these values so that we dont have to keep using reflection to get them all the time,
						// they do not change at runtime
						this.packetInterval = (float)packetInterval;
						this.packetSize = (float)packetSize;
						this.packetResourceCost = (float)packetResourceCost;
						this.energyCost = (float)energyCost;

						double dataRate = this.packetSize / this.packetInterval;
						double transmissionEnergyCost = this.packetResourceCost * dataRate;
						double idleEnergyCost = this.energyCost;

						specs.Add(Local.DataTransmitter_ECidle, Lib.Color(Lib.HumanOrSIRate(idleEnergyCost, Lib.ECResID), Lib.Kolor.Orange));//"EC (idle)"
						specs.Add(Local.DataTransmitter_ECTX + " Max", Lib.Color(Lib.HumanOrSIRate(transmissionEnergyCost + idleEnergyCost, Lib.ECResID), Lib.Kolor.Orange));//"EC (transmitting)"
						specs.Add("");
						specs.Add(Local.DataTransmitter_Maxspeed, Lib.HumanReadableDataRate(dataRate));//"Max. speed"
						break;
					}
				}
			}

			return specs.Info();
		}
	}
}
