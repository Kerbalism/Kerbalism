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

		/// <summary>
		/// Return the array of all bodies. If this instance is a :<br/>
		/// - SimVessel : this is Sim.Bodies, an array of SimBody<br/>
		/// - StepSimVessel : this is StepSim.Bodies, an array of StepSimBody
		/// </summary>
		internal virtual SimBody[] Bodies => Sim.Bodies;

		/// <summary>
		/// Must be called for every simulated vessel, at the beginning of every FixedUpdate
		/// </summary>
		public void UpdateCurrent(VesselDataBase vdb, Vector3d position = default)
		{
			mainBody = MainBody(vdb);
			landed = vdb.EnvLanded;
			vesselAltitude = vdb.Altitude;
			if (vdb is VesselData vd)
			{
				this.position = position;
				vesselLatitude = vd.Vessel.latitude;
				vesselLongitude = vd.Vessel.longitude;
			}
		}

		/// <summary>
		/// Return the vessel world position. If this instance is a :<br/>
		/// - SimVessel : the position was calculated from UpdateCurrent() and is the last FU position <br/>
		/// - StepSimVessel : the position is calculated from the provided step UT
		/// </summary>
		internal virtual Vector3d GetPosition(Step step)
		{
			return position;
		}

		/// <summary>
		/// Return the mainbody. If this instance is a :<br/>
		/// - SimVessel : the returned instance will be a SimBody<br/>
		/// - StepSimVessel : the returned instance will be a StepSimBody
		/// </summary>
		protected SimBody MainBody(VesselDataBase vdb)
		{
			return Bodies[vdb.MainBody.flightGlobalsIndex];
		}
	}
}
