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

		// note 1 : the Id must be unique and always the same (persistence), so the Name property must always be the
		// same, and be unique in case multiple modules of the same type exists on the part.
		// note 2 : dynamically generate the id when first requested.
		// can't do it in the base ctor because the PartId and Name may be overloaded.
		public uint Id
		{
			get
			{
				if (id == uint.MaxValue)
					id = PartId + (uint)Name.GetHashCode();

				return id;
			}
		}
		private uint id = uint.MaxValue; // lets just hope nothing will ever have that id

		public string DeviceType { get; private set; }

		// return device name, must be static and unique in case several modules of the same type are on the part
		public abstract string Name { get; }

		// the name that will be displayed. can be overloaded in case some dynamic text is added (see experiments)
		public virtual string DisplayName => Name;

		// return part id
		public abstract uint PartId { get; }

		// return part name
		public abstract string PartName { get; }

		// return short device status string
		public abstract string Status { get; }

		// return tooltip string
		public virtual string Tooltip => Lib.BuildString(Lib.Bold(DisplayName), "\non ", PartName);

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
		}

		public override string PartName => prefab.part.partInfo.title;
		public override string Name => prefab is IModuleInfo ? ((IModuleInfo)prefab).GetModuleTitle() : prefab.GUIName;
		public override uint PartId => protoPart.flightID;
	}


} // KERBALISM
