using UnityEngine;
using System;
using System.Collections;

namespace KERBALISM
{
	public sealed class Animator
	{
		private Animation anim;
		private readonly string name;
		public bool invertPlayDirection = false;

		public bool IsDefined { get; private set; } = false;

		public Animator(Part p, string anim_name, bool invertPlayDirection = false)
		{
			if (string.IsNullOrEmpty(anim_name))
				return;

			anim = null;
			name = string.Empty;
			this.invertPlayDirection = invertPlayDirection;

			if (anim_name.Length > 0)
			{
				Animation[] animations = p.FindModelAnimators(anim_name);
				if (animations.Length > 0)
				{
					IsDefined = true;
					anim = animations[0];
					name = anim_name;
					Still(0f);
				}
			}
		}

		/// <summary>
		/// Play the animation from it's current state. Note : the callback won't be called if the animation doesn't reach its end
		/// </summary>
		/// <param name="inReverse">false : play from start to end then fire the callback <para/>true : fire the callback then play from end to start</param>
		/// <param name="loop">if true, animation will keep playing from start to end</param>
		public void Play(bool inReverse, bool loop, Action callback = null, float speed = 1f)
		{
			if (!IsDefined)
			{
				callback?.Invoke();
				return;
			}

			Kerbalism.Fetch.StartCoroutine(PlayCoroutine(inReverse, loop, callback, speed));
		}

		private IEnumerator PlayCoroutine(bool inReverse, bool loop, Action callback = null, float speed = 1f)
		{
			bool towardStart = invertPlayDirection ^ inReverse;

			// if the animation is already playing in the same direction, do nothing.
			if (anim.IsPlaying(name) && ((towardStart && anim[name].speed < 0f) || (!towardStart && anim[name].speed > 0f)))
				yield break;

			// if playing toward the start, fire the callback immediately
			if (inReverse && callback != null) callback();

			anim[name].speed = (towardStart ? -1f : 1f) * speed;
			anim[name].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

			// normalizedTime is always reset to 0 when animation end is reached, so set it back manually to 1 to play backward
			if (towardStart && anim[name].normalizedTime == 0f)
				anim[name].normalizedTime = 1f; 
			else if (anim[name].normalizedTime == 0f)
				anim[name].normalizedTime = 0.0001f; // just so we don't fire the callback immediatly

			anim.Play(name);

			// if playing from start to end, wait for the end to fire the callback 
			if (!inReverse && callback != null)
			{
				float lastNormalizedTime = 0f;
				
				while (true)
				{
					float newNormalizedTime = anim[name].normalizedTime;
					if (newNormalizedTime == 0f)
						break;

					// if the playing direction has changed, abort and never fire the callback
					if (anim[name].speed > 0f)
					{
						if (newNormalizedTime < lastNormalizedTime)
							yield break;
					}
					else
					{
						if (newNormalizedTime > lastNormalizedTime)
							yield break;
					}

					lastNormalizedTime = newNormalizedTime;
					yield return null;
				}

				callback();
			}
		}

		/// <summary> stop a looping animation and fire the callback when it has reached the start point </summary>
		public void StopLoop(Action callback = null)
		{
			if (!IsDefined)
			{
				callback?.Invoke();
				return;
			}

			Kerbalism.Fetch.StartCoroutine(StopLoopCoroutine(callback));
		}

		private IEnumerator StopLoopCoroutine(Action callback = null)
		{
			anim[name].wrapMode = WrapMode.Once;
			anim.Play(name);

			if (callback != null)
			{
				float playDuration = anim[name].length * (anim[name].speed < 0f ? anim[name].normalizedTime : 1f - anim[name].normalizedTime);
				yield return new WaitForSeconds(playDuration);
				callback();
			}
		}

		/// <summary> pause the animation - Note : won't delay the callback call if one was defined in Play / StopLoop</summary>
		public void Pause()
		{
			if (IsDefined)
			{
				anim[name].speed = 0.0f;
			}
		}

		/// <summary> resume playing the animation </summary>
		public void Resume(bool inReverse)
		{
			if (IsDefined)
			{
				bool towardStart = invertPlayDirection ^ inReverse;
				anim[name].speed = towardStart ? -1f : 1f;
			}
		}

		public void Still(float normalizedTime)
		{
			if (IsDefined)
			{
				normalizedTime = invertPlayDirection ? 1f - normalizedTime : normalizedTime;
				anim[name].normalizedTime = normalizedTime;
				anim[name].speed = 0f;
				anim.Play(name);
			}
		}

		public bool Playing => IsDefined ? (anim[name].speed != 0f) && anim.IsPlaying(name) : false;

		public float NormalizedTime
		{
			get
			{
				if (!IsDefined)
					return 0f;

				if (invertPlayDirection)
					return anim[name].normalizedTime == 0f ? 1f : 1f - anim[name].normalizedTime;
				else
					return anim[name].normalizedTime;
			}
		}
	}
}
