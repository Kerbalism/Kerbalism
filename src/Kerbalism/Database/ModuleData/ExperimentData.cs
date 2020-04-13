using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class ExperimentData : ModuleData<ModuleKsmExperiment, ExperimentData>
	{
		public enum ExpStatus { Stopped, Running, Forced, Waiting, Issue, Broken }
		public enum RunningState { Stopped, Running, Forced, Broken }

		#region FIELDS

		// persistence
		private ExperimentModuleDefinition moduleDefinition;
		private RunningState expState;
		private ExpStatus status;
		public bool shrouded;
		public double remainingSampleMass;

		// we use a nullable int here to be able to keep track of if that has already been assigned or not :
		// null => not assigned yet, will be assigned the first time the partmodule OnStart is called in flight
		// 0 => no private drive for that experiment
		// some value => the private drive moduledata flightid
		private int? privateDriveId = null;

		// this was persisted, but this doesn't seem necessary anymore.
		// At worst, there will be a handfull of fixedUpdate were the unloaded vessels won't have it
		// until they get their background update. Since this is now only used for UI purposes, this
		// isn't really a problem.
		public string issue = string.Empty;

		private SubjectData subject;
		private Situation situation;

		#endregion

		#region PROPERTIES

		public RunningState State
		{
			get => expState;
			set
			{
				expState = value;
				Status = GetStatus(value, Subject, issue);
			}
		}

		public ExpStatus Status
		{
			get => status;
			private set
			{
				if(status != value)
				{
					status = value;
					if(!Lib.IsEditor)
						API.OnExperimentStateChanged.Notify(((VesselData)partData.vesselData).VesselId, ExperimentID, status);
				}
			}
		}

		public SubjectData Subject => subject;

		public Situation Situation => situation;

		public ExperimentModuleDefinition ModuleDefinition => moduleDefinition;

		public string ExperimentID => ModuleDefinition.Info.ExperimentId;

		public string ExperimentTitle => ModuleDefinition.Info.Title;

		public int PrivateDriveFlightId => (int)privateDriveId;

		public bool IsExperimentRunning
		{
			get
			{
				switch (status)
				{
					case ExpStatus.Running:
					case ExpStatus.Forced:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsRunningRequested
		{
			get
			{
				switch (expState)
				{
					case RunningState.Running:
					case RunningState.Forced:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsBroken => expState == RunningState.Broken;

		#endregion

		#region LIFECYCLE

		public override void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule = null, ProtoPartSnapshot protoPart = null)
		{
			expState = RunningState.Stopped;
			status = ExpStatus.Stopped;
			shrouded = false;
			remainingSampleMass = 0.0;
			moduleDefinition = null;
			subject = null;

			if (!moduleIsEnabled)
				return;

			if (!TrySetupDefinition(modulePrefab.moduleDefinition))
				moduleIsEnabled = false;

			if(!Lib.IsEditor)
				API.OnExperimentStateChanged.Notify(((VesselData)partData.vesselData).VesselId, ExperimentID, status);
		}

		public bool TrySetupDefinition(string definitionId)
		{
			if (string.IsNullOrEmpty(definitionId))
				return false;

			moduleDefinition = ScienceDB.GetExperimentModuleDefinition(definitionId);

			if (moduleDefinition == null)
			{
				Lib.Log($"No MODULE_DEFINITION found with name `{definitionId}`, is your config broken?", Lib.LogLevel.Error);
				return false;
			}

			if (!moduleDefinition.SampleCollecting && moduleDefinition.Info.SampleMass > 0.0)
				remainingSampleMass = moduleDefinition.Info.SampleMass * moduleDefinition.Samples;

			return true;
		}

		public override void OnLoad(ConfigNode node)
		{
			string id = Lib.ConfigValue(node, "id", string.Empty);

			if (id == string.Empty)
			{
				moduleIsEnabled = false;
				return;
			}

			moduleDefinition = ScienceDB.GetExperimentModuleDefinition(id);

			if (moduleDefinition == null)
			{
				Lib.Log($"No MODULE_DEFINITION found with name `{id}`, is your config broken?", Lib.LogLevel.Error);
				moduleIsEnabled = false;
				return;
			}

			expState = Lib.ConfigEnum(node, "expState", RunningState.Stopped);
			status = Lib.ConfigEnum(node, "status", ExpStatus.Stopped);
			shrouded = Lib.ConfigValue(node, "shrouded", false);
			remainingSampleMass = Lib.ConfigValue(node, "sampleMass", 0.0);

			if (node.HasValue("privateDriveId"))
				privateDriveId = Lib.ConfigValue(node, "privateDriveId", 0);

			if (!Lib.IsEditor)
				API.OnExperimentStateChanged.Notify(((VesselData)partData.vesselData).VesselId, ExperimentID, status);
		}

		public override void OnSave(ConfigNode node)
		{
			if (moduleDefinition == null)
				return;

			node.AddValue("id", moduleDefinition.Name);
			node.AddValue("expState", expState);
			node.AddValue("status", status);
			node.AddValue("shrouded", shrouded);
			node.AddValue("sampleMass", remainingSampleMass);

			if (privateDriveId.HasValue)
				node.AddValue("privateDriveId", privateDriveId);
		}

		// note : since this called from the partmodule OnStart(), it won't work if the module is created unloaded
		// but that's probably a non-issue in practice.
		public void CheckPrivateDriveId()
		{
			if (!privateDriveId.HasValue && Lib.IsGameRunning)
			{
				foreach (ModuleKsmDrive driveModule in loadedModule.part.Modules.OfType<ModuleKsmDrive>())
				{
					if (driveModule.experiment_id == ExperimentID)
					{
						privateDriveId = driveModule.dataFlightId;
					}
				}

				if (!privateDriveId.HasValue)
				{
					privateDriveId = 0;
				}
			}
		}

		#endregion

		#region EVALUATION

		public override void OnVesselDataUpdate(VesselDataBase vd)
		{
			if (Lib.IsEditor)
				return;

			situation = ((VesselData)vd).VesselSituations.GetExperimentSituation(moduleDefinition.Info);
			subject = ScienceDB.GetSubjectData(moduleDefinition.Info, situation);
		}

		public void UpdateAfterExperimentUpdate()
		{
			Status = GetStatus(expState, subject, issue);
		}

		private ExpStatus GetStatus(RunningState state, SubjectData subject, string issue)
		{
			switch (state)
			{
				case RunningState.Broken:
					return ExpStatus.Broken;
				case RunningState.Stopped:
					return ExpStatus.Stopped;
				case RunningState.Running:
					if (issue.Length > 0) return ExpStatus.Issue;
					if (subject == null || subject.ScienceRemainingToCollect <= 0.0) return ExpStatus.Waiting;
					return ExpStatus.Running;
				case RunningState.Forced:
					if (issue.Length > 0) return ExpStatus.Issue;
					return ExpStatus.Forced;
				default:
					return ExpStatus.Stopped;
			}
		}


		#endregion
	}
}
