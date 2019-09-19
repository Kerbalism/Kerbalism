using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;
using KSP.Localization;
using System.Collections;


namespace KERBALISM
{

	public class Experiment : PartModule, ISpecifics, IPartMassModifier, IModuleRollout
	{
		// config
		[KSPField] public string experiment_id;               // id of associated experiment definition
		[KSPField] public string experiment_desc = string.Empty;  // some nice lines of text
		[KSPField] public double data_rate;                   // sampling rate in Mb/s
		[KSPField] public double ec_rate;                     // EC consumption rate per-second
		[KSPField] public float sample_mass = 0f;             // if set to anything but 0, the experiment is a sample.
		[KSPField] public float sample_reservoir = 0f;        // the amount of sampling mass this unit is shipped with
		[KSPField] public bool sample_collecting = false;     // if set to true, the experiment will generate mass out of nothing
		[KSPField] public bool allow_shrouded = true;         // true if data can be transmitted
		[KSPField] public string requires = string.Empty;     // additional requirements that must be met
		[KSPField] public string crew_operate = string.Empty; // operator crew. if set, crew has to be on vessel while recording
		[KSPField] public string crew_reset = string.Empty;   // reset crew. if set, experiment will stop recording after situation change
		[KSPField] public string crew_prepare = string.Empty; // prepare crew. if set, experiment will require crew to set up before it can start recording 
		[KSPField] public string resources = string.Empty;    // resources consumed by this experiment
		[KSPField] public bool hide_when_unavailable = false; // don't show UI when the experiment is unavailable

		// animations
		[KSPField] public string anim_deploy = string.Empty; // deploy animation
		[KSPField] public bool anim_deploy_reverse = false;

		[KSPField] public string anim_loop = string.Empty; // deploy animation
		[KSPField] public bool anim_loop_reverse = false;

		// persistence
		[KSPField(isPersistant = true)] public bool recording;
		[KSPField(isPersistant = true)] public string issue = string.Empty;
		[KSPField(isPersistant = true)] public string last_subject_id = string.Empty;
		[KSPField(isPersistant = true)] public bool didPrepare = false;
		[KSPField(isPersistant = true)] public bool shrouded = false;
		[KSPField(isPersistant = true)] public double remainingSampleMass = 0.0;
		[KSPField(isPersistant = true)] public bool broken = false;
		[KSPField(isPersistant = true)] public bool forcedRun = false;
		[KSPField(isPersistant = true)] public uint privateHdId = 0;

		private static readonly string insufficient_storage = "insufficient storage";

		private State state = State.STOPPED;
		// animations
		internal Animator deployAnimator;
		internal Animator loopAnimator;

		private CrewSpecs operator_cs;
		private CrewSpecs reset_cs;
		private CrewSpecs prepare_cs;
		private List<KeyValuePair<string, double>> resourceDefs;
		private double next_check = 0;

		public enum State
		{
			STOPPED = 0, WAITING, RUNNING, ISSUE
		}

		public static State GetState(ExperimentInfo expInfo, string issue, bool recording, bool forcedRun)
		{
			bool hasValue = expInfo.ScienceRemainingToCollect > 0f;
			bool smartScience = PreferencesScience.Instance.smartScience;

			if (issue.Length > 0) return State.ISSUE;
			if (!recording) return State.STOPPED;
			if (!hasValue && forcedRun) return State.RUNNING;
			if (!hasValue && smartScience) return State.WAITING;
			return State.RUNNING;
		}

		public override void OnLoad(ConfigNode node)
		{
			// build up science sample mass database
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				if (experiment_id == null)
				{
					Lib.Log("ERROR: EXPERIMENT WITHOUT EXPERIMENT_ID IN PART " + part);
				}
				else
				{
					Science.RegisterSampleMass(experiment_id, sample_mass);
				}
			}

			base.OnLoad(node);
		}

		/// <summary>Called by Callbacks just after rollout to launch pad</summary>
		public void OnRollout()
		{
			if (Lib.DisableScenario(this)) return;

			// initialize the remaining sample mass
			// this needs to be done only once just after launch
			if (!sample_collecting)
			{
				remainingSampleMass = sample_mass;
				if (sample_reservoir > float.Epsilon)
					remainingSampleMass = sample_reservoir;
			}
		}

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			if (last_subject_id == string.Empty) last_subject_id = experiment_id;

