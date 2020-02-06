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

		public PartDrive Drive { get; set; }
		public PartHabitat Habitat { get; set; }

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

		public void Save(ConfigNode partCollectionNode)
		{
			if (Drive == null && Habitat == null)
				return;

			ConfigNode partNode = partCollectionNode.AddNode(FlightId.ToString());
			partNode.AddValue("name", PartName); // isn't technically needed, this is for sfs editing / debugging purposes
			if (Drive != null)
			{
				ConfigNode driveNode = partNode.AddNode("drive");
				Drive.Save(driveNode);
			}
			if (Habitat != null)
			{
				ConfigNode habitatNode = partNode.AddNode("habitat");
				Habitat.Save(habitatNode);
			}
		}

		public void Load(ConfigNode partDataNode)
		{
			ConfigNode driveNode = partDataNode.GetNode("drive");
			if (driveNode != null)
				Drive = new PartDrive(driveNode);

			ConfigNode habitatNode = partDataNode.GetNode("habitat");
			if (habitatNode != null)
				Habitat = new PartHabitat(habitatNode);
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
