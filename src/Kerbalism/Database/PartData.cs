using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class PartData
	{
		public uint FlightId { get; private set; }

		public Drive Drive { get; set; }

		public PartData(Part part)
		{
			FlightId = part.flightID;
		}

		public PartData(ProtoPartSnapshot protopart)
		{
			FlightId = protopart.flightID;
		}

		public void Save(ConfigNode node)
		{
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
	}
}
