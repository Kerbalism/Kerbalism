using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class RingDevice : Device
	{
		public RingDevice(GravityRing ring)
		{
			this.ring = ring;
		}

		public override string Name()
		{
			return "gravity ring";
		}

		public override uint Part()
		{
			return ring.part.flightID;
		}

		public override string Info()
		{
			return ring.deployed ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_DEPLOYED") + "</color>" : "<color=red>" + Localizer.Format("KERBALISM_Generic_RETRACTED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			if (ring.deployed != value)
			{
				ring.Toggle();
			}
		}

		public override void Toggle()
		{
			Ctrl(!ring.deployed);
		}

		GravityRing ring;
	}


	public sealed class ProtoRingDevice : Device
	{
		public ProtoRingDevice(ProtoPartModuleSnapshot ring, uint part_id)
		{
			this.ring = ring;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "gravity ring";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			bool deployed = Lib.Proto.GetBool(ring, "deployed");
			return deployed ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_DEPLOYED") + "</color>" : "<color=red>" + Localizer.Format("KERBALISM_Generic_RETRACTED") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(ring, "deployed", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(ring, "deployed"));
		}

		private readonly ProtoPartModuleSnapshot ring;
		private readonly uint part_id;
	}


} // KERBALISM