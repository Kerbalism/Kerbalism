using System;
using UnityEngine;

namespace KERBALISM
{


	public abstract class Device
	{
		public class DeviceIcon
		{
			public Texture2D texture;
			public string tooltip;
			public Action onClick;

			public DeviceIcon(Texture2D texture, string tooltip = "", Action onClick = null)
			{
				this.texture = texture;
				this.tooltip = tooltip;
				this.onClick = onClick;
			}
		}

		public Device()
		{
			DeviceType = GetType().Name;
		}

		// generate (probably) unique id for the module
		public uint Id { get; protected set; }

		public string DeviceType { get; private set; }

		// return device name
		public abstract string Name { get; }

		// return part id
		public abstract uint PartId { get; }

		// return part name
		public abstract string PartName { get; }

		// return short device status string
		public abstract string Status { get; }

		// return tooltip string
		public virtual string Tooltip => Lib.BuildString(Lib.Bold(Name), "\non ", PartName);

		// return icon/button
		public virtual DeviceIcon Icon => null;

		// control the device using a value
		public abstract void Ctrl(bool value);

		// toggle the device state
		public abstract void Toggle();

		public virtual bool IsVisible => true;

		public virtual void OnUpdate() { }
	}

	public abstract class LoadedDevice<T> : Device where T : PartModule
	{
		protected readonly T module;

		public LoadedDevice(T module) : base()
		{
			this.module = module;
			Id = PartId + (uint)DeviceType.GetHashCode() + (uint)Lib.RandomInt(int.MaxValue);
		}

		public override string PartName => module.part.partInfo.title;
		public override string Name => module is IModuleInfo ? ((IModuleInfo)module).GetModuleTitle() : module.GUIName;
		public override uint PartId => module.part.flightID;
	}

	public abstract class ProtoDevice<T> : Device where T : PartModule
	{
		protected readonly T prefab;
		protected readonly ProtoPartSnapshot protoPart;
		protected readonly ProtoPartModuleSnapshot protoModule;

		public ProtoDevice(T prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule) : base()
		{
			this.prefab = prefab;
			this.protoPart = protoPart;
			this.protoModule = protoModule;
			Id = PartId + (uint)DeviceType.GetHashCode() + (uint)Lib.RandomInt(int.MaxValue);
		}

		public override string PartName => prefab.part.partInfo.title;
		public override string Name => prefab is IModuleInfo ? ((IModuleInfo)prefab).GetModuleTitle() : prefab.GUIName;
		public override uint PartId => protoPart.flightID;
	}


} // KERBALISM
