using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM.EngineFailures
{
	public class EngineFailures : PartModule, ISpecifics, IModuleInfo, IPartCostModifier, IPartMassModifier
	{
		[KSPField] public string repair = string.Empty;
		[KSPField] public string title = string.Empty;
		[KSPField] public string redundancy = string.Empty;
		[KSPField] public double extra_cost;
		[KSPField] public double extra_mass;

		[KSPField] public bool engine_reliability_auto = false;
		[KSPField] public string engine_reliability_family = "auto";
		[KSPField] public double turnon_failure_probability = -1;
		[KSPField] public double rated_operation_duration = -1;
		[KSPField] public int rated_ignitions = -1;

		[KSPField(isPersistant = true)] public bool broken;
		[KSPField(isPersistant = true)] public bool critical;
		[KSPField(isPersistant = true)] public bool quality;
		[KSPField(isPersistant = true)] public double last = 0.0;
		[KSPField(isPersistant = true)] public double next = 0.0;
		[KSPField(isPersistant = true)] public double last_inspection = 0.0;
		[KSPField(isPersistant = true)] public bool needMaintenance = false;
		[KSPField(isPersistant = true)] public bool enforce_breakdown = false;
		[KSPField(isPersistant = true)] public bool running = false;
		[KSPField(isPersistant = true)] public double operation_duration = 0.0;
		[KSPField(isPersistant = true)] public double fail_duration = 0.0;
		[KSPField(isPersistant = true)] public int ignitions = 0;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Reliability", groupDisplayName = "#KERBALISM_Group_Reliability")]
		public string Status;

		List<PartModule> modules;
		List<ModuleAlternator> alternators;
		CrewSpecs repair_cs;
		bool explode = false;
		bool engineReliabilityRatingsApplied;

		public override void OnStart(StartState state)
		{
			if (Lib.DisableScenario(this)) return;

			EnsureRatings();
			Fields["Status"].guiName = Reliability.LocalizeTitle(title);

			if (!Lib.IsFlight()) return;

			if (last_inspection <= 0) last_inspection = Planetarium.GetUniversalTime();

			modules = new List<PartModule>();
			foreach (ModuleEngines engine in part.FindModulesImplementing<ModuleEngines>())
				modules.Add(engine);
			alternators = part.FindModulesImplementing<ModuleAlternator>();

			repair_cs = new CrewSpecs(repair);

			Events["Inspect"].guiName = Local.Reliability_Inspect.Format("<b>" + Reliability.LocalizeTitle(title) + "</b>");
			Events["Repair"].guiName = Local.Reliability_Repair.Format("<b>" + Reliability.LocalizeTitle(title) + "</b>");

			if (broken)
			{
				foreach (PartModule m in modules)
				{
					m.enabled = false;
					m.isEnabled = false;
				}
				SetAlternatorsEnabled(false);
			}

			if (broken) StartCoroutine(DeferredApply());
		}

		public IEnumerator DeferredApply()
		{
			yield return new WaitUntil(() => !vessel.packed);
			if (broken)
				Apply(true);
		}

		protected bool IgnitionCheck()
		{
			if (!PreferencesEngineFailures.Instance.engineFailures)
				return false;

			if (Time.time < Kerbalism.gameLoadTime + 3)
				return false;

			ignitions++;
			vessel.KerbalismData().ResetReliabilityStatus();

			bool fail = false;
			bool launchpad = vessel.situation == Vessel.Situations.PRELAUNCH || ignitions <= 1 && vessel.situation == Vessel.Situations.LANDED;

			if (turnon_failure_probability > 0)
			{
				var q = quality ? Settings.QualityScale : 1.0;
				if (launchpad) q /= 2.5;

				q += Lib.Clamp(ignitions - 1, 0.0, 6.0) / 20.0;

				if (Lib.RandomDouble() < (turnon_failure_probability * PreferencesEngineFailures.Instance.ignitionFailureChance) / q)
					fail = true;
			}

			if (rated_ignitions > 0)
			{
				int total_ignitions = Reliability.EffectiveIgnitions(quality, rated_ignitions);
				if (ignitions >= total_ignitions * 0.9) needMaintenance = true;
				if (ignitions > total_ignitions)
				{
					var q = (quality ? Settings.QualityScale : 1.0) * Lib.RandomDouble();
					q /= PreferencesEngineFailures.Instance.ignitionFailureChance;
					q /= (ignitions - total_ignitions);

					if (q < 0.3)
						fail = true;
				}
			}

			if (fail)
			{
				enforce_breakdown = true;
				explode = Lib.RandomDouble() < 0.1;

				next = Planetarium.GetUniversalTime() + Lib.RandomDouble() * 2.0;

				var fuelSystemFailureProbability = 0.1;
				if (launchpad) fuelSystemFailureProbability = 0.5;

				if (Lib.RandomDouble() < fuelSystemFailureProbability)
				{
					explode = true;
					next += Lib.RandomDouble() * 10 + 4;
					FlightLogger.fetch?.LogEvent(Local.FlightLogger_Destruction.Format(part.partInfo.title));
				}
				else
				{
					FlightLogger.fetch?.LogEvent(Local.FlightLogger_Ignition.Format(part.partInfo.title));
				}
			}
			return fail;
		}

		public void Update()
		{
			if (Lib.IsFlight())
			{
				if (broken)
				{
					foreach (PartModule m in modules)
					{
						m.enabled = false;
						m.isEnabled = false;
					}
					SetAlternatorsEnabled(false);
				}

				if (part.IsPAWVisible())
				{
					Status = string.Empty;

					if (broken)
					{
						Status = critical ? Lib.Color(Local.Reliability_criticalfailure, Lib.Kolor.Red) : Lib.Color(Local.Reliability_malfunction, Lib.Kolor.Yellow);
					}
					else if (PreferencesEngineFailures.Instance.engineFailures && (rated_operation_duration > 0 || rated_ignitions > 0))
					{
						if (rated_operation_duration > 0)
						{
							double effective_duration = Reliability.EffectiveDuration(quality, rated_operation_duration);
							Status = Lib.BuildString(Local.Reliability_burnremaining, " ", Lib.HumanReadableDuration(Math.Max(0, effective_duration - operation_duration)));
						}
						if (rated_ignitions > 0)
						{
							int effective_ignitions = Reliability.EffectiveIgnitions(quality, rated_ignitions);
							Status = Lib.BuildString(Status,
								(string.IsNullOrEmpty(Status) ? "" : ", "),
								Local.Reliability_ignitions, " ", Math.Max(0, effective_ignitions - ignitions).ToString());
						}
					}

					if (string.IsNullOrEmpty(Status)) Status = Local.Generic_NOMINAL;

					Events["Inspect"].active = !broken && !needMaintenance;
					Events["Repair"].active = repair_cs && (broken || needMaintenance);

					if (needMaintenance)
						Events["Repair"].guiName = Local.Reliability_Service.Format("<b>" + Reliability.LocalizeTitle(title) + "</b>");
				}

				RunningCheck();

				var now = Planetarium.GetUniversalTime();
				if (next > 0 && now > next && !broken)
					Break();

				Highlight(part);
			}
			else if (part.IsPAWVisible())
			{
				Events["Quality"].guiName = Lib.StatusToggle(Local.Reliability_qualityinfo.Format("<b>" + Reliability.LocalizeTitle(title) + "</b>"), quality ? Local.Reliability_qualityhigh : Local.Reliability_qualitystandard);

				Status = string.Empty;

				if (rated_operation_duration > 0 && PreferencesEngineFailures.Instance.engineFailures)
				{
					double effective_duration = Reliability.EffectiveDuration(quality, rated_operation_duration);
					Status = Lib.BuildString(Status,
						(string.IsNullOrEmpty(Status) ? "" : ", "),
						Local.Reliability_Burntime + " ",
						Lib.HumanReadableDuration(effective_duration));
				}

				if (rated_ignitions > 0 && PreferencesEngineFailures.Instance.engineFailures)
				{
					int effective_ignitions = Reliability.EffectiveIgnitions(quality, rated_ignitions);
					Status = Lib.BuildString(Status,
						(string.IsNullOrEmpty(Status) ? "" : ", "),
						Local.Reliability_ignitions + " ", effective_ignitions.ToString());
				}
			}
		}

		protected double nextRunningCheck = 0.0;
		protected double lastRunningCheck = 0.0;

		protected void RunningCheck()
		{
			if (!PreferencesEngineFailures.Instance.engineFailures) return;

			if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > 1)
			{
				lastRunningCheck = 0;
				return;
			}

			if (broken || enforce_breakdown || turnon_failure_probability <= 0 && rated_operation_duration <= 0) return;
			double now = Planetarium.GetUniversalTime();
			if (now < nextRunningCheck) return;
			nextRunningCheck = now + 0.5;

			if (!running)
			{
				if (IsRunning())
				{
					running = true;
					if (IgnitionCheck())
						Break();
				}
			}
			else
			{
				running = IsRunning();
			}

			if (running && rated_operation_duration > 1 && lastRunningCheck > 0)
			{
				var duration = now - lastRunningCheck;
				operation_duration += duration;
				vessel.KerbalismData().ResetReliabilityStatus();

				if (fail_duration <= 0)
				{
					int r = 8;
					var g = 0.4;

					var f = rated_operation_duration;
					if (quality) f *= Settings.QualityScale;

					f /= PreferencesEngineFailures.Instance.engineOperationFailureChance;

					var p = Math.Pow(Lib.RandomDouble(), r);
					p = 1 - p;

					var guaranteed_operation = f * g;
					fail_duration = guaranteed_operation + f * p;
				}

				if (fail_duration < operation_duration)
				{
					next = now;
					enforce_breakdown = true;
					explode = Lib.RandomDouble() < 0.2;
					FlightLogger.fetch?.LogEvent(Local.FlightLogger_MaterialFatigue.Format(part.partInfo.title));
				}
			}

			lastRunningCheck = now;
		}

		[KSPEvent(guiActiveEditor = true, guiName = "_", active = true, groupName = "Reliability", groupDisplayName = "#KERBALISM_Group_Reliability")]
		public void Quality()
		{
			quality = !quality;

			foreach (Part p in part.symmetryCounterparts)
			{
				EngineFailures ef = p.Modules[part.Modules.IndexOf(this)] as EngineFailures;
				if (ef != null)
					ef.quality = !ef.quality;
			}

			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		[KSPEvent(guiActiveUnfocused = true, unfocusedRange = 3.5f, guiName = "_", active = false, groupName = "Reliability", groupDisplayName = "#KERBALISM_Group_Reliability")]
		public void Inspect()
		{
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null) return;

			if (rated_ignitions > 0 && ignitions >= Math.Ceiling(Reliability.EffectiveIgnitions(quality, rated_ignitions) * 0.4)) needMaintenance = true;
			if (rated_operation_duration > 0 && operation_duration >= Reliability.EffectiveDuration(quality, rated_operation_duration) * 0.4) needMaintenance = true;

			v.KerbalismData().ResetReliabilityStatus();

			if (!needMaintenance)
			{
				last_inspection = Planetarium.GetUniversalTime();
				Message.Post(Lib.TextVariant(
					Local.Reliability_MessagePost1,
					Local.Reliability_MessagePost2,
					Local.Reliability_MessagePost3,
					Local.Reliability_MessagePost4,
					Local.Reliability_MessagePost5
				));
			}
			else
			{
				Message.Post(Lib.TextVariant(
					Local.Reliability_MessagePost6,
					Local.Reliability_MessagePost7,
					Local.Reliability_MessagePost8,
					Local.Reliability_MessagePost9,
					Local.Reliability_MessagePost10
				));
			}
		}

		[KSPEvent(guiActiveUnfocused = true, unfocusedRange = 3.5f, guiName = "_", active = false, groupName = "Reliability", groupDisplayName = "#KERBALISM_Group_Reliability")]
		public void Repair()
		{
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null) return;

			CrewSpecs requiredRepairCrew = critical ? repair_cs.ElevatedForCritical() : repair_cs;
			if (!requiredRepairCrew.Check(v))
			{
				Message.Post
				(
				  Lib.TextVariant
				  (
					Local.Reliability_MessagePost11,
					Local.Reliability_MessagePost12,
					Local.Reliability_MessagePost13
				  ),
				  requiredRepairCrew.Warning()
				);
				return;
			}

			needMaintenance = false;
			enforce_breakdown = false;

			last = 0.0;
			next = 0.0;
			lastRunningCheck = 0;
			last_inspection = Planetarium.GetUniversalTime();

			operation_duration = 0;
			ignitions = 0;
			fail_duration = 0;
			vessel.KerbalismData().ResetReliabilityStatus();

			if (broken)
			{
				int repairKitCost = critical ? 2 : 1;
				if (!Reliability.ConsumeRepairKits(v, Reliability.LocalizeTitle(title), repairKitCost)) return;

				broken = false;
				critical = false;

				foreach (PartModule m in modules)
				{
					m.isEnabled = true;
					m.enabled = true;
				}

				Apply(false);

				part.FindModulesImplementing<Configure>().ForEach(k => k.DoConfigure());

				Message.Post
				(
				  Local.Reliability_MessagePost14.Format("<b>" + Reliability.LocalizeTitle(title) + "</b>"),
				  Lib.TextVariant
				  (
					Local.Reliability_MessagePost15,
					Local.Reliability_MessagePost16,
					Local.Reliability_MessagePost17,
					Local.Reliability_MessagePost18
				  )
				);
			}
			else
			{
				Message.Post
				(
				  Local.Reliability_MessagePost19.Format("<b>" + Reliability.LocalizeTitle(title) + "</b>"),
				  Lib.TextVariant
				  (
					Local.Reliability_MessagePost20,
					Local.Reliability_MessagePost21,
					Local.Reliability_MessagePost22,
					Local.Reliability_MessagePost23
				  )
				);
			}
		}

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

			if (enforce_breakdown || vessel.KerbalismData().CrewCapacity > 0 || Lib.RandomDouble() > PreferencesReliability.Instance.safeModeChance)
			{
				broken = true;
				critical = Lib.RandomDouble() < PreferencesReliability.Instance.criticalChance;

				foreach (PartModule m in modules)
				{
					m.isEnabled = false;
					m.enabled = false;
				}

				Apply(true);
				Broken_msg(vessel, title, critical);
			}
			else
			{
				last = 0.0;
				next = 0.0;
				Safemode_msg(vessel, title);
			}

			if (PreferencesReliability.Instance.incentiveRedundancy)
				Incentive_redundancy(vessel, redundancy);
		}

		public override string GetInfo()
		{
			return Specs().Info();
		}

		public Specifics Specs()
		{
			EnsureRatings();
			Specifics specs = new Specifics();
			if (redundancy.Length > 0) specs.Add(Local.Reliability_info1, Reliability.LocalizeRedundancyGroup(redundancy));
			specs.Add(Local.Reliability_info2, new CrewSpecs(repair).Info());

			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>" + Local.Reliability_info3 + "</color>");
			if (turnon_failure_probability > 0) specs.Add(Local.Reliability_info5, Lib.HumanReadablePerc(turnon_failure_probability, "F1"));
			if (rated_operation_duration > 0) specs.Add(Local.Reliability_info6, Lib.HumanReadableDuration(Reliability.EffectiveDuration(false, rated_operation_duration)));
			if (rated_ignitions > 0) specs.Add(Local.Reliability_info7, Reliability.EffectiveIgnitions(false, rated_ignitions).ToString());

			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>" + Local.Reliability_info9 + "</color>");
			if (extra_cost > double.Epsilon) specs.Add(Local.Reliability_info10, Lib.HumanReadableCost(extra_cost * part.partInfo.cost));
			if (extra_mass > double.Epsilon) specs.Add(Local.Reliability_info11, Lib.HumanReadableMass(extra_mass * part.partInfo.partPrefab.mass));
			if (turnon_failure_probability > 0) specs.Add(Local.Reliability_info5, Lib.HumanReadablePerc(turnon_failure_probability / Settings.QualityScale, "F1"));
			if (rated_operation_duration > 0) specs.Add(Local.Reliability_info6, Lib.HumanReadableDuration(Reliability.EffectiveDuration(true, rated_operation_duration)));
			if (rated_ignitions > 0) specs.Add(Local.Reliability_info7, Reliability.EffectiveIgnitions(true, rated_ignitions).ToString());

			return specs;
		}

		internal void EnsureRatings()
		{
			if (engineReliabilityRatingsApplied)
				return;
			engineReliabilityRatingsApplied = EngineReliabilityHeuristics.Apply(this);
		}

		public string GetModuleTitle() { return Lib.BuildString(Reliability.LocalizeTitle(title), " ", Local.Reliability_Reliability); }
		public override string GetModuleDisplayName() { return Lib.BuildString(Reliability.LocalizeTitle(title), " ", Local.Reliability_Reliability); }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return quality ? (float)extra_cost * part.partInfo.cost : 0.0f; }
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return quality ? (float)extra_mass * part.partInfo.partPrefab.mass : 0.0f; }
		public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		protected bool IsRunning()
		{
			foreach (PartModule m in modules)
			{
				var e = m as ModuleEngines;
				return e.currentThrottle > 0 && e.EngineIgnited && e.resultingThrust > 0;
			}
			return false;
		}

		void SetAlternatorsEnabled(bool enabled)
		{
			if (alternators == null) return;
			foreach (ModuleAlternator alt in alternators)
			{
				alt.enabled = enabled;
				alt.isEnabled = enabled;
			}
		}

		protected void Apply(bool b)
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

			SetAlternatorsEnabled(!b);
			API.Failure.Notify(part, "EngineFailures", b);
		}

		static void Incentive_redundancy(Vessel v, string redundancyGroup)
		{
			foreach (EngineFailures m in PartModuleCache.GetModules<EngineFailures>(v))
			{
				if (m.isEnabled && m.redundancy == redundancyGroup)
					m.next += m.next - m.last;
			}
		}

		static void Highlight(Part p)
		{
			if (!p.vessel.KerbalismData().cfg_highlights) return;

			bool brokenState = false;
			bool criticalState = false;
			foreach (EngineFailures m in p.FindModulesImplementing<EngineFailures>())
			{
				brokenState |= m.broken;
				criticalState |= m.critical;
			}

			if (brokenState)
				Highlighter.Set(p.flightID, !criticalState ? Color.yellow : Color.red);
		}

		static void Broken_msg(Vessel v, string componentTitle, bool isCritical)
		{
			componentTitle = Reliability.LocalizeTitle(componentTitle);
			if (!v.KerbalismData().cfg_malfunction) return;

			if (!isCritical)
			{
				Message.Post
				(
				  Severity.warning,
				  Local.Reliability_MessagePost24.Format("<b>" + componentTitle + "</b>", "<b>" + v.vesselName + "</b>"),
				  Local.Reliability_MessagePost25
				);
			}
			else
			{
				Message.Post
				(
				  Severity.danger,
				  Local.Reliability_MessagePost26.Format("<b>" + componentTitle + "</b>", "<b>" + v.vesselName + "</b>"),
				  Local.Reliability_MessagePost27
				);
			}
		}

		static void Safemode_msg(Vessel v, string componentTitle)
		{
			componentTitle = Reliability.LocalizeTitle(componentTitle);
			Message.Post
			(
			  Local.Reliability_MessagePost28.Format("<b>" + componentTitle + "</b>", "<b>" + v.vesselName + "</b>"),
			  Local.Reliability_MessagePost29
			);
		}
	}
}
