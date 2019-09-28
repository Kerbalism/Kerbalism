using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class KsmGuiTooltipController : MonoBehaviour
	{
		public static KsmGuiTooltipController Instance { get; private set; }

		private GameObject tooltipObject;
		private TextMeshProUGUI textComponent;
		public RectTransform TopTransform { get; private set; }
		public RectTransform ContentTransform { get; private set; }
		public bool IsVisible { get; private set; }

		private void Awake()
		{
			Instance = this;

			tooltipObject = new GameObject("KsmGuiTooltip");

			TopTransform = tooltipObject.AddComponent<RectTransform>();
			// default of 0, 1 mean pivot is at the window top-left corner
			// pivotX = 0 => left, pivotX = 1 => right
			// pivotY = 0 => bottom, pivotY = 1 => top
			TopTransform.pivot = new Vector2(0f, 0f);
			// distance in pixels between the pivot and the center of the screen
			TopTransform.anchoredPosition = new Vector2(0f, 0f);

			TopTransform.sizeDelta = new Vector2(KsmGuiStyle.tooltipMaxWidth, 0f); // max width of tooltip, text wrap will occur if larger.

			// set the parent canvas
			// render order of the various UI canvases (lower value = on top)
			// maincanvas => Z 750
			// appCanvas => Z 625
			// actionCanvas => Z 500
			// screenMessageCanvas => Z 450
			// dialogCanvas => Z 400
			// dragDropcanvas => Z 333
			// debugCanvas => Z 315
			// tooltipCanvas => Z 300
			TopTransform.SetParentAtOneScale(UIMasterController.Instance.tooltipCanvas.transform, false);

			tooltipObject.AddComponent<CanvasRenderer>();

			VerticalLayoutGroup toplayout = tooltipObject.AddComponent<VerticalLayoutGroup>();
			toplayout.childAlignment = TextAnchor.UpperLeft;
			toplayout.childControlHeight = true;
			toplayout.childControlWidth = true;
			toplayout.childForceExpandHeight = false;
			toplayout.childForceExpandWidth = false;

			ContentSizeFitter topFitter = tooltipObject.AddComponent<ContentSizeFitter>();
			topFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			topFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			// first child : 1px white border
			GameObject border = new GameObject("KsmGuiTooltipBorder");
			ContentTransform = border.AddComponent<RectTransform>();
			ContentTransform.SetParentAtOneScale(TopTransform);
			border.AddComponent<CanvasRenderer>();

			VerticalLayoutGroup borderLayout = border.AddComponent<VerticalLayoutGroup>();
			borderLayout.padding = new RectOffset(1,1,1,1);
			borderLayout.childAlignment = TextAnchor.UpperLeft;
			borderLayout.childControlHeight = true;
			borderLayout.childControlWidth = true;
			borderLayout.childForceExpandHeight = false;
			borderLayout.childForceExpandWidth = false;

			Image borderImage = border.AddComponent<Image>();
			borderImage.color = KsmGuiStyle.tooltipBorderColor;
			borderImage.raycastTarget = false;

			// 2nd child : black background
			GameObject background = new GameObject("KsmGuiTooltipBackground");
			RectTransform backgroundTranform = background.AddComponent<RectTransform>();
			backgroundTranform.SetParentAtOneScale(ContentTransform);
			background.AddComponent<CanvasRenderer>();

			VerticalLayoutGroup backgroundLayout = background.AddComponent<VerticalLayoutGroup>();
			backgroundLayout.padding = new RectOffset(5, 5, 2, 2);
			backgroundLayout.childAlignment = TextAnchor.UpperLeft;
			backgroundLayout.childControlHeight = true;
			backgroundLayout.childControlWidth = true;
			backgroundLayout.childForceExpandHeight = false;
			backgroundLayout.childForceExpandWidth = false;

			Image backgroundImage = background.AddComponent<Image>();
			backgroundImage.color = KsmGuiStyle.tooltipBackgroundColor;
			backgroundImage.raycastTarget = false;

			// last child : text
			GameObject textObject = new GameObject("KsmGuiTooltipText");
			RectTransform textTransform = textObject.AddComponent<RectTransform>();
			textTransform.SetParentAtOneScale(backgroundTranform);
			textObject.AddComponent<CanvasRenderer>();

			textComponent = textObject.AddComponent<TextMeshProUGUI>();
			textComponent.raycastTarget = false;
			textComponent.color = KsmGuiStyle.textColor;
			textComponent.font = KsmGuiStyle.textFont;
			textComponent.fontSize = KsmGuiStyle.textSize;
			textComponent.alignment = TextAlignmentOptions.Top;

			tooltipObject.SetLayerRecursive(5);
			//KsmGuiBase.ApplyCanvasScalerScale(TopTransform); 
			tooltipObject.SetActive(false);
			IsVisible = false;
		}

		public void SetTooltipText(string text)
		{
			textComponent.SetText(text);
		}

		public void ShowTooltip(string text)
		{
			if (!string.IsNullOrEmpty(text))
			{
				tooltipObject.SetActive(true);
				textComponent.SetText(text);
				LayoutRebuilder.ForceRebuildLayoutImmediate(TopTransform);
				IsVisible = true;
			}
		}

		public void HideTooltip()
		{
			tooltipObject.SetActive(false);
			IsVisible = false;
		}

		private void Update()
		{
			if (IsVisible)
			{
				Vector3 mouseWorldPos;
				RectTransformUtility.ScreenPointToWorldPointInRectangle(TopTransform, Input.mousePosition, UIMasterController.Instance.uiCamera, out mouseWorldPos);

				mouseWorldPos.x -= ContentTransform.rect.width / 2f;
				mouseWorldPos.y += 15f;
				TopTransform.position = mouseWorldPos;
			}
		}
	}
}
