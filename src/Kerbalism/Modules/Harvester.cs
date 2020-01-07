using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{


	public class Harvester : PartModule, IAnimatedModule, IModuleInfo, ISpecifics, IContractObjectiveModule
	{
		// config
		[KSPField] public string title = string.Empty;            // name to show on ui
		[KSPField] public int type = 0;                           // type of resource
		[KSPField] public string resource = string.Empty;         // resource to extract
		[KSPField] public double min_abundance = 0.0;             // minimal abundance required, in percentual
		[KSPField] public double min_pressure = 0.0;              // minimal pressure required, in kPA
		[KSPField] public double rate = 0.0;                      // rate of resource to extract at 100% abundance
		[KSPField] public double abundance_rate = 0.1;            // abundance level at which rate is specified (10% by default)
		[KSPField] public double ec_rate = 0.0;                   // rate of ec consumption per-second, irregardless of abundance
		[KSPField] public string drill = string.Empty;            // the drill head transform
		[KSPField] public float length = 5f;                    // tolerable distance between drill head and the ground (length of the extendible part)

		// persistence
		[KSPField(isPersistant = true)] public bool deployed;     // true if the harvester is deployed
		[KSPField(isPersistant = true)] public bool running;      // true if the harvester is running
		[KSPField(isPersistant = true)] public string issue = string.Empty; // if not empty, the reason why resource can't be harvested

		// show abundance level
		[KSPField(guiActive = false, guiName = "_")] public string Abundance;

		// the drill head transform
		Transform drill_head;


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// assume deployed if there is no animator
			deployed |= part.FindModuleImplementing<ModuleAnimationGroup>() == null;

			// setup ui
			Fields["Abundance"].guiName = Lib.BuildString(resource, " abundance");

			// get drill head transform only once
			if (drill.Length > 0) drill_head = part.FindModelTransform(drill);
		}


		public void Update()
		{
			// in editor, merely update ui button label
			if (Lib.IsEditor())
			{
				Events["Toggle"].guiName = Lib.StatusToggle(title, running ? Localizer.Format("#KERBALISM_Harvester_running") : Localizer.Format("#KERBALISM_Harvester_stopped"));//"running""stopped"
			}

			// if in flight, and the stock planet resource system is online
			if (Lib.IsFlight() && ResourceMap.Instance != null)
			{
				// sample abundance
				double abundance = SampleAbundance(vessel, this);

				// determine if resource can be extracted
				issue = DetectIssue(abundance);

				// update ui
				Events["Toggle"].guiActive = deployed;
				Fields["Abundance"].guiActive = deployed;
				if (deployed)
				{
					string status = !running
					  ? Localizer.Format("#KERBALISM_Harvester_stopped")//"stopped"
					  : issue.Length == 0
					  ? Localizer.Format("#KERBALISM_Harvester_running")//"running"
					  : Lib.BuildString("<color=yellow>", issue, "</color>");

					Events["Toggle"].guiName = Lib.StatusToggle(title, status);
					Abundance = abundance > double.Epsilon ? Lib.HumanReadablePerc(abundance, "F2") : Localizer.Format("#KERBALISM_Harvester_none");//"none"
				}
			}
		}

		private static void ResourceUpdate(Vessel v, Harvester harvester, double min_abundance, double elapsed_s)
		{
			double abundance = SampleAbundance(v, harvester);
			if (abundance > min_abundance)
			{
				double rate = harvester.rate;

				// Bonus(..., -2): a level 0 engineer will alreaday add 2 bonus points jsut because he's there,
				// regardless of level. efficiency will raise further with higher levels.
				int bonus = engineer_cs.Bonus(v, -2);
				double crew_gain = 1 + bonus * Settings.HarvesterCrewLevelBonus;
				crew_gain = Lib.Clamp(crew_gain, 1, Settings.MaxHarvesterBonus);
				rate *= crew_gain;

				ResourceRecipe recipe = new ResourceRecipe("harvester");
				recipe.AddInput("ElectricCharge", harvester.ec_rate * elapsed_s);
				recipe.AddOutput(harvester.resource, harvester.rate * (abundance/harvester.abundance_rate) * elapsed_s, false);
				ResourceCache.AddRecipe(v, recipe);
			}
		}

		public void FixedUpdate()
		{
			if (Lib.IsEditor()) return;

			if (deployed && running && (issue.Length == 0))
			{
				ResourceUpdate(vessel, this, min_abundance, Kerbalism.elapsed_s);
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Harvester harvester, double elapsed_s)
		{
			if (Lib.Proto.GetBool(m, "deployed") && Lib.Proto.GetBool(m, "running") && Lib.Proto.GetString(m, "issue").Length == 0)
			{
				ResourceUpdate(v, harvester, Lib.Proto.GetDouble(m, "min_abundance"), elapsed_s);
			}
		}


		[KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			running = !running;

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// return resource abundance at vessel position
		private static double SampleAbundance(Vessel v, Harvester harvester)
		{
			// get abundance
			AbundanceRequest request = new AbundanceRequest
			{
				ResourceType = (HarvestTypes)harvester.type,
				ResourceName = harvester.resource,
				BodyId = v.mainBody.flightGlobalsIndex,
				Latitude = v.latitude,
				Longitude = v.longitude,
				Altitude = v.altitude,
				CheckForLock = false
			};
			return ResourceMap.Instance.GetAbundance(request);
		}


		// return the reason why resource can't be harvested, or an empty string otherwise
		string DetectIssue(double abundance)
		{
			// shortcut
			CelestialBody body = vessel.mainBody;

			// check situation
			switch (type)
			{
				case 0:
					bool land_valid = vessel.Landed && GroundContact();
					if (!land_valid) return Localizer.Format("#KERBALISM_Harvester_land_valid");//"no ground contact"
					break;

				case 1:
					bool ocean_valid = body.ocean && (vessel.Splashed || vessel.altitude < 0.0);
					if (!ocean_valid) return Localizer.Format("#KERBALISM_Harvester_ocean_valid");//"not in ocean"
					break;

				case 2:
					bool atmo_valid = body.atmosphere && vessel.altitude < body.atmosphereDepth;
					if (!atmo_valid) return Localizer.Format("#KERBALISM_Harvester_atmo_valid");//"not in atmosphere"
					break;

				case 3:
					bool space_valid = vessel.altitude > body.atmosphereDepth || !body.atmosphere;
					if (!space_valid) return Localizer.Format("#KERBALISM_Harvester_space_valid");//"not in space"
					break;
			}

			// check against pressure
			if (type == 2 && body.GetPressure(vessel.altitude) < min_pressure)
			{
				return Localizer.Format("#KERBALISM_Harvester_pressurebelow");//"pressure below threshold"
			}

			// check against abundance
			if (abundance < min_abundance)
			{
				return Localizer.Format("#KERBALISM_Harvester_abundancebelow");//"abundance below threshold"
			}

			// all good
			return string.Empty;
		}


		// return true if the drill head penetrate the ground
		bool GroundContact()
		{
			// if there is no drill transform specified, or if the specified one doesn't exist, assume ground contact
			if (drill_head == null) return true;

			// Replicating ModuleResourceHarvester.CheckForImpact()
			return Physics.Raycast(drill_head.position, drill_head.forward, length, 32768);
		}

		// action groups
		[KSPAction("#KERBALISM_Harvester_Action")] public void Action(KSPActionParam param) { Toggle(); }


		// part tooltip
		public override string GetInfo()
		{
			// generate description
			string source = string.Empty;
			switch (type)
			{
				case 0: source = Localizer.Format("#KERBALISM_Harvester_source1"); break;//"the surface"
				case 1: source = Localizer.Format("#KERBALISM_Harvester_source2"); break;//"the ocean"
				case 2: source = Localizer.Format("#KERBALISM_Harvester_source3"); break;//"the atmosphere"
				case 3: source = Localizer.Format("#KERBALISM_Harvester_source4"); break;//"space"
			}
			string desc = Localizer.Format("#KERBALISM_Harvester_generatedescription", resource,source);//Lib.BuildString("Extract ", , " from ", )

			// generate tooltip info
			return Specs().Info(desc);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add(Localizer.Format("#KERBALISM_Harvester_info1"), ((HarvestTypes)type).ToString());//"type"
			specs.Add(Localizer.Format("#KERBALISM_Harvester_info2"), resource);//"resource"
			if (min_abundance > double.Epsilon) specs.Add(Localizer.Format("#KERBALISM_Harvester_info3"), Lib.HumanReadablePerc(min_abundance, "F2"));//"min abundance"
			if (type == 2 && min_pressure > double.Epsilon) specs.Add(Localizer.Format("#KERBALISM_Harvester_info4"), Lib.HumanReadablePressure(min_pressure));//"min pressure"
			specs.Add(Localizer.Format("#KERBALISM_Harvester_info5"), Lib.HumanReadableRate(rate));//"extraction rate"
			specs.Add(Localizer.Format("#KERBALISM_Harvester_info6"), Lib.HumanReadablePerc(abundance_rate, "F2"));//"at abundance"
			if (ec_rate > double.Epsilon) specs.Add(Localizer.Format("#KERBALISM_Harvester_info7"), Lib.HumanReadableRate(ec_rate));//"ec consumption"
			return specs;
		}

		// animation group support
		public void EnableModule() { deployed = true; }
		public void DisableModule() { deployed = false; running = false; }
		public bool ModuleIsActive() { return running && issue.Length == 0; }
		public bool IsSituationValid() { return true; }

		// module info support
		public string GetModuleTitle() { return title; }
		public override string GetModuleDisplayName() { return title; }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// contract objective support
		public bool CheckContractObjectiveValidity() { return true; }
		public string GetContractObjectiveType() { return "Harvester"; }

		private static CrewSpecs engineer_cs = new CrewSpecs("Engineer@0");
	}


} // KERBALISM
