using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class TimedOut
	{
		public static bool Timeout(this Panel p, VesselData vd)
		{
			if (!vd.Connection.linked && vd.CrewCount == 0)
			{
				p.AddHeader(msg[((int)Time.realtimeSinceStartup) % msg.Length]);
				return true;
			}
			return false;
		}

		static string[] msg =
		{
	"<i>Connection in progress</i>",
	"<i>Connection in progress.</i>",
	"<i>Connection in progress..</i>",
	"<i>Connection in progress...</i>",
	"<i>Connection in progress....</i>",
	"<i>Connection in progress.....</i>",
	"<b><color=#ff3333><i>Connection timed-out</i></color></b>",
	"<b><color=#ff3333><i>Connection timed-out</i></color></b>",
	"<b><color=#ff3333><i>Connection timed-out</i></color></b>",
	"<b><color=#ff3333><i>Connection timed-out</i></color></b>",
	"<b><color=#ff3333><i>Connection timed-out</i></color></b>",
	"<i>New tentative in 3s</i>",
	"<i>New tentative in 2s</i>",
	"<i>New tentative in 1s</i>"
  };
	}


} // KERBALISM

