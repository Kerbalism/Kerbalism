using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public partial class VesselData : VesselDataBase
	{
		/// <summary> update the rescue state of kerbals when a vessel is loaded, return true if the vessek</summary>
		public static bool CheckRescueStatus(Vessel v, out bool rescueJustLoaded)
		{
			bool isRescue = false;
			rescueJustLoaded = false;

			// deal with rescue missions
			foreach (ProtoCrewMember c in Lib.CrewList(v))
			{
				// get kerbal data
				// note : this whole thing rely on KerbalData.rescue being initialized to true
				// when DB.Kerbal() (which is a get-or-create) is called for the first time
				KerbalData kd = DB.Kerbal(c.name);

				// flag the kerbal as not rescue at prelaunch
				// if the KerbalData wasn't created during prelaunch, that code won't be called
				// and KerbalData.rescue will stay at the default "true" value
				if (v.situation == Vessel.Situations.PRELAUNCH)
				{
					kd.rescue = false;
				}

				if (kd.rescue)
				{
					if (!v.loaded)
					{
						isRescue |= true;
					}
					// we de-flag a rescue kerbal when the rescue vessel is first loaded
					else
					{
						rescueJustLoaded |= true;
						isRescue &= false;

						// flag the kerbal as non-rescue
						// note: enable life support mechanics for the kerbal
						kd.rescue = false;

						// show a message
						Message.Post(Lib.BuildString(Local.Rescuemission_msg1, " <b>", c.name, "</b>"), Lib.BuildString((c.gender == ProtoCrewMember.Gender.Male ? Local.Kerbal_Male : Local.Kerbal_Female), Local.Rescuemission_msg2));//We found xx  "He"/"She"'s still alive!"
					}
				}
			}
			return isRescue;
		}

		/// <summary> Gift resources to a rescue vessel, to be called when a rescue vessel is first being loaded</summary>
		private void OnRescueVesselLoaded()
		{
			// give the vessel some propellant usable on eva
			string monoprop_name = Lib.EvaPropellantName();
			double monoprop_amount = Lib.EvaPropellantCapacity();
			foreach (var part in Vessel.parts)
			{
				if (part.CrewCapacity > 0 || part.FindModuleImplementing<KerbalEVA>() != null)
				{
					if (Lib.Capacity(part, monoprop_name) <= double.Epsilon)
					{
						Lib.AddResource(part, monoprop_name, 0.0, monoprop_amount);
					}
					break;
				}
			}
			ResHandler.Produce(monoprop_name, monoprop_amount, ResourceBroker.Generic);

			// give the vessel some supplies
			Profile.SetupRescue(this);
		}
	}
}
