using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	public sealed class RemoteTechAntennaDevice : Device
	{
		public RemoteTechAntennaDevice(PartModule antenna)
		{
			this.antenna = antenna;
		}

		public override string Name()
		{
			return antenna.GUIName;
		}

		public override uint Part()
		{
			return antenna.part.flightID;
		}

		public override string Info()
		{
			return Lib.ReflectionValue<bool>(antenna, "IsRTActive")
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ACTIVE") + "</color>"
			  : "<color=red>" + Localizer.Format("#KERBALISM_Generic_INACTIVE") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			Lib.ReflectionValue(antenna, "IsRTActive", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.ReflectionValue<bool>(antenna, "IsRTActive"));
		}

		PartModule antenna;
	}


	public sealed class ProtoRemoteTechAntennaDevice : Device
	{
		public ProtoRemoteTechAntennaDevice(ProtoPartModuleSnapshot antenna, Part part_prefab, Vessel v, uint part_id)
		{
			this.antenna = antenna;
			this.part_prefab = part_prefab;
			this.vessel = v;
			this.part_id = part_id;
		}

		public override string Name()
		{
			return "antenna";
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			return Lib.Proto.GetBool(antenna, "IsRTActive")
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ACTIVE") + "</color>"
			  : "<color=red>" + Localizer.Format("#KERBALISM_Generic_INACTIVE") + "</color>";
		}

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(antenna, "IsRTActive", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(antenna, "IsRTActive"));
		}

		private readonly ProtoPartModuleSnapshot antenna;
		private readonly Part part_prefab;
		private readonly Vessel vessel;
		private readonly uint part_id;
	}


} // KERBALISM

