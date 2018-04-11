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

		public override string name()
		{
			return "gravity ring";
		}

		public override uint part()
		{
			return ring.part.flightID;
		}

		public override string info()
		{
			return ring.deployed ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_DEPLOYED") + "</color>" : "<color=red>" + Localizer.Format("KERBALISM_Generic_RETRACTED") + "</color>";
		}

		public override void ctrl(bool value)
		{
			if (ring.deployed != value)
			{
				ring.Toggle();
			}
		}

		public override void toggle()
		{
			ctrl(!ring.deployed);
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

		public override string name()
		{
			return "gravity ring";
		}

		public override uint part()
		{
			return part_id;
		}

		public override string info()
		{
			bool deployed = Lib.Proto.GetBool(ring, "deployed");
			return deployed ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_DEPLOYED") + "</color>" : "<color=red>" + Localizer.Format("KERBALISM_Generic_RETRACTED") + "</color>";
		}

		public override void ctrl(bool value)
		{
			Lib.Proto.Set(ring, "deployed", value);
		}

		public override void toggle()
		{
			ctrl(!Lib.Proto.GetBool(ring, "deployed"));
		}

		ProtoPartModuleSnapshot ring;
		uint part_id;
	}


} // KERBALISM