#if DEBUG_PROFILER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using KSP.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#endif

namespace KERBALISM
{
#if !DEBUG_PROFILER
    /// <summary> Simple profiler for measuring the execution time of code placed between the Start and Stop methods. </summary>
    public sealed class Profiler
    {
#endif
#if DEBUG_PROFILER
	/// <summary> Simple profiler for measuring the execution time of code placed between the Start and Stop methods. </summary>
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public sealed class Profiler: MonoBehaviour
	{
		// constants
		private const float width = 600.0f;
		private const float height = 500.0f;

		private const float value_width = 65.0f;

		// visible flag
		private static bool visible = false;
		private static bool show_zero = true;

		// column sorting state
		private enum SortColumn { None, Name, Last, Avg, Worst, Calls, AvgCalls }
		private static SortColumn sort_column = SortColumn.None;
		private static bool sort_ascending = false;
		// up/down sort-direction markers appended to the active column header
		private const string sort_asc_marker = " ˄";
		private const string sort_desc_marker = " ˅";

		// popup window
		private static MultiOptionDialog multi_dialog;
		private static PopupDialog popup_dialog;
		private static DialogGUIVerticalLayout dialog_items;

		// an entry in the profiler
		private class Entry
		{
			public long calls;          // number of calls in current simulation step
			public double time;         // time in current simulation step
			public long prev_calls;     // number of calls in previous simulation step
			public double prev_time;    // time in previous simulation step
			public long tot_calls;      // number of calls in total used for avg calculation
			public double tot_time;     // total time used for avg calculation
			public double worst_time;   // worst single-call time ever seen

			public string last_txt = "";        // last call time display string
			public string avg_txt = "";         // average call time display string
			public string worst_txt = "";       // worst call time display string
			public string calls_txt = "";       // number of calls display string
			public string avg_calls_txt = "";   // number of average calls display string

			public string name;                          // entry name, used as the NAME-column sort key
			public DialogGUIHorizontalLayout dialog_row; // this entry's GUI row, reordered in place when sorting
		}

		// store all entries
		private Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

		// reusable scratch list for ordering the displayed rows (avoids per-refresh allocation)
		private readonly List<Entry> sort_buffer = new List<Entry>();

		// maps each header label to the column it sorts, so Start() can wire up its click handler
		private struct HeaderBinding { public DialogGUILabel label; public SortColumn column; }
		private readonly List<HeaderBinding> header_bindings = new List<HeaderBinding>();

		// a sample on the call stack (used to time nested BeginSample/EndSample pairs)
		private struct ProfileSample
		{
			public readonly string name;
			public readonly double start;

			public ProfileSample(string name, double start)
			{
				this.name = name;
				this.start = start;
			}
		}

		// call stack mirroring the Unity profiler's hierarchical BeginSample/EndSample pairing
		private readonly Stack<ProfileSample> callStack = new Stack<ProfileSample>();

		// display update timer
		private static double update_timer = Lib.Clocks();
		private readonly static double timeout = Stopwatch.Frequency / update_fps;
		private const double update_fps = 5.0;      // Frames per second the entry value display will update.
		private static long tot_frames = 0;         // total physics frames used for avg calculation
		private static string tot_frames_txt = "";  // total physics frames display string


		// permit global access
		public static Profiler Fetch { get; private set; } = null;

		//  constructor
		public Profiler()
		{
			// enable global access
			Fetch = this;

			BuildDialog();
		}

		// (Re)build the dialog GUI tree. Called from the constructor and again whenever the popup must
		// be respawned - e.g. after the user closes it with ESC, which destroys the popup GameObject
		// (PopupDialog.Update -> Dismiss -> Object.Destroy) and leaves the cached DialogGUI tree stale.
		// Reusing the old tree is not safe (DialogGUIScrollList.Create re-appends its layout child on
		// every call), so we always construct a fresh one here.
		private void BuildDialog()
		{
			// MakeHeader below re-registers every header; drop any bindings from a previous build
			header_bindings.Clear();

			// create window
			dialog_items = new DialogGUIVerticalLayout();
			multi_dialog = new MultiOptionDialog(
			   "KerbalismProfilerWindow",
			   "",
			   GetTitle(),
			   HighLogic.UISkin,
			   new Rect(0.5f, 0.5f, width, height),
			   new DialogGUIBase[]
			   {
				   new DialogGUIVerticalLayout(false, false, 0, new RectOffset(), TextAnchor.UpperCenter,
                       // create average reset and show zero calls buttons
                       new DialogGUIHorizontalLayout(false, false,
						   new DialogGUIButton(Localizer.Format("#autoLOC_900305"),
							   OnButtonClick_Reset, () => true, 75, 25, false),
						   new DialogGUIToggle(() => { return show_zero; },"Show zero calls", OnButtonClick_ShowZero),
						   new DialogGUILabel(() => { return tot_frames_txt; }, value_width + 50f)),
                       // sort hint
                       new DialogGUILabel("<i>click a column header to sort</i>", true),
                       // create header line (each label is wired up to sort-on-click in Start())
                       new DialogGUIHorizontalLayout(
						   MakeHeader("   NAME", SortColumn.Name, 0f, true),
						   MakeHeader("LAST", SortColumn.Last, value_width, false),
						   MakeHeader("AVG", SortColumn.Avg, value_width, false),
						   MakeHeader("WORST", SortColumn.Worst, value_width, false),
						   MakeHeader("CALLS", SortColumn.Calls, value_width - 15f, false),
						   MakeHeader("AVG", SortColumn.AvgCalls, value_width - 10f, false))),
                   // create scrollbox for entry data
                   new DialogGUIScrollList(new Vector2(), false, true, dialog_items)
			   });

			// re-create rows for entries recorded before this (re)build. On the first build from the
			// constructor "entries" is empty; when respawning after an ESC dismiss this restores every
			// known row (BeginSample only adds a row the first time it sees a name, so it won't re-add).
			foreach (string name in entries.Keys)
				AddDialogItem(name);
		}

		private void Start()
		{
			SpawnDialog();
		}

		// Spawn the popup from the current multi_dialog tree and apply its runtime tweaks. The popup
		// starts hidden; Ctrl-P toggles it. Safe to call again after a previous popup was destroyed.
		private void SpawnDialog()
		{
			// create popup dialog
			popup_dialog = PopupDialog.SpawnPopupDialog(multi_dialog, false, HighLogic.UISkin, false, "");
			if (popup_dialog != null)
			{
				// The stock DialogGUIScrollList leaves the content RectTransform at a fixed
				// (viewport) height. Without a ContentSizeFitter the VerticalLayoutGroup compresses
				// rows toward their minHeight (overlapping lines) instead of growing the content and
				// letting the scrollbar actually scroll. Drive the content height off its children.
				ScrollRect scroll = popup_dialog.GetComponentInChildren<ScrollRect>();
				if (scroll != null && scroll.content != null)
				{
					ContentSizeFitter fitter = scroll.content.GetComponent<ContentSizeFitter>();
					if (fitter == null)
						fitter = scroll.content.gameObject.AddComponent<ContentSizeFitter>();
					fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
				}

				// Make the column headers clickable to sort. A DialogGUILabel has no button, but its
				// TextMeshProUGUI is a uGUI Graphic whose rect fills the header cell, so adding a
				// PointerClick EventTrigger lets the whole cell act as a sort toggle - no button needed.
				foreach (HeaderBinding binding in header_bindings)
				{
					if (binding.label.uiItem == null)
						continue;

					if (binding.label.text != null)
						binding.label.text.raycastTarget = true;

					SortColumn column = binding.column;
					EventTrigger trigger = binding.label.uiItem.AddComponent<EventTrigger>();
					EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
					click.callback.AddListener(_ => OnSortClick(column));
					trigger.triggers.Add(click);
				}

				popup_dialog.gameObject.SetActive(false);

				// keep the visibility flag in sync with the freshly-hidden popup
				visible = false;
			}
		}

		private void Update()
		{
			if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
					 Input.GetKeyUp(KeyCode.P))
			{
				// Closing the window with ESC destroys the popup GameObject (see SpawnDialog), so the
				// reference reads as null here. Rebuild a fresh dialog before toggling - SpawnDialog
				// resets "visible" to false, so the toggle below then shows it.
				if (popup_dialog == null)
				{
					BuildDialog();
					SpawnDialog();
				}

				if (popup_dialog != null)
				{
					visible = !visible;
					popup_dialog.gameObject.SetActive(visible);
				}
			}

			// skip updates for a smoother display
			if (((Lib.Clocks() - update_timer) > timeout) && visible)
			{
				update_timer = Lib.Clocks();
				Calculate();
			}
		}

