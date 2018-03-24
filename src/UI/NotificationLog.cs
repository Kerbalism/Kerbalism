using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public static class NotificationLog
	{
		public static void logman(this Panel p, Vessel v)
		{
			p.title("<color=#cccccc>ALL LOGS</color>");
			p.width(320.0f);
			p.section("LOGS");
			if (Message.all_logs == null || Message.all_logs.Count == 0)
			{
				p.content("<i>no logs</i>", string.Empty);
			}
			else
			{
				p.content(String.Empty, String.Empty); //keeps it from bumping into the top
				for (int i = Message.all_logs.Count - 1; i >= 0; --i) //count backwards so most recent is first
				{
					Message.MessageObject log = Message.all_logs[i];
					if (log.title != null)
					{
						p.content(log.title.Replace("\n", "   "), log.msg.Replace("\n", ". "));
					}
					else
					{
						p.content("<color=#DCDCDC><b>ALERT</b></color>   ", log.msg.Replace("\n", ". "));
					}
					if (Message.all_logs.Count > 1)
					{
						p.content(String.Empty, String.Empty); //this avoids things flowing into each other.
					}
				}
			}
		}
	}
}
