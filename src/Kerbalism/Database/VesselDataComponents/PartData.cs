using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class PartData
	{
		public VesselDataBase vesselData;
		public uint flightId;
		public AvailablePart PartInfo { get; private set; }
		public string Title => PartInfo.title;
		public Part LoadedPart { get; private set; }
		public bool IsOnShip => !LoadedPart.frozen;

		public override string ToString() => Title;

		public List<ModuleData> modules = new List<ModuleData>();

		public PartData(VesselDataBase vesselData, Part part)
		{
			this.vesselData = vesselData;
			LoadedPart = part;
			flightId = part.flightID;
			PartInfo = part.partInfo;

		}

		public PartData(VesselDataBase vesselData, ProtoPartSnapshot protopart)
		{
			this.vesselData = vesselData;
			flightId = protopart.flightID;
			PartInfo = protopart.partInfo;
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
	}
}
