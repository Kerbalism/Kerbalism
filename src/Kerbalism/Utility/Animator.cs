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

		public bool IsDefined => anim != null;

		public float NormalizedTime => reversed ? Math.Abs(anim[name].normalizedTime - 1) : anim[name].normalizedTime;

		public Animator(Part p, string anim_name)
		{
			anim = null;
			name = string.Empty;

			if (anim_name.Length > 0)
			{
				Animation[] animations = p.FindModelAnimators(anim_name);
				if (animations.Length > 0)
				{
					anim = animations[0];
					name = anim_name;
				}
			}
		}

		public void Play(bool reverse, bool loop, double speed = 1.0, double fromNormalizedTime = -1.0)
		{
			if (anim == null)
				return;

			bool playDirection = reverse;
			if (reversed) playDirection = !playDirection;

			if (fromNormalizedTime >= 0f)
				anim[name].normalizedTime = (float)(reversed ? Math.Abs(fromNormalizedTime - 1) : fromNormalizedTime);
			else
				anim[name].normalizedTime = !playDirection ? 0.0f : 1.0f;

			anim[name].speed = (float)(!playDirection ? speed : -speed);
			anim[name].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
			anim.Play(name);
		}


		// Note: This function resets animation to the beginning
		public void Play(bool reverse, bool loop, Action callback, double speed = 1.0, double fromNormalizedTime = -1.0)
		{
			if(anim == null)
			{
				callback?.Invoke();
				return;
			}

			Kerbalism.Fetch.StartCoroutine(PlayAnimationWithCallback(reverse, loop, callback, speed, fromNormalizedTime));
		}

		IEnumerator PlayAnimationWithCallback(bool reverse, bool loop, Action callback, double speed = 1.0, double fromNormalizedTime = -1)
		{
			if (reverse && callback != null) callback();

			Play(reverse, loop, speed, fromNormalizedTime);

			yield return new WaitForSeconds(anim[name].length);

			if (!reverse && callback != null) callback();
		}

		public void Stop(Action callback = null)
		{
			if (anim == null)
			{
				callback?.Invoke();
				return;
			}

			Kerbalism.Fetch.StartCoroutine(StopAnimation(callback));
		}

		IEnumerator StopAnimation(Action callback = null)
		{
			anim.Stop(name);
			yield return new WaitForSeconds(anim[name].length);
			callback?.Invoke();
		}

		public void Pause()
		{
			if (anim != null)
			{
				anim[name].speed = 0.0f;
			}
		}

		public void Resume(bool reverse)
		{
			if (anim != null)
			{
				if (reversed) reverse = !reverse;
				anim[name].speed = !reverse ? 1.0f : -1.0f;
			}
		}

		public void Still(double t)
		{
			if (anim != null)
			{
				t = reversed ? 1 - t : t;
				anim[name].normalizedTime = (float)t;
				anim[name].speed = 0.0f;
				anim.Play(name);
			}
		}

		public bool Playing()
		{
			if (anim != null)
			{
				return (anim[name].speed != 0f) && anim.IsPlaying(name);
			}
			return false;
		}
   	}
}
