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
		public List<PartProcessData> Processes { get; set; }

		public PartData(Part part)
		{
			FlightId = part.flightID;
			PartName = part.name;
		}

		public PartData(ProtoPartSnapshot protopart)
		{
			FlightId = protopart.flightID;
			PartName = protopart.partName;
		}

		public void Save(ConfigNode partCollectionNode)
		{
			if (Drive == null && Habitat == null && Processes == null)
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

			if (Processes != null)
			{
				ConfigNode processesNode = partNode.AddNode("processes");
				foreach(var process in Processes)
				{
					process.Save(processesNode.AddNode(DB.To_safe_key(process.processName)));
				}
			}
		}

		public void Add(PartProcessData data)
		{
			if (Processes == null)
				Processes = new List<PartProcessData>();
			Processes.Add(data);
		}

		public void Load(ConfigNode partDataNode)
		{
			ConfigNode driveNode = partDataNode.GetNode("drive");
			if (driveNode != null)
				Drive = new Drive(driveNode);

			ConfigNode habitatNode = partDataNode.GetNode("habitat");
			if (habitatNode != null)
				Habitat = new HabitatData(habitatNode);

			ConfigNode processesNode = partDataNode.GetNode("processes");
			if(processesNode != null)
			{
				Processes = new List<PartProcessData>();
				foreach (var pn in processesNode.GetNodes())
					Processes.Add(new PartProcessData(pn));
			}
		}

		/// <summary> Must be called if the part is destroyed </summary>
		public void OnPartWillDie()
		{
			if (Drive != null)
				Drive.DeleteDriveData();

			// TODO unregister all processes
			if (Processes != null) { }
		}

		public override int GetHashCode() => FlightId.GetHashCode();

		public bool Equals(PartData other) => other != null && other.FlightId == FlightId;

		public override bool Equals(object obj) => obj is PartData other && Equals(other);
	}
}
