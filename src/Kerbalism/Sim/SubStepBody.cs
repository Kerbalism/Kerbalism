using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class SubStepBody : SimBody
	{
		// stock body orbital properties
		public double gravParameter;
		public double initialRotation;
		public double rotPeriodRecip;

		// values updated every FixedUpdate
		public SubStepOrbit orbit;

		// last step values
		private bool lastStepIsValid = false;
		private Vector3d lastStepPosition;
		private Planetarium.CelestialFrame lastStepframe;

		// last evaluated UT cache
		private double cachedUT;
		private Vector3d positionAtCachedUT;
		private Planetarium.CelestialFrame frameAtCachedUT;

		public override SimBody ReferenceBody => SubStepSim.Bodies[referenceBodyFlightGlobalsIndex];

		public SubStepBody(CelestialBody body) : base(body)
		{
			// orbital properties
			initialRotation = body.initialRotation;
			rotPeriodRecip = body.rotPeriodRecip;
			gravParameter = body.gravParameter;
			isSun = Sim.IsStar(body);

			cachedUT = -1.0;

			if (body.orbit != null)
				orbit = new SubStepOrbit(body.orbit);
		}

		public override void Update()
		{
			base.Update();

			if (orbit != null)
				orbit.Update(stockBody.orbit);

			lastStepIsValid = false;
		}

		public override Vector3d GetPosition(double ut = -1)
		{
			if (ut < 0.0)
			{
				if (!lastStepIsValid)
					ComputeNextStep();

				return lastStepPosition;
			}
			else
			{
				CheckCacheForUT(ut);
				return positionAtCachedUT;

			}
		}

		public override Vector3d GetSurfacePosition(double lat, double lon, double alt, double ut = -1)
		{
			lat *= Math.PI / 180.0;
			lon *= Math.PI / 180.0;
			Vector3d spericalvector = Planetarium.SphericalVector(lat, lon).xzy;
			spericalvector *= radius + alt;

			if (ut < 0.0)
			{
				if (!lastStepIsValid)
					ComputeNextStep();
				return lastStepframe.LocalToWorld(spericalvector.xzy).xzy + lastStepPosition;
			}
			else
			{
				CheckCacheForUT(ut);
				return frameAtCachedUT.LocalToWorld(spericalvector.xzy).xzy + positionAtCachedUT;
			}
		}

		public void ComputeNextStep()
		{
			if (orbit == null)
				lastStepPosition = currentPosition;
			else
				lastStepPosition = orbit.GetSafeTruePosition();

			lastStepframe = GetBodyFrameForUT(SubStepSim.lastStep.ut);
			lastStepIsValid = true;

		}

		private void CheckCacheForUT(double ut)
		{
			if (ut == cachedUT)
				return;

			cachedUT = ut;

			if (orbit == null)
				positionAtCachedUT = currentPosition;
			else
				positionAtCachedUT = orbit.GetSafeTruePosition(ut);

			frameAtCachedUT = GetBodyFrameForUT(ut);
		}

		private Planetarium.CelestialFrame GetBodyFrameForUT(double ut)
		{
			if (canRotate)
			{
				double rotationAngle = (initialRotation + 360.0 * rotPeriodRecip * ut) % 360.0;
				if (currentInverseRotation)
				{
					rotationAngle = (rotationAngle - SubStepSim.lastStep.inverseRotAngle) % 360.0;
				}
				Planetarium.CelestialFrame frame = default;
				Planetarium.CelestialFrame.PlanetaryFrame(0.0, 90.0, rotationAngle, ref frame);
				return frame;
			}

			return SubStepSim.lastStep.zup;
		}
	}
}
