using System.Collections.Generic;

namespace KERBALISM
{
	public sealed class AntennaInfoCommNet
	{
		/// <summary> science data rate. note that internal transmitters can not transmit science data only telemetry data </summary>
		public double rate = 0.0;

		/// <summary> ec cost </summary>
		public double ec = 0.0;

		public AntennaInfoCommNet(Vessel v)
		{
			List<ModuleDataTransmitter> transmitters;

			// if vessel is loaded
			if (v.loaded)
			{
				// find transmitters
				transmitters = v.FindPartModulesImplementing<ModuleDataTransmitter>();

				if (transmitters != null)
				{
					foreach (ModuleDataTransmitter t in transmitters)
					{
						// Disable all stock buttons
						t.Events["TransmitIncompleteToggle"].active = false;
						t.Events["StartTransmission"].active = false;
						t.Events["StopTransmission"].active = false;
						t.Actions["StartTransmissionAction"].active = false;

						Lib.Log("Data rate: " + t.name + " " + t.DataRate);

						if (t.antennaType == AntennaType.INTERNAL) // do not include internal data rate, ec cost only
							ec += t.DataResourceCost * t.DataRate;
						else
						{
							// do we have an animation
							ModuleDeployableAntenna animation = t.part.FindModuleImplementing<ModuleDeployableAntenna>();
							ModuleAnimateGeneric animationGeneric = t.part.FindModuleImplementing<ModuleAnimateGeneric>();
							if (animation != null)
							{
								// only include data rate and ec cost if transmitter is extended
								if (animation.deployState == ModuleDeployablePart.DeployState.EXTENDED)
								{
									rate += t.DataRate;
									ec += t.DataResourceCost * t.DataRate;
								}
							}
							else if (animationGeneric != null)
							{
								// only include data rate and ec cost if transmitter is extended
								if (animationGeneric.animSpeed > 0)
								{
									rate += t.DataRate;
									ec += t.DataResourceCost * t.DataRate;
								}
							}
							// no animation
							else
							{
								rate += t.DataRate;
								ec += t.DataResourceCost * t.DataRate;
							}
						}
					}
				}
			}
			// if vessel is not loaded
			else
			{
				// find proto transmitters
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					// get part prefab (required for module properties)
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

					transmitters = part_prefab.FindModulesImplementing<ModuleDataTransmitter>();

					if (transmitters != null)
					{
						foreach (ModuleDataTransmitter t in transmitters)
						{
							if (t.antennaType == AntennaType.INTERNAL) // do not include internal data rate, ec cost only
								ec += t.DataResourceCost * t.DataRate;
							else
							{
								// do we have an animation
								ProtoPartModuleSnapshot m = p.FindModule("ModuleDeployableAntenna") ?? p.FindModule("ModuleAnimateGeneric");
								if (m != null)
								{
									// only include data rate and ec cost if transmitter is extended
									string deployState = Lib.Proto.GetString(m, "deployState");
									float animSpeed = Lib.Proto.GetFloat(m, "animSpeed");
									if (deployState == "EXTENDED" || animSpeed > 0)
									{
										rate += t.DataRate;
										ec += t.DataResourceCost * t.DataRate;
									}
								}
								// no animation
								else
								{
									rate += t.DataRate;
									ec += t.DataResourceCost * t.DataRate;
								}
							}
						}
					}
				}
			}
		}
	}
}
