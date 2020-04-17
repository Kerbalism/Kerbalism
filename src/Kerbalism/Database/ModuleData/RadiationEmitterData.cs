using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class RadiationEmitterData : ModuleData<ModuleKsmRadiationEmitter, RadiationEmitterData>, IRadiationEmitter
	{
		public bool running;

		// IRadiationEmitter implementation
		public double RadiationRate => modulePrefab.radiation;
		public int ModuleId => ID;
		public bool IsActive => running;
		public PartRadiationData RadiationData => partData.radiationData;

		public override void OnSave(ConfigNode node)
		{
			node.AddValue(nameof(running), running);
		}

		public override void OnLoad(ConfigNode node)
		{
			running = Lib.ConfigValue(node, nameof(running), true);
		}
	}
}
