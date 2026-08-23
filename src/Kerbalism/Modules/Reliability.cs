using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public class Reliability : PartModule, ISpecifics, IModuleInfo, IPartCostModifier, IPartMassModifier
	{
		// config
		[KSPField(isPersistant = true)] public string type;                 // component name
		[KSPField] public double mtbf = 3600 * 6 * 1000;                    // mean time between failures, in seconds
		[KSPField] public string repair = string.Empty;                     // repair crew specs
		[KSPField] public string title = string.Empty;                      // short description of component
		[KSPField] public string redundancy = string.Empty;                 // redundancy group
		[KSPField] public double extra_cost;                                // extra cost for high-quality, in proportion of part cost
		[KSPField] public double extra_mass;                                // extra mass for high-quality, in proportion of part mass

		[KSPField] public double rated_radiation = 0;                       // rad/h this part can sustain without taking any damage. Only effective with MTBF failures.
		[KSPField] public double radiation_decay_rate = 1;                  // time to next failure is reduced by (rad/h - rated_radiation) * radiation_decay_rate per second

		// persistence
		[KSPField(isPersistant = true)] public bool broken;                 // true if broken
		[KSPField(isPersistant = true)] public bool critical;               // true if failure requires a more qualified engineer and two repair kits
		[KSPField(isPersistant = true)] public bool quality;                // true if the component is high-quality
		[KSPField(isPersistant = true)] public double last = 0.0;           // time of last failure
		[KSPField(isPersistant = true)] public double next = 0.0;           // time of next failure
		[KSPField(isPersistant = true)] public double last_inspection = 0.0;   // time of last service
		[KSPField(isPersistant = true)] public bool needMaintenance = false;// true when component is inspected and about to fail
		[KSPField(isPersistant = true)] public bool enforce_breakdown = false; // true when the next failure is enforced
		[KSPField(isPersistant = true)] public bool radiator_state_stored;  // true after caching a switch radiator's pre-failure state
		[KSPField(isPersistant = true)] public bool radiator_was_cooling;   // switch radiator state restored after repair

		// status ui
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Reliability", groupDisplayName = "#KERBALISM_Group_Reliability")]//Reliability
		public string Status; // show component status

		// data
		List<PartModule> modules;                                           // components cache
		List<ModuleAlternator> alternators;                                 // engine-mounted ModuleAlternator cache
		CrewSpecs repair_cs;                                                // crew specs
		bool explode = false;
		string localizedTitle = string.Empty;                               // cached LocalizeTitle(title)
		bool flightPawInitialized;
		bool lastFlightPawBroken;
		bool lastFlightPawCritical;
		bool lastFlightPawMaintenance;
		bool lastFlightPawRadiationWarning;
		bool editorPawInitialized;
		bool lastEditorPawQuality;
		bool lastEditorPawMtbfFailures;
		double lastEditorPawMtbf;
		double lastEditorPawRatedRadiation;
		double lastEditorPawQualityScale;

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			localizedTitle = LocalizeTitle(title);
			Fields["Status"].guiName = localizedTitle;
#if DEBUG_RELIABILITY
			Events["Break"].guiName = "Break " + localizedTitle + " [DEBUG]";
#endif

			// do nothing in the editors and when compiling parts
			if (!Lib.IsFlight()) return;

			if (last_inspection <= 0) last_inspection = Planetarium.GetUniversalTime();

			if (part.FindModuleImplementingFast<SystemHeatRadiatorKerbalism>() != null
				&& (type == "USRadiatorSwitch" || type == "ModuleActiveRadiator" || type == "ModuleSystemHeatRadiator"))
			{
				// Migrate persistent Reliability fields on vessels saved before the
				// SystemHeat sidecar remap.
				type = "SystemHeatRadiatorKerbalism";
			}

			// cache list of modules
			if(type.StartsWith("ModuleEngines", StringComparison.Ordinal))
			{
				// do this generically. there are many different engine types derived from ModuleEngines:
				// ModuleEnginesFX, ModuleEnginesRF, all the SolverEngines, possibly more
				// this will also reduce the amount of configuration overhead, no need to duplicate the same
				// config for stock with ModuleEngines and ModuleEnginesFX
				modules = new List<PartModule>();
				var engines = Lib.FindModules<ModuleEngines>(part);
                foreach (var engine in engines)
                {
					modules.Add(engine);
                }
				// stock alternators keep producing EC unless disabled separately (#747)
				alternators = Lib.FindModules<ModuleAlternator>(part).ToList();
            }
			else
			{
				modules = new List<PartModule>();
				for (int i = 0; i < part.Modules.Count; i++)
				{
					PartModule m = part.Modules[i];
					if (m != null && m.moduleName == type)
						modules.Add(m);
				}
			}

			// parse crew specs
			repair_cs = new CrewSpecs(repair);

			// setup ui
			Events["Inspect"].guiName = Local.Reliability_Inspect.Format("<b>"+localizedTitle+"</b>");//Lib.BuildString("Inspect <<1>>)
			Events["Repair"].guiName = Local.Reliability_Repair.Format("<b>"+localizedTitle+"</b>");//Lib.BuildString("Repair <<1>>)
			
			// sync monobehaviour state with module state
			// - required as the monobehaviour state is not serialized
			if (broken)
			{
				foreach (PartModule m in modules)
				{
					m.enabled = false;
					m.isEnabled = false;
				}
				SetAlternatorsEnabled(false);
			}

			if(broken) StartCoroutine(DeferredApply());
		}

		public IEnumerator DeferredApply()
		{
			// wait until vessel is unpacked. doing this will ensure that module
			// specific hacks are executed after the module itself was OnStart()ed.
			yield return new WaitUntil(() => !vessel.packed);
			if (broken)
			{
				Apply(true);
			}
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
					SetAlternatorsEnabled(false);
					EnforceBrokenRadiatorState();
				}

				// update ui
				if (part.IsPAWVisible())
					RefreshFlightPAW();

				// if it has failed, trigger malfunction
				var now = Planetarium.GetUniversalTime();
				if (next > 0 && now > next && !broken)
				{
#if DEBUG_RELIABILITY
					Lib.Log("Reliablity: breakdown for " + part.partInfo.title);
#endif
					Break();
				}

				// set highlight
				Highlight(part);
			}
			else
			{
				// update ui
				if (part.IsPAWVisible())
					RefreshEditorPAW();
			}
		}

		private void RefreshFlightPAW()
		{
			bool radiationWarning = false;
			if (!broken && rated_radiation > 0)
			{
				double rated = quality ? rated_radiation * Settings.QualityScale : rated_radiation;
				radiationWarning = rated < vessel.KerbalismData().EnvRadiation * 3600.0;
			}

			if (flightPawInitialized
				&& lastFlightPawBroken == broken
				&& lastFlightPawCritical == critical
				&& lastFlightPawMaintenance == needMaintenance
				&& lastFlightPawRadiationWarning == radiationWarning)
				return;

			string newStatus = broken
				? critical
					? Lib.Color(Local.Reliability_criticalfailure, Lib.Kolor.Red)
					: Lib.Color(Local.Reliability_malfunction, Lib.Kolor.Yellow)
				: radiationWarning
					? Lib.Color(Local.Reliability_takingradiationdamage, Lib.Kolor.Orange)
					: Local.Generic_NOMINAL;
			Lib.SetPAWValue(ref Status, newStatus);

			bool inspectActive = !broken && !needMaintenance;
			bool repairActive = repair_cs && (broken || needMaintenance);
			if (Events["Inspect"].active != inspectActive)
				Events["Inspect"].active = inspectActive;
			if (Events["Repair"].active != repairActive)
				Events["Repair"].active = repairActive;

			if (needMaintenance)
				Lib.SetEventGuiName(Events["Repair"], Local.Reliability_Service.Format("<b>" + localizedTitle + "</b>"));//Lib.BuildString("Service <<1>>")

			flightPawInitialized = true;
			lastFlightPawBroken = broken;
			lastFlightPawCritical = critical;
			lastFlightPawMaintenance = needMaintenance;
			lastFlightPawRadiationWarning = radiationWarning;
		}

		private void RefreshEditorPAW()
		{
			bool mtbfFailures = PreferencesReliability.Instance.mtbfFailures;
			double qualityScale = Settings.QualityScale;
			if (editorPawInitialized
				&& lastEditorPawQuality == quality
				&& lastEditorPawMtbfFailures == mtbfFailures
				&& lastEditorPawMtbf.Equals(mtbf)
				&& lastEditorPawRatedRadiation.Equals(rated_radiation)
				&& lastEditorPawQualityScale.Equals(qualityScale))
				return;

			Lib.SetEventGuiName(Events["Quality"], Lib.StatusToggle(
				Local.Reliability_qualityinfo.Format("<b>" + localizedTitle + "</b>"),
				quality ? Local.Reliability_qualityhigh : Local.Reliability_qualitystandard));//Lib.BuildString(<<1>> quality")"high""standard"

			string newStatus = string.Empty;
			if (mtbf > 0 && mtbfFailures)
			{
				double effectiveMtbf = EffectiveMTBF(quality, mtbf);
				newStatus = Lib.BuildString(Local.Reliability_MTBF + " ", Lib.HumanReadableDuration(effectiveMtbf));//"MTBF:"
			}

			if (rated_radiation > 0 && mtbfFailures)
			{
				double radiationRating = quality ? rated_radiation * qualityScale : rated_radiation;
				newStatus = Lib.BuildString(newStatus,
					(string.IsNullOrEmpty(newStatus) ? "" : ", "),
					Lib.HumanReadableRadiation(radiationRating / 3600.0));
			}
			Lib.SetPAWValue(ref Status, newStatus);

			editorPawInitialized = true;
			lastEditorPawQuality = quality;
			lastEditorPawMtbfFailures = mtbfFailures;
			lastEditorPawMtbf = mtbf;
			lastEditorPawRatedRadiation = rated_radiation;
			lastEditorPawQualityScale = qualityScale;
		}

		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor()) return;

			var now = Planetarium.GetUniversalTime();

			// if it has not malfunctioned
			if (!broken && mtbf > 0 && PreferencesReliability.Instance.mtbfFailures)
			{
				// calculate time of next failure if necessary
				if (next <= 0)
				{
					last = now;
					var guaranteed = mtbf / 2.0;
					var r = 1 - Math.Pow(Lib.RandomDouble(), 3);
					next = now + guaranteed + mtbf * (quality ? Settings.QualityScale : 1.0) * r;
#if DEBUG_RELIABILITY
					Lib.Log("Reliability: MTBF failure in " + (now - next) + " for " + part.partInfo.title);
#endif
				}

				var decay = RadiationDecay(quality, vessel.KerbalismData().EnvRadiation, Kerbalism.elapsed_s, rated_radiation, radiation_decay_rate);
				next -= decay;
			}
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Reliability reliability, double elapsed_s)
		{
			if(!PreferencesReliability.Instance.mtbfFailures) return;

			// check for existing malfunction and if it actually uses MTBF failures
			if (Lib.Proto.GetBool(m, "broken")) return;
			if (reliability.mtbf <= 0) return;

			// get time of next failure
			double next = Lib.Proto.GetDouble(m, "next");
			bool quality = Lib.Proto.GetBool(m, "quality");
			var now = Planetarium.GetUniversalTime();

			// calculate epoch of failure if necessary
			if (next <= 0)
			{
				var guaranteed = reliability.mtbf / 2.0;
				var r = 1 - Math.Pow(Lib.RandomDouble(), 3);
				next = now + guaranteed + reliability.mtbf * (quality ? Settings.QualityScale : 1.0) * r;
				Lib.Proto.Set(m, "last", now);
				Lib.Proto.Set(m, "next", next);
#if DEBUG_RELIABILITY
				Lib.Log("Reliability: background MTBF failure in " + (now - next) + " for " + p);
#endif
			}

			var rad = v.KerbalismData().EnvRadiation;
			var decay = RadiationDecay(quality, rad, elapsed_s, reliability.rated_radiation, reliability.radiation_decay_rate);
			if (decay > 0)
			{
				next -= decay;
				Lib.Proto.Set(m, "next", next);
			}

			// if it has failed, trigger malfunction
			if (now > next)
			{
#if DEBUG_RELIABILITY
				Lib.Log("Reliablity: background MTBF failure for " + p);
#endif
					ProtoBreak(v, p, m);
			}
		}

		[KSPEvent(guiActiveEditor = true, guiName = "_", active = true, groupName = "Reliability", groupDisplayName = "#KERBALISM_Group_Reliability")]//Reliability
		// toggle between standard and high quality
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
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		[KSPEvent(guiActiveUnfocused = true, unfocusedRange = 3.5f, guiName = "_", active = false, groupName = "Reliability", groupDisplayName = "#KERBALISM_Group_Reliability")]//Reliability
		// show a message with some hint on time to next failure
		public void Inspect()
		{
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null) return;

			// get normalized time to failure
			double time_k = (Planetarium.GetUniversalTime() - last) / (next - last);
			needMaintenance = mtbf > 0 && time_k > 0.35;

			v.KerbalismData().ResetReliabilityStatus();

			// notify user
			if (!needMaintenance)
			{
				last_inspection = Planetarium.GetUniversalTime();
				Message.Post(Lib.TextVariant(
					Local.Reliability_MessagePost1,//"It is practically new"
					Local.Reliability_MessagePost2,//"It is in good shape"
					Local.Reliability_MessagePost3,//"This will last for ages"
					Local.Reliability_MessagePost4,//"Brand new!"
					Local.Reliability_MessagePost5//"Doesn't look used. Is this even turned on?"
				));
			}
			else
			{
				Message.Post(Lib.TextVariant(
					Local.Reliability_MessagePost6,//"Looks like it's going to fall off soon."
					Local.Reliability_MessagePost7,//"Better get the duck tape ready!"
					Local.Reliability_MessagePost8,//"It is reaching its operational limits."
					Local.Reliability_MessagePost9,//"How is this still working?"
					Local.Reliability_MessagePost10//"It could fail at any moment now."
				));
			}
		}

		[KSPEvent(guiActiveUnfocused = true, unfocusedRange = 3.5f, guiName = "_", active = false, groupName = "Reliability", groupDisplayName = "#KERBALISM_Group_Reliability")]//Reliability
		// repair malfunctioned component
		public void Repair()
		{
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null) return;

			// check trait
			CrewSpecs requiredRepairCrew = critical ? repair_cs.ElevatedForCritical() : repair_cs;
			if (!requiredRepairCrew.Check(v))
			{
				Message.Post
				(
				  Lib.TextVariant
				  (
					Local.Reliability_MessagePost11,//"I'm not qualified for this"
					Local.Reliability_MessagePost12,//"I will not even know where to start"
					Local.Reliability_MessagePost13//"I'm afraid I can't do that"
				  ),
				  requiredRepairCrew.Warning()
				);
				return;
			}

			needMaintenance = false;
			enforce_breakdown = false;

			// reset times
			last = 0.0;
			next = 0.0;
			last_inspection = Planetarium.GetUniversalTime();

			vessel.KerbalismData().ResetReliabilityStatus();

			if (broken)
			{
				int repairKitCost = critical ? 2 : 1;
				if (!ConsumeRepairKits(v, LocalizeTitle(title), repairKitCost)) return;

				// flag as not broken
				broken = false;
				critical = false;

				// re-enable module
				foreach (PartModule m in modules)
				{
					m.isEnabled = true;
					m.enabled = true;
				}

				// type-specific hacks
				Apply(false);

				// we need to reconfigure the module here, because if all modules of a type
				// share the broken state, and these modules are part of a configure setup,
				// then repairing will enable all of them, messing up with the configuration
				foreach (Configure cfg in Lib.FindModules<Configure>(part))
					cfg.DoConfigure();

				// notify user
				Message.Post
				(
				  Local.Reliability_MessagePost14.Format("<b>"+LocalizeTitle(title)+"</b>"),//Lib.BuildString("<<1>> repaired")
				  Lib.TextVariant
				  (
					Local.Reliability_MessagePost15,//"A powerkick did the trick."
					Local.Reliability_MessagePost16,//"Duct tape, is there something it can't fix?"
					Local.Reliability_MessagePost17,//"Fully operational again."
					Local.Reliability_MessagePost18//"We are back in business."
				  )
				);
			} else {
				// notify user
				Message.Post
				(
				  Local.Reliability_MessagePost19.Format("<b>"+LocalizeTitle(title)+"</b>"),//Lib.BuildString(<<1>> serviced")
				  Lib.TextVariant
				  (
					Local.Reliability_MessagePost20,//"I don't know how this was still working."
					Local.Reliability_MessagePost21,//"Fastened that loose screw."
					Local.Reliability_MessagePost22,//"Someone forgot a toothpick in there."
					Local.Reliability_MessagePost23//"As good as new!"
				  )
				);
			}
		}

		public static bool ConsumeRepairKits(Vessel v, string localizedTitle, int amount)
		{
			if (!PreferencesReliability.Instance.requireRepairKits) return true;

			int repairKits = 0;
			KerbalEVA kerbalEVA = v.evaController;
			if (v.isEVA && kerbalEVA != null && kerbalEVA.ModuleInventoryPartReference != null)
			{
				foreach (StoredPart storedPart in kerbalEVA.ModuleInventoryPartReference.storedParts.Values)
				{
					// Note: the "evaRepairKit" string is hardcoded in the KSP source.
					if (storedPart.partName == "evaRepairKit") repairKits += storedPart.quantity;
				}
			}

			if (repairKits < amount)
			{
				Message.Post
				(
				  Local.Reliability_MessagePost30.Format("<b>" + localizedTitle + "</b>"),//Lib.BuildString("<<1>> needs a repair kit")
				  Lib.TextVariant
				  (
					Local.Reliability_MessagePost31,//"Did I forget something."
					Local.Reliability_MessagePost32//"Oh crap..."
				  )
				);
				return false;
			}

			kerbalEVA.ModuleInventoryPartReference.RemoveNPartsFromInventory("evaRepairKit", amount, true);
			return true;
		}

