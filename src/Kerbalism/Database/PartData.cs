using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class PartData
	{
		public uint flightId;
		public AvailablePart PartInfo { get; private set; }
		public string Title => PartInfo.title;
		public Part LoadedPart { get; private set; }
		public bool IsOnShip => !LoadedPart.frozen;

		public override string ToString() => Title;

		public List<ModuleData> modules = new List<ModuleData>();

		public Drive Drive { get; set; }
		public HabitatData Habitat { get; set; }
		public List<PartProcessData> Processes { get; set; }

		public PartData(Part part)
		{
			LoadedPart = part;
			flightId = part.flightID;
			PartInfo = part.partInfo;

		}

		public PartData(ProtoPartSnapshot protopart)
		{
			flightId = protopart.flightID;
			PartInfo = protopart.partInfo;
		}

		public void Save(ConfigNode partCollectionNode)
		{
			if (Drive == null && Habitat == null && Processes == null)
				return;

			ConfigNode partNode = partCollectionNode.AddNode(flightId.ToString());

			partNode.AddValue("name", Title); // isn't technically needed, this is for sfs editing / debugging purposes

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
					process.Save(processesNode.AddNode(DB.ToSafeKey(process.processName)));
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
	}
}
