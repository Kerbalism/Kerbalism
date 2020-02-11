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
			Analyze_comms(parts, env);
			Analyze_habitat(parts, env);
			Analyze_radiation(parts, sim);
			Analyze_reliability(parts);
			
		}

		void Analyze_crew(List<Part> parts)
		{
			// get number of kerbals assigned to the vessel in the editor
			// note: crew manifest is not reset after root part is deleted
			VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
			crew = manifest.GetAllCrew(false).FindAll(k => k != null);
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

		void Analyze_habitat(List<Part> parts, EnvironmentAnalyzer env)
		{

			List<HabitatData> habitats = new List<HabitatData>();
			foreach (Part part in parts)
			{
				foreach (ModuleKsmHabitat habitat in part.Modules.GetModules<ModuleKsmHabitat>())
				{
					if (habitat.HabitatData != null)
					{
						habitats.Add(habitat.HabitatData);
					}
				}
			}

			habitatInfo = new HabitatVesselData();
			HabitatData.EvaluateHabitat(habitatInfo, habitats, connectionInfo, env.landed, (int)crew_count, Vector3d.zero, false);
		}

		void Analyze_comms(List<Part> parts, EnvironmentAnalyzer env)
		{
			connectionInfo = new ConnectionInfoEditor(parts, env);
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
						emitter.Recalculate();

						if (emitter.running)
						{
							if (emitter.radiation > 0) emitted += emitter.radiation * emitter.radiation_impact;
							else emitted += emitter.radiation;
						}
					}
				}
			}
		}

		void Analyze_reliability(List<Part> parts)
		{
			// reset data
			high_quality = 0.0;
			components = 0;
			failure_year = 0.0;
			redundancy = new Dictionary<string, int>();

			// scan the parts
			double year_time = 60.0 * 60.0 * Lib.HoursInDay * Lib.DaysInYear;
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

		// general
		public List<ProtoCrewMember> crew;                  // full information on all crew
		public uint crew_count;                             // crew member on board
		public uint crew_capacity;                          // crew member capacity
		public bool crew_engineer;                          // true if an engineer is among the crew
		public bool crew_scientist;                         // true if a scientist is among the crew
		public bool crew_pilot;                             // true if a pilot is among the crew
		public uint crew_engineer_maxlevel;                 // experience level of top engineer on board
		public uint crew_scientist_maxlevel;                // experience level of top scientist on board
		public uint crew_pilot_maxlevel;                    // experience level of top pilot on board

		///////// TODO : REWIRE ALL THAT STUFF //////////////

		// habitat
		public double volume;                               // total volume in m^3
		public double surface;                              // total surface in m^2
		public bool pressurized;                            // true if the vessel has pressure control capabilities
		public bool scrubbed;                               // true if the vessel has co2 scrubbing capabilities
		public double shielding;                            // shielding factor
		public double volume_per_crew;                         // living space factor
		public double living_space;                         // living space factor
		public int comfortMask;                           // comfort info
		public double comfortFactor;                           // comfort info

		///////// ENDTODO //////////////

		// radiation related
		public double emitted;                              // amount of radiation emitted by components

		// reliability-related
		public uint components;                             // number of components that can fail
		public double high_quality;                         // percentage of high quality components
		public double failure_year;                         // estimated failures per-year, averaged per-component
		public Dictionary<string, int> redundancy;          // number of components per redundancy group

		public ConnectionInfoEditor connectionInfo;
		public HabitatVesselData habitatInfo;
	}


} // KERBALISM
