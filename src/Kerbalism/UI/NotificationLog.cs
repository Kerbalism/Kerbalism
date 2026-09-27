using System;
using KSP.Localization;

namespace KERBALISM
{
	public static class NotificationLog
	{
		public static void Logman(this Panel p, Vessel v)
		{
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(40)), " ", Lib.Color(Local.LogMan_ALLLOGS, Lib.Kolor.LightGrey)));//"ALL LOGS"
			p.Width(Styles.ScaleWidthFloat(465.0f));
			p.paneltype = Panel.PanelType.log;

			p.AddSection(Local.LogMan_LOGS);//"LOGS"
			if (Message.all_logs == null || Message.all_logs.Count == 0)
			{
				p.AddContent("<i>"+Local.LogMan_nologs +"</i>", string.Empty);//no logs
			}
			else
			{
				for (int i = Message.all_logs.Count - 1; i >= 0; --i) //count backwards so most recent is first
				{
					Message.MessageObject log = Message.all_logs[i];
					string title = log.title != null
						? log.title.Replace("\n", "   ")
						: Lib.Color(Local.LogMan_ALERT, Lib.Kolor.Yellow);//"ALERT   "
					p.AddWrappingContent(title, log.msg);
				}
			}
		}
	}
}
