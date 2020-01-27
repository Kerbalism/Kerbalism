using UnityEngine;
using System;

namespace KERBALISM
{
	public sealed class Transformator
	{
		private readonly Part part;
		private Transform transform;
		private readonly string name;

		private Quaternion baseAngles;

		private float rotationRateGoal;
		private float CurrentSpinRate;

		private readonly float SpinRate;
		private readonly float spinAccel;
		private readonly bool rotate_iva;

		public Transformator(Part p, string transf_name, float SpinRate, float spinAccel, bool iva = true)
		{
			transform = null;
			name = string.Empty;
			part = p;
			rotate_iva = iva;

			if (transf_name.Length > 0)
			{
				//Lib.Log("Looking for : {0}", transf_name);
				transform = p.FindModelTransform(transf_name);
				if (transform != null)
				{
					name = transf_name;
					//Lib.Log("Transform {0} has been found", name);

					this.SpinRate = SpinRate;
					this.spinAccel = spinAccel;
					baseAngles = transform.localRotation;
				}
			}
		}

		public void Play()
		{
			//Lib.Log("Playing Transformation {0}", name);
			if (transform != null) rotationRateGoal = 1.0f;
		}

		public void Stop()
		{
			//Lib.Log("Stopping Transformation {0}", name);
			if (transform != null) rotationRateGoal = 0.0f;
		}

		public void DoSpin()
		{
			CurrentSpinRate = Mathf.MoveTowards(CurrentSpinRate, rotationRateGoal * SpinRate, TimeWarp.fixedDeltaTime * spinAccel);
			float spin = Mathf.Clamp(TimeWarp.fixedDeltaTime * CurrentSpinRate, -10.0f, 10.0f);
			//Lib.Log("Transform {0} spin rate {1}", name, CurrentSpinRate);
			// Part rotation
			transform.Rotate(Vector3.forward * spin);

			if(rotate_iva && part.internalModel != null)
			{
				// IVA rotation
				if (part.internalModel != null) part.internalModel.transform.Rotate(Vector3.forward * (spin * -1));
			}
		}

		public bool IsRotating()
		{
			return Math.Abs(CurrentSpinRate) > Math.Abs(float.Epsilon * SpinRate);
		}

		public bool IsStopping()
		{
			return Math.Abs(rotationRateGoal) <= float.Epsilon;
		}
	}
}
