using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiButton : KsmGuiHorizontalLayout, IKsmGuiText, IKsmGuiInteractable, IKsmGuiButton, IKsmGuiIcon
	{
		public Image ImageComponent { get; private set; }
		public Button ButtonComponent { get; private set; }
		public KsmGuiText TextObject { get; private set; }
		public KsmGuiIcon IconObject { get; private set; }

		private UnityAction onClick;

		public KsmGuiButton
			(
			KsmGuiBase parent,
			string buttonText,
			UnityAction onClick,
			string tooltipText = null,
			int width = -1,
			int height = 18,
			Texture2D iconTexture = null,
			int iconWidth = 16,
			int iconHeight = 16
			) : base(parent, 2, 3, 3, 0, 0, TextAnchor.MiddleCenter)
		{
			// buttons are 18 px high and expand horizontaly in their container by default, but a fixed width can be defined in the ctor
			// in any case, SetLayoutElement can be called after to customise the button size.
			if (width <= 0)
				SetLayoutElement(true, false, -1, height);
			else
				SetLayoutElement(false, false, width, height);

			ImageComponent = TopObject.AddComponent<Image>();
			ImageComponent.sprite = Textures.KsmGuiSpriteBtnNormal;
			ImageComponent.type = Image.Type.Sliced;
			ImageComponent.fillCenter = true;

			ButtonComponent = TopObject.AddComponent<Button>();
			ButtonComponent.transition = Selectable.Transition.SpriteSwap;
			ButtonComponent.spriteState = KsmGuiStyle.buttonSpriteSwap;
			ButtonComponent.navigation = new Navigation() { mode = Navigation.Mode.None }; // fix the transitions getting stuck

			SetButtonOnClick(onClick);

			SetIconTexture(iconTexture, iconWidth, iconHeight);

			TextObject = new KsmGuiText(this, buttonText, null, TextAlignmentOptions.Center);
			TextObject.SetLayoutElement(true);
			TextObject.TopTransform.SetParentFixScale(TopTransform);

			if (tooltipText != null) SetTooltipText(tooltipText);
		}

		public string Text
		{
			get => TextObject.Text;
			set => TextObject.Text = value;
		}

		public bool Interactable
		{
			get => ButtonComponent.interactable;
			set => ButtonComponent.interactable = value;
		}

		public void SetButtonOnClick(UnityAction action)
		{
			if (onClick != null)
				ButtonComponent.onClick.RemoveListener(onClick);

			onClick = action;

			if (action != null)
				ButtonComponent.onClick.AddListener(onClick);
		}

		public void SetIconTexture(Texture2D texture, int width = 16, int height = 16)
		{
			if (texture != null && IconObject == null)
			{
				IconObject = new KsmGuiIcon(this, texture, null, width, width);
				IconObject.TopTransform.SetParentFixScale(TopTransform);
			}

			if (IconObject != null)
				IconObject.SetIconTexture(texture);
		}

		public void SetIconColor(Color color)
		{
			IconObject.SetIconColor(color);
		}

		public void SetIconColor(Lib.Kolor kColor)
		{
			IconObject.SetIconColor(kColor);
		}
	}
}
