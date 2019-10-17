using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public enum Severity
	{
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
			public float first_seen;
		}

		public sealed class MessageObject
		{
			public string title;
			public string msg;
		}

		public static List<MessageObject> all_logs;


		// ctor
		public Message()
		{
			// enable global access
			instance = this;

			// setup style
			style = new GUIStyle();
			style.normal.background = Lib.GetTexture("black-background");
			style.normal.textColor = new Color(0.66f, 0.66f, 0.66f, 1.0f);
			style.richText = true;
			style.stretchWidth = true;
			style.stretchHeight = true;
			style.fixedWidth = 0;
			style.fixedHeight = 0;
			style.fontSize = Styles.ScaleInteger(12);
			style.alignment = TextAnchor.MiddleCenter;
			style.border = new RectOffset(0, 0, 0, 0);
			style.padding = new RectOffset(Styles.ScaleInteger(2), Styles.ScaleInteger(2), Styles.ScaleInteger(2), Styles.ScaleInteger(2));

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

			// get current time
			float time = Time.realtimeSinceStartup;

			// get first entry in the queue
			Entry e = entries.Peek();

			// if never visualized, remember first time shown
			if (e.first_seen <= float.Epsilon) e.first_seen = time;

			// if visualized for too long, remove from the queue and skip this update
			if (e.first_seen + PreferencesMessages.Instance.messageLength < time) { entries.Dequeue(); return; }

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
		}


		// add a message
		public static void Post(Severity severity, string text, string subtext = "")
		{
			// ignore the message if muted
			if (instance.muted) return;

			string title = "";
			switch (severity)
			{
				case Severity.relax: title = Lib.BuildString(Lib.Color("RELAX", Lib.Kolor.Green, true), "\n"); break;
				case Severity.warning: title = Lib.BuildString(Lib.Color("WARNING", Lib.Kolor.Yellow, true), "\n"); Lib.StopWarp(); break; 
				case Severity.danger: title = Lib.BuildString(Lib.Color("DANGER", Lib.Kolor.Red, true), "\n"); Lib.StopWarp(); break; 
				case Severity.fatality: title = Lib.BuildString(Lib.Color("FATALITY", Lib.Kolor.Red, true), "\n"); Lib.StopWarp(); break; 
				case Severity.breakdown: title = Lib.BuildString(Lib.Color("BREAKDOWN", Lib.Kolor.Orange, true), "\n"); Lib.StopWarp(); break; 
			}
			if (subtext.Length == 0) Post(Lib.BuildString(title, text));
			else Post(Lib.BuildString(title, text, "\n<i>", subtext, "</i>"));
			all_logs.Add(new MessageObject
			{
				title = title,
				msg = Lib.BuildString(text, "\n<i>", subtext, "</i>"),
			});
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