		private static void Calculate()
		{
			foreach (KeyValuePair<string, Entry> p in Fetch.entries)
			{
				Entry e = p.Value;

				if (e.prev_calls > 0L)
				{
					e.last_txt = Lib.Microseconds((ulong)(e.prev_time / e.prev_calls)).ToString("F2") + "µs";
					e.calls_txt = e.prev_calls.ToString();
				}
				else if (show_zero)
				{
					e.last_txt = "µs";
					e.calls_txt = "0";
				}

				e.avg_txt = (e.tot_calls > 0L ? Lib.Microseconds((ulong)(e.tot_time / e.tot_calls)).ToString("F2") : "") + "µs";
				e.worst_txt = e.worst_time > 0.0 ? Lib.Microseconds((ulong)e.worst_time).ToString("F2") + "µs" : "µs";
				e.avg_calls_txt = tot_frames > 0L ? ((float)e.tot_calls / (float)tot_frames).ToString("F3") : "0";
			}

			tot_frames_txt = tot_frames.ToString() + " Frames";

			// keep the displayed order in sync with the live values
			ApplySort();
		}

		private void FixedUpdate()
		{
			foreach (KeyValuePair<string, Entry> p in Fetch.entries)
			{
				Entry e = p.Value;

				e.prev_calls = e.calls;
				e.prev_time = e.time;
				e.tot_calls += e.calls;
				e.tot_time += e.time;
				e.calls = 0L;
				e.time = 0.0;
			}

			++tot_frames;
		}

