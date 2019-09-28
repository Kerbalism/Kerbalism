using System;
using System.Collections.Generic;
using KSP.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiMasterController : MonoBehaviour
	{
		public static KsmGuiMasterController Instance { get; private set; }

		public GameObject KsmGuiCanvas { get; private set; }
		public RectTransform KsmGuiTransform { get; private set; }
		public CanvasScaler CanvasScaler { get; private set; }
		public GraphicRaycaster GraphicRaycaster { get; private set; }
		public float ScaleFactor { get; private set; }

		public static void Initialize()
		{
			Instance = UIMasterController.Instance.gameObject.AddOrGetComponent<KsmGuiMasterController>();
		}

		private void Awake()
		{
			// Add tooltip controller to the tooltip canvas
			UIMasterController.Instance.tooltipCanvas.gameObject.AddComponent<KsmGuiTooltipController>();

			// create our own canvas as a child of the UIMaster object. this allow :
			// - using an independant scaling factor
			// - setting the render order as we need
			// - making our UI framework completely independant from any KSP code
			KsmGuiCanvas = new GameObject("KerbalismCanvas");
			KsmGuiTransform = KsmGuiCanvas.AddComponent<RectTransform>();

			Canvas canvas = KsmGuiCanvas.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceCamera;
			canvas.pixelPerfect = true;
			canvas.worldCamera = UIMasterController.Instance.uiCamera;
			canvas.sortingLayerName = "Dialogs"; // not sure this is needed, but doesn't hurt
			canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

			// render order of the various UI canvases (lower value = on top)
			// maincanvas => Z 750
			// appCanvas => Z 625
			// actionCanvas => Z 500 (PAW)
			// screenMessageCanvas => Z 450
			// dialogCanvas => Z 400 (DialogGUI windows)
			// dragDropcanvas => Z 333
			// debugCanvas => Z 315
			// tooltipCanvas => Z 300

			// above the PAW but behind the stock dialogs
			canvas.planeDistance = 475f;
			CanvasScaler = KsmGuiCanvas.AddComponent<CanvasScaler>();

			// note : we don't use app scale, but it might become necessary with fixed-position windows that are "attached" to the app launcher (toolbar buttons)
			CanvasScaler.scaleFactor = Settings.UIScale * GameSettings.UI_SCALE;

			GraphicRaycaster = KsmGuiCanvas.AddComponent<GraphicRaycaster>();

			KsmGuiTransform.SetParentAtOneScale(UIMasterController.Instance.transform, false);

			// things not on layer 5 will not be rendered
			KsmGuiCanvas.SetLayerRecursive(5);
			Canvas.ForceUpdateCanvases();

			GameEvents.onUIScaleChange.Add(OnScaleChange);
		}

		private void OnScaleChange()
		{
			CanvasScaler.scaleFactor = Settings.UIScale * GameSettings.UI_SCALE;

			foreach (RectTransform tranforms in GetComponentsInChildren<RectTransform>())
				tranforms.localScale = Vector3.one;

			// not sure why but on some occasions our windows will stop receive raycasts
			// after a scale change. The following code seems to fix it :
			Canvas.ForceUpdateCanvases();
			GraphicRaycaster.enabled = false;
			GraphicRaycaster.enabled = true;
		}


	}
}
