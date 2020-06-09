﻿using System;

namespace KERBALISM
{
	/// <summary> Wrapper for manipulating the stock PartResource / ProtoPartResourceSnapshot objects without having to use separate code </summary>
	public abstract class PartResourceWrapper
	{
		public abstract string ResName { get; }
		public abstract double Amount { get; set; }
		public abstract double Capacity { get; set; }
		public abstract bool FlowState { get; set; }
		public double Level => Capacity > 0.0 ? Amount / Capacity : 0.0;
	}

	public class LoadedPartResourceWrapper : PartResourceWrapper
	{
		private PartResource partResource;

		public LoadedPartResourceWrapper(PartResource stockResource)
		{
			partResource = stockResource;
		}

		public override string ResName => partResource.resourceName;
		public override double Amount { get => partResource.amount; set => partResource.amount = value; }
		public override double Capacity { get => partResource.maxAmount; set => partResource.maxAmount = value; }
		public override bool FlowState { get => partResource.flowState; set => partResource.flowState = value; }
	}

	public class ProtoPartResourceWrapper : PartResourceWrapper
	{
		private ProtoPartResourceSnapshot partResource;

		public ProtoPartResourceWrapper(ProtoPartResourceSnapshot stockResource)
		{
			partResource = stockResource;
		}

		public override string ResName => partResource.resourceName;
		public override double Amount { get => partResource.amount; set => partResource.amount = value; }
		public override double Capacity { get => partResource.maxAmount; set => partResource.maxAmount = value; }
		public override bool FlowState { get => partResource.flowState; set => partResource.flowState = value; }
	}

	public class VirtualContainer : PartResourceWrapper
	{
		private string resName;
		private double amount;
		private double capacity;
		private bool flowState;

		public VirtualContainer(string resourceName, double amount, double capacity, bool flowState = true)
		{
			this.resName = resourceName;
			this.amount = Math.Min(capacity, amount);
			this.capacity = capacity;
			this.flowState = flowState;
		}

		public override string ResName => resName;
		public override double Amount { get => amount; set => amount = value; }
		public override double Capacity { get => capacity; set => capacity = value; }
		public override bool FlowState { get => flowState; set => flowState = value; }
	}
}
