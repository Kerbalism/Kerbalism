using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class PartData : IEquatable<PartData>
	{
		public uint FlightId { get; private set; }
		public string PartName { get; private set; }

		public Drive Drive { get; set; }
		public HabitatData Habitat { get; set; }

		public PartData(Part part)
		{
			FlightId = part.flightID;
			PartName = part.partName;
		}

		public PartData(ProtoPartSnapshot protopart)
		{
			FlightId = protopart.flightID;
			PartName = protopart.partName;
		}

		public void Save(ConfigNode node)
		{
			// TODO: only save if there is something to save, change node parameter to be the parent node
			// (create the "part" node here instead of in PartDataList, change the node parameter to the PartDataList node)
			node.AddValue("name", PartName); // isn't technically needed, this is for sfs editing / debugging purposes
			if (Drive != null)
			{
				ConfigNode driveNode = node.AddNode("drive");
				Drive.Save(driveNode);
			}
		}

		public void Load(ConfigNode node)
		{
			if (node.HasNode("drive"))
			{
				Drive = new Drive(node.GetNode("drive"));
			}
		}

		/// <summary> Must be called if the part is destroyed </summary>
		public void OnPartWillDie()
		{
			if (Drive != null)
				Drive.DeleteDriveData();
		}

		public override int GetHashCode() => FlightId.GetHashCode();

		public bool Equals(PartData other) => other != null && other.FlightId == FlightId;

		public override bool Equals(object obj) => obj is PartData other && Equals(other);

		public static bool operator ==(PartData lhs, PartData rhs) => lhs.Equals(rhs);

		public static bool operator !=(PartData lhs, PartData rhs) => !lhs.Equals(rhs);

	}
}
