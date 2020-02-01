using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	public class PartDataCollection : IEnumerable<PartData>
	{
		private Dictionary<uint, PartData> partDictionary = new Dictionary<uint, PartData>(); // all parts by flightID
		private List<PartData> partList = new List<PartData>(); // all parts
		private VesselData vd;

		public PartDataCollection(VesselData vd) => this.vd = vd;

		public void Populate(Vessel vessel)
		{
			Clear();
			foreach (Part part in vessel.Parts)
				Add(part.flightID, new PartData(part));
		}
		public void Populate(ProtoVessel protoVessel)
		{
			Clear();
			foreach (ProtoPartSnapshot protoPart in protoVessel.protoPartSnapshots)
				Add(protoPart.flightID, new PartData(protoPart));
		}

		public void Load(ConfigNode vesselDataNode)
		{
			ConfigNode partsNode = new ConfigNode();
			if (vesselDataNode.TryGetNode("parts", ref partsNode))
			{
				foreach (ConfigNode partDataNode in partsNode.GetNodes())
				{
					if (partDictionary.TryGetValue(Lib.Parse.ToUInt(partDataNode.name), out PartData partData))
					{
						partData.Load(partDataNode);
					}
				}
			}
		}

		public void Save(ConfigNode vesselDataNode)
		{
			ConfigNode partsNode = vesselDataNode.AddNode("parts");
			foreach (PartData partData in partList)
			{
				ConfigNode partNode = partsNode.AddNode(partData.FlightId.ToString());
				partData.Save(partNode);
			}
		}

		// List / dictionary alike implementation
		public IEnumerator<PartData> GetEnumerator() => partList.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => partList.GetEnumerator();
		public int Count => partList.Count;
		public PartData this[uint flightId] => partDictionary[flightId];

		public bool Contains(uint flightID) => partDictionary.ContainsKey(flightID);

		public bool TryGet(uint flightID, out PartData partData) => partDictionary.TryGetValue(flightID, out partData);

		public PartData Get(uint flightID)
		{
			// in some cases (KIS added parts), we might try to get partdata before it is added by part-adding events
			// so we implement a fallback here
			if (!partDictionary.TryGetValue(flightID, out PartData partData))
			{
				if (vd.Vessel.loaded)
				{
					foreach (Part part in vd.Vessel.parts)
						if (part.flightID == flightID)
							partData = new PartData(part);
				}
				else
				{
					foreach (ProtoPartSnapshot protoPart in vd.Vessel.protoVessel.protoPartSnapshots)
						if (protoPart.flightID == flightID)
							partData = new PartData(protoPart);
				}

				if (partData != null)
					Add(flightID, partData);
				else
					Lib.LogStack($"Trying to create a part with flightID '{flightID}' on vessel '{vd.Vessel.vesselName}', but the part doesn't exists", Lib.LogLevel.Error);
			}
			return partData;
		}

		public void Add(uint flightID, PartData partData)
		{
			partDictionary.Add(flightID, partData);
			partList.Add(partData);
		}

		public void Remove(uint flightID)
		{
			if (partDictionary.TryGetValue(flightID, out PartData partData))
			{
				partDictionary.Remove(flightID);
				partList.Remove(partData);
			}
		}

		public void Clear()
		{
			partDictionary.Clear();
			partList.Clear();
		}
	}
}
