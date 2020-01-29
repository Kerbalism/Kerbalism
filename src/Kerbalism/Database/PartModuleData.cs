using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public abstract class PartModuleData
	{
		[Persistent] string moduleName;
		[Persistent] int moduleIndex;
		[Persistent] int duplicateModuleIndex;

		// use in BackgroundUpdate
		public static T GetData<T>(ProtoPartModuleSnapshot protoModule) where T : PartModuleData { return null; }

		// use in OnStart (no need to get it every update). Must work in editor and in flight
		public static T GetData<T>(PartModule module) where T : PartModuleData { return null; }

		public void Save(ConfigNode node) { }

		public void Load(ConfigNode node) { }
	}
}
