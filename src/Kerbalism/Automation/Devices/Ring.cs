namespace KERBALISM
{
	public sealed class RingDevice : LoadedDevice<GravityRing>
	{
		private readonly Habitat habitat;

		public RingDevice(GravityRing module) : base(module)
		{
			if (module.isDeployedByHabitat)
				habitat = module.part.FindModuleImplementing<Habitat>();
		}

		// keep Name English for stable device Id hashing across languages
		public override string Name => "gravity ring";
		public override string DisplayName => Local.Brokers_GravityRing;

		// Habitat-owned rings must be controlled via HabitatDevice (inflate/enable flow)
		public override bool IsVisible => !module.isDeployedByHabitat;

		public override string Status => Lib.Color(module.deployed, Local.Generic_DEPLOYED, Lib.Kolor.Green, Local.Generic_RETRACTED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			// Keep the old gravity-ring device Id as a hidden compatibility alias for
			// persisted scripts, but route control through Habitat's state machine.
			if (module.isDeployedByHabitat)
			{
				if (habitat != null && Habitat.IsEnabledOrEnabling(habitat.state) != value)
					habitat.Toggle();
				return;
			}
			if (module.deployed != value)
			{
				module.Toggle();
			}
		}

		public override void Toggle()
		{
			Ctrl(!module.deployed);
		}
	}


	public sealed class ProtoRingDevice : ProtoDevice<GravityRing>
	{
		private readonly bool deployedByHabitat;
		private readonly Habitat habitatPrefab;
		private readonly ProtoPartModuleSnapshot habitatModule;

		public ProtoRingDevice(GravityRing prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			habitatModule = protoPart.FindModule("Habitat");
			deployedByHabitat = habitatModule != null;
			if (deployedByHabitat)
				habitatPrefab = prefab.part.FindModuleImplementing<Habitat>();
		}

		// keep Name English for stable device Id hashing across languages
		public override string Name => "gravity ring";
		public override string DisplayName => Local.Brokers_GravityRing;

		// Habitat-owned rings must be controlled via HabitatDevice (inflate/enable flow)
		public override bool IsVisible => !deployedByHabitat;

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "deployed"), Local.Generic_DEPLOYED, Lib.Kolor.Green, Local.Generic_RETRACTED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			// Keep the old gravity-ring device Id as a hidden compatibility alias for
			// persisted scripts, but route control through Habitat's state machine.
			if (deployedByHabitat)
			{
				if (habitatPrefab != null)
					Habitat.ProtoCtrl(protoPart, habitatModule, habitatPrefab, value);
				return;
			}
			Lib.Proto.Set(protoModule, "deployed", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "deployed"));
		}
	}
}
