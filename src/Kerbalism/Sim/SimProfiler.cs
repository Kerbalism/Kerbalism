﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
	public sealed class MiniProfiler : MonoBehaviour
	{
		private class Measure
		{
			string name;
			List<double> secList = new List<double>();
			List<double> minList = new List<double>();
			double maxSec = -1.0;
			double minSec = -1.0;
			double avgSec = -1.0;
			double maxMin = -1.0;
			double minMin = -1.0;
			double avgMin = -1.0;
			Func<double, string> valueFormatter;

			public Measure(string name, Func<double, string> valueFormatter)
			{
				this.name = name;
				this.valueFormatter = valueFormatter;
			}

			public void Update(double current)
			{
				secList.Add(current);
				minList.Add(current);

				if (secList.Count < 50)
					return;

				maxSec = 0.0;
				minSec = double.MaxValue;
				avgSec = 0.0;
				foreach (double sample in secList)
				{
					if (sample > maxSec)
						maxSec = sample;

					if (sample < minSec)
						minSec = sample;

					avgSec += sample;
				}
				avgSec /= secList.Count;
				secList.Clear();

				if (minList.Count < 50 * 15)
					return;

				maxMin = 0.0;
				minMin = double.MaxValue;
				avgMin = 0.0;
				foreach (double sample in minList)
				{
					if (sample > maxMin)
						maxMin = sample;

					if (sample < minMin)
						minMin = sample;

					avgMin += sample;
				}
				avgMin /= minList.Count;
				minList.Clear();
			}

			public void CreateDialogEntry()
			{
				dialog_items.AddChild(
					new DialogGUIHorizontalLayout(
						new DialogGUILabel(name, true),
						new DialogGUILabel(() => { return valueFormatter(avgSec); }, value_width),
						new DialogGUILabel(() => { return valueFormatter(minSec); }, value_width),
						new DialogGUILabel(() => { return valueFormatter(maxSec); }, value_width),
						new DialogGUILabel(() => { return valueFormatter(avgMin); }, value_width),
						new DialogGUILabel(() => { return valueFormatter(minMin); }, value_width),
						new DialogGUILabel(() => { return valueFormatter(maxMin); }, value_width)));

				// required to force the Gui creation
				Stack<Transform> stack = new Stack<Transform>();
				stack.Push(dialog_items.uiItem.gameObject.transform);
				dialog_items.children[dialog_items.children.Count - 1].Create(ref stack, HighLogic.UISkin);
			}
		}

		// constants
		private const float width = 550.0f;
		private const float height = 220.0f;

		private const float value_width = 65.0f;

		// visible flag
		private static bool visible = false;

		// popup window
		private static MultiOptionDialog multi_dialog;
		private static PopupDialog popup_dialog;
		private static DialogGUIVerticalLayout dialog_items;

		public static long lastFuTicks;
		public static long lastWorkerTicks;
		public static long lastKerbalismFuTicks;
		public static double workerTimeMissed;
		public static double workerTimeUsed;
		static double lastFps;

		static Measure fps = new Measure("Update (FPS)", (v) => v.ToString("00.0;--"));
		static Measure fuMs = new Measure("FixedUpdate (FU)", (v) => v.ToString("00.00 ms;--"));
		static Measure ksmFuMs = new Measure("Kerbalism FU", (v) => v.ToString("00.00 ms;--"));
		static Measure ksmFuLoad = new Measure("Kerbalism FU load", (v) => v.ToString("00.0 %;--"));
		static Measure workerMs = new Measure("Sim thread", (v) => v.ToString("00.00 ms;--"));
		static Measure workerLoad = new Measure("Sim thread load", (v) => v.ToString("00.0 %;--"));
		static Measure wTimeMissed = new Measure("Sim time missed", (v) => Lib.HumanReadableDuration(v));
		static Measure wTimeUsed = new Measure("Sim time used", (v) => Lib.HumanReadableDuration(v));

		// permit global access
		public static MiniProfiler Fetch { get; private set; } = null;

		//  constructor
		public MiniProfiler()
		{
			// enable global access
			Fetch = this;

			// create window
			dialog_items = new DialogGUIVerticalLayout();
			multi_dialog = new MultiOptionDialog(
			   "KerbalismMiniProfiler",
			   "",
			   "Kerbalism mini-profiler",
			   HighLogic.UISkin,
			   new Rect(0.5f, 0.5f, width, height),
			   new DialogGUIBase[]
			   {
				   new DialogGUIVerticalLayout(false, false, 0, new RectOffset(), TextAnchor.UpperCenter,
                       // create header line
					   new DialogGUILabel(GetIntervalInfo, true),
					   new DialogGUIHorizontalLayout(
						   new DialogGUILabel("<b>Measure</b>", true),
						   new DialogGUILabel("<b>Avg/s</b>", value_width),
						   new DialogGUILabel("<b>Min/s</b>", value_width),
						   new DialogGUILabel("<b>Max/s</b>", value_width),
							new DialogGUILabel("<b>Avg/15s</b>", value_width),
						   new DialogGUILabel("<b>Min/15s</b>", value_width),
						   new DialogGUILabel("<b>Max/15s</b>", value_width))),
                   // create scrollbox for entry data
                   new DialogGUIScrollList(new Vector2(), false, false, dialog_items)
			   });
		}

		private void Start()
		{
			if (Fetch == null)
				return;

			// create popup dialog
			popup_dialog = PopupDialog.SpawnPopupDialog(multi_dialog, false, HighLogic.UISkin, false, "");
			if (popup_dialog != null)
				popup_dialog.gameObject.SetActive(false);

			fps.CreateDialogEntry();
			fuMs.CreateDialogEntry();
			ksmFuMs.CreateDialogEntry();
			workerMs.CreateDialogEntry();
			ksmFuLoad.CreateDialogEntry();
			workerLoad.CreateDialogEntry();
			wTimeMissed.CreateDialogEntry();
			wTimeUsed.CreateDialogEntry();
		}

		private void FixedUpdate()
		{
			if (!visible)
				return;

			double frequency = Stopwatch.Frequency;
			double lastFuMs = lastFuTicks / frequency * 1000.0;
			double lastWorkerMs = lastWorkerTicks / frequency * 1000.0;
			double lastWorkerPercent = lastWorkerMs / lastFuMs;
			double lastkerbalismFuMs = lastKerbalismFuTicks / frequency * 1000.0;
			double lastkerbalismFuPercent = lastkerbalismFuMs / lastFuMs;

			fps.Update(lastFps);
			fuMs.Update(lastFuMs);
			ksmFuMs.Update(lastkerbalismFuMs);
			workerMs.Update(lastWorkerMs);
			ksmFuLoad.Update(lastkerbalismFuPercent);
			workerLoad.Update(lastWorkerPercent);
			wTimeMissed.Update(workerTimeMissed);
			wTimeUsed.Update(workerTimeUsed);
		}

		private string GetIntervalInfo()
		{
			return Lib.BuildString("Sim sub-stepping interval : ", Lib.HumanReadableDuration(SubStepSim.subStepInterval), ", max sub-steps : ", SubStepSim.subStepsAtMaxWarp.ToString());
		}

		private void Update()
		{
			lastFps = 1.0f / Time.unscaledDeltaTime;

			if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
				&& Input.GetKeyUp(KeyCode.M)
				&& popup_dialog != null)
			{
				visible = !visible;
				popup_dialog.gameObject.SetActive(visible);
			}
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
	}
}
