using UnityEngine;
using System;
using System.Collections;

namespace KERBALISM
{
	public sealed class Transformator
	{
		private Part part;
		private Transform rotateTransform;
		private Vector3 rotateAxis;
		private Quaternion transformInitialRotation;
		private Animator animator;

		private bool requestSpin;
		private float currentSpinRate;

		public Callback onSpinRateReached;
		public Callback onSpinStopped;

		private float spinRate;
		private float spinAccel;
		private bool rotateIVA;
		private bool invertPlayDirection;

		public bool IsDefined { get; private set; } = false;
		public bool IsAnimator { get; private set; }
		public bool IsSpinning => IsDefined && currentSpinRate > 0f;
		public bool IsAccelerating => IsDefined && requestSpin && currentSpinRate != spinRate;
		public bool IsStopping => IsDefined && !requestSpin && currentSpinRate != 0f;

		/// <summary> Create a transformator based on a transform </summary>
		public Transformator(Part part, string transformName, Vector3 rotateAxis, float spinRate, float spinAccel, bool invertPlayDirection, bool rotateIVA)
		{
			if (string.IsNullOrEmpty(transformName))
				return;

			this.part = part;
			this.spinRate = spinRate;
			this.spinAccel = spinAccel;
			this.invertPlayDirection = invertPlayDirection;
			this.rotateAxis = rotateAxis;
			this.rotateIVA = rotateIVA;

			rotateTransform = part.FindModelTransform(transformName);
			if (rotateTransform != null)
			{
				transformInitialRotation = rotateTransform.localRotation;
				IsDefined = true;
				IsAnimator = false;
			}
		}

		/// <summary> Create a transformator based on an animation </summary>
		public Transformator(Part part, string animationName, float spinRate, float spinAccel, bool invertPlayDirection)
		{
			if (string.IsNullOrEmpty(animationName))
				return;

			this.part = part;
			this.spinRate = spinRate;
			this.spinAccel = spinAccel;
			this.invertPlayDirection = invertPlayDirection;
			rotateIVA = false;

			animator = new Animator(part, animationName);
			IsDefined = animator.IsDefined;
			IsAnimator = true;
		}

		public void StartSpin(bool instantly = false)
		{
			if (!IsDefined)
				return;

			requestSpin = true;

			if (instantly)
				currentSpinRate = spinRate;
		}

		public void StopSpin(bool instantly = false)
		{
			if (!IsDefined)
				return;

			requestSpin = false;

			if (instantly)
			{
				currentSpinRate = 0f;
				if (IsAnimator)
					animator.Still(0f);
				else
					rotateTransform.localRotation = transformInitialRotation;

				onSpinStopped?.Invoke();
				return;
			}
		}

		public void Update()
		{
			if (!IsDefined)
				return;

			
			// accelerating
			if (requestSpin && currentSpinRate != spinRate)
			{
				currentSpinRate += spinAccel * TimeWarp.deltaTime;

				if (currentSpinRate >= spinRate)
				{
					currentSpinRate = spinRate;
					onSpinRateReached?.Invoke();
				}
			}
			// decelerating
			else if (!requestSpin && currentSpinRate != 0f)
			{
				float currentNormalizedTime;
				if (IsAnimator)
				{
					currentNormalizedTime = animator.NormalizedTime;
				}
				else
				{
					// get a [0 ; 1] factor representing the [0 ; 360]° angle from the start position to the current position
					Vector3 eulerAngle = Vector3.Scale(rotateTransform.localRotation.eulerAngles + transformInitialRotation.eulerAngles, rotateAxis);
					currentNormalizedTime = ((eulerAngle.x + eulerAngle.y + eulerAngle.z) % 360f) / 360f;
				}
					
				// calculate total rotation (in degrees) needed to stop, then clamp it to 1 rotation
				float deltaToStop = (Mathf.Pow(currentSpinRate, 2f) / (2f * spinAccel)) % 360f;

				// calculate rotation between current position and the zero position
				float deltaToZero;
				if (!invertPlayDirection && currentNormalizedTime != 0f) // currentNormalizedTime range is [0 ; 0.999...], it will never be 1.
					deltaToZero = 1f - currentNormalizedTime;
				else
					deltaToZero = currentNormalizedTime;

				deltaToZero *= 360f;

				// decelerate when we are at the right spot to reach zero point
				if (deltaToStop >= deltaToZero && deltaToStop <= deltaToZero + currentSpinRate) 
					currentSpinRate =
						currentSpinRate
						- (spinAccel * TimeWarp.deltaTime) // remove speed using the acceleration rate
						- (deltaToStop - deltaToZero); // compensate the current position to make sure we stop at the zero point (± the FP precison) 

				// we will never exactly reach the stop point, so just force it
				if (deltaToStop <= spinAccel * TimeWarp.deltaTime)
				{
					currentSpinRate = 0f;
					if (IsAnimator)
						animator.Still(0f);
					else
						rotateTransform.localRotation = transformInitialRotation;

					onSpinStopped?.Invoke();
					return;
				}
			}
			// avoid reseting animations (and calling useless code)
			if (currentSpinRate == 0f)
				return;

			// clamp visual effect to 10° / frame (note : not sure how that will interact with the stopping code... Should be fine ?)
			float spinDelta = Mathf.Clamp(currentSpinRate * TimeWarp.deltaTime, 0f, 10f);
			if (invertPlayDirection)
				spinDelta *= -1f;

			if (IsAnimator)
			{
				float targetNormalizedTime = (animator.NormalizedTime + (spinDelta / 360f)) % 1f;
				animator.Still(targetNormalizedTime);
			}
			else
			{
				// Rotate the transform
				rotateTransform.Rotate(rotateAxis * spinDelta);

				// IVA rotation
				if (rotateIVA && part.internalModel != null)
					part.internalModel.transform.Rotate(rotateAxis * (invertPlayDirection ? spinDelta : -spinDelta)); // IVA transform seems to always be rotated 180° (?) 
			}


		}


	}
}