		private void OnDestroy()
		{
			Fetch = null;
			if (popup_dialog != null)
			{
				popup_dialog.Dismiss();
				popup_dialog = null;
			}
		}

		private static string GetTitle()
		{
			switch (Localizer.CurrentLanguage)
			{
				case "es-es":
					return "Kerbalism Profiler";
				case "ru":
					return "Провайдер Kerbalism";
				case "zh-cn":
					return "Kerbalism 分析器";
				case "ja":
					return "Kerbalism プロファイラ";
				case "de-de":
					return "Kerbalism Profiler";
				case "fr-fr":
					return "Kerbalism Profiler";
				case "it-it":
					return "Kerbalism Profiler";
				case "pt-br":
					return "Kerbalism perfil";
				default:
					return "Kerbalism Profiler";
			}
		}

		private static void OnButtonClick_Reset()
		{
			foreach (KeyValuePair<string, Entry> e in Fetch.entries)
			{
				e.Value.tot_calls = 0L;
				e.Value.tot_time = 0.0;
				e.Value.worst_time = 0.0;
			}

			tot_frames = 0L;
		}

		private static void OnButtonClick_ShowZero(bool inState)
		{
			show_zero = inState;
		}

		// create a header label and register it so Start() can wire it up to sort when clicked
		private DialogGUILabel MakeHeader(string text, SortColumn column, float w, bool expand)
		{
			DialogGUILabel label = expand
				? new DialogGUILabel(() => HeaderLabel(text, column), true)
				: new DialogGUILabel(() => HeaderLabel(text, column), w);

			header_bindings.Add(new HeaderBinding { label = label, column = column });
			return label;
		}

		// bold header caption, with a direction marker appended when this is the active sort column
		private static string HeaderLabel(string text, SortColumn column)
		{
			string marker = sort_column == column ? (sort_ascending ? sort_asc_marker : sort_desc_marker) : "";
			return "<b>" + text + marker + "</b>";
		}

		private static void OnSortClick(SortColumn column)
		{
			if (sort_column == column)
			{
				// clicking the active column flips the direction
				sort_ascending = !sort_ascending;
			}
			else
			{
				sort_column = column;
				// names read most naturally A->Z, timings/counts most usefully high->low
				sort_ascending = column == SortColumn.Name;
			}

			ApplySort();
		}

		// per-call time of the previous simulation step (sort key for the LAST column)
		private static double LastValue(Entry e)
		{
			return e.prev_calls > 0L ? e.prev_time / e.prev_calls : 0.0;
		}

		// average per-call time (sort key for the AVG column)
		private static double AvgValue(Entry e)
		{
			return e.tot_calls > 0L ? e.tot_time / e.tot_calls : 0.0;
		}

		// average number of calls per frame (sort key for the AVG-calls column)
		private static double AvgCallsValue(Entry e)
		{
			return tot_frames > 0L ? (double)e.tot_calls / tot_frames : 0.0;
		}

		private static int CompareEntries(Entry a, Entry b)
		{
			int cmp;
			switch (sort_column)
			{
				case SortColumn.Name:     cmp = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase); break;
				case SortColumn.Last:     cmp = LastValue(a).CompareTo(LastValue(b)); break;
				case SortColumn.Avg:      cmp = AvgValue(a).CompareTo(AvgValue(b)); break;
				case SortColumn.Worst:    cmp = a.worst_time.CompareTo(b.worst_time); break;
				case SortColumn.Calls:    cmp = a.prev_calls.CompareTo(b.prev_calls); break;
				case SortColumn.AvgCalls: cmp = AvgCallsValue(a).CompareTo(AvgCallsValue(b)); break;
				default:                  cmp = 0; break;
			}

