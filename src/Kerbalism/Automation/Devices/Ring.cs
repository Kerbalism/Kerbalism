using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class RingDevice : LoadedDevice<GravityRing>
	{
		public RingDevice(GravityRing module) : base(module) { }

		public override string Name => "gravity ring";

		public override string Status => Lib.Color(module.deployed, Local.Generic_DEPLOYED, Lib.Kolor.Green, Local.Generic_RETRACTED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
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
		public ProtoRingDevice(GravityRing prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
			: base(prefab, protoPart, protoModule) { }

		public override string Name => "gravity ring";

		public override string Status => Lib.Color(Lib.Proto.GetBool(protoModule, "deployed"), Local.Generic_DEPLOYED, Lib.Kolor.Green, Local.Generic_RETRACTED, Lib.Kolor.Yellow);

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(protoModule, "deployed", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(protoModule, "deployed"));
		}
	}


} // KERBALISM
