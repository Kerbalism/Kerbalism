using UnityEngine;
using System;
using System.Collections;

namespace KERBALISM
{
	public sealed class Animator
	{
		private Animation anim;
		private readonly string name;
		public bool reversed = false;

		public bool IsDefined { get; private set; } = false;

		public Animator(Part p, string anim_name, bool reversed = false)
		{
			anim = null;
			name = string.Empty;
			this.reversed = reversed;

			if (anim_name.Length > 0)
			{
				Animation[] animations = p.FindModelAnimators(anim_name);
				if (animations.Length > 0)
				{
					IsDefined = true;
					anim = animations[0];
					name = anim_name;
				}
			}
		}

		// Note: This function resets animation to the beginning
		public void Play(bool reverse, bool loop, Action callback = null)
		{
			if(!IsDefined)
			{
				callback?.Invoke();
				return;
			}

			Kerbalism.Fetch.StartCoroutine(PlayAnimation(reverse, loop, callback));
		}

		private IEnumerator PlayAnimation(bool reverse, bool loop, Action callback = null)
		{
			if (reverse && callback != null) callback();

			var playDirection = reverse;
			if (reversed) playDirection = !playDirection;

			anim[name].normalizedTime = !playDirection ? 0.0f : 1.0f;
			anim[name].speed = !playDirection ? 1.0f : -1.0f;
			anim[name].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
			anim.Play(name);
			yield return new WaitForSeconds(anim[name].length);

			if (!reverse && callback != null) callback();
		}

		public void Stop(Action callback = null)
		{
			if (!IsDefined)
			{
				callback?.Invoke();
				return;
			}

			Kerbalism.Fetch.StartCoroutine(StopAnimation(callback));
		}

		private IEnumerator StopAnimation(Action callback = null)
		{
			anim.Stop(name);
			yield return new WaitForSeconds(anim[name].length);
			callback?.Invoke();
		}

		public void Pause()
		{
			if (IsDefined)
			{
				anim[name].speed = 0.0f;
			}
		}

		public void Resume(bool reverse)
		{
			if (IsDefined)
			{
				if (reversed) reverse = !reverse;
				anim[name].speed = !reverse ? 1.0f : -1.0f;
			}
		}

		public void Still(double t)
		{
			if (IsDefined)
			{
				t = reversed ? 1 - t : t;
				anim[name].normalizedTime = (float)t;
				anim[name].speed = 0.0f;
				anim.Play(name);
			}
		}

		public bool Playing()
		{
			if (IsDefined)
			{
				return (anim[name].speed > float.Epsilon) && anim.IsPlaying(name);
			}
			return false;
		}
   	}
}
