using System;
using System.Collections.Generic;
using System.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
//using RealFuels;


namespace KERBALISM
{


	public sealed class Reliability : PartModule, ISpecifics, IModuleInfo, IPartCostModifier, IPartMassModifier
	{
		// config
		[KSPField(isPersistant = true)] public string type;                 // component name
		[KSPField] public double mtbf = 21600000.0;                         // mean time between failures, in seconds
		[KSPField] public string repair = string.Empty;                     // repair crew specs
		[KSPField] public string title = string.Empty;                      // short description of component
		[KSPField] public string redundancy = string.Empty;                 // redundancy group
		[KSPField] public double extra_cost;                                // extra cost for high-quality, in proportion of part cost
		[KSPField] public double extra_mass;                                // extra mass for high-quality, in proportion of part mass

		// persistence
		[KSPField(isPersistant = true)] public bool broken;                 // true if broken
		[KSPField(isPersistant = true)] public bool critical;               // true if failure can't be repaired
		[KSPField(isPersistant = true)] public bool quality;                // true if the component is high-quality
		[KSPField(isPersistant = true)] public double last;                 // time of last failure
		[KSPField(isPersistant = true)] public double next;                 // time of next failure
		[KSPField(isPersistant = true)] public bool needMaintenance = false;// true when component is inspected and about to fail

		// status ui
		[KSPField(guiActive = false, guiName = "_")] public string Status;  // show component status

		// data
		List<PartModule> modules;                                           // components cache
		CrewSpecs repair_cs;                                                // crew specs


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// do nothing in the editors and when compiling parts
			if (!Lib.IsFlight()) return;

			// cache list of modules
			modules = part.FindModulesImplementing<PartModule>().FindAll(k => k.moduleName == type);

			// parse crew specs
			repair_cs = new CrewSpecs(repair);

			// setup ui
			Fields["Status"].guiName = title;
			Events["Inspect"].guiName = Lib.BuildString("Inspect <b>", title, "</b>");
			Events["Repair"].guiName = Lib.BuildString("Repair <b>", title, "</b>");

			// sync monobehaviour state with module state
			// - required as the monobehaviour state is not serialized
			if (broken)
			{
				foreach (PartModule m in modules)
				{
					m.enabled = false;
				}
			}

			// type-specific hacks
			if (broken) Apply(true);
		}


