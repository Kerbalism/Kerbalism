using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public sealed class SimProfiler : MonoBehaviour
	{
		// constants
		private const float width = 500.0f;
		private const float height = 500.0f;

		private const float value_width = 65.0f;

		// visible flag
		private static bool visible = false;

		// popup window
		private static MultiOptionDialog multi_dialog;
		private static PopupDialog popup_dialog;
		private static DialogGUIVerticalLayout dialog_items;

		public static long lastFuTicks;
		public static long maxFuTicks;
		public static long minFuTicks;

		public static long lastWorkerTicks;
		public static long maxWorkerTicks;
		public static long minWorkerTicks;

		// permit global access
		public static SimProfiler Fetch { get; private set; } = null;

		//  constructor
		public SimProfiler()
		{
			// enable global access
			Fetch = this;

			// create window
			dialog_items = new DialogGUIVerticalLayout();
			multi_dialog = new MultiOptionDialog(
			   "KerbalismProfilerWindow",
			   "",
			   "Sim Profiler",
			   HighLogic.UISkin,
			   new Rect(0.5f, 0.5f, width, height),
			   new DialogGUIBase[]
			   {
				   new DialogGUIVerticalLayout(false, false, 0, new RectOffset(), TextAnchor.UpperCenter,
                       // create average reset and show zero calls buttons
                       new DialogGUIHorizontalLayout(false, false,
						   new DialogGUIButton("Reset",
							   OnButtonClick_Reset, () => true, 75, 25, false)),
                       // create header line
                       new DialogGUIHorizontalLayout(
						   new DialogGUILabel("<b>NAME</b>", true),
						   new DialogGUILabel("<b>LAST</b>", value_width),
						   new DialogGUILabel("<b>MAX</b>", value_width),
						   new DialogGUILabel("<b>MIN</b>", value_width - 15f))),
                   // create scrollbox for entry data
                   new DialogGUIScrollList(new Vector2(), false, true, dialog_items)
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

			dialog_items.AddChild(
				new DialogGUIHorizontalLayout(
					new DialogGUILabel("FIXEDUPDATE", true),
					new DialogGUILabel(() => { return lastFuTicks.ToString(); }, value_width),
					new DialogGUILabel(() => { return maxFuTicks.ToString(); }, value_width),
					new DialogGUILabel(() => { return minFuTicks.ToString(); }, value_width - 15f)));

			// required to force the Gui creation
			Stack<Transform> stack = new Stack<Transform>();
			stack.Push(dialog_items.uiItem.gameObject.transform);
			dialog_items.children[dialog_items.children.Count - 1].Create(ref stack, HighLogic.UISkin);

			dialog_items.AddChild(
				new DialogGUIHorizontalLayout(
					new DialogGUILabel("WORKERTHREAD", true),
					new DialogGUILabel(() => { return lastWorkerTicks.ToString(); }, value_width),
					new DialogGUILabel(() => { return maxWorkerTicks.ToString(); }, value_width),
					new DialogGUILabel(() => { return minWorkerTicks.ToString(); }, value_width - 15f)));

			// required to force the Gui creation
			stack = new Stack<Transform>();
			stack.Push(dialog_items.uiItem.gameObject.transform);
			dialog_items.children[dialog_items.children.Count - 1].Create(ref stack, HighLogic.UISkin);
		}

		private void Update()
		{
			if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
					 Input.GetKeyUp(KeyCode.S) && popup_dialog != null)
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

		private static void OnButtonClick_Reset()
		{
			maxFuTicks = 0;
			minFuTicks = 0;

			maxWorkerTicks = 0;
			minWorkerTicks = 0;
		}
	}
}
