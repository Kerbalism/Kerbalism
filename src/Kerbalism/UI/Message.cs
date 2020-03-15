using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


namespace KERBALISM
{


	public enum Severity
	{
		none,
		relax,    // something went back to nominal
		warning,  // the user should start being worried about something
		danger,   // the user should start panicking about something
		fatality, // somebody died
		breakdown // somebody is breaking down
	}


	public sealed class Message
	{
		// represent an entry in the message list
		sealed class Entry
		{
			public string msg;
			public float duration;
			public float first_seen;
		}

		public sealed class MessageObject
		{
			public Severity msgSeverity = Severity.none;
			public string title;
			public string msg;
			public double time;
		}

		public static List<MessageObject> all_logs;


		// ctor
		public Message()
		{
			// enable global access
			instance = this;

			// setup style
			style = Styles.message;

			if (all_logs == null)
			{
				all_logs = new List<MessageObject>();
			}
		}


		// called every frame
		public void On_gui()
		{
			// if queue is empty, do nothing
			if (entries.Count == 0) return;

			// don't show messages when in screenshot (F2) mode 
			if (!KSP.UI.UIMasterController.Instance.screenMessageCanvas.enabled)
				return;

			// get current time
			float time = Time.realtimeSinceStartup;

			// get first entry in the queue
			Entry e = entries.Peek();

			// if never visualized, remember first time shown
			if (e.first_seen <= float.Epsilon) e.first_seen = time;

			// if visualized for too long, remove from the queue and skip this update
			if (e.first_seen + Math.Min(e.duration, PreferencesMessages.Instance.messageLength) < time) { entries.Dequeue(); return; }

			// calculate content size
			GUIContent content = new GUIContent(e.msg);
			Vector2 size = style.CalcSize(content);
			size = style.CalcScreenSize(size);
			size.x += style.padding.left + style.padding.right;
			size.y += style.padding.bottom + style.padding.top;

			// calculate position
			Rect rect = new Rect((Screen.width - size.x) * 0.5f, (Screen.height - size.y - offset), size.x, size.y);

			// render the message
			var prev_style = GUI.skin.label;
			GUI.skin.label = style;
			GUI.Label(rect, e.msg);
			GUI.skin.label = prev_style;
		}


		// add a plain message
		public static void Post(string msg)
		{
			// ignore the message if muted
			if (instance.muted) return;

			// if the user want to use the stock message system, just post it there
			if (PreferencesMessages.Instance.stockMessages)
			{
				ScreenMessages.PostScreenMessage(msg, PreferencesMessages.Instance.messageLength, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			// avoid adding the same message if already present in the queue
			foreach (Entry e in instance.entries) { if (e.msg == msg) return; }

			// compile entry
			Entry entry = new Entry
			{
				msg = msg,
				duration = Math.Max(5f, msg.Length / 20f),
				first_seen = 0
			};

			// add entry
			instance.entries.Enqueue(entry);
		}


		// add a message
		public static void Post(string text, string subtext)
		{
			// ignore the message if muted
			if (instance.muted) return;

			if (subtext.Length == 0) Post(text);
			else Post(Lib.BuildString(text, "\n<i>", subtext, "</i>"));
			all_logs.Add(new MessageObject
			{
				msg = Lib.BuildString(text, "\n<i>", subtext, "</i>"),
			});
			TruncateLogs();
		}


		// add a message
		public static void Post(Severity severity, string text, string subtext = "", bool stopWarp = true)
		{
			// ignore the message if muted
			if (instance.muted) return;

			string title = "";
			switch (severity)
			{
				case Severity.relax:
					title = Lib.BuildString(Lib.Color(Local.Message_RELAX, Lib.Kolor.Green, true), "\n");
					break;
				case Severity.warning:
					title = Lib.BuildString(Lib.Color(Local.Message_WARNING, Lib.Kolor.Yellow, true), "\n");
					if (stopWarp) Lib.StopWarp();
					break;
				case Severity.danger:
					title = Lib.BuildString(Lib.Color(Local.Message_DANGER, Lib.Kolor.Red, true), "\n");
					if (stopWarp) Lib.StopWarp();
					break; 
				case Severity.fatality:
					title = Lib.BuildString(Lib.Color(Local.Message_FATALITY, Lib.Kolor.Red, true), "\n");
					if (stopWarp) Lib.StopWarp();
					break;
				case Severity.breakdown:
					title = Lib.BuildString(Lib.Color(Local.Message_BREAKDOWN, Lib.Kolor.Orange, true), "\n");
					if (stopWarp) Lib.StopWarp();
					break;
			}

			// concatenate messages posted at the same time and of same severity
			MessageObject lastLog = all_logs.Count > 0 ? all_logs[all_logs.Count - 1] : null;
			Entry lastEntry = instance.entries.LastOrDefault();
			if (lastEntry != null && lastLog != null && lastLog.time == Planetarium.GetUniversalTime() && lastLog.msgSeverity == severity)
			{
				if (subtext.Length == 0)
					lastLog.msg = Lib.BuildString(lastEntry.msg, "\n", text);
				else
					lastLog.msg = Lib.BuildString(lastEntry.msg, "\n", text, "\n<i>", subtext, "</i>");

				lastEntry.msg = lastLog.msg;
				lastEntry.duration = Math.Max(5f, lastLog.msg.Length / 20f);
			}
			else
			{
				if (subtext.Length == 0)
					Post(Lib.BuildString(title, text));
				else
					Post(Lib.BuildString(title, text, "\n<i>", subtext, "</i>"));

				all_logs.Add(new MessageObject
				{
					title = title,
					msg = Lib.BuildString(text, "\n<i>", subtext, "</i>"),
					msgSeverity = severity,
					time = Planetarium.GetUniversalTime()
				}); ;
				TruncateLogs();
			}
		}

		// This is a bad workaround for the poor performance we have in the log window,
		// which instantiates all GUI elements for every log message for every frame.
		// Especially when viewing longer logs this can lead to a serious performance
		// impact, so we keep the log length short.
		// A good solution would have to re-implement the log using the new UI classes,
		// and while doing that also fix the broken layouting we get with long messages.
		// Note on that (Got) : it would still cause performance issues if every message
		// instantiate one text object. We should probably concatenate all messages into a
		// single StringBuilder instance instead.
		private static void TruncateLogs()
		{
			while(all_logs.Count > 25)
			{
				// remove oldest entries at the front, keep the newest entries added at the end
				all_logs.RemoveAt(0);
			}
		}

		/// <summary> Clear all log lists. Called when a new game is loaded </summary>
		public static void Clear()
		{
			all_logs.Clear();
			instance.entries.Clear();
		}

		// disable rendering of messages
		public static void Mute()
		{
			instance.muted = true;
		}


		// re-enable rendering of messages
		public static void Unmute()
		{
			instance.muted = false;
		}


		// return true if user channel is muted
		public static bool IsMuted()
		{
			return instance.muted;
		}


		private readonly float offset = Styles.ScaleFloat(266.0f);

		// store entries
		private Queue<Entry> entries = new Queue<Entry>();

		// disable message rendering
		private bool muted;

		// styles
		private GUIStyle style;

		// permit global access
		private static Message instance;
	}


} // KERBALISM