			// create animators
			deployAnimator = new Animator(part, anim_deploy);
			deployAnimator.reversed = anim_deploy_reverse;

			loopAnimator = new Animator(part, anim_loop);
			loopAnimator.reversed = anim_loop_reverse;

			// set initial animation states
			deployAnimator.Still(recording ? 1.0 : 0.0);
			loopAnimator.Still(recording ? 1.0 : 0.0);
			if (recording) loopAnimator.Play(false, true);

			// parse crew specs
			if(!string.IsNullOrEmpty(crew_operate))
				operator_cs = new CrewSpecs(crew_operate);
			if (!string.IsNullOrEmpty(crew_reset))
				reset_cs = new CrewSpecs(crew_reset);
			if (!string.IsNullOrEmpty(crew_prepare))
				prepare_cs = new CrewSpecs(crew_prepare);

			resourceDefs = ParseResources(resources);

			Events["Toggle"].guiActiveUncommand = true;
			Events["Toggle"].externalToEVAOnly = true;
			Events["Toggle"].requireFullControl = false;

			Events["Prepare"].guiActiveUncommand = true;
			Events["Prepare"].externalToEVAOnly = true;
			Events["Prepare"].requireFullControl = false;

			Events["Reset"].guiActiveUncommand = true;
			Events["Reset"].externalToEVAOnly = true;
			Events["Reset"].requireFullControl = false;

