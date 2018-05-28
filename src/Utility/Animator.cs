using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Animator
	{
		Animation anim;
		string name;

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

		// Note: This function resets animation to the beginning
		public void Play(bool reverse, bool loop)
		{
			if (anim != null)
			{
				anim[name].normalizedTime = !reverse ? 0.0f : 1.0f;
				anim[name].speed = !reverse ? 1.0f : -1.0f;
				anim[name].wrapMode = !loop ? WrapMode.Once : WrapMode.Loop;
				anim.Play(name);
			}
		}


		public void Stop()
		{
			if (anim != null)
			{
				anim.Stop();
			}
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
				anim[name].speed = !reverse ? 1.0f : -1.0f;
			}
		}


		public void Still(double t)
		{
			if (anim != null)
			{
				anim[name].normalizedTime = (float)t;
				anim[name].speed = 0.0f;
				anim.Play(name);
			}
		}


		public bool Playing()
		{
			if (anim != null)
			{
				return (anim[name].speed > float.Epsilon) && anim.IsPlaying(name);
			}
			return false;
		}
	}


} // KERBALISM

