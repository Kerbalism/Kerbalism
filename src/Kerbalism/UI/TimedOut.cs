using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{


	public static class TimedOut
	{
		public static bool Timeout(this Panel p, VesselData vd)
		{
			if (!vd.Connection.linked && vd.CrewCount == 0 && !vd.Vessel.isEVA)
			{ 
				p.AddHeader(msg[((int)Time.realtimeSinceStartup) % msg.Length]);
				return true;
			}
			return false;
		}

		static string[] msg =
		{
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg1")+"</i>",//Connection in progress
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg1")+".</i>",//Connection in progress.
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg1")+"..</i>",//Connection in progress..
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg1")+"...</i>",//Connection in progress...
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg1")+"....</i>",//Connection in progress....
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg1")+".....</i>",//Connection in progress.....
	"<b><color=#ff3333><i>"+Localizer.Format("#KERBALISM_TimeoutMsg2")+"</i></color></b>",//Connection timed-out
	"<b><color=#ff3333><i>"+Localizer.Format("#KERBALISM_TimeoutMsg2")+"</i></color></b>",//Connection timed-out
	"<b><color=#ff3333><i>"+Localizer.Format("#KERBALISM_TimeoutMsg2")+"</i></color></b>",//Connection timed-out
	"<b><color=#ff3333><i>"+Localizer.Format("#KERBALISM_TimeoutMsg2")+"</i></color></b>",//Connection timed-out
	"<b><color=#ff3333><i>"+Localizer.Format("#KERBALISM_TimeoutMsg2")+"</i></color></b>",//Connection timed-out
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg3","3s")+"</i>",//New tentative in <<1>>
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg3","2s")+"</i>",//New tentative in <<1>>
	"<i>"+Localizer.Format("#KERBALISM_TimeoutMsg3","1s")+"</i>"//New tentative in <<1>>
  };
	}


} // KERBALISM

