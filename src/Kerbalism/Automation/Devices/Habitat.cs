namespace KERBALISM
{
	public sealed class HabitatDevice : LoadedDevice<Habitat>
	{
		private readonly bool hasGravityRing;

		public HabitatDevice(Habitat module) : base(module)
		{
			hasGravityRing = module.part.FindModuleImplementing<GravityRing>() != null;
		}

		// keep Name English for stable device Id hashing across languages
		public override string Name => "habitat";
		// Centrifuges previously appeared as "gravity ring" in Auto; keep that label while
		// routing control through Habitat's inflate/enable flow.
		public override string DisplayName => hasGravityRing ? Local.Brokers_GravityRing : Local.StatuToggle_Habitat;

		public override bool IsVisible => module.toggle && module.state != Habitat.State.evaKerbal;

		public override string Status => Habitat.AutomationStatus(module.state);

		public override void Ctrl(bool value)
		{
			if (!IsVisible) return;
			if (Habitat.IsEnabledOrEnabling(module.state) == value) return;
			module.Toggle();
		}

		public override void Toggle()
		{
			if (!IsVisible) return;
			module.Toggle();
		}
	}

	public sealed class ProtoHabitatDevice : ProtoDevice<Habitat>
	{
		private readonly bool hasGravityRing;

		public ProtoHabitatDevice(Habitat prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule)
		{
			hasGravityRing = protoPart.FindModule("GravityRing") != null;
		}

		// keep Name English for stable device Id hashing across languages
		public override string Name => "habitat";
		public override string DisplayName => hasGravityRing ? Local.Brokers_GravityRing : Local.StatuToggle_Habitat;

		public override bool IsVisible
		{
			get
			{
				if (!prefab.toggle) return false;
				Habitat.State state = Lib.Proto.GetEnum(protoModule, nameof(Habitat.state), Habitat.State.disabled);
				return state != Habitat.State.evaKerbal;
			}
		}

		public override string Status
		{
			get
			{
				Habitat.State state = Lib.Proto.GetEnum(protoModule, nameof(Habitat.state), Habitat.State.disabled);
				return Habitat.AutomationStatus(state);
			}
		}

		public override void Ctrl(bool value)
		{
			if (!IsVisible) return;
			Habitat.ProtoCtrl(protoPart, protoModule, prefab, value);
		}

		public override void Toggle()
		{
			if (!IsVisible) return;
			Habitat.ProtoToggle(protoPart, protoModule, prefab);
		}
	}
}
