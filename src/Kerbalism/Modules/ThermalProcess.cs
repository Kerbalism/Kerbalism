using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public class ThermalProcess : PartModule
	{

		/// <summary>
		/// resource stored inside the module and that can require special condtions to be loaded/unloaded (ex : EnrichedUranium, DepletedFuel)
		/// </summary>
		public class InternalResource
		{
			public string name;
			public double rate;			// rate of consumption/production
			public double transferRate; // transfer rate when loading/unloading the resource
			public double amount;		// current stored amount
			public double maxAmount;	// storage capacity

			public CrewSpecs loadingReqs;	// loading requirements
			public CrewSpecs unloadingReqs; // unloading requirements
		}

		public class Resource
		{
			public string name;
			public double rate; // rate of consumption/production
			public bool dump;   // should a produced resource be dumped of no storage available
		}

		[KSPField] public double maxThermalPower;		// thermal power produced when running at nominal rate (kW)
		[KSPField] public double minThermalPower;		// minimal thermal power produced as long as thermalProcessEnabled = true (kW)
		[KSPField] public double passiveCoolingPower;	// thermal power passively removed all the time (kW)
		[KSPField] public FloatCurve thermalDecayCurve; // keys : time after process shutdown (hours), values : heat generated as a percentage of minThermalPowerFactor ([0;1] range)
		[KSPField] public double powerFactor;			// multiplier applied to the config-defined resource rates. rates * powerFactor = nominal rates. Allow to keep the same resource rate definitions for reactors that have the same balance but are more/less powerful
		[KSPField] public double meltdownEnergy;		// accumulated thermal energy required to trigger a meltdown (kJ)
		[KSPField] public double explosionEnergy;       // accumulated thermal energy required for the part to explode (kJ)
		[KSPField] public string startupTime;			// time before the reactor will start producing power and heat (hours)
		[KSPField] public string startupResource;       // name of the resource consumed during startup
		[KSPField] public double startupResourceRate;   // rate of the resource consumed during startup

		[KSPField] public string coolantResourceName = "Coolant";
		[KSPField] public double coolantResourceJoules = 1000.0; // ex : 1000 -> 1 unit of coolant == 1000 joules (1kJ)


		public double storedEnergy; // joules (heat) currently stored


		List<Resource> resources;
		List<InternalResource> internalResources;

		[KSPField]
		public bool thermalProcessEnabled;


		private bool isStarting;
		private double startUT;
		private double shutdownUT; 



	}
}
