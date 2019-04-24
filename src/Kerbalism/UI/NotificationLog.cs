using System;

namespace KERBALISM
{
	public static class NotificationLog
	{
		public static void Logman(this Panel p, Vessel v)
		{
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(40)), " <color=#cccccc>ALL LOGS</color>"));
			p.Width(Styles.ScaleWidthFloat(465.0f));
			p.paneltype = Panel.PanelType.log;

			p.AddSection("LOGS");
			if (Message.all_logs == null || Message.all_logs.Count == 0)
			{
				p.AddContent("<i>no logs</i>", string.Empty);
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
						p.AddContent("<color=#DCDCDC><b>ALERT</b></color>   ", log.msg.Replace("\n", ". "));
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
