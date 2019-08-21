using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM.Planner
{

	///<summary> Planners simulator for all vessel aspects other than resource simulation </summary>
	public sealed class VesselAnalyzer
	{
		public void Analyze(List<Part> parts, ResourceSimulator sim, EnvironmentAnalyzer env)
		{
			// note: vessel analysis require resource analysis, but at the same time resource analysis
			// require vessel analysis, so we are using resource analysis from previous frame (that's okay)
			// in the past, it was the other way around - however that triggered a corner case when va.comforts
			// was null (because the vessel analysis was still never done) and some specific rule/process
			// in resource analysis triggered an exception, leading to the vessel analysis never happening
			// inverting their order avoided this corner-case

			Analyze_crew(parts);
			Analyze_habitat(parts, sim, env);
			Analyze_radiation(parts, sim);
			Analyze_reliability(parts);
			Analyze_qol(parts, sim, env);
			Analyze_comms(parts);
		}

		void Analyze_crew(List<Part> parts)
		{
			// get number of kerbals assigned to the vessel in the editor
			// note: crew manifest is not reset after root part is deleted
			VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
			List<ProtoCrewMember> crew = manifest.GetAllCrew(false).FindAll(k => k != null);
			crew_count = (uint)crew.Count;
			crew_engineer = crew.Find(k => k.trait == "Engineer") != null;
			crew_scientist = crew.Find(k => k.trait == "Scientist") != null;
			crew_pilot = crew.Find(k => k.trait == "Pilot") != null;

			crew_engineer_maxlevel = 0;
			crew_scientist_maxlevel = 0;
			crew_pilot_maxlevel = 0;
			foreach (ProtoCrewMember c in crew)
			{
				switch (c.trait)
				{
					case "Engineer":
						crew_engineer_maxlevel = Math.Max(crew_engineer_maxlevel, (uint)c.experienceLevel);
						break;
					case "Scientist":
						crew_scientist_maxlevel = Math.Max(crew_scientist_maxlevel, (uint)c.experienceLevel);
						break;
					case "Pilot":
						crew_pilot_maxlevel = Math.Max(crew_pilot_maxlevel, (uint)c.experienceLevel);
						break;
				}
			}

			// scan the parts
			crew_capacity = 0;
			foreach (Part p in parts)
			{
				// accumulate crew capacity
				crew_capacity += (uint)p.CrewCapacity;
			}

			// if the user press ALT, the planner consider the vessel crewed at full capacity
			if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
				crew_count = crew_capacity;
		}

		void Analyze_habitat(List<Part> parts, ResourceSimulator sim, EnvironmentAnalyzer env)
		{
			// calculate total volume
			volume = sim.Resource("Atmosphere").capacity / 1e3;

			// calculate total surface
			surface = sim.Resource("Shielding").capacity;

			// determine if the vessel has pressure control capabilities
			pressurized = sim.Resource("Atmosphere").produced > 0.0 || env.breathable;

			// determine if the vessel has scrubbing capabilities
			scrubbed = sim.Resource("WasteAtmosphere").consumed > 0.0 || env.breathable;

			// determine if the vessel has humidity control capabilities
			humid = sim.Resource("MoistAtmosphere").consumed > 0.0 || env.breathable;

			// scan the parts
			double max_pressure = 1.0;
			foreach (Part p in parts)
			{
				// for each module
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled)
						continue;

					if (m.moduleName == "Habitat")
					{
						Habitat h = m as Habitat;
						max_pressure = Math.Min(max_pressure, h.max_pressure);
					}
				}
			}

			pressurized &= max_pressure >= Settings.PressureThreshold;
		}

		void Analyze_comms(List<Part> parts)
		{
			has_comms = false;
			foreach (Part p in parts)
			{

				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled)
						continue;

					// RemoteTech enabled, passive's don't count
					if (m.moduleName == "ModuleRTAntenna")
						has_comms = true;
					else if (m.moduleName == "ModuleDataTransmitter")
					{
						// CommNet enabled and external transmitter
						if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
						{
							if (Lib.ReflectionValue<AntennaType>(m, "antennaType") != AntennaType.INTERNAL)
								has_comms = true;
						}
						// the simple stupid always connected signal system
						else
							has_comms = true;
					}
				}
			}
		}

		void Analyze_radiation(List<Part> parts, ResourceSimulator sim)
		{
			// scan the parts
			emitted = 0.0;
			foreach (Part p in parts)
			{
				// for each module
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled)
						continue;

					// accumulate emitter radiation
					if (m.moduleName == "Emitter")
					{
						Emitter emitter = m as Emitter;

						emitted += emitter.running ? emitter.radiation : 0.0;
					}
				}
			}

			// calculate shielding factor
			double amount = sim.Resource("Shielding").amount;
			double capacity = sim.Resource("Shielding").capacity;

			shielding = capacity > double.Epsilon
				? Radiation.ShieldingEfficiency(amount / capacity)
				: PreferencesStorm.Instance.shieldingEfficiency;
		}

		void Analyze_reliability(List<Part> parts)
		{
			// reset data
			high_quality = 0.0;
			components = 0;
			failure_year = 0.0;
			redundancy = new Dictionary<string, int>();

			// scan the parts
			double year_time = 60.0 * 60.0 * Lib.HoursInDay() * Lib.DaysInYear();
			foreach (Part p in parts)
			{
				// for each module
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled)
						continue;

					// malfunctions
					if (m.moduleName == "Reliability")
					{
						Reliability reliability = m as Reliability;

						// calculate mtbf
						double mtbf = reliability.mtbf * (reliability.quality ? Settings.QualityScale : 1.0);
						if (mtbf <= 0) continue;

						// accumulate failures/y
						failure_year += year_time / mtbf;

						// accumulate high quality percentage
						high_quality += reliability.quality ? 1.0 : 0.0;

						// accumulate number of components
						++components;

						// compile redundancy data
						if (reliability.redundancy.Length > 0)
						{
							int count = 0;
							if (redundancy.TryGetValue(reliability.redundancy, out count))
							{
								redundancy[reliability.redundancy] = count + 1;
							}
							else
							{
								redundancy.Add(reliability.redundancy, 1);
							}
						}

					}
				}
			}

			// calculate high quality percentage
			high_quality /= Math.Max(components, 1u);
		}

		void Analyze_qol(List<Part> parts, ResourceSimulator sim, EnvironmentAnalyzer env)
		{
			// calculate living space factor
			living_space = Lib.Clamp((volume / Math.Max(crew_count, 1u)) / PreferencesComfort.Instance.livingSpace, 0.1, 1.0);

			// calculate comfort factor
			comforts = new Comforts(parts, env.landed, crew_count > 1, has_comms);


		}


		// general
		public uint crew_count;                             // crew member on board
		public uint crew_capacity;                          // crew member capacity
		public bool crew_engineer;                          // true if an engineer is among the crew
		public bool crew_scientist;                         // true if a scientist is among the crew
		public bool crew_pilot;                             // true if a pilot is among the crew
		public uint crew_engineer_maxlevel;                 // experience level of top engineer on board
		public uint crew_scientist_maxlevel;                // experience level of top scientist on board
		public uint crew_pilot_maxlevel;                    // experience level of top pilot on board

		// habitat
		public double volume;                               // total volume in m^3
		public double surface;                              // total surface in m^2
		public bool pressurized;                            // true if the vessel has pressure control capabilities
		public bool scrubbed;                               // true if the vessel has co2 scrubbing capabilities
		public bool humid;                                  // true if the vessel has co2 scrubbing capabilities

		// radiation related
		public double emitted;                              // amount of radiation emitted by components
		public double shielding;                            // shielding factor

		// quality-of-life related
		public double living_space;                         // living space factor
		public Comforts comforts;                           // comfort info

		// reliability-related
		public uint components;                             // number of components that can fail
		public double high_quality;                         // percentage of high quality components
		public double failure_year;                         // estimated failures per-year, averaged per-component
		public Dictionary<string, int> redundancy;          // number of components per redundancy group

		public bool has_comms;
	}


} // KERBALISM
