using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	/// <summary>
	/// a 16x16 icon that can be clicked
	/// </summary>
	public class KsmGuiIconButton : KsmGuiIcon, IKsmGuiInteractable, IKsmGuiButton, IKsmGuiIcon
	{
		public Button ButtonComponent { get; private set; }
		private UnityAction onClick;

		public KsmGuiIconButton(KsmGuiBase parent, Texture2D texture, UnityAction onClick = null, string tooltipText = null, int width = 16, int height = 16)
			: base(parent, texture, tooltipText, width, height)
		{
			ButtonComponent = TopObject.AddComponent<Button>();
			ButtonComponent.targetGraphic = Image;
			ButtonComponent.interactable = true;
			ButtonComponent.transition = Selectable.Transition.ColorTint;
			ButtonComponent.colors = KsmGuiStyle.iconTransitionColorBlock;
			ButtonComponent.navigation = new Navigation() { mode = Navigation.Mode.None }; // fix the transitions getting stuck

			this.onClick = onClick;
			SetButtonOnClick(onClick);
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
	}
}
