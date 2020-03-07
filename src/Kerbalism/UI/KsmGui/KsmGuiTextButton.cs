using System;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiTextButton : KsmGuiText
	{
		public Button ButtonComponent { get; private set; }
		private UnityAction onClick;

		public KsmGuiTextButton(KsmGuiBase parent, string text, UnityAction onClick, string tooltipText = null, TextAlignmentOptions alignement = TextAlignmentOptions.TopLeft, bool wordWrap = true, TextOverflowModes overflowMode = TextOverflowModes.Overflow) : base(parent, text, tooltipText, alignement, wordWrap, overflowMode)
		{
			ButtonComponent = TopObject.AddComponent<Button>();
			ButtonComponent.interactable = true;
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
