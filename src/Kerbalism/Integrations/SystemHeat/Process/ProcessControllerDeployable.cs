namespace KERBALISM
{
	public class ProcessControllerDeployable : ProcessController
	{
		[KSPField(isPersistant = true)] public bool deployed;
		[KSPField] public string deployModuleType = "";
		[KSPField] public bool requireDeploy = false;
		[KSPField] public string processID = "";

		private bool requiresDeploy;
		private bool waitingForDeployAnimation;
		private int deployAnimationSettleFrames;
		private const int DeployAnimationSettleFrames = 2;

		internal bool RequiresDeployGate() => requiresDeploy;

		public new void Start()
		{
			base.Start();
			InitializeDeployState();
		}

		private void InitializeDeployState()
		{
			requiresDeploy = requireDeploy && part.HasModuleImplementingFast<ModuleAnimationGroup>();
			if (!requiresDeploy || Lib.IsEditor())
				deployed = true;
			else
			{
				SyncDeployedFromAnimator();
				if (!deployed && running)
					base.SetRunning(false);
			}
		}

		private void SyncDeployedFromAnimator()
		{
			ModuleAnimationGroup animator = part.FindModuleImplementingFast<ModuleAnimationGroup>();
			if (animator == null)
				return;

			bool wasDeployed = deployed;
			if (animator.isDeployed)
			{
				AdvanceDeployWait(animator);
				deployed = IsAnimatorReadyForUse(animator);
			}
			else
			{
				waitingForDeployAnimation = false;
				deployAnimationSettleFrames = 0;
				deployed = false;
			}

			if (wasDeployed && !deployed && running)
				base.SetRunning(false);
		}

		internal bool IsDeployedForUse()
		{
			if (!DeployGateActive())
				return true;

			ModuleAnimationGroup animator = part.FindModuleImplementingFast<ModuleAnimationGroup>();
			return animator == null || IsAnimatorReadyForUse(animator);
		}

		internal void MarkDeployStarted()
		{
			if (!DeployGateActive())
				return;

			waitingForDeployAnimation = true;
			deployAnimationSettleFrames = DeployAnimationSettleFrames;
			deployed = false;
		}

		public override string GetModuleDisplayName()
		{
			if (!string.IsNullOrEmpty(deployModuleType))
				return deployModuleType;
			return base.GetModuleDisplayName();
		}

		private bool DeployGateActive() => requiresDeploy && !Lib.IsEditor();

		private bool IsAnimatorReadyForUse(ModuleAnimationGroup animator)
		{
			if (animator == null)
				return true;

			if (!animator.isDeployed)
				return false;

			if (deployAnimationSettleFrames > 0)
				return false;

			return !waitingForDeployAnimation || !DeployAnimationGate.IsDeployAnimationPlaying(animator);
		}

		private void AdvanceDeployWait(ModuleAnimationGroup animator)
		{
			if (deployAnimationSettleFrames > 0)
				deployAnimationSettleFrames--;

			if (waitingForDeployAnimation
				&& deployAnimationSettleFrames <= 0
				&& !DeployAnimationGate.IsDeployAnimationPlaying(animator))
				waitingForDeployAnimation = false;
		}

		public new void EnableModule()
		{
			if (!DeployGateActive())
				return;

			ModuleAnimationGroup animator = part.FindModuleImplementingFast<ModuleAnimationGroup>();
			deployed = IsAnimatorReadyForUse(animator);
		}

		public new void DisableModule()
		{
			if (!DeployGateActive())
				return;

			waitingForDeployAnimation = false;
			deployAnimationSettleFrames = 0;
			deployed = false;
			if (running)
				base.SetRunning(false);
		}

		public new bool ModuleIsActive()
		{
			return IsDeployedForUse() && !broken && running;
		}

		public new bool IsSituationValid() => true;

		internal void OnRunningChanged()
		{
			if (DeployGateActive() && !IsDeployedForUse() && running)
				base.SetRunning(false);
		}

		public new void Update()
		{
			if (DeployGateActive())
				SyncDeployedFromAnimator();

			if (!part.IsPAWVisible())
				return;

			bool toggleActive = DeployGateActive() ? IsDeployedForUse() && !broken : !broken;
			bool? dumpActive = Events["DumpValve"].active
				? !DeployGateActive() || IsDeployedForUse()
				: (bool?)null;
			RefreshPAWLabels(toggleActive, dumpActive, false);
		}
	}
}
