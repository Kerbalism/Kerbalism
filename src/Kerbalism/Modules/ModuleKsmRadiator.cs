using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public class RadiatorData : ModuleData<ModuleKsmRadiator, RadiatorData>
	{
		public bool running;

		public override void OnFixedUpdate(double elapsedSec)
		{
			
		}

	}

	public class ModuleKsmRadiator : KsmPartModule<ModuleKsmRadiator, RadiatorData>
	{
		public enum FacingDirection { Undefined = 0, Up, Right, Forward, Down, Left, Backward }

		[KSPField] public double surface = 1.0;            // radiator surface
		[KSPField] public bool doubleSided = false;                  // optional factor on emissivity, affect output rate
		[KSPField] public bool isDeployable = false;                  // optional factor on emissivity, affect output rate
		[KSPField] public double flowRate = 0.1;		// coolant pump capacity, in m3/s
		[KSPField] public string inputResource = "ElectricCharge"; // resource consumed to make the pump work
		[KSPField] public double inputResourceRate = 0.1; // input resource rate at flowRate, per m²
		[KSPField] public string heatResource = Settings.aboveThEnergyRes; // heat pseudo-resource
		[KSPField] public FacingDirection facingTransformDirection = FacingDirection.Undefined;
		[KSPField] public string facingTransformName = string.Empty;

		private ModuleDeployableRadiator deployableRadiator;
		private FieldInfo mdrTrackingLOS;
		private Transform facingTransform;
		private double cosineFactor;
		

		public void Start()
		{
			deployableRadiator = part.Modules.GetModule<ModuleDeployableRadiator>();
			if (deployableRadiator != null)
			{
				mdrTrackingLOS = typeof(ModuleDeployableRadiator).GetField("trackingLOS", BindingFlags.Instance | BindingFlags.NonPublic);
			}

			if (facingTransformDirection == FacingDirection.Undefined)
			{
				if (deployableRadiator != null)
				{
					if (deployableRadiator.isTracking)
					{
						facingTransformDirection = FacingDirection.Up;
					}
					else
					{
						facingTransformDirection = FacingDirection.Right;
					}
				}
				else if (doubleSided)
				{
					facingTransformDirection = FacingDirection.Right;
				}
				else
				{
					facingTransformDirection = FacingDirection.Backward;
				}
			}

			if (facingTransformName != string.Empty)
			{
				facingTransform = part.FindModelComponent<Transform>(facingTransformName);
			}

			if (facingTransform == null)
			{
				if (deployableRadiator != null)
				{
					facingTransform = part.FindModelComponent<Transform>(deployableRadiator.pivotName);
				}
				else
				{
					facingTransform = part.partTransform;
				}
			}
		}

		private Vector3 GetFacingDirection()
		{
			switch (facingTransformDirection)
			{
				case FacingDirection.Up:       return facingTransform.up;
				case FacingDirection.Right:    return facingTransform.right;
				case FacingDirection.Forward:  return facingTransform.forward;
				case FacingDirection.Down:     return -facingTransform.up;
				case FacingDirection.Left:     return -facingTransform.right;
				case FacingDirection.Backward: return -facingTransform.forward;
				default:                       return Vector3.zero;
			}
		}

		private bool IsSunOccluded()
		{
			if (deployableRadiator != null)
			{
				return (bool)mdrTrackingLOS.GetValue(deployableRadiator);
			}


		}

		public void FixedUpdate()
		{
			cosineFactor = 
		}
	}
}
