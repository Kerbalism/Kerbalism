using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{


	public class Harvester : PartModule, IAnimatedModule, IModuleInfo, ISpecifics, IContractObjectiveModule
	{
		// config
		[KSPField] public string title = string.Empty;            // name to show on ui
		[KSPField] public int type = 0;                           // 0-3: stock HarvestTypes; 4: asteroid/comet space object
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

		// In the editor, allow the user to tweak the abundance for simulation purposes
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_Harvester_simulatedabundance")]
		[UI_FloatRange(scene = UI_Scene.Editor, minValue = 0f, maxValue = 1f, stepIncrement = 0.01f)]
		public float simulated_abundance = 0.1f;

		// the drill head transform
		Transform drill_head;

		private sealed class HarvestSource
		{
			public double abundance;
			public double mass;
			public double mass_threshold;
			public double resource_density;

			private readonly PartModule loaded_info;
			private readonly ProtoPartModuleSnapshot proto_info;

			public HarvestSource(PartModule loaded_info, double abundance, double mass, double mass_threshold, double resource_density)
			{
				this.loaded_info = loaded_info;
				this.abundance = abundance;
				this.mass = mass;
				this.mass_threshold = mass_threshold;
				this.resource_density = resource_density;
			}

			public HarvestSource(ProtoPartModuleSnapshot proto_info, double abundance, double mass, double mass_threshold, double resource_density)
			{
				this.proto_info = proto_info;
				this.abundance = abundance;
				this.mass = mass;
				this.mass_threshold = mass_threshold;
				this.resource_density = resource_density;
			}

			public bool HasResource => abundance > double.Epsilon && mass > mass_threshold && resource_density > double.Epsilon;
			public double AvailableAmount => HasResource ? (mass - mass_threshold) / resource_density : 0.0;

			public double Limit(double amount, double requestedExecution)
			{
				if (amount <= double.Epsilon || requestedExecution <= double.Epsilon) return 0.0;

				UpdateMass();
				double available = AvailableAmount;
				if (available <= double.Epsilon) return 0.0;

				return Math.Min(requestedExecution, available / amount);
			}

			public void Consume(double amount)
			{
				if (amount <= double.Epsilon || resource_density <= double.Epsilon) return;

				UpdateMass();
				mass = Math.Max(mass_threshold, mass - resource_density * amount);

				if (loaded_info != null)
					SetModuleDouble(loaded_info, "currentMassVal", mass);
				else if (proto_info != null)
					SetProtoCurrentMass(proto_info, mass);
			}

			private void UpdateMass()
			{
				if (loaded_info != null)
					mass = GetModuleDouble(loaded_info, "currentMassVal");
				else if (proto_info != null)
					mass = GetProtoCurrentMass(proto_info);
			}
		}


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// assume deployed if there is no animator
			deployed |= part.FindModuleImplementing<ModuleAnimationGroup>() == null;

			// setup ui
			Fields["Abundance"].guiName = Local.Harvester_abundance.Format(Lib.GetResourceDisplayName(resource));

			// get drill head transform only once
			if (drill.Length > 0) drill_head = part.FindModelTransform(drill);
		}


		public void Update()
		{
			// in editor, merely update ui button label
			if (Lib.IsEditor())
			{
				Events["Toggle"].guiName = Lib.StatusToggle(Localizer.Format(title), running ? Local.Harvester_running : Local.Harvester_stopped);//"running""stopped"
			}

			// if in flight
			if (Lib.IsFlight())
			{
				HarvestSource source = IsSpaceObjectType(type) ? FindSpaceObjectSource(vessel, this) : null;

				// sample abundance
				double abundance = SampleAbundance(vessel, this, source);

				// determine if resource can be extracted
				issue = DetectIssue(abundance, source);

				// update ui
				if (part.IsPAWVisible())
				{
					Events["Toggle"].guiActive = deployed;
					Fields["Abundance"].guiActive = deployed;
					if (deployed)
					{
						string status = !running
							? Local.Harvester_stopped//"stopped"
							: issue.Length == 0
								? Local.Harvester_running//"running"
								: Lib.BuildString("<color=yellow>", issue, "</color>");

						Events["Toggle"].guiName = Lib.StatusToggle(Localizer.Format(title), status);
						Abundance = abundance > double.Epsilon ? Lib.HumanReadablePerc(abundance, "F2") : Local.Harvester_none;//"none"
					}
				}
			}
		}

		public static double AdjustedRate(Harvester harvester, CrewSpecs engineer_cs, List<ProtoCrewMember> crew, double abundance)
		{
			// Bonus(..., -2): a level 0 engineer will alreaday add 2 bonus points jsut because he's there,
			// regardless of level. efficiency will raise further with higher levels.
			int bonus = engineer_cs.Bonus(crew, -2);
			double crew_gain = 1 + bonus * Settings.HarvesterCrewLevelBonus;
			crew_gain = Lib.Clamp(crew_gain, 1, Settings.MaxHarvesterBonus);

			return harvester.rate * crew_gain * (abundance / harvester.abundance_rate);
		}

		private static void ResourceUpdate(Vessel v, Harvester harvester, double min_abundance, double elapsed_s, HarvestSource source = null)
		{
			if (IsSpaceObjectType(harvester.type))
			{
				source = source ?? FindSpaceObjectSource(v, harvester);
				if (source == null || !source.HasResource) return;
			}
			else
			{
				source = null;
			}

			double abundance = SampleAbundance(v, harvester, source);
			if (abundance <= min_abundance) return;

			double amount = Harvester.AdjustedRate(harvester, engineer_cs, Lib.CrewList(v), abundance) * elapsed_s;
			if (source != null)
			{
				amount = Math.Min(amount, source.AvailableAmount);
			}

			if (amount <= double.Epsilon) return;

			ResourceRecipe recipe = new ResourceRecipe(ResourceBroker.Harvester);
			recipe.AddInput("ElectricCharge", harvester.ec_rate * elapsed_s);
			recipe.AddOutput(
				harvester.resource,
				amount,
				dump: false);

			if (source != null)
			{
				recipe.AddExecutionLimiter(k => source.Limit(amount, k));
				recipe.AddExecutionCallback(k => source.Consume(amount * k));
			}

			ResourceCache.AddRecipe(v, recipe);
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
			if (!Lib.Proto.GetBool(m, "deployed") || !Lib.Proto.GetBool(m, "running")) return;

			if (IsSpaceObjectType(harvester.type))
			{
				ResourceUpdate(v, harvester, Lib.Proto.GetDouble(m, "min_abundance"), elapsed_s);
				return;
			}

			if (Lib.Proto.GetString(m, "issue").Length == 0)
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

		// return resource abundance at vessel position, or from an attached space object (type 4)
		private static double SampleAbundance(Vessel v, Harvester harvester, HarvestSource source = null)
		{
			if (IsSpaceObjectType(harvester.type))
			{
				return source != null ? source.abundance : 0.0;
			}

			if (ResourceMap.Instance == null) return 0.0;

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

		private static bool IsSpaceObjectType(int harvestType)
		{
			return harvestType == 4;
		}

		private static HarvestSource FindSpaceObjectSource(Vessel v, Harvester harvester)
		{
			if (v == null || string.IsNullOrEmpty(harvester.resource)) return null;

			return v.loaded
				? FindLoadedSpaceObjectSource(v, harvester.resource)
				: FindProtoSpaceObjectSource(v, harvester.resource);
		}

		private static HarvestSource FindLoadedSpaceObjectSource(Vessel v, string resource_name)
		{
			foreach (Part part in v.parts)
			{
				PartModule info = null;
				for (int i = 0; i < part.Modules.Count; ++i)
				{
					PartModule module = part.Modules[i];
					if (IsSpaceObjectInfo(module.moduleName))
					{
						info = module;
						break;
					}
				}

				if (info == null) continue;

				for (int i = 0; i < part.Modules.Count; ++i)
				{
					PartModule module = part.Modules[i];
					if (!IsSpaceObjectResource(module.moduleName)) continue;
					if (!string.Equals(GetModuleString(module, "resourceName"), resource_name, StringComparison.Ordinal)) continue;

					PartResourceDefinition definition = PartResourceLibrary.Instance.GetDefinition(resource_name);
					if (definition == null) continue;

					return new HarvestSource(
						info,
						GetModuleDouble(module, "abundance"),
						GetModuleDouble(info, "currentMassVal"),
						GetModuleDouble(info, "massThresholdVal"),
						definition.density);
				}
			}

			return null;
		}

		private static HarvestSource FindProtoSpaceObjectSource(Vessel v, string resource_name)
		{
			if (v.protoVessel == null) return null;

			foreach (ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
			{
				ProtoPartModuleSnapshot info = null;
				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (IsSpaceObjectInfo(module.moduleName))
					{
						info = module;
						break;
					}
				}

				if (info == null) continue;

				int resourceModuleIndex = 0;
				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (!IsSpaceObjectResource(module.moduleName)) continue;

					string moduleResourceName = GetProtoSpaceObjectResourceName(part, module, resourceModuleIndex);
					++resourceModuleIndex;

					if (!string.Equals(moduleResourceName, resource_name, StringComparison.Ordinal)) continue;

					PartResourceDefinition definition = PartResourceLibrary.Instance.GetDefinition(resource_name);
					if (definition == null) continue;

					// Post-1.10 proto snapshots persist currentMass/massThreshold (no Val suffix).
					return new HarvestSource(
						info,
						Lib.Proto.GetDouble(module, "abundance"),
						GetProtoCurrentMass(info),
						GetProtoMassThreshold(info),
						definition.density);
				}
			}

			return null;
		}

		// resourceName is often omitted from unloaded Module*Resource snapshots; fall back to the part prefab.
		private static string GetProtoSpaceObjectResourceName(ProtoPartSnapshot part, ProtoPartModuleSnapshot module, int resourceModuleIndex)
		{
			string name = Lib.Proto.GetString(module, "resourceName");
			if (!string.IsNullOrEmpty(name)) return name;

			AvailablePart available = PartLoader.getPartInfoByName(part.partName);
			if (available == null || available.partPrefab == null) return string.Empty;

			int prefabIndex = 0;
			foreach (PartModule prefabModule in available.partPrefab.Modules)
			{
				if (!IsSpaceObjectResource(prefabModule.moduleName)) continue;
				if (prefabIndex == resourceModuleIndex)
					return GetModuleString(prefabModule, "resourceName");
				++prefabIndex;
			}

			return string.Empty;
		}

		private static double GetProtoCurrentMass(ProtoPartModuleSnapshot info)
		{
			if (info?.moduleValues != null && info.moduleValues.HasValue("currentMass"))
				return Lib.Proto.GetDouble(info, "currentMass");
			return Lib.Proto.GetDouble(info, "currentMassVal");
		}

		private static double GetProtoMassThreshold(ProtoPartModuleSnapshot info)
		{
			if (info?.moduleValues != null && info.moduleValues.HasValue("massThreshold"))
				return Lib.Proto.GetDouble(info, "massThreshold");
			return Lib.Proto.GetDouble(info, "massThresholdVal");
		}

		private static void SetProtoCurrentMass(ProtoPartModuleSnapshot info, double mass)
		{
			if (info?.moduleValues != null && info.moduleValues.HasValue("currentMass"))
				Lib.Proto.Set(info, "currentMass", mass);
			else
				Lib.Proto.Set(info, "currentMassVal", mass);
		}

		private static bool IsSpaceObjectInfo(string module_name)
		{
			return module_name == "ModuleAsteroidInfo" || module_name == "ModuleCometInfo";
		}

		private static bool IsSpaceObjectResource(string module_name)
		{
			return module_name == "ModuleAsteroidResource" || module_name == "ModuleCometResource";
		}

		private static string GetModuleString(PartModule module, string name)
		{
			object value = GetModuleMemberValue(module, name);
			return value as string ?? string.Empty;
		}

		private static double GetModuleDouble(PartModule module, string name)
		{
			object value = GetModuleMemberValue(module, name);
			if (value == null) return 0.0;
			try
			{
				return Convert.ToDouble(value);
			}
			catch
			{
				return 0.0;
			}
		}

		private static void SetModuleDouble(PartModule module, string name, double value)
		{
			PropertyInfo property = module.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanWrite)
			{
				property.SetValue(module, value, null);
				return;
			}

			FieldInfo field = module.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				field.SetValue(module, value);
			}
		}

		private static object GetModuleMemberValue(PartModule module, string name)
		{
			PropertyInfo property = module.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanRead)
			{
				return property.GetValue(module, null);
			}

			FieldInfo field = module.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return field != null ? field.GetValue(module) : null;
		}


		// return the reason why resource can't be harvested, or an empty string otherwise
		string DetectIssue(double abundance, HarvestSource source)
		{
			if (IsSpaceObjectType(type))
			{
				if (source == null) return Local.Harvester_spaceobject_valid;//"no asteroid or comet attached"
				return source.HasResource && abundance >= min_abundance ? string.Empty : Local.Harvester_abundancebelow;
			}

			// shortcut
			CelestialBody body = vessel.mainBody;

			// check situation
			switch (type)
			{
				case 0:
					bool land_valid = vessel.Landed && GroundContact();
					if (!land_valid) return Local.Harvester_land_valid;//"no ground contact"
					break;

				case 1:
					bool ocean_valid = body.ocean && (vessel.Splashed || vessel.altitude < 0.0);
					if (!ocean_valid) return Local.Harvester_ocean_valid;//"not in ocean"
					break;

				case 2:
					bool atmo_valid = body.atmosphere && vessel.altitude < body.atmosphereDepth;
					if (!atmo_valid) return Local.Harvester_atmo_valid;//"not in atmosphere"
					break;

				case 3:
					bool space_valid = vessel.altitude > body.atmosphereDepth || !body.atmosphere;
					if (!space_valid) return Local.Harvester_space_valid;//"not in space"
					break;
			}

			// check against pressure
			if (type == 2 && body.GetPressure(vessel.altitude) < min_pressure)
			{
				return Local.Harvester_pressurebelow;//"pressure below threshold"
			}

			// check against abundance
			if (abundance < min_abundance)
			{
				return Local.Harvester_abundancebelow;//"abundance below threshold"
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
				case 0: source = Local.Harvester_source1; break;//"the surface"
				case 1: source = Local.Harvester_source2; break;//"the ocean"
				case 2: source = Local.Harvester_source3; break;//"the atmosphere"
				case 3: source = Local.Harvester_source4; break;//"space"
				case 4: source = Local.Harvester_source5; break;//"asteroids and comets"
			}
			string desc = Local.Harvester_generatedescription.Format(Lib.GetResourceDisplayName(resource), source);

			// generate tooltip info
			return Specs().Info(desc);
		}

		static string LocalizeHarvestType(int harvestType)
		{
			switch (harvestType)
			{
				case 0: return Local.Harvester_type0; // Planetary
				case 1: return Local.Harvester_type1; // Oceanic
				case 2: return Local.Harvester_type2; // Atmospheric
				case 3: return Local.Harvester_type3; // Exospheric
				case 4: return Local.Harvester_type4; // Asteroid / Comet
				default: return harvestType.ToString();
			}
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add(Local.Harvester_info1, LocalizeHarvestType(type));//"type"
			specs.Add(Local.Harvester_info2, Lib.GetResourceDisplayName(resource));//"resource"
			if (min_abundance > double.Epsilon) specs.Add(Local.Harvester_info3, Lib.HumanReadablePerc(min_abundance, "F2"));//"min abundance"
			if (type == 2 && min_pressure > double.Epsilon) specs.Add(Local.Harvester_info4, Lib.HumanReadablePressure(min_pressure));//"min pressure"
			// NOTE: we aren't using SI here.
			specs.Add(Local.Harvester_info5, Lib.HumanReadableRate(rate));//"extraction rate"
			specs.Add(Local.Harvester_info6, Lib.HumanReadablePerc(abundance_rate, "F2"));//"at abundance"
			if (ec_rate > double.Epsilon) specs.Add(Local.Harvester_info7, Lib.HumanOrSIRate(ec_rate, Lib.ECResID));//"ec consumption"
			return specs;
		}

		// animation group support
		public void EnableModule() { deployed = true; }
		public void DisableModule() { deployed = false; running = false; }
		public bool ModuleIsActive() { return running && issue.Length == 0; }
		public bool IsSituationValid() { return true; }

		// module info support
		public string GetModuleTitle() { return Localizer.Format(title); }
		public override string GetModuleDisplayName() { return Localizer.Format(title); }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// contract objective support
		public bool CheckContractObjectiveValidity() { return true; }
		public string GetContractObjectiveType() { return "Harvester"; }

		private static CrewSpecs engineer_cs = new CrewSpecs("Engineer@0");
	}


} // KERBALISM