			if (Lib.IsFlight())
			{
				foreach (var hd in part.FindModulesImplementing<HardDrive>())
				{
					if (hd.experiment_id == experiment_id) privateHdId = part.flightID;
				}
			}
		}

		public static bool Done(ExperimentInfo exp) => exp.ScienceRemainingToCollect <= 0f;

		public static string DoneInfo(ExperimentInfo expInfo, double dataRate)
		{
			if (float.IsInfinity(expInfo.ScienceValue))
				return Lib.BuildString(
					Lib.Color("?", Lib.KColor.Science, true),
					" ", Lib.HumanReadableCountdown(dataRate * expInfo.MaxAmount));

			if (expInfo.PercentCollectedTotal < 1f)
				return Lib.BuildString(
					Lib.Color(Lib.BuildString(expInfo.ScienceCollectedTotal.ToString("F1"), "/", expInfo.ScienceValue.ToString("F1")), Lib.KColor.Science, true),
					" ", Lib.HumanReadableCountdown((expInfo.MaxAmount / dataRate) * Math.Max(1.0 - expInfo.PercentCollectedTotal, 0.0)));

			return Lib.BuildString(expInfo.PercentCollectedTotal.ToString("P0"), " done (",
				Lib.Color(expInfo.ScienceValue.ToString("F1"), Lib.KColor.Science, true),
				")");
		}

		public static string StateInfo(State state, ExperimentInfo expInfo, double dataRate, string issue)
		{
			switch (state)
			{
				case State.ISSUE:
					return Lib.Color(issue, Lib.KColor.Orange);
				case State.RUNNING:
					return Lib.BuildString("running, ", DoneInfo(expInfo, dataRate));
				case State.WAITING:
					return Lib.Color("waiting", Lib.KColor.Science);
				case State.STOPPED:
					return Lib.BuildString(Lib.Color(Localizer.Format("#KERBALISM_Generic_STOPPED"), Lib.KColor.Yellow), " ", DoneInfo(expInfo, dataRate));
			}
			return "error";
		}

		public void Update()
		{
			var expInfo = Science.Experiment(last_subject_id);

			// in flight
			if (Lib.IsFlight())
			{
				Vessel v = FlightGlobals.ActiveVessel;
				if (v == null || EVA.IsDead(v)) return;

				// do nothing if vessel is invalid
				if (!vessel.KerbalismIsValid()) return;

				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(
					Lib.Ellipsis(expInfo.Name, Styles.ScaleStringLength(15)),
					StateInfo(state, expInfo, data_rate, issue)
					);
				Events["Toggle"].active = (prepare_cs == null || didPrepare);

				Events["Prepare"].guiName = Lib.BuildString("Prepare <b>", expInfo.Name, "</b>");
				Events["Prepare"].active = !didPrepare && prepare_cs != null && string.IsNullOrEmpty(last_subject_id);

				Events["Reset"].guiName = Lib.BuildString("Reset <b>", expInfo.Name, "</b>");
				// we need a reset either if we have recorded data or did a setup
				bool resetActive = (reset_cs != null || prepare_cs != null) && !string.IsNullOrEmpty(last_subject_id);
				Events["Reset"].active = resetActive;

				if(issue.Length > 0 && hide_when_unavailable && issue != insufficient_storage)
				{
					Events["Toggle"].active = false;
				}
			}
			// in the editor
			else if (Lib.IsEditor())
			{
				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(expInfo.Name, recording ? "recording" : "stopped");
				Events["Reset"].active = false;
				Events["Prepare"].active = false;
			}
		}

		public void FixedUpdate()
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.FixedUpdate");

			if (!recording)
			{
				state = State.STOPPED;
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// basic sanity checks
			if (Lib.IsEditor())
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}
			if (!vessel.KerbalismIsValid())
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}
			if (next_check > Planetarium.GetUniversalTime())
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// get ec handler
			ResourceInfo ec = ResourceCache.GetResource(vessel, "ElectricCharge");
			shrouded = part.ShieldedFromAirstream;

			string new_subject_id;

			issue = TestForIssues(vessel, ec, this, privateHdId, broken,
				remainingSampleMass, didPrepare, shrouded, last_subject_id, out new_subject_id);

			if (new_subject_id != last_subject_id)
			{
				forcedRun = false;
				last_subject_id = new_subject_id;
			}

			if (string.IsNullOrEmpty(issue))
				issue = TestForResources(vessel, resourceDefs, Kerbalism.elapsed_s, ResourceCache.Get(vessel));

			if (!string.IsNullOrEmpty(issue))
			{
				next_check = Planetarium.GetUniversalTime() + Math.Max(3, Kerbalism.elapsed_s * 3);
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			ExperimentInfo expInfo = Science.Experiment(new_subject_id);

			state = GetState(expInfo, issue, recording, forcedRun);

			if (state != State.RUNNING)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// if experiment is active and there are no issues
			DoRecord(ec, new_subject_id, expInfo);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		private void DoRecord(ResourceInfo ec, string subject_id, ExperimentInfo expInfo)
		{
			var stored = DoRecord(this, subject_id, expInfo, vessel, ec, privateHdId,
				ResourceCache.Get(vessel), resourceDefs,
				remainingSampleMass, Kerbalism.elapsed_s,
				out remainingSampleMass);

			if (!stored) issue = insufficient_storage;
		}

		private static Drive GetDrive(Experiment experiment, Vessel vessel, uint hdId, double chunkSize, string subject_id)
		{
			bool isFile = experiment.sample_mass < float.Epsilon;
			Drive drive = null;
			if (hdId != 0) drive = DB.Drive(hdId);
			else drive = isFile ? Drive.FileDrive(vessel, chunkSize) : Drive.SampleDrive(vessel, chunkSize, subject_id);
			return drive;
		}

		private static bool DoRecord(Experiment experiment, string subject_id, ExperimentInfo expInfo, Vessel vessel, ResourceInfo ec, uint hdId, 
			VesselResources resources, List<KeyValuePair<string, double>> resourceDefs,
			double remainingSampleMass, double elapsed_s,
			out double remainingSampleMassOut)
		{
			// default output values for early returns
			remainingSampleMassOut = remainingSampleMass;

			//double elapsed_s = Kerbalism.elapsed_s;
			double chunkSize = Math.Min(experiment.data_rate * elapsed_s, expInfo.MaxAmount);
			double massDelta = experiment.sample_mass * chunkSize / expInfo.MaxAmount;

			Drive drive = GetDrive(experiment, vessel, hdId, chunkSize, subject_id);

			// on high time warp this chunk size could be too big, but we could store a sizable amount if we process less
			bool isFile = experiment.sample_mass < float.Epsilon;
			double maxCapacity = isFile ? drive.FileCapacityAvailable() : drive.SampleCapacityAvailable(subject_id);

			Drive warpCacheDrive = null;
			if(isFile) {
				if (drive.GetFileSend(subject_id)) warpCacheDrive = Cache.WarpCache(vessel);
				if (warpCacheDrive != null) maxCapacity += warpCacheDrive.FileCapacityAvailable();
			}

			double factor = Rate(vessel, chunkSize, maxCapacity, elapsed_s, ec, experiment.ec_rate, resources, resourceDefs);
			if (factor < double.Epsilon)
				return false;

			chunkSize *= factor;
			massDelta *= factor;
			elapsed_s *= factor;

			bool stored = false;
			if (chunkSize > 0.0)
			{
				if (isFile)
				{
					if (warpCacheDrive != null)
					{
						double s = Math.Min(chunkSize, warpCacheDrive.FileCapacityAvailable());
						stored = warpCacheDrive.Record_file(subject_id, s, true);

						if(chunkSize > s) // only write to persisted drive if the data cannot be transmitted in this tick
							stored &= drive.Record_file(subject_id, chunkSize - s, true);
						else if (!drive.files.ContainsKey(subject_id)) // if everything is transmitted, create an empty file so the player know what is happening
							drive.Record_file(subject_id, 0.0, true);
					}
					else
					{
						stored = drive.Record_file(subject_id, chunkSize, true);
					}
				}
				else
					stored = drive.Record_sample(subject_id, chunkSize, massDelta);
			}

			if (!stored)
				return false;

			// consume resources
			ec.Consume(experiment.ec_rate * elapsed_s, "experiment");
			foreach (var p in resourceDefs)
				resources.Consume(vessel, p.Key, p.Value * elapsed_s, "experiment");

			if (!experiment.sample_collecting)
			{
				remainingSampleMass -= massDelta;
				remainingSampleMass = Math.Max(remainingSampleMass, 0);
			}
			remainingSampleMassOut = remainingSampleMass;

			return true;
		}

		private static double Rate(Vessel v, double chunkSize, double maxCapacity, double elapsed, ResourceInfo ec, double ec_rate, VesselResources resources, List<KeyValuePair<string, double>> resourceDefs)
		{
			double result = Lib.Clamp(maxCapacity / chunkSize, 0.0, 1.0);
			result = Math.Min(result, Lib.Clamp(ec.Amount / (ec_rate * elapsed), 0.0, 1.0));

			foreach (var p in resourceDefs) {
				var ri = resources.GetResource(v, p.Key);
				result = Math.Min(result, Lib.Clamp(ri.Amount / (p.Value * elapsed), 0.0, 1.0));
			}

			return result;
		}

		private static List<KeyValuePair<string, double>> noResources = new List<KeyValuePair<string, double>>();
		internal static List<KeyValuePair<string, double>> ParseResources(string resources, bool logErros = false)
		{
			if (string.IsNullOrEmpty(resources)) return noResources;

			List<KeyValuePair<string, double>> defs = new List<KeyValuePair<string, double>>();
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;

			foreach (string s in Lib.Tokenize(resources, ','))
			{
				// definitions are Resource@rate
				var p = Lib.Tokenize(s, '@');
				if (p.Count != 2) continue;             // malformed definition
				string res = p[0];
				if (!reslib.Contains(res)) continue;    // unknown resource
				double rate = double.Parse(p[1]);
				if (res.Length < 1 || rate < double.Epsilon) continue;  // rate <= 0
				defs.Add(new KeyValuePair<string, double>(res, rate));
			}
			return defs;
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Experiment experiment, ResourceInfo ec, VesselResources resources, double elapsed_s)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate");
			bool recording = Lib.Proto.GetBool(m, "recording", false);

			if (!recording)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			bool didPrepare = Lib.Proto.GetBool(m, "didPrepare", false);
			bool shrouded = Lib.Proto.GetBool(m, "shrouded", false);
			string last_subject_id = Lib.Proto.GetString(m, "last_subject_id", "");
			double remainingSampleMass = Lib.Proto.GetDouble(m, "remainingSampleMass", 0);
			bool broken = Lib.Proto.GetBool(m, "broken", false);
			bool forcedRun = Lib.Proto.GetBool(m, "forcedRun", false);
			
			uint privateHdId = Lib.Proto.GetUInt(m, "privateHdId", 0);

			string new_subject_id;

			string issue = TestForIssues(v, ec, experiment, privateHdId, broken,
				remainingSampleMass, didPrepare, shrouded, last_subject_id, out new_subject_id);

			if (new_subject_id != last_subject_id)
			{
				Lib.Proto.Set(m, "forcedRun", false);
				Lib.Proto.Set(m, "last_subject_id", new_subject_id);
			}

			if (string.IsNullOrEmpty(issue))
				issue = TestForResources(v, ParseResources(experiment.resources), elapsed_s, resources);

			Lib.Proto.Set(m, "issue", issue);

			if (!string.IsNullOrEmpty(issue))
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}	
			
			ExperimentInfo expInfo = Science.Experiment(new_subject_id);

			State state = GetState(expInfo, issue, recording, forcedRun);
			if (state != State.RUNNING)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			var stored = DoRecord(experiment, new_subject_id, expInfo, v, ec, privateHdId,
				resources, ParseResources(experiment.resources),
				remainingSampleMass, elapsed_s,
				out remainingSampleMass);
			if (!stored) Lib.Proto.Set(m, "issue", insufficient_storage);

			Lib.Proto.Set(m, "remainingSampleMass", remainingSampleMass);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		internal static double RestoreSampleMass(double restoredAmount, ProtoPartModuleSnapshot m, string id)
		{
			var broken = Lib.Proto.GetBool(m, "broken", false);
			if (broken) return 0;

			var experiment_id = Lib.Proto.GetString(m, "experiment_id", string.Empty);
			if (experiment_id != id) return 0;

			var sample_collecting = Lib.Proto.GetBool(m, "sample_collecting", false);
			if (sample_collecting) return 0;

			double remainingSampleMass = Lib.Proto.GetDouble(m, "remainingSampleMass", 0);
			double sample_reservoir = Lib.Proto.GetDouble(m, "sample_reservoir", 0);
			if (remainingSampleMass >= sample_reservoir) return 0;

			double delta = Math.Max(restoredAmount, sample_reservoir - remainingSampleMass);
			remainingSampleMass += delta;
			remainingSampleMass = Math.Min(sample_reservoir, remainingSampleMass);
			Lib.Proto.Set(m, "remainingSampleMass", remainingSampleMass);
			return delta;
		}

		internal double RestoreSampleMass(double restoredAmount, string id)
		{
			if (broken) return 0;
			if (sample_collecting || experiment_id != id) return 0;
			if (remainingSampleMass >= sample_reservoir) return 0;
			double delta = Math.Max(restoredAmount, sample_reservoir - remainingSampleMass);
			remainingSampleMass += delta;
			remainingSampleMass = Math.Min(sample_reservoir, remainingSampleMass);
			return delta;
		}

		public void ReliablityEvent(bool breakdown)
		{
			broken = breakdown;
		}

		private static string TestForResources(Vessel v, List<KeyValuePair<string, double>> defs, double elapsed_s, VesselResources res)
		{
			if (defs.Count < 1) return string.Empty;

			// test if there are enough resources on the vessel
			foreach(var p in defs)
			{
				var ri = res.GetResource(v, p.Key);
				if (ri.Amount < p.Value * elapsed_s)
					return "missing " + ri.ResourceName;
			}

			return string.Empty;
		}

		private static string TestForIssues(Vessel v, ResourceInfo ec, Experiment experiment, uint hdId, bool broken,
			double remainingSampleMass, bool didPrepare, bool isShrouded, string last_subject_id, out string subject_id)
		{
			ExperimentSituation situation = Science.GetExperimentSituation(v);
			subject_id = Science.Generate_subject_id(experiment.experiment_id, v, situation);

			if (broken)
				return "broken";

			if (isShrouded && !experiment.allow_shrouded)
				return "shrouded";
			
			bool needsReset = experiment.crew_reset.Length > 0
				&& !string.IsNullOrEmpty(last_subject_id) && subject_id != last_subject_id;
			if (needsReset) return "reset required";

			if (ec.Amount < double.Epsilon && experiment.ec_rate > double.Epsilon)
				return "no Electricity";
			
			if (!string.IsNullOrEmpty(experiment.crew_operate))
			{
				var cs = new CrewSpecs(experiment.crew_operate);
				if (!cs && Lib.CrewCount(v) > 0) return "crew on board";
				else if (cs && !cs.Check(v)) return cs.Warning();
			}

			if (!experiment.sample_collecting && remainingSampleMass < double.Epsilon && experiment.sample_mass > double.Epsilon)
				return "depleted";

			if (!didPrepare && !string.IsNullOrEmpty(experiment.crew_prepare))
				return "not prepared";

			string situationIssue = Science.TestRequirements(subject_id, experiment.experiment_id, experiment.requires, v, situation);
			if (situationIssue.Length > 0)
				return Science.RequirementText(situationIssue);

			var experimentSize = Science.Experiment(subject_id).MaxAmount;
			double chunkSize = Math.Min(experiment.data_rate * Kerbalism.elapsed_s, experimentSize);
			Drive drive = GetDrive(experiment, v, hdId, chunkSize, subject_id);

			var isFile = experiment.sample_mass < double.Epsilon;
			double available;
			if(isFile) {
				available = drive.FileCapacityAvailable();
				available += Cache.WarpCache(v).FileCapacityAvailable();
			} else {
				available = drive.SampleCapacityAvailable(subject_id);
			}

			if (Math.Min(experiment.data_rate * Kerbalism.elapsed_s, experimentSize) > available)
				return insufficient_storage;

			return string.Empty;
		}

		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
		public void Prepare()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v))
				return;

			if (prepare_cs == null)
				return;

			// check trait
			if (!prepare_cs.Check(v))
			{
				Message.Post(
				  Lib.TextVariant
				  (
					"I'm not qualified for this",
					"I will not even know where to start",
					"I'm afraid I can't do that"
				  ),
				  reset_cs.Warning()
				);
			}

			didPrepare = true;

			Message.Post(
			  "Preparation Complete",
			  Lib.TextVariant
			  (
				"Ready to go",
				"Let's start doing some science!"
			  )
			);
		}

		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
		public void Reset()
		{
			Reset(true);
		}

		public bool Reset(bool showMessage)
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v))
				return false;

			if (reset_cs == null)
				return false;

			// check trait
			if (!reset_cs.Check(v))
			{
				if(showMessage)
				{
					Message.Post(
					  Lib.TextVariant
					  (
						"I'm not qualified for this",
						"I will not even know where to start",
						"I'm afraid I can't do that"
					  ),
					  reset_cs.Warning()
					);
				}
				return false;
			}

			last_subject_id = string.Empty;
			didPrepare = false;

			if(showMessage)
			{
				Message.Post(
				  "Reset Done",
				  Lib.TextVariant
				  (
					"It's good to go again",
					"Ready for the next bit of science"
				  )
				);
			}
			return true; 
		}

		private bool IsExperimentRunningOnVessel()
		{
			foreach(var e in vessel.FindPartModulesImplementing<Experiment>())
			{
				if (e.enabled && e.experiment_id == experiment_id && e.recording) return true;
			}
			return false;
		}

		public static void PostMultipleRunsMessage(string title, string vesselName)
		{
			Message.Post(Lib.Color("ALREADY RUNNING", Lib.KColor.Orange, true), "Can't start " + title + " a second time on vessel " + vesselName);
		}

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			if(Lib.IsEditor())
			{
				if(!recording)
				{
					recording = EditorTracker.Instance.AllowStart(this);
					if (!recording) PostMultipleRunsMessage(Science.Experiment(experiment_id).Name, "");
				}
				else
					recording = !recording;
				
				deployAnimator.Play(!recording, false);
				GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
				return;
			}

			if (Lib.IsFlight() && !vessel.IsControllable)
				return;

			if (state == State.WAITING)
			{
				forcedRun = true;
				recording = true;
				return;
			}

			if (deployAnimator.Playing())
				return; // nervous clicker? wait for it, goddamnit.

			var previous_recording = recording;

			// The same experiment must run only once on a vessel
			if (!recording)
			{
				recording = !IsExperimentRunningOnVessel();
				if(!recording) PostMultipleRunsMessage(Science.Experiment(experiment_id).Name, vessel.vesselName);
			}
			else
				recording = false;

			if (!recording)
			{
				forcedRun = false;
			}

			var new_recording = recording;
			recording = previous_recording;

			if(previous_recording != new_recording)
			{
				if(!new_recording)
				{
					// stop experiment

					// plays the deploy animation in reverse
					Action stop = delegate () { recording = false; deployAnimator.Play(true, false); };

					// wait for loop animation to stop before deploy animation
					if (loopAnimator.Playing())
						loopAnimator.Stop(stop);
					else
						stop.Invoke();
				}
				else
				{
					// start experiment

					// play the deploy animation, when it's done start the loop animation
					deployAnimator.Play(false, false, delegate () { recording = true; loopAnimator.Play(false, true); });
				}
			}
		}

		// action groups
		[KSPAction("Start")] public void StartAction(KSPActionParam param)
		{
			switch (GetState(Science.Experiment(last_subject_id), issue, recording, forcedRun)) {
				case State.STOPPED:
				case State.WAITING:
					Toggle();
					break;
			}
		}
		[KSPAction("Stop")] public void StopAction(KSPActionParam param) {
			if(recording) Toggle();
		}

		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}

		// specifics support
		public Specifics Specs()
		{
			var specs = new Specifics();
			var exp = Science.Experiment(experiment_id);
			if (exp == null)
			{
				specs.Add(Localizer.Format("#KERBALISM_ExperimentInfo_Unknown"));
				return specs;
			}

			specs.Add(Lib.BuildString("<b>", exp.Name, "</b>"));
			if(!string.IsNullOrEmpty(experiment_desc))
			{
				specs.Add(Lib.BuildString("<i>", experiment_desc, "</i>"));
			}
			
			specs.Add(string.Empty);
			double expSize = exp.MaxAmount;
			if (sample_mass < float.Epsilon)
			{
				specs.Add("Data", Lib.HumanReadableDataSize(expSize));
				specs.Add("Data rate", Lib.HumanReadableDataRate(data_rate));
				specs.Add("Duration", Lib.HumanReadableDuration(expSize / data_rate));
			}
			else
			{
				specs.Add("Sample size", Lib.HumanReadableSampleSize(expSize));
				specs.Add("Sample mass", Lib.HumanReadableMass(sample_mass));
				if(!sample_collecting && Math.Abs(sample_reservoir - sample_mass) > double.Epsilon && sample_mass > double.Epsilon)
					specs.Add("Experiments", "" + Math.Round(sample_reservoir / sample_mass, 0));
				specs.Add("Duration", Lib.HumanReadableDuration(expSize / data_rate));
			}

			List<string> situations = exp.Situations();
			if (situations.Count > 0)
			{
				specs.Add(string.Empty);
				specs.Add("<color=#00ffff>Situations:</color>", string.Empty);
				foreach (string s in situations) specs.Add(Lib.BuildString("• <b>", s, "</b>"));
			}

			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>Needs:</color>");

			specs.Add("EC", Lib.HumanReadableRate(ec_rate));
			foreach(var p in ParseResources(resources))
				specs.Add(p.Key, Lib.HumanReadableRate(p.Value));

			if (crew_prepare.Length > 0)
			{
				var cs = new CrewSpecs(crew_prepare);
				specs.Add("Preparation", cs ? cs.Info() : "none");
			}
			if (crew_operate.Length > 0)
			{
				var cs = new CrewSpecs(crew_operate);
				specs.Add("Operation", cs ? cs.Info() : "unmanned");
			}
			if (crew_reset.Length > 0)
			{
				var cs = new CrewSpecs(crew_reset);
				specs.Add("Reset", cs ? cs.Info() : "none");
			}

			if(!string.IsNullOrEmpty(requires))
			{
				specs.Add(string.Empty);
				specs.Add("<color=#00ffff>Requires:</color>", string.Empty);
				var tokens = Lib.Tokenize(requires, ',');
				foreach (string s in tokens) specs.Add(Lib.BuildString("• <b>", Science.RequirementText(s), "</b>"));
			}

			return specs;
		}

		// module mass support
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return (float)remainingSampleMass; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
	}

	internal class EditorTracker
	{
		private static EditorTracker instance;
		private readonly List<Experiment> experiments = new List<Experiment>();

		static EditorTracker()
		{
			if (instance == null)
				instance = new EditorTracker();
		}

		private EditorTracker()
		{
			if(instance == null) {
				instance = this;
				GameEvents.onEditorShipModified.Add(instance.ShipModified);
			}
		}

		internal void ShipModified(ShipConstruct construct)
		{
			experiments.Clear();
			foreach(var part in construct.Parts)
			{
				foreach (var experiment in part.FindModulesImplementing<Experiment>())
				{
					if (!experiment.enabled) experiment.recording = false;
					if (experiment.recording && !AllowStart(experiment))
					{
						// An experiment was added in recording state? Cheeky bugger!
						experiment.recording = false;
						experiment.deployAnimator.Still(0);
					}
					experiments.Add(experiment);
				}
			}
		}

		internal bool AllowStart(Experiment experiment)
		{
			foreach (var e in experiments)
				if (e.recording && e.experiment_id == experiment.experiment_id)
					return false;
			return true;
		}

		internal static EditorTracker Instance
		{
			get
			{
				if (instance == null)
					instance = new EditorTracker();
				return instance;
			}
		}
	}
} // KERBALISM
