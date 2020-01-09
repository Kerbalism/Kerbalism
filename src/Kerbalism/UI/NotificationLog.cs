using System;
using KSP.Localization;

namespace KERBALISM
{
	public static class NotificationLog
	{
		public static void Logman(this Panel p, Vessel v)
		{
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(40)), " ", Lib.Color(Localizer.Format("#KERBALISM_LogMan_ALLLOGS"), Lib.Kolor.LightGrey)));//"ALL LOGS"
			p.Width(Styles.ScaleWidthFloat(465.0f));
			p.paneltype = Panel.PanelType.log;

			p.AddSection(Localizer.Format("#KERBALISM_LogMan_LOGS"));//"LOGS"
			if (Message.all_logs == null || Message.all_logs.Count == 0)
			{
				p.AddContent("<i>"+Localizer.Format("#KERBALISM_LogMan_nologs") +"</i>", string.Empty);//no logs
			}
			else
			{
				p.AddContent(String.Empty, String.Empty); //keeps it from bumping into the top
				for (int i = Message.all_logs.Count - 1; i >= 0; --i) //count backwards so most recent is first
				{
					Message.MessageObject log = Message.all_logs[i];
					if (log.title != null)
					{
						p.AddContent(log.title.Replace("\n", "   "), log.msg.Replace("\n", ". "));
					}
					else
					{
						p.AddContent(Lib.Color(Localizer.Format("#KERBALISM_LogMan_ALERT"), Lib.Kolor.Yellow), log.msg.Replace("\n", ". "));//"ALERT   "
					}
					if (Message.all_logs.Count > 1)
					{
						p.AddContent(String.Empty, String.Empty); //this avoids things flowing into each other.
					}
				}
			}
		}
	}
}
