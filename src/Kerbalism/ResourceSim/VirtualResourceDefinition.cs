using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class VirtualResourceDefinition
	{
		public static Dictionary<string, VirtualResourceDefinition> definitions = new Dictionary<string, VirtualResourceDefinition>();

		public enum ResType { PartResource, VesselResource }

		public string name;
		public string title;
		public bool isVisible;
		public ResType resType;
		public int id;

		public VirtualResourceDefinition(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", string.Empty);
			title = Lib.ConfigValue(node, "title", name);
			isVisible = Lib.ConfigValue(node, "isVisible", false);
			resType = Lib.ConfigEnum(node, "type", ResType.VesselResource);

			// check that name is specified
			if (name.Length == 0) throw new Exception("skipping unnamed rule");
		}

		private VirtualResourceDefinition(string name, bool isVisible, ResType resType, string title = null)
		{
			this.name = name;
			this.title = title == null ? name : title;
			this.isVisible = isVisible;
			this.resType = resType;
			id = VesselResHandler.GetVirtualResourceId(name);
		}

		public static VirtualResourceDefinition GetOrCreateDefinition(string name, bool isVisible, ResType resType, string title = null)
		{
			if (definitions.TryGetValue(name, out VirtualResourceDefinition newRes))
			{
				if (newRes.resType != resType)
				{
					Lib.Log($"Can't create the VirtualResourceDefinition `{name}` of type `{resType}`, that definition exists already with the type `{newRes.resType}`", Lib.LogLevel.Error);
					return null;
				}
			}
			else
			{
				newRes = new VirtualResourceDefinition(name, isVisible, resType, title);
				definitions.Add(newRes.name, newRes);
			}
			return newRes;
		}
	}
}