#if DEBUG_RELIABILITY
		[KSPEvent(guiActive = true, guiActiveUnfocused = true, guiName = "_", active = true)] // [for testing]
#endif
		public void Break()
		{
			vessel.KerbalismData().ResetReliabilityStatus();

			if (broken) return;

			if (explode)
			{
				foreach (PartModule m in modules)
					m.part.explode();
				return;
			}

			// if enforced, manned, or if safemode didn't trigger
			if (enforce_breakdown || vessel.KerbalismData().CrewCapacity > 0 || Lib.RandomDouble() > PreferencesReliability.Instance.safeModeChance)
			{
				// flag as broken
				broken = true;

				// determine if this is a critical failure
				critical = Lib.RandomDouble() < PreferencesReliability.Instance.criticalChance;

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
			if (PreferencesReliability.Instance.incentiveRedundancy)
			{
				Incentive_redundancy(vessel, redundancy);
			}
		}

		public static void ProtoBreak(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m)
		{
			v.KerbalismData().ResetReliabilityStatus();

			// get reliability module prefab
			string type = Lib.Proto.GetString(m, "type", string.Empty);
			Reliability reliability = Lib.FindModules<Reliability>(p.partPrefab).Find(k => k.type == type);
			if (reliability == null && (type == "USRadiatorSwitch"
				|| type == "ModuleActiveRadiator"
				|| type == "ModuleSystemHeatRadiator"))
			{
				// Existing vessels keep persistent Reliability fields from before the
				// SystemHeat MM remap. Accept legacy radiator types as sidecar aliases.
				reliability = Lib.FindModules<Reliability>(p.partPrefab).Find(k => k.type == "SystemHeatRadiatorKerbalism");
			}
			if (reliability == null) return;
			if (reliability.type != type)
				Lib.Proto.Set(m, "type", reliability.type);

			bool enforce_breakdown = Lib.Proto.GetBool(m, "enforce_breakdown", false);

			// if manned, or if safemode didn't trigger
			if (enforce_breakdown || v.KerbalismData().CrewCapacity > 0 || Lib.RandomDouble() > PreferencesReliability.Instance.safeModeChance)
			{
				// flag as broken
				Lib.Proto.Set(m, "broken", true);

				// determine if this is a critical failure
				bool critical = Lib.RandomDouble() < PreferencesReliability.Instance.criticalChance;
				Lib.Proto.Set(m, "critical", critical);

				ProtoStoreRadiatorState(p, m, reliability.type);

				// for each associated module
				foreach (var proto_module in p.modules.FindAll(k => k.moduleName == reliability.type))
				{
					// disable the module
					Lib.Proto.Set(proto_module, "isEnabled", false);

					if (reliability.type == "ProcessController")
						Lib.Proto.Set(proto_module, nameof(ProcessController.broken), true);
				}

				// engine failures must also stop the co-located stock alternator (#747)
				if (reliability.type.StartsWith("ModuleEngines", StringComparison.Ordinal))
				{
					foreach (var proto_module in p.modules.FindAll(k => k.moduleName == "ModuleAlternator"))
					{
						Lib.Proto.Set(proto_module, "isEnabled", false);
					}
				}

				ProtoPartModuleCache.Purge(Lib.VesselID(v));

				// type-specific hacks
				switch (reliability.type)
				{
					case "ProcessController":
						foreach (ProcessController pc in Lib.FindModules<ProcessController>(p.partPrefab))
						{
							ProtoPartResourceSnapshot res = p.resources.Find(k => k.resourceName == pc.resource);
							if (res != null) res.flowState = false;
						}
						break;

					case "SystemHeatRadiatorKerbalism":
						ProtoDisableSystemHeatNativeRadiators(p);
						break;

					case "USRadiatorSwitch":
						foreach (var proto_module in p.modules.FindAll(k => k.moduleName == "USRadiatorSwitch"
							|| k.moduleName == "SystemHeatRadiatorKerbalism"
							|| k.moduleName == "ModuleSystemHeatRadiator"))
						{
							Lib.Proto.Set(proto_module, "isEnabled", false);
							Lib.Proto.Set(proto_module, "IsCooling", false);
							if (proto_module.moduleName == "USRadiatorSwitch")
								Lib.Proto.Set(proto_module, "ActiveCooling", false);
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
			if (PreferencesReliability.Instance.incentiveRedundancy)
			{
				Incentive_redundancy(v, reliability.redundancy);
			}
		}

		static void ProtoStoreRadiatorState(ProtoPartSnapshot part, ProtoPartModuleSnapshot reliability, string reliabilityType)
		{
			if (reliabilityType != "SystemHeatRadiatorKerbalism" && reliabilityType != "USRadiatorSwitch")
				return;

			string nativeModuleName = reliabilityType;
			if (reliabilityType == "SystemHeatRadiatorKerbalism" && part.partPrefab != null)
			{
				SystemHeatRadiatorKerbalism wrapper = part.partPrefab.FindModuleImplementingFast<SystemHeatRadiatorKerbalism>();
				if (wrapper != null && !string.IsNullOrEmpty(wrapper.radiatorModuleName))
					nativeModuleName = wrapper.radiatorModuleName;
			}

			bool wasCooling = true;
			ProtoPartModuleSnapshot wrapperSnapshot = IntegrationUtils.TryFindPartModuleSnapshot(part, "SystemHeatRadiatorKerbalism");
			if (wrapperSnapshot != null)
				wasCooling = Lib.Proto.GetBool(wrapperSnapshot, "IsCooling", wasCooling);

			ProtoPartModuleSnapshot nativeSnapshot = IntegrationUtils.TryFindPartModuleSnapshot(part, nativeModuleName);
			if (nativeSnapshot != null)
			{
				wasCooling = nativeModuleName == "USRadiatorSwitch"
					? Lib.Proto.GetBool(nativeSnapshot, "ActiveCooling", Lib.Proto.GetBool(nativeSnapshot, "IsCooling", wasCooling))
					: Lib.Proto.GetBool(nativeSnapshot, "IsCooling", wasCooling);
			}

			Lib.Proto.Set(reliability, nameof(radiator_was_cooling), wasCooling);
			Lib.Proto.Set(reliability, nameof(radiator_state_stored), true);
		}

		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}

		public static double EffectiveMTBF(bool quality, double mtbf)
		{
			return mtbf * (quality ? Settings.QualityScale : 1.0);
		}

		public static double EffectiveDuration(bool quality, double duration)
		{
			return duration * (quality ? Settings.QualityScale : 1.0);
		}

		public static int EffectiveIgnitions(bool quality, int ignitions)
		{
			if(quality) return ignitions + (int)Math.Ceiling(ignitions * Settings.QualityScale * 0.2);
			return ignitions;
		}

		public static double RadiationDecay(bool quality, double rad, double elapsed_s, double rated_radiation, double radiation_decay_rate)
		{
			rad *= 3600.0;
			if (quality) rated_radiation *= Settings.QualityScale;
			if (rad <= 0 || rated_radiation <= 0 || rad < rated_radiation) return 0.0;

			rad -= rated_radiation;

			return rad * elapsed_s * radiation_decay_rate;
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			if (redundancy.Length > 0) specs.Add(Local.Reliability_info1, LocalizeRedundancyGroup(redundancy));//"Redundancy"
			specs.Add(Local.Reliability_info2, new CrewSpecs(repair).Info());//"Repair"

			

			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>"+Local.Reliability_info3 +"</color>");//Standard quality
			if(mtbf > 0) specs.Add(Local.Reliability_info4, Lib.HumanReadableDuration(EffectiveMTBF(false, mtbf)));//"MTBF"
			if (mtbf > 0 && rated_radiation > 0) specs.Add(Local.Reliability_info8, Lib.HumanReadableRadiation(rated_radiation / 3600.0));//"Radiation rating"

			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>"+Local.Reliability_info9 +"</color>");//High quality
			if (extra_cost > double.Epsilon) specs.Add(Local.Reliability_info10, Lib.HumanReadableCost(extra_cost * part.partInfo.cost));//"Extra cost"
			if (extra_mass > double.Epsilon) specs.Add(Local.Reliability_info11, Lib.HumanReadableMass(extra_mass * part.partInfo.partPrefab.mass));//"Extra mass"
			if (mtbf > 0) specs.Add(Local.Reliability_info4, Lib.HumanReadableDuration(EffectiveMTBF(true, mtbf)));//"MTBF"
			if (mtbf > 0 && rated_radiation > 0) specs.Add(Local.Reliability_info8, Lib.HumanReadableRadiation(Settings.QualityScale * rated_radiation / 3600.0));//"Radiation rating"

			return specs;
		}

		// module info support
		public string GetModuleTitle() { return Lib.BuildString(LocalizeTitle(title), " ", Local.Reliability_Reliability); }
		public override string GetModuleDisplayName() { return Lib.BuildString(LocalizeTitle(title), " ",Local.Reliability_Reliability); }//Reliability
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }


		// module cost support
		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return quality ? (float)extra_cost * part.partInfo.cost : 0.0f; }


		// module mass support
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return quality ? (float)extra_mass * part.partInfo.partPrefab.mass : 0.0f; }
		public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		void SetAlternatorsEnabled(bool enabled)
		{
			if (alternators == null) return;
			foreach (ModuleAlternator alt in alternators)
			{
				alt.enabled = enabled;
				alt.isEnabled = enabled;
			}
    }
		static bool GetRadiatorCoolingState(PartModule radiator, bool fallback)
		{
			if (radiator == null)
				return fallback;

			bool isCooling = IntegrationReflection.GetBool(radiator, "IsCooling", fallback);
			return radiator.moduleName == "USRadiatorSwitch"
				? IntegrationReflection.GetBool(radiator, "ActiveCooling", isCooling)
				: isCooling;
		}

		static void SetRadiatorCoolingState(PartModule radiator, bool isCooling)
		{
			if (radiator == null)
				return;

			IntegrationReflection.SetField(radiator, "IsCooling", isCooling);
			if (radiator.moduleName == "USRadiatorSwitch")
				IntegrationReflection.SetField(radiator, "ActiveCooling", isCooling);
		}

		// apply type-specific hacks to enable/disable the module
		protected void Apply(bool b)
		{
			if(type.StartsWith("ModuleEngines", StringComparison.Ordinal))
			{
				if (b)
				{
					foreach (PartModule m in modules)
					{
						var e = m as ModuleEngines;
						e.Shutdown();
						e.EngineIgnited = false;
						e.flameout = true;

						var efx = m as ModuleEnginesFX;
						if (efx != null)
						{
							efx.DeactivateRunningFX();
							efx.DeactivatePowerFX();
							efx.DeactivateLoopingFX();
						}
					}
				}

				// ModuleAlternator is a separate PartModule; disable it when the engine fails
				// so it stops producing ElectricCharge until repaired (#747)
				SetAlternatorsEnabled(!b);
			}

			switch (type)
			{
				case "ProcessController":
					foreach (PartModule m in modules)
					{
						(m as ProcessController).ReliablityEvent(b);
					}
					break;

				case "ModuleDeployableRadiator":
					if (b)
					{
						part.FindModelComponents<Animation>().ForEach(k => k.Stop());
					}
					break;

				case "USRadiatorSwitch":
					foreach (PartModule m in modules)
					{
						if (b && !radiator_state_stored)
						{
							radiator_was_cooling = GetRadiatorCoolingState(m, false);
							radiator_state_stored = true;
						}
						SetRadiatorCoolingState(m, b ? false : radiator_was_cooling);
					}
					foreach (SystemHeatRadiatorKerbalism wrapper in Lib.FindModules<SystemHeatRadiatorKerbalism>(part))
					{
						if (wrapper.radiatorModuleName != "USRadiatorSwitch")
							continue;

						if (b && !radiator_state_stored)
						{
							radiator_was_cooling = wrapper.IsCooling;
							radiator_state_stored = true;
						}
						wrapper.isEnabled = !b;
						wrapper.enabled = !b;
						wrapper.IsCooling = b ? false : radiator_was_cooling;
						foreach (PartModule nativeRadiator in wrapper.FindNativeRadiatorsForReliability())
						{
							if (b)
								wrapper.ClearRadiatorFluxForReliability(nativeRadiator);
							nativeRadiator.isEnabled = !b;
							nativeRadiator.enabled = !b;
							SetRadiatorCoolingState(nativeRadiator, b ? false : radiator_was_cooling);
						}
					}
					if (!b)
						radiator_state_stored = false;
					break;

				case "SystemHeatRadiatorKerbalism":
					// Reliability type is remapped to the Kerbalism sidecar; also shut down the
					// native SystemHeat / stock radiator so loaded vessels stop rejecting heat.
					foreach (PartModule m in modules)
					{
						SystemHeatRadiatorKerbalism wrapper = m as SystemHeatRadiatorKerbalism;
						if (wrapper == null)
							continue;

						List<PartModule> nativeRadiators = wrapper.FindNativeRadiatorsForReliability();
						PartModule nativeRadiator = nativeRadiators.Count > 0 ? nativeRadiators[0] : null;
						if (b && !radiator_state_stored)
						{
							radiator_was_cooling = nativeRadiator != null
								? GetRadiatorCoolingState(nativeRadiator, wrapper.IsCooling)
								: wrapper.IsCooling;
							radiator_state_stored = true;
						}

						foreach (PartModule radiator in nativeRadiators)
						{
							if (b)
								wrapper.ClearRadiatorFluxForReliability(radiator);
							radiator.isEnabled = !b;
							radiator.enabled = !b;
							SetRadiatorCoolingState(radiator, b ? false : radiator_was_cooling);
						}
						wrapper.IsCooling = b ? false : radiator_was_cooling;
					}
					if (!b)
						radiator_state_stored = false;
					break;

				case "ModuleLight":
					if (b)
					{
						foreach (PartModule m in modules)
						{
							ModuleLight l = m as ModuleLight;
							if (l.animationName.Length > 0)
							{
								new Animator(part, l.animationName).Still(0.0f);
							}
							else
							{
								part.FindModelComponents<Light>().ForEach(k => k.enabled = false);
							}
						}
					}
					break;

				case "ModuleRCSFX":
					if (b)
					{
						foreach (PartModule m in modules)
						{
							var e = m as ModuleRCSFX;
							if(e != null)
							{
								e.DeactivateFX();
								e.DeactivatePowerFX();
							}
						}
					}
					break;

				case "ModuleScienceExperiment":
					foreach (PartModule m in modules)
					{
						if (b)
							(m as ModuleScienceExperiment).SetInoperable();
						else
							(m as ModuleScienceExperiment).ResetExperiment();
					}
					break;

				case "Experiment":
					foreach (PartModule m in modules)
					{
						(m as Experiment).ReliablityEvent(b);
					}
					break;

				case "SolarPanelFixer":
					foreach (PartModule m in modules)
					{
						(m as SolarPanelFixer).ReliabilityEvent(b);
					}
					break;
			}

			API.Failure.Notify(part, type, b);
		}

		/// <summary>
		/// When SystemHeatRadiatorKerbalism fails unloaded, also disable its native radiator snapshot
		/// so packed vessels do not keep IsCooling / rejection state armed until reload.
		/// </summary>
		static void ProtoDisableSystemHeatNativeRadiators(ProtoPartSnapshot part)
		{
			// radiatorModuleName is not persistent on the wrapper; read from the part prefab.
			string radiatorModuleName = "ModuleSystemHeatRadiator";
			if (part.partPrefab != null)
			{
				SystemHeatRadiatorKerbalism prefabWrapper = part.partPrefab.FindModuleImplementingFast<SystemHeatRadiatorKerbalism>();
				if (prefabWrapper != null && !string.IsNullOrEmpty(prefabWrapper.radiatorModuleName))
					radiatorModuleName = prefabWrapper.radiatorModuleName;
			}

			foreach (ProtoPartModuleSnapshot wrapper in part.modules)
			{
				if (wrapper.moduleName == "SystemHeatRadiatorKerbalism")
					Lib.Proto.Set(wrapper, "IsCooling", false);
			}

			foreach (ProtoPartModuleSnapshot native in part.modules)
			{
				if (native.moduleName != radiatorModuleName
					&& !(radiatorModuleName == "ModuleSystemHeatRadiator" && native.moduleName == "ModuleActiveRadiator")
					&& !(radiatorModuleName == "ModuleActiveRadiator" && native.moduleName == "ModuleSystemHeatRadiator")
					&& !(radiatorModuleName == "USRadiatorSwitch" && native.moduleName == "ModuleSystemHeatRadiator"))
					continue;

				Lib.Proto.Set(native, "isEnabled", false);
				Lib.Proto.Set(native, "IsCooling", false);
				if (native.moduleName == "USRadiatorSwitch")
					Lib.Proto.Set(native, "ActiveCooling", false);
			}
		}

		void EnforceBrokenRadiatorState()
		{
			if (type == "USRadiatorSwitch")
			{
				foreach (SystemHeatRadiatorKerbalism wrapper in Lib.FindModules<SystemHeatRadiatorKerbalism>(part))
				{
					if (wrapper.radiatorModuleName != "USRadiatorSwitch")
						continue;

					wrapper.enabled = false;
					wrapper.isEnabled = false;
					wrapper.IsCooling = false;
					foreach (PartModule radiator in wrapper.FindNativeRadiatorsForReliability())
					{
						wrapper.ClearRadiatorFluxForReliability(radiator);
						radiator.enabled = false;
						radiator.isEnabled = false;
						SetRadiatorCoolingState(radiator, false);
					}
				}
			}
			else if (type == "SystemHeatRadiatorKerbalism")
			{
				foreach (PartModule module in modules)
				{
					SystemHeatRadiatorKerbalism wrapper = module as SystemHeatRadiatorKerbalism;
					if (wrapper == null)
						continue;

					wrapper.IsCooling = false;
					foreach (PartModule radiator in wrapper.FindNativeRadiatorsForReliability())
					{
						wrapper.ClearRadiatorFluxForReliability(radiator);
						radiator.enabled = false;
						radiator.isEnabled = false;
						SetRadiatorCoolingState(radiator, false);
					}
				}
			}
		}


		static void Incentive_redundancy(Vessel v, string redundancy)
		{
			if (v.loaded)
			{
				foreach (Reliability m in PartModuleCache.GetModules<Reliability>(v))
				{
					if (m.isEnabled && m.redundancy == redundancy)
					{
						m.next += m.next - m.last;
					}
				}
			}
			else
			{
				var PD = new Dictionary<string, Lib.Module_prefab_data>();

				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;
					PD.Clear();

					foreach (ProtoPartModuleSnapshot m in p.modules)
					{
						if (m.moduleName != "Reliability") continue;

						PartModule module_prefab = Lib.ModulePrefab(part_prefab.Modules, m.moduleName, PD);
						if (!module_prefab) continue;

						string r = Lib.Proto.GetString(m, "redundancy", string.Empty);
						if (r == redundancy)
						{
							double last = Lib.Proto.GetDouble(m, "last");
							double next = Lib.Proto.GetDouble(m, "next");
							Lib.Proto.Set(m, "next", next + (next - last));
						}
					}
				}
			}
		}


		// set highlighting
		static void Highlight(Part p)
		{
			if (p.vessel.KerbalismData().cfg_highlights)
			{
				// get state among all reliability components in the part
				bool broken = false;
				bool critical = false;
				foreach (Reliability m in Lib.FindModules<Reliability>(p))
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
			title = LocalizeTitle(title);
			if (v.KerbalismData().cfg_malfunction)
			{
				if (!critical)
				{
					Message.Post
					(
					  Severity.warning,
					  Local.Reliability_MessagePost24.Format("<b>"+title+"</b>","<b>"+v.vesselName+"</b>"),//Lib.BuildString(<<1>> malfunctioned on <<2>>)
					  Local.Reliability_MessagePost25//"We can still repair it"
					);
				}
				else
				{
					Message.Post
					(
					  Severity.danger,
					  Local.Reliability_MessagePost26.Format("<b>" + title + "</b>", "<b>" + v.vesselName + "</b>"),//Lib.BuildString(<<1>> failed on <<2>>)
					  Local.Reliability_MessagePost27//"It can still be repaired, but requires greater expertise"
					);
				}
			}
		}


		static void Safemode_msg(Vessel v, string title)
		{
			title = LocalizeTitle(title);
			Message.Post
			(
			  Local.Reliability_MessagePost28.Format("<b>" + title + "</b>", "<b>" + v.vesselName + "</b>"),//Lib.BuildString("There has been a problem with <<1>> on <<2>>)
			  Local.Reliability_MessagePost29//"We were able to fix it remotely, this time"
			);
		}


		// cause a part at random to malfunction
		public static void CauseMalfunction(Vessel v)
		{
			// if vessel is loaded
			if (v.loaded)
			{
				// choose a module at random
				var modules = PartModuleCache.GetModules<Reliability>(v).FindAll(k => k.isEnabled && !k.broken);
				if (modules.Count == 0) return;
				var m = modules[Lib.RandomInt(modules.Count)];

				// break it
				m.Break();
			}
			// if vessel is not loaded
			else
			{
				// choose a module at random
				var modules = ProtoPartModuleCache.GetModules(v.protoVessel, "Reliability").FindAll(k => !Lib.Proto.GetBool(k, "broken"));
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


		///<summary>evaluate the malfunction and critical failure state of a vessel in a single pass</summary>
		public static void GetVesselState(Vessel v, out bool malfunction, out bool critical)
		{
			malfunction = false;
			critical = false;

			if (v.loaded)
			{
				foreach (Reliability m in PartModuleCache.GetModules<Reliability>(v))
				{
					malfunction |= m.broken;
					critical |= m.critical;
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in ProtoPartModuleCache.GetModules(v.protoVessel, "Reliability"))
				{
					malfunction |= Lib.Proto.GetBool(m, "broken");
					critical |= Lib.Proto.GetBool(m, "critical");
				}
			}
		}


		public static string LocalizeRedundancyGroup(string group)
		{
			switch (group)
			{
				case "Life Support": return Local.Reliability_group_LifeSupport;
				case "Power Generation": return Local.Reliability_group_PowerGeneration;
				case "Attitude Control": return Local.Reliability_group_AttitudeControl;
				case "Landing": return Local.Reliability_group_Landing;
				case "Propulsion": return Local.Reliability_group_Propulsion;
				case "Communication": return Local.Reliability_group_Communication;
			}
			return group;
		}

		public static string LocalizeTitle(string title)
		{
			if (string.IsNullOrEmpty(title))
				return title;

			// Loc keys (#KERBALISM_...) — do not match English switch cases below
			if (title[0] == '#')
				return Localizer.Format(title);

			switch (title)
			{
				case "ECLSS": return Local.Reliability_title_ECLSS;
				case "Shield": return Local.Reliability_title_Shield;
				case "Solar Panel": return Local.Reliability_title_SolarPanel;
				case "Reaction Wheel": return Local.Reliability_title_ReactionWheel;
				case "RCS": return Local.Reliability_title_RCS;
				case "Light": return Local.Reliability_title_Light;
				case "Parachute": return Local.Reliability_title_Parachute;
				case "Engine": return Local.Reliability_title_Engine;
				case "Radiator": return Local.Reliability_title_Radiator;
				case "Radiator motor": return Local.Reliability_title_Radiatormotor;
				case "Radiator panel": return Local.Reliability_title_Radiatorpanel;
				case "Converter": return Local.Reliability_title_Converter;
				case "Harvester": return Local.Reliability_title_Harvester;
				case "ScienceInstrument": return Local.Reliability_title_ScienceInstrument;
				case "Data Transmitter": return Local.Reliability_title_DataTransmitter;
			}
			return Localizer.Format(title);
		}
	}


} // KERBALISM

