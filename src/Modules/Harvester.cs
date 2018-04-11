using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Harvester : PartModule, IAnimatedModule, IModuleInfo, ISpecifics, IContractObjectiveModule
	{
		// config
		[KSPField] public string title = string.Empty;            // name to show on ui
		[KSPField] public int type = 0;                        // type of resource
		[KSPField] public string resource = string.Empty;         // resource to extract
		[KSPField] public double min_abundance = 0.0;             // minimal abundance required, in percentual
		[KSPField] public double min_pressure = 0.0;              // minimal pressure required, in kPA
		[KSPField] public double rate = 0.0;                      // rate of resource to extract at 100% abundance
		[KSPField] public double ec_rate = 0.0;                   // rate of ec consumption per-second, irregardless of abundance
		[KSPField] public string drill = string.Empty;            // the drill head transform
		[KSPField] public double length = 5.0;                    // tolerable distance between drill head and the ground (length of the extendible part)

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
				Events["Toggle"].guiName = Lib.StatusToggle(title, running ? "running" : "stopped");
			}

			// if in flight, and the stock planet resource system is online
			if (Lib.IsFlight() && ResourceMap.Instance != null)
			{
				// sample abundance
				double abundance = SampleAbundance();

				// determine if resource can be extracted
				issue = DetectIssue(abundance);

				// update ui
				Events["Toggle"].guiActive = deployed;
				Fields["Abundance"].guiActive = deployed;
				if (deployed)
				{
					string status = !running
					  ? "stopped"
					  : issue.Length == 0
					  ? "running"
					  : Lib.BuildString("<color=yellow>", issue, "</color>");

					Events["Toggle"].guiName = Lib.StatusToggle(title, status);
					Abundance = abundance > double.Epsilon ? Lib.HumanReadablePerc(abundance, "F2") : "none";
				}
			}
		}


		public void FixedUpdate()
		{
			if (Lib.IsEditor()) return;

			if (deployed && running && issue.Length == 0)
			{
				resource_recipe recipe = new resource_recipe();
				recipe.Input("ElectricCharge", ec_rate * Kerbalism.elapsed_s);
				recipe.Output(resource, rate * Kerbalism.elapsed_s, true);
				ResourceCache.Transform(vessel, recipe);
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Harvester harvester, double elapsed_s)
		{
			if (Lib.Proto.GetBool(m, "deployed") && Lib.Proto.GetBool(m, "running") && Lib.Proto.GetString(m, "issue").Length == 0)
			{
				resource_recipe recipe = new resource_recipe();
				recipe.Input("ElectricCharge", harvester.ec_rate * elapsed_s);
				recipe.Output(harvester.resource, harvester.rate * elapsed_s, true);
				ResourceCache.Transform(v, recipe);
			}
		}


		[KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			running = !running;
		}

		// return resource abundance at vessel position
		double SampleAbundance()
		{
			// get abundance
			AbundanceRequest request = new AbundanceRequest
			{
				ResourceType = (HarvestTypes)type,
				ResourceName = resource,
				BodyId = vessel.mainBody.flightGlobalsIndex,
				Latitude = vessel.latitude,
				Longitude = vessel.longitude,
				Altitude = vessel.altitude,
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
					if (!land_valid) return "no ground contact";
					break;

				case 1:
					bool ocean_valid = body.ocean && (vessel.Splashed || vessel.altitude < 0.0);
					if (!ocean_valid) return "not in ocean";
					break;

				case 2:
					bool atmo_valid = body.atmosphere && vessel.altitude < body.atmosphereDepth;
					if (!atmo_valid) return "not in atmosphere";
					break;

				case 3:
					bool space_valid = vessel.altitude > body.atmosphereDepth || !body.atmosphere;
					if (!space_valid) return "not in space";
					break;
			}

			// check against pressure
			if (type == 2 && body.GetPressure(vessel.altitude) < min_pressure)
			{
				return "pressure below threshold";
			}

			// check against abundance
			if (abundance < min_abundance)
			{
				return "abundance below threshold";
			}

			// all good
			return string.Empty;
		}


		// return true if the drill head penetrate the ground
		bool GroundContact()
		{
			// if there is no drill transform specified, or if the specified one doesn't exist, assume ground contact
			if (drill_head == null) return true;

			// get distance from drill head to terrain
			// - the drill head transform of stock parts doesn't refer to the drill head (of course),
			//   but to the start of the extendible portion of the drill
			return Lib.TerrainHeight(vessel.mainBody, drill_head.position) < length;
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
				case 0: source = "the surface"; break;
				case 1: source = "the ocean"; break;
				case 2: source = "the atmosphere"; break;
				case 3: source = "space"; break;
			}
			string desc = Lib.BuildString("Extract ", resource, " from ", source);

			// generate tooltip info
			return Specs().info(desc);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.add("type", ((HarvestTypes)type).ToString());
			specs.add("resource", resource);
			if (min_abundance > double.Epsilon) specs.add("min abundance", Lib.HumanReadablePerc(min_abundance, "F2"));
			if (type == 2 && min_pressure > double.Epsilon) specs.add("min pressure", Lib.HumanReadablePressure(min_pressure));
			specs.add("extraction rate", Lib.HumanReadableRate(rate));
			if (ec_rate > double.Epsilon) specs.add("ec consumption", Lib.HumanReadableRate(ec_rate));
			return specs;
		}

		// animation group support
		public void EnableModule() { deployed = true; }
		public void DisableModule() { deployed = false; running = false; }
		public bool ModuleIsActive() { return running && issue.Length == 0; }
		public bool IsSituationValid() { return true; }

		// module info support
		public string GetModuleTitle() { return title; }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// contract objective support
		public bool CheckContractObjectiveValidity() { return true; }
		public string GetContractObjectiveType() { return "Harvester"; }
	}


} // KERBALISM