using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM
{
	public class KsmGuiVerticalScrollView : KsmGuiBase
	{
		public RectTransform Content { get; private set; }

		public KsmGuiVerticalScrollView(int minHeight) : base ()
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

			SetLayoutElement(true, false, -1, -1, -1, minHeight);

			// viewport object (child of top object)
			GameObject viewport = new GameObject("viewport");
			RectTransform viewportTransform = viewport.AddComponent<RectTransform>();
			viewport.AddComponent<CanvasRenderer>();

			Mask viewportMask = viewport.AddComponent<Mask>();
			viewportMask.showMaskGraphic = false;

			Image viewportImage = viewport.AddComponent<Image>();
			viewportImage.color = new Color(1f, 1f, 1f, 1f);

			viewportTransform.SetParentAtOneScale(TopTransform);
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

			VerticalLayoutGroup contentGroup = contentObject.AddComponent<VerticalLayoutGroup>();
			contentGroup.padding = new RectOffset(5, 5, 5, 5);
			contentGroup.spacing = 5f;
			contentGroup.childAlignment = TextAnchor.UpperLeft;
			contentGroup.childControlHeight = true;
			contentGroup.childControlWidth = true;
			contentGroup.childForceExpandHeight = false;
			contentGroup.childForceExpandWidth = false;

			Content.SetParentAtOneScale(viewportTransform, false);
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

			scrollbarTransform.SetParentAtOneScale(TopTransform, false);

			// scrollbar sliding area
			GameObject slidingArea = new GameObject("slidingArea");
			RectTransform slidingAreaTransform = slidingArea.AddComponent<RectTransform>();
			slidingAreaTransform.anchorMin = new Vector2(0f, 0f);
			slidingAreaTransform.anchorMax = new Vector2(1f, 1f);
			slidingAreaTransform.pivot = new Vector2(0.5f, 0.5f);
			slidingAreaTransform.anchoredPosition = new Vector2(5f, 5f);
			slidingAreaTransform.sizeDelta = new Vector2(5f, 5f); // scrollbar width / 2
			slidingAreaTransform.SetParentAtOneScale(scrollbarTransform, false);

			// scrollbar handle
			GameObject scrollbarHandle = new GameObject("scrollbarHandle");
			RectTransform handleTransform = scrollbarHandle.AddComponent<RectTransform>();
			scrollbarHandle.AddComponent<CanvasRenderer>();
			handleTransform.pivot = new Vector2(0.5f, 0.5f);
			handleTransform.anchoredPosition = new Vector2(-4f, -4f); // relative to sliding area width
			handleTransform.sizeDelta = new Vector2(-4f, -4f); // relative to sliding area width
			scrollbarComponent.handleRect = handleTransform;

			Image handleImage = scrollbarHandle.AddComponent<Image>();
			handleImage.color = new Color(0.4f, 0.4f, 0.4f);
			handleTransform.SetParentAtOneScale(slidingAreaTransform, false);
			scrollbarComponent.targetGraphic = handleImage;
		}

		public override void Add(KsmGuiBase elementToAdd)
		{
			elementToAdd.TopTransform.SetParentAtOneScale(Content);
			elementToAdd.TopObject.SetLayerRecursive(5);
			//ApplyCanvasScalerScale(elementToAdd.TopTransform);
		}

		public override void AddFirst(KsmGuiBase elementToAdd)
		{
			elementToAdd.TopTransform.SetParentAtOneScale(Content);
			elementToAdd.TopObject.SetLayerRecursive(5);
			elementToAdd.TopTransform.SetAsFirstSibling();
			//ApplyCanvasScalerScale(elementToAdd.TopTransform);
		}

	}
}
