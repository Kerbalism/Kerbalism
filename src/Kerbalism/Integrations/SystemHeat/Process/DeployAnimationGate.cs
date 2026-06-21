using UnityEngine;

namespace KERBALISM
{
	internal static class DeployAnimationGate
	{
		internal static bool IsDeployAnimationPlaying(ModuleAnimationGroup animator)
		{
			if (animator == null || animator.part == null || string.IsNullOrEmpty(animator.deployAnimationName))
				return false;

			Animation[] animations = animator.part.FindModelAnimators(animator.deployAnimationName);
			if (animations == null)
				return false;

			foreach (Animation animation in animations)
			{
				if (animation != null && animation.IsPlaying(animator.deployAnimationName))
					return true;
			}

			return false;
		}
	}
}
