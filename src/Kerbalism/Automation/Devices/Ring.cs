namespace KERBALISM
{
	public sealed class RingDevice : LoadedDevice<GravityRing>
	{
		public RingDevice(GravityRing module) : base(module) { }

		// keep Name English for stable device Id hashing across languages
		public override string Name => "gravity ring";
		public override string DisplayName => Local.Brokers_GravityRing;

		// Habitat-owned rings must be controlled via HabitatDevice (inflate/enable flow)
		public override bool IsVisible => !module.isDeployedByHabitat;

		public override string Status => Lib.Color(module.deployed, Local.Generic_DEPLOYED, Lib.Kolor.Green, Local.Generic_RETRACTED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (module.isDeployedByHabitat) return;
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

		public ProtoRingDevice(GravityRing prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			deployedByHabitat = protoPart.FindModule("Habitat") != null;
		}

		// keep Name English for stable device Id hashing across languages
		public override string Name => "gravity ring";
		public override string DisplayName => Local.Brokers_GravityRing;

		// Habitat-owned rings must be controlled via HabitatDevice (inflate/enable flow)
		public override bool IsVisible => !deployedByHabitat;

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "deployed"), Local.Generic_DEPLOYED, Lib.Kolor.Green, Local.Generic_RETRACTED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			if (deployedByHabitat) return;
			Lib.Proto.Set(protoModule, "deployed", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "deployed"));
		}
	}
}