			if (!sort_ascending)
				cmp = -cmp;

			// stable, deterministic order when the primary keys are equal
			if (cmp == 0)
				cmp = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);

			return cmp;
		}

		// reorder the displayed rows in place to match the active sort column
		private static void ApplySort()
		{
			if (Fetch == null || sort_column == SortColumn.None)
				return;

			List<Entry> buffer = Fetch.sort_buffer;
			buffer.Clear();
			foreach (Entry e in Fetch.entries.Values)
				buffer.Add(e);

			buffer.Sort(CompareEntries);

			for (int i = 0; i < buffer.Count; i++)
			{
				Entry e = buffer[i];
				if (e.dialog_row != null && e.dialog_row.uiItem != null)
					e.dialog_row.uiItem.transform.SetSiblingIndex(i);
			}
		}

		private void AddDialogItem(string e_name)
		{
			// add item
			dialog_items.AddChild(
				new DialogGUIHorizontalLayout(
					new DialogGUILabel("  " + e_name, true),
					new DialogGUILabel(() => { return entries[e_name].last_txt; }, value_width),
					new DialogGUILabel(() => { return entries[e_name].avg_txt; }, value_width),
					new DialogGUILabel(() => { return entries[e_name].worst_txt; }, value_width),
					new DialogGUILabel(() => { return entries[e_name].calls_txt; }, value_width - 15f),
					new DialogGUILabel(() => { return entries[e_name].avg_calls_txt; }, value_width - 10f)));

			// remember the row so the sorting code can reorder it in place
			entries[e_name].dialog_row = (DialogGUIHorizontalLayout)dialog_items.children[dialog_items.children.Count - 1];

			// If the dialog GUI hasn't been spawned yet (e.g. a sample is opened during OnLoad,
			// before this addon's Start() has created the popup), just register the child. It will
			// be instantiated together with the rest of the dialog when the popup spawns. Forcing
			// creation now would dereference the not-yet-existing uiItem and throw.
			if (dialog_items.uiItem == null)
				return;

			// required to force the Gui creation
			Stack<Transform> stack = new Stack<Transform>();
			stack.Push(dialog_items.uiItem.gameObject.transform);
			dialog_items.children[dialog_items.children.Count - 1].Create(ref stack, HighLogic.UISkin);

			// place the freshly created row according to the active sort
			ApplySort();
		}
#endif

		/// <summary>
		/// Begin a profiler sample. Feeds both the Unity profiler (when ENABLE_PROFILER is set)
		/// and the in-game Kerbalism profiler (when DEBUG_PROFILER is set). Compiled out entirely
		/// when neither symbol is defined. Must be paired with a matching <see cref="EndSample"/>.
		/// </summary>
		[System.Diagnostics.Conditional("ENABLE_PROFILER"), System.Diagnostics.Conditional("DEBUG_PROFILER")]
		public static void BeginSample(string name)
		{
			UnityEngine.Profiling.Profiler.BeginSample(name);
#if DEBUG_PROFILER
			if (Fetch == null)
				return;

			if (!Fetch.entries.ContainsKey(name))
			{
				Fetch.entries.Add(name, new Entry { name = name });
				Fetch.AddDialogItem(name);
			}

			Fetch.callStack.Push(new ProfileSample(name, Lib.Clocks()));
#endif
		}

		/// <summary>
		/// End the most recently started profiler sample. Feeds both the Unity profiler and the
		/// in-game Kerbalism profiler, mirroring <see cref="BeginSample"/>.
		/// </summary>
		[System.Diagnostics.Conditional("ENABLE_PROFILER"), System.Diagnostics.Conditional("DEBUG_PROFILER")]
		public static void EndSample()
		{
			UnityEngine.Profiling.Profiler.EndSample();
#if DEBUG_PROFILER
			if (Fetch == null || Fetch.callStack.Count == 0)
				return;

			ProfileSample sample = Fetch.callStack.Pop();
			Entry e = Fetch.entries[sample.name];

			double call_time = Lib.Clocks() - sample.start;
			++e.calls;
			e.time += call_time;
			if (call_time > e.worst_time) e.worst_time = call_time;
#endif
		}

#if DEBUG_PROFILER

		/// <summary> Profile a function scope. Use with a <c>using</c> block to guarantee a balanced EndSample. </summary>
		public sealed class ProfileScope: IDisposable
		{
			public ProfileScope(string name)
			{
				Profiler.BeginSample(name);
			}

			public void Dispose()
			{
				Profiler.EndSample();
			}
		}

#endif
	}

} // KERBALISM