		public void Update()
		{
			if (Lib.IsFlight())
			{
				// enforce state
				// - required as things like Configure or AnimationGroup can re-enable broken modules
				if (broken)
				{
					foreach (PartModule m in modules)
					{
						m.enabled = false;
						m.isEnabled = false;
					}
				}

				// update ui
				if (broken)
				{
					Status = !critical
					  ? "<color=yellow>Malfunction</color=yellow>"
					  : "<color=red>Critical failure</color>";
				}
				Fields["Status"].guiActive = broken;
				Events["Inspect"].active = !broken && !needMaintenance;
				Events["Repair"].active = repair_cs && (broken || needMaintenance) && !critical;

				if(needMaintenance) {
					Events["Repair"].guiName = Lib.BuildString("Service <b>", title, "</b>");
				}


				// set highlight
				Highlight(part);
			}
			else
			{
				// update ui
				string quality_label = part.FindModulesImplementing<Reliability>().Count > 1
				  ? Lib.BuildString("<b>", title, "</b> quality") : "Quality";
				Events["Quality"].guiName = Lib.StatusToggle(quality_label, quality ? "high" : "standard");
			}
		}


		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor()) return;

			// if it has not malfunctioned
			if (!broken)
			{
				// calculate time of next failure if necessary
				if (next <= double.Epsilon)
				{
					last = Planetarium.GetUniversalTime();
					next = last + mtbf * (quality ? Settings.QualityScale : 1.0) * 2.0 * Lib.RandomDouble();
				}

				// if it has failed, trigger malfunction
				if (Planetarium.GetUniversalTime() > next) Break();
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Reliability reliability)
		{
			// if it has not malfunctioned
			if (!Lib.Proto.GetBool(m, "broken"))
			{
				// get time of next failure
				double next = Lib.Proto.GetDouble(m, "next");

				// get quality
				bool quality = Lib.Proto.GetBool(m, "quality");

				// calculate epoch of failure if necessary
				if (next <= double.Epsilon)
				{
					double last = Planetarium.GetUniversalTime();
					next = last + reliability.mtbf * (quality ? Settings.QualityScale : 1.0) * 2.0 * Lib.RandomDouble();
					Lib.Proto.Set(m, "last", last);
					Lib.Proto.Set(m, "next", next);
				}

				// if it has failed, trigger malfunction
				if (Planetarium.GetUniversalTime() > next) ProtoBreak(v, p, m);
			}
		}


		// toggle between standard and high quality
		[KSPEvent(guiActiveEditor = true, guiName = "_", active = true)]
		public void Quality()
		{
			quality = !quality;

			// sync all other modules in the symmetry group
			foreach (Part p in part.symmetryCounterparts)
			{
				Reliability reliability = p.Modules[part.Modules.IndexOf(this)] as Reliability;
				if (reliability != null)
				{
					reliability.quality = !reliability.quality;
				}
			}

			// refresh VAB/SPH ui
			GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}


		// show a message with some hint on time to next failure
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
		public void Inspect()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// get normalized time to failure
			double time_k = (Planetarium.GetUniversalTime() - last) / (next - last);

			// notify user
			if (time_k < 0.2) Message.Post("It is practically new");
			else if (time_k < 0.35) Message.Post("It is in good shape");
			else {
				needMaintenance = true;
				if (time_k < 0.6) Message.Post("It will keep working for some more time");
				else if (time_k < 0.8) Message.Post("It is reaching its operational limits");
				else Message.Post("It could fail at any moment now");
			}
		}


		// repair malfunctioned component
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
		public void Repair()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// check trait
			if (!repair_cs.Check(v))
			{
				Message.Post
				(
				  Lib.TextVariant
				  (
					"I'm not qualified for this",
					"I will not even know where to start",
					"I'm afraid I can't do that"
				  ),
				  repair_cs.Warning()
				);
				return;
			}

			needMaintenance = false;

			// reset times
			last = 0.0;
			next = 0.0;

			if (broken)
			{
				// flag as not broken
				broken = false;

				// re-enable module
				foreach (PartModule m in modules)
				{
					m.isEnabled = true;
					m.enabled = true;
				}

				// we need to reconfigure the module here, because if all modules of a type
				// share the broken state, and these modules are part of a configure setup,
				// then repairing will enable all of them, messing up with the configuration
				part.FindModulesImplementing<Configure>().ForEach(k => k.DoConfigure());

				// type-specific hacks
				Apply(false);

				// notify user
				Message.Post
				(
				  Lib.BuildString("<b>", title, "</b> repaired"),
				  Lib.TextVariant
				  (
					"A powerkick did the trick",
					"Duct tape, is there something it can't fix?",
					"Fully operational again",
					"We are back in business"
				  )
				);
			} else {
				// notify user
				Message.Post
				(
				  Lib.BuildString("<b>", title, "</b> serviced"),
				  Lib.TextVariant
				  (
					"I don't know how this was still working",
					"Fastened a loose screw there",
					"Someone forgot a cloth in there",
					"As good as new"
				  )
				);
			}
		}


		//[KSPEvent(guiActive = true, guiActiveUnfocused = true, guiName = "Break [TEST]", active = true)] // [for testing]
		public void Break()
		{
			// if manned, or if safemode didn't trigger
			if (Cache.VesselInfo(vessel).crew_capacity > 0 || Lib.RandomDouble() > PreferencesBasic.Instance.safeModeChance)
			{
				// flag as broken
				broken = true;

				// determine if this is a critical failure
				critical = Lib.RandomDouble() < PreferencesBasic.Instance.criticalChance;

				// disable module
				foreach (PartModule m in modules)
				{
					m.isEnabled = false;
					m.enabled = false;
				}

				// type-specific hacks
				Apply(true);

				// notify user
				Broken_msg(vessel, title, critical);
			}
			// safemode
			else
			{
				// reset age
				last = 0.0;
				next = 0.0;

				// notify user
				Safemode_msg(vessel, title);
			}

			// in any case, incentive redundancy
			if (PreferencesBasic.Instance.incentiveRedundancy)
			{
				Incentive_redundancy(vessel, redundancy);
			}
		}


		public static void ProtoBreak(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m)
		{
			// get reliability module prefab
			string type = Lib.Proto.GetString(m, "type", string.Empty);
			Reliability reliability = p.partPrefab.FindModulesImplementing<Reliability>().Find(k => k.type == type);
			if (reliability == null) return;

			// if manned, or if safemode didn't trigger
			if (Cache.VesselInfo(v).crew_capacity > 0 || Lib.RandomDouble() > PreferencesBasic.Instance.safeModeChance)
			{
				// flag as broken
				Lib.Proto.Set(m, "broken", true);

				// determine if this is a critical failure
				bool critical = Lib.RandomDouble() < PreferencesBasic.Instance.criticalChance;
				Lib.Proto.Set(m, "critical", critical);

				// for each associated module
				foreach (var proto_module in p.modules.FindAll(k => k.moduleName == reliability.type))
				{
					// disable the module
					Lib.Proto.Set(proto_module, "isEnabled", false);
				}

				// type-specific hacks
				switch (reliability.type)
				{
					case "ProcessController":
						foreach (ProcessController pc in p.partPrefab.FindModulesImplementing<ProcessController>())
						{
							ProtoPartResourceSnapshot res = p.resources.Find(k => k.resourceName == pc.resource);
							if (res != null) res.flowState = false;
						}
						break;
				}

				// show message
				Broken_msg(v, reliability.title, critical);
			}
			// safe mode
			else
			{
				// reset age
				Lib.Proto.Set(m, "last", 0.0);
				Lib.Proto.Set(m, "next", 0.0);

				// notify user
				Safemode_msg(v, reliability.title);
			}

			// in any case, incentive redundancy
			if (PreferencesBasic.Instance.incentiveRedundancy)
			{
				Incentive_redundancy(v, reliability.redundancy);
			}
		}


		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}


		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			if (redundancy.Length > 0) specs.Add("Redundancy", redundancy);
			specs.Add("Repair", new CrewSpecs(repair).Info());
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>Standard quality</color>");
			specs.Add("MTBF", Lib.HumanReadableDuration(mtbf));
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>High quality</color>");
			specs.Add("MTBF", Lib.HumanReadableDuration(mtbf * Settings.QualityScale));
			if (extra_cost > double.Epsilon) specs.Add("Extra cost", Lib.HumanReadableCost(extra_cost * part.partInfo.cost));
			if (extra_mass > double.Epsilon) specs.Add("Extra mass", Lib.HumanReadableMass(extra_mass * part.partInfo.partPrefab.mass));
			return specs;
		}


		// module info support
		public string GetModuleTitle() { return Lib.BuildString(title, " Reliability"); }
		public override string GetModuleDisplayName() { return Lib.BuildString(title, " Reliability"); }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }


		// module cost support
		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return quality ? (float)extra_cost * part.partInfo.cost : 0.0f; }


		// module mass support
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return quality ? (float)extra_mass * part.partInfo.partPrefab.mass : 0.0f; }
		public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }


		// apply type-specific hacks to enable/disable the module
		void Apply(bool b)
		{
			switch (type)
			{
				case "ProcessController":
					if (b)
					{
						foreach (PartModule m in modules)
						{
							(m as ProcessController).ReliablityEvent(b);
						}
					}
					break;

				case "ModuleDeployableSolarPanel":
				case "ModuleDeployableRadiator":
					if (b)
					{
						part.FindModelComponents<Animation>().ForEach(k => k.Stop());
					}
					break;

				case "ModuleLight":
					if (b)
					{
						foreach (PartModule m in modules)
						{
							ModuleLight light = m as ModuleLight;
							if (light.animationName.Length > 0)
							{
								new Animator(part, light.animationName).Still(0.0f);
							}
							else
							{
								part.FindModelComponents<Light>().ForEach(k => k.enabled = false);
							}
						}
					}
					break;

				case "ModuleEngines":
				case "ModuleEnginesFX":
					if (b)
					{
						foreach (PartModule m in modules)
						{
							(m as ModuleEngines).Shutdown();
						}
					}
					break;
				case "ModuleEnginesRF":
					if (b)
					{
						foreach (PartModule m in modules)
						{
							Lib.ReflectionCall(m, "Shutdown");
						}
					}
					break;
				case "ModuleScienceExperiment":
					if (b)
					{
						foreach (PartModule m in modules)
						{
							(m as ModuleScienceExperiment).SetInoperable();
						}
					}
					break;
			}

			if (RemoteTech.Enabled)
			{
				foreach (PartModule m in part.FindModulesImplementing<PartModule>())
				{
					if(RemoteTech.IsAntenna(m))
					{
						RemoteTech.SetBroken(m, b);
					}
				}
			}
		}


		static void Incentive_redundancy(Vessel v, string redundancy)
		{
			if (v.loaded)
			{
				foreach (Reliability m in Lib.FindModules<Reliability>(v))
				{
					if (m.redundancy == redundancy)
					{
						m.next += m.next - m.last;
					}
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Reliability"))
				{
					// find part
					ProtoPartSnapshot p = v.protoVessel.protoPartSnapshots.Find(k => k.modules.Contains(m));

					// find module prefab
					string type = Lib.Proto.GetString(m, "type", string.Empty);
					Reliability reliability = p.partPrefab.FindModulesImplementing<Reliability>().Find(k => k.type == type);
					if (reliability == null) continue;

					// double time to next failure
					if (reliability.redundancy == redundancy)
					{
						double last = Lib.Proto.GetDouble(m, "last");
						double next = Lib.Proto.GetDouble(m, "next");
						Lib.Proto.Set(m, "next", next + (next - last));
					}
				}
			}
		}


		// set highlighting
		static void Highlight(Part p)
		{
			if (DB.Vessel(p.vessel).cfg_highlights)
			{
				// get state among all reliability components in the part
				bool broken = false;
				bool critical = false;
				foreach (Reliability m in p.FindModulesImplementing<Reliability>())
				{
					broken |= m.broken;
					critical |= m.critical;
				}

				if (broken)
				{
					Highlighter.Set(p.flightID, !critical ? Color.yellow : Color.red);
				}
			}
		}


		static void Broken_msg(Vessel v, string title, bool critical)
		{
			if (DB.Vessel(v).cfg_malfunction)
			{
				if (!critical)
				{
					Message.Post
					(
					  Severity.warning,
					  Lib.BuildString("<b>", title, "</b> malfunctioned on <b>", v.vesselName, "</b>"),
					  "We can still repair it"
					);
				}
				else
				{
					Message.Post
					(
					  Severity.danger,
					  Lib.BuildString("<b>", title, "</b> failed on <b>", v.vesselName, "</b>"),
					  "It is gone for good"
					);
				}
			}
		}


		static void Safemode_msg(Vessel v, string title)
		{
			Message.Post
			(
			  Lib.BuildString("There has been a problem with <b>", title, "</b> on <b>", v.vesselName, "</b>"),
			  "We were able to fix it remotely, this time"
			);
		}


		// cause a part at random to malfunction
		public static void CauseMalfunction(Vessel v)
		{
			// if vessel is loaded
			if (v.loaded)
			{
				// choose a module at random
				var modules = Lib.FindModules<Reliability>(v).FindAll(k => !k.broken);
				if (modules.Count == 0) return;
				var m = modules[Lib.RandomInt(modules.Count)];

				// break it
				m.Break();
			}
			// if vessel is not loaded
			else
			{
				// choose a module at random
				var modules = Lib.FindModules(v.protoVessel, "Reliability").FindAll(k => !Lib.Proto.GetBool(k, "broken"));
				if (modules.Count == 0) return;
				var m = modules[Lib.RandomInt(modules.Count)];

				// find its part
				ProtoPartSnapshot p = v.protoVessel.protoPartSnapshots.Find(k => k.modules.Contains(m));

				// break it
				ProtoBreak(v, p, m);
			}
		}


		// return true if it make sense to trigger a malfunction on the vessel
		public static bool CanMalfunction(Vessel v)
		{
			if (v.loaded)
			{
				return Lib.HasModule<Reliability>(v, k => !k.broken);
			}
			else
			{
				return Lib.HasModule(v.protoVessel, "Reliability", k => !Lib.Proto.GetBool(k, "broken"));
			}
		}


		// return true if at least a component has malfunctioned or had a critical failure
		public static bool HasMalfunction(Vessel v)
		{
			if (v.loaded)
			{
				foreach (Reliability m in Lib.FindModules<Reliability>(v))
				{
					if (m.broken) return true;
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Reliability"))
				{
					if (Lib.Proto.GetBool(m, "broken")) return true;
				}
			}
			return false;
		}


		// return true if at least a component has a critical failure
		public static bool HasCriticalFailure(Vessel v)
		{
			if (v.loaded)
			{
				foreach (Reliability m in Lib.FindModules<Reliability>(v))
				{
					if (m.critical) return true;
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Reliability"))
				{
					if (Lib.Proto.GetBool(m, "critical")) return true;
				}
			}
			return false;
		}
	}


} // KERBALISM

