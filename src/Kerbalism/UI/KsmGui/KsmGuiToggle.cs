using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiToggle : KsmGuiHorizontalLayout, IKsmGuiText, IKsmGuiInteractable
	{
		public bool IsOn => ToggleComponent.isOn;
		public Toggle ToggleComponent { get; private set; }
		public KsmGuiText TextObject { get; private set; }
		private UnityAction<bool> onClickAction;

		public KsmGuiToggle(KsmGuiBase parent, string toggleText, bool initialOnState, UnityAction<bool> onClick, string tooltipText = null, int width = -1, int height = 18)
			: base(parent, 5)
		{
			onClickAction = onClick;

			if (width <= 0)
				SetLayoutElement(true, false, -1, height);
			else
				SetLayoutElement(false, false, width, height);

			ToggleComponent = TopObject.AddComponent<Toggle>();
			ToggleComponent.transition = Selectable.Transition.SpriteSwap;
			ToggleComponent.spriteState = KsmGuiStyle.buttonSpriteSwap;
			ToggleComponent.navigation = new Navigation() { mode = Navigation.Mode.None }; // fix the transitions getting stuck
			ToggleComponent.toggleTransition = Toggle.ToggleTransition.Fade;
			ToggleComponent.isOn = initialOnState;
			ToggleComponent.onValueChanged.AddListener(onClickAction);

			GameObject background = new GameObject("Background");
			RectTransform backgroundTransform = background.AddComponent<RectTransform>();
			LayoutElement backgroundLayout = background.AddComponent<LayoutElement>();
			backgroundLayout.preferredHeight = 18f; // the toggle is always 18x18
			backgroundLayout.preferredWidth = 18f;
			backgroundTransform.SetParentFixScale(TopTransform);
			background.AddComponent<CanvasRenderer>();
			Image backgroundImage = background.AddComponent<Image>();
			backgroundImage.sprite = Textures.KsmGuiSpriteBtnNormal;
			backgroundImage.type = Image.Type.Sliced;
			backgroundImage.fillCenter = true;
			ToggleComponent.targetGraphic = backgroundImage;

			GameObject checkmark = new GameObject("Checkmark");
			RectTransform checkmarkTransform = checkmark.AddComponent<RectTransform>();
			checkmarkTransform.SetAnchorsAndPosition(TextAnchor.MiddleCenter, TextAnchor.MiddleCenter, 0, 0);
			checkmarkTransform.SetSizeDelta(10, 10); // a checkbox is always 10x10, centered in the toggle
			checkmarkTransform.SetParentFixScale(backgroundTransform);
			checkmark.AddComponent<CanvasRenderer>();
			RawImage checkmarkImage = checkmark.AddComponent<RawImage>();
			checkmarkImage.texture = Textures.KsmGuiTexCheckmark;
			ToggleComponent.graphic = checkmarkImage;

			TextObject = new KsmGuiText(this, toggleText, null, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
			if (width <= 0)
				SetLayoutElement(true, false, -1, height);
			else
				SetLayoutElement(false, false, width-18-5, height);
			TextObject.TopTransform.SetParentFixScale(TopTransform);

			if (tooltipText != null) SetTooltipText(tooltipText);
		}

		public void SetInteractable(bool interactable)
		{
			ToggleComponent.interactable = interactable;
		}

		public void SetText(string text)
		{
			TextObject.SetText(text);
		}

		public void SetOnState(bool isOn, bool fireOnClick = false)
		{
			if (!fireOnClick)
				ToggleComponent.onValueChanged.RemoveListener(onClickAction);

			ToggleComponent.isOn = isOn;

			if (!fireOnClick)
				ToggleComponent.onValueChanged.AddListener(onClickAction);
		}
	}
}
