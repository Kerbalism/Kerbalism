using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class RadiationEmitterData : ModuleData<ModuleKsmRadiationEmitter, RadiationEmitterData>
	{
		private const string NODENAME_HABITATS = "HABITATS";

		public bool running;

		// for every habitat considered, the scaled by distance radiation effect from this emitter
		public Dictionary<int, double> habitatsRadiation = new Dictionary<int, double>();

		public override void VesselDataUpdate(VesselDataBase vd)
		{
			// calculate the radiation impact for this vessel
			EvaluateHabitatsRadiation(vd);

			// calculate the radiation impact for other loaded vessels, but only
			// if they are landed or an EVA kerbal
			foreach (Vessel vessel in FlightGlobals.VesselsLoaded)
			{
				if (!(vessel.isEVA || vessel.LandedOrSplashed))
					return;

				if (!DB.TryGetVesselDataNoError(vessel, out VesselData otherVd))
					continue;

				if (otherVd == vd)
					continue;

				EvaluateHabitatsRadiation(otherVd);
			}
		}

		private void EvaluateHabitatsRadiation(VesselDataBase vd)
		{
			foreach (HabitatData habitat in vd.Habitat.Habitats)
			{
				double scaledRadiation;
				// always recalculate on loaded vessels
				if (vd.LoadedOrEditor)
				{
					double distance = Vector3.Distance(loadedModule.transform.position, habitat.loadedModule.transform.position);
					scaledRadiation = Radiation.DistanceRadiation(modulePrefab.radiation, distance);
					habitatsRadiation[habitat.ID] = scaledRadiation;
				}
				// on unloaded vessels, get the saved value
				else
				{
					habitatsRadiation.TryGetValue(habitat.ID, out scaledRadiation);
				}

				if (running && scaledRadiation != 0.0)
				{
					habitat.localRadiation += modulePrefab.ecRate > 0.0 ? scaledRadiation * vd.ResHandler.ElectricCharge.AvailabilityFactor : scaledRadiation;
				}
			}
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue(nameof(running), running);
			ConfigNode habitatsRadiationNode = node.AddNode(NODENAME_HABITATS);
			foreach (KeyValuePair<int, double> habitatRadiation in habitatsRadiation)
			{
				habitatsRadiationNode.AddValue(habitatRadiation.Key.ToString(), habitatRadiation.Value);
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			running = Lib.ConfigValue(node, nameof(running), true);

			ConfigNode habitatsRadiationNode = node.GetNode(NODENAME_HABITATS);
			if (habitatsRadiationNode != null)
			{
				foreach (ConfigNode.Value value in habitatsRadiationNode.values)
				{
					habitatsRadiation[Lib.Parse.ToInt(value.name)] = Lib.Parse.ToDouble(value.value);
				}
			}
		}
	}
}
