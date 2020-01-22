using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiVerticalScrollView : KsmGuiBase
	{
		public RectTransform Content { get; private set; }
		public VerticalLayoutGroup ContentGroup { get; private set; }

		public override RectTransform ParentTransformForChilds => Content;

		public KsmGuiVerticalScrollView(KsmGuiBase parent, int contentSpacing = 5, int contentPaddingLeft = 5, int contentPaddingRight = 5, int contentPaddingTop = 5, int contentPaddingBottom = 5) : base (parent)
		{
			ScrollRect scrollRect = TopObject.AddComponent<ScrollRect>();
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Elastic;
			scrollRect.elasticity = 0.1f;
			scrollRect.inertia = true;
			scrollRect.decelerationRate = 0.15f;
			scrollRect.scrollSensitivity = 10f;
			scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
			scrollRect.verticalScrollbarSpacing = -3f;

			Image scrollRectBackground = TopObject.AddComponent<Image>();
			scrollRectBackground.color = KsmGuiStyle.boxColor;

			// viewport object (child of top object)
			GameObject viewport = new GameObject("viewport");
			RectTransform viewportTransform = viewport.AddComponent<RectTransform>();
			viewport.AddComponent<CanvasRenderer>();

			// Note : using a standard "Mask" has a bug where scrollrect content is visible
			// in other windows scollrects (like if all masks were "global" for all masked content)
			// see https://issuetracker.unity3d.com/issues/scroll-view-content-is-visible-outside-of-mask-when-there-is-another-masked-ui-element-in-the-same-canvas
			// using a RectMask2D fixes it, at the cost of the ability to use an image mask (but we don't care)
			viewport.AddComponent<RectMask2D>();

			//Mask viewportMask = viewport.AddComponent<Mask>();
			//viewportMask.showMaskGraphic = false;
			//Image viewportImage = viewport.AddComponent<Image>();
			//viewportImage.color = new Color(1f, 1f, 1f, 1f);

			viewportTransform.SetParentFixScale(TopTransform);
			scrollRect.viewport = viewportTransform;

			// content object (child of viewport)
			GameObject contentObject = new GameObject("content");
			Content = contentObject.AddComponent<RectTransform>();
			Content.anchorMin = new Vector2(0f, 1f);
			Content.anchorMax = new Vector2(1f, 1f);
			Content.pivot = new Vector2(0f, 1f);
			Content.anchoredPosition = new Vector2(0f, 0f);
			Content.sizeDelta = new Vector2(0f, 0f);

			ContentSizeFitter sizeFitter = contentObject.AddComponent<ContentSizeFitter>();
			sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			ContentGroup = contentObject.AddComponent<VerticalLayoutGroup>();
			ContentGroup.padding = new RectOffset(contentPaddingLeft, contentPaddingRight, contentPaddingTop, contentPaddingBottom);
			ContentGroup.spacing = contentSpacing;
			ContentGroup.childAlignment = TextAnchor.UpperLeft;
			ContentGroup.childControlHeight = true;
			ContentGroup.childControlWidth = true;
			ContentGroup.childForceExpandHeight = false;
			ContentGroup.childForceExpandWidth = false;

			Content.SetParentFixScale(viewportTransform);
			scrollRect.content = Content;

			// scrollbar (child of top object)
			GameObject scrollbar = new GameObject("scrollbar");
			RectTransform scrollbarTransform = scrollbar.AddComponent<RectTransform>();
			scrollbarTransform.anchorMin = new Vector2(1f, 0f);
			scrollbarTransform.anchorMax = new Vector2(1f, 1f);
			scrollbarTransform.pivot = new Vector2(1f, 1f);
			scrollbarTransform.anchoredPosition = new Vector2(0f, 0f);
			scrollbarTransform.sizeDelta = new Vector2(10f, 0f); // scrollbar width
			scrollbar.AddComponent<CanvasRenderer>();

			Image scrollBarImage = scrollbar.AddComponent<Image>();
			scrollBarImage.color = Color.black;

			Scrollbar scrollbarComponent = scrollbar.AddComponent<Scrollbar>();
			scrollbarComponent.interactable = true;
			scrollbarComponent.transition = Selectable.Transition.ColorTint;
			scrollbarComponent.colors = new ColorBlock()
			{
				normalColor = Color.white,
				highlightedColor = Color.white,
				pressedColor = new Color(0.8f, 0.8f, 0.8f),
				disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f),
				colorMultiplier = 1f,
				fadeDuration = 0.1f
			};
			scrollbarComponent.navigation = new Navigation() { mode = Navigation.Mode.None };
			scrollbarComponent.direction = Scrollbar.Direction.BottomToTop;
			scrollRect.verticalScrollbar = scrollbarComponent;

			scrollbarTransform.SetParentFixScale(TopTransform);

			// scrollbar sliding area
			GameObject slidingArea = new GameObject("slidingArea");
			RectTransform slidingAreaTransform = slidingArea.AddComponent<RectTransform>();
			slidingAreaTransform.anchorMin = new Vector2(0f, 0f);
			slidingAreaTransform.anchorMax = new Vector2(1f, 1f);
			slidingAreaTransform.pivot = new Vector2(0.5f, 0.5f);
			slidingAreaTransform.anchoredPosition = new Vector2(5f, 5f);
			slidingAreaTransform.sizeDelta = new Vector2(5f, 5f); // scrollbar width / 2
			slidingAreaTransform.SetParentFixScale(scrollbarTransform);

			// scrollbar handle
			GameObject scrollbarHandle = new GameObject("scrollbarHandle");
			RectTransform handleTransform = scrollbarHandle.AddComponent<RectTransform>();
			scrollbarHandle.AddComponent<CanvasRenderer>();
			handleTransform.anchorMin = new Vector2(0f, 0f);
			handleTransform.anchorMax = new Vector2(1f, 1f);
			handleTransform.pivot = new Vector2(0.5f, 0.5f);
			handleTransform.anchoredPosition = new Vector2(-4f, -4f); // relative to sliding area width
			handleTransform.sizeDelta = new Vector2(-4f, -4f); // relative to sliding area width
			scrollbarComponent.handleRect = handleTransform;

			Image handleImage = scrollbarHandle.AddComponent<Image>();
			handleImage.color = new Color(0.4f, 0.4f, 0.4f);
			handleTransform.SetParentFixScale(slidingAreaTransform);
			scrollbarComponent.targetGraphic = handleImage;
		}
	}
}
