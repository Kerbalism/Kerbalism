﻿using UnityEngine;

namespace KERBALISM
{
	public sealed class Transformator
	{
		Part part;
		Transform transf;
		string name;

		Quaternion baseAngles;

		float rotationRateGoal = 0f;
		float CurrentSpinRate = 0f;

		float SpinRate = 0f;
		float spinAccel = 0f;

		public Transformator(Part p, string transf_name, float SpinRate, float spinAccel)
		{
			transf = null;
			name = string.Empty;
			part = p;

			if (transf_name.Length > 0)
			{
				Lib.Debug("Looking for : {0}", transf_name);
				Transform[] transfArray = p.FindModelTransforms(transf_name);
				if (transfArray.Length > 0)
				{
					Lib.Debug("Transform has been found");
					name = transf_name;

					transf = transfArray[0];
					this.SpinRate = SpinRate;
					this.spinAccel = spinAccel;
					baseAngles = transf.localRotation;
				}
			}
		}

		public void Play()
		{
			Lib.Debug("Playing Transformation");
			if (transf != null) rotationRateGoal = 1.0f;
		}

		public void Stop()
		{
			Lib.Debug("Stopping Transformation");
			if (transf != null) rotationRateGoal = 0.0f;
		}

		public void DoSpin()
		{
			CurrentSpinRate = Mathf.MoveTowards(CurrentSpinRate, rotationRateGoal * SpinRate, TimeWarp.fixedDeltaTime * spinAccel);
			float spin = Mathf.Clamp(TimeWarp.fixedDeltaTime * CurrentSpinRate, -10.0f, 10.0f);
			transf.Rotate(Vector3.forward * spin);
		}

		public bool IsRotating()
		{
			return CurrentSpinRate > (float.Epsilon * SpinRate);
		}

		public bool IsStopping()
		{
			return rotationRateGoal <= float.Epsilon;
		}
	}
}