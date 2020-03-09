using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiSlider : KsmGuiBase, IKsmGuiInteractable
	{
		public Slider SliderComponent { get; private set; }

		RectTransform fillAreaTransform;
		RectTransform fillTransform;
		RectTransform handleAreaTransform;
		RectTransform handleTransform;

		public KsmGuiSlider(
			KsmGuiBase parent,
			float minValue,
			float maxValue,
			bool wholeNumbers,
			UnityAction<float> onValueChanged = null,
			string tooltipText = null,
			int width = -1,
			int height = 18

			) : base(parent)
		{
			if (width <= 0)
				SetLayoutElement(true, false, -1, height);
			else
				SetLayoutElement(false, false, width, height);

			SliderComponent = TopObject.AddComponent<Slider>();
			if (onValueChanged != null)
				SliderComponent.onValueChanged.AddListener(onValueChanged);

			SliderComponent.minValue = minValue;
			SliderComponent.maxValue = maxValue;
			SliderComponent.wholeNumbers = wholeNumbers;
			SliderComponent.navigation = new Navigation() { mode = Navigation.Mode.None };

			GameObject background = new GameObject("Background");
			RectTransform backgroundTransform = background.AddComponent<RectTransform>();
			backgroundTransform.SetParentFixScale(TopTransform);
			backgroundTransform.anchorMin = new Vector2(0f, 0f);
			backgroundTransform.anchorMax = new Vector2(1f, 1f);
			backgroundTransform.pivot = new Vector2(0.5f, 0.5f);
			backgroundTransform.offsetMin = new Vector2(0f, 0f);
			backgroundTransform.offsetMax = new Vector2(0f, 0f);
			background.AddComponent<CanvasRenderer>();
			Image backgroundImage = background.AddComponent<Image>();
			backgroundImage.sprite = Textures.KsmGuiSpriteBtnNormal;
			backgroundImage.type = Image.Type.Sliced;
			backgroundImage.fillCenter = true;

			GameObject fillArea = new GameObject("Fill Area");
			fillAreaTransform = fillArea.AddComponent<RectTransform>();
			fillAreaTransform.SetParentFixScale(TopTransform);
			fillAreaTransform.anchorMin = new Vector2(0f, 0f);
			fillAreaTransform.anchorMax = new Vector2(1f, 1f);
			fillAreaTransform.pivot = new Vector2(0.5f, 0.5f);
			fillAreaTransform.offsetMin = new Vector2(5f, 4f);
			fillAreaTransform.offsetMax = new Vector2(-15f, -4f);

			GameObject fill = new GameObject("Fill");
			fillTransform = fill.AddComponent<RectTransform>();
			fillTransform.SetParentFixScale(fillAreaTransform);
			fillTransform.anchoredPosition = new Vector2(0f, 0f);
			fillTransform.sizeDelta = new Vector2(0f, 0f);
			fill.AddComponent<CanvasRenderer>();
			Image fillImage = fill.AddComponent<Image>();
			fillImage.color = new Color(0.43529f, 0.38039f, 0.15294f);
			SliderComponent.fillRect = fillTransform;

			GameObject handleArea = new GameObject("Handle Area");
			handleAreaTransform = handleArea.AddComponent<RectTransform>();
			handleAreaTransform.SetParentFixScale(TopTransform);
			handleAreaTransform.anchorMin = new Vector2(0f, 0f);
			handleAreaTransform.anchorMax = new Vector2(1f, 1f);
			handleAreaTransform.pivot = new Vector2(0.5f, 0.5f);
			handleAreaTransform.offsetMin = new Vector2(12f, 0f);
			handleAreaTransform.offsetMax = new Vector2(-12f, 0f);

			GameObject handle = new GameObject("Handle");
			handleTransform = handle.AddComponent<RectTransform>();
			handleTransform.SetParentFixScale(handleAreaTransform);
			handleTransform.pivot = new Vector2(0.5f, 0.5f);
			handleTransform.offsetMin = new Vector2(-8f, 2f);
			handleTransform.offsetMax = new Vector2(8f, -2f);
			fill.AddComponent<CanvasRenderer>();
			RawImage handleImage = handle.AddComponent<RawImage>();
			handleImage.texture = Textures.KsmGuiTexCheckmark;
			SliderComponent.targetGraphic = handleImage;
			SliderComponent.handleRect = handleTransform;
		}

		public float Value
		{
			get => SliderComponent.value;
			set => SliderComponent.value = value;
		}

		public float NormalizedValue
		{
			get => SliderComponent.normalizedValue;
			set => SliderComponent.normalizedValue = value;
		}
		public bool Interactable
		{
			get => SliderComponent.interactable;
			set => SliderComponent.interactable = value;
		}
	}
}
