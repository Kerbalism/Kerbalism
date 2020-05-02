using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class SimVessel
	{
		private Vector3d position;
		public bool landed;
		public double vesselLatitude;
		public double vesselLongitude;
		public double vesselAltitude;
		public SimBody mainBody;
		public bool soiHasChanged;

		/// <summary>
		/// Return the array of all bodies. If this instance is a :<br/>
		/// - SimVessel : this is Sim.Bodies, an array of SimBody<br/>
		/// - StepSimVessel : this is StepSim.Bodies, an array of StepSimBody
		/// </summary>
		public virtual SimBody[] Bodies => Sim.Bodies;

		/// <summary>
		/// Must be called for every simulated vessel, at the beginning of every FixedUpdate
		/// </summary>
		public void UpdatePosition(VesselDataBase vdb, Vector3d position = default)
		{
			SimBody newMainBody = Bodies[vdb.MainBody.flightGlobalsIndex];
			if (mainBody == null)
			{
				mainBody = newMainBody;
				soiHasChanged = false;
			}
			else if (newMainBody != mainBody)
			{
				mainBody = newMainBody;
				soiHasChanged = true;
			}
			else
			{
				soiHasChanged = false;
			}

			landed = vdb.EnvLanded;
			vesselAltitude = vdb.Altitude;

			this.position = position;
			vesselLatitude = vdb.Latitude;
			vesselLongitude = vdb.Longitude;
		}

		/// <summary>
		/// Return the vessel world position. If this instance is a :<br/>
		/// - SimVessel : the position was calculated from UpdateCurrent() and is the last FU position <br/>
		/// - StepSimVessel : the position is calculated from the provided step UT
		/// </summary>
		public virtual Vector3d GetPosition(SimStep step)
		{
			return position;
		}
	}
}
