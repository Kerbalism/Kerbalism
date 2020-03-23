using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class PartDataShip : PartData
	{
		public Part LoadedPart { get; private set; }
		public bool IsOnShip => !LoadedPart.frozen;

		public PartDataShip(VesselDataBase vesselData, Part part) : base(vesselData, part)
		{
			LoadedPart = part;
		}
	}

	public class PartData
	{
		public VesselDataBase vesselData;
		public uint flightId;
		private AvailablePart partInfo;
		public Part PartPrefab { get; private set; }
		
		

		/// <summary> Localized part title </summary>
		public string Title => partInfo.title;

		/// <summary> part internal name as defined in configs </summary>
		public string Name => partInfo.name;

		public override string ToString() => Title;

		public List<ModuleData> modules = new List<ModuleData>();

		public PartData(VesselDataBase vesselData, Part part)
		{
			this.vesselData = vesselData;
			
			flightId = part.flightID;
			partInfo = part.partInfo;
			PartPrefab = GetPartPrefab(part.partInfo);
		}

		public PartData(VesselDataBase vesselData, ProtoPartSnapshot protopart)
		{
			this.vesselData = vesselData;
			flightId = protopart.flightID;
			partInfo = protopart.partInfo;
			PartPrefab = GetPartPrefab(protopart.partInfo);
		}

		/// <summary> Must be called if the part is destroyed </summary>
		public void PartWillDie()
		{
			
			foreach (ModuleData moduleData in modules)
			{
				moduleData.PartWillDie();
			}

			// TODO : reimplement this by passing the event to every ModuleData :
			// + do we need to do something for habitat and processes ?
			//if (Drive != null)
			//	Drive.DeleteDriveData();
		}

		// The kerbalEVA part variants (vintage/future) prefabs are created in some weird way
		// causing the PartModules from the base KerbalEVA definition to not exist on them, depending
		// on what DLCs are installed (!). The issue with that is that we rely on the prefab modules
		// for ModuleData instantiation, so in those specific cases we return the base kerbalEVA
		// prefab
		private Part GetPartPrefab(AvailablePart partInfo)
		{
			switch (partInfo.name)
			{
				case "kerbalEVAVintage":
				case "kerbalEVAFuture":
					return PartLoader.getPartInfoByName("kerbalEVA").partPrefab;
				case "kerbalEVAfemaleVintage":
				case "kerbalEVAfemaleFuture":
					return PartLoader.getPartInfoByName("kerbalEVAfemale").partPrefab;
				default:
					return partInfo.partPrefab;
			}
		}
	}
}
