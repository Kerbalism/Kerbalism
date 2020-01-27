using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiToggleList<T> : KsmGuiVerticalLayout
	{
		public ToggleGroup ToggleGroupComponent { get; private set; }
		public UnityAction<T> OnChildToggleActivated { get; set; }
		public List<KsmGuiToggleListElement<T>> ChildToggles { get; private set; } = new List<KsmGuiToggleListElement<T>>();

		public KsmGuiToggleList(KsmGuiBase parent, UnityAction<T> onChildToggleActivated)
			: base(parent, 2, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{
			ToggleGroupComponent = TopObject.AddComponent<ToggleGroup>();
			OnChildToggleActivated = onChildToggleActivated;
		}
	}

	public class KsmGuiToggleListElement<T> : KsmGuiHorizontalLayout, IKsmGuiInteractable, IKsmGuiText, IKsmGuiToggle
	{
		public KsmGuiText TextObject { get; private set; }
		public Toggle ToggleComponent { get; private set; }
		public T ToggleId { get; private set; }
		private KsmGuiToggleList<T> parent;

		public KsmGuiToggleListElement(KsmGuiToggleList<T> parent, T toggleId, string text) : base(parent)
		{
			ToggleComponent = TopObject.AddComponent<Toggle>();
			ToggleComponent.transition = Selectable.Transition.None;
			ToggleComponent.navigation = new Navigation() { mode = Navigation.Mode.None };
			ToggleComponent.isOn = false;
			ToggleComponent.toggleTransition = Toggle.ToggleTransition.Fade;
			ToggleComponent.group = parent.ToggleGroupComponent;

			this.parent = parent;
			parent.ChildToggles.Add(this);
			ToggleId = toggleId;
			ToggleComponent.onValueChanged.AddListener(NotifyParent);

			Image image = TopObject.AddComponent<Image>();
			image.color = KsmGuiStyle.boxColor;

			SetLayoutElement(false, false, -1, -1, -1, 14);

			KsmGuiVerticalLayout highlightImage = new KsmGuiVerticalLayout(this);
			Image bgImage = highlightImage.TopObject.AddComponent<Image>();
			bgImage.color = KsmGuiStyle.selectedBoxColor;
			bgImage.raycastTarget = false;
			ToggleComponent.graphic = bgImage;

			TextObject = new KsmGuiText(highlightImage, text);
			TextObject.SetLayoutElement(true);
		}

		private void NotifyParent(bool enabled)
		{
			if (enabled && parent.OnChildToggleActivated != null)
			{
				parent.OnChildToggleActivated(ToggleId);
			}
		}

		public void SetInteractable(bool interactable)
		{
			ToggleComponent.interactable = interactable;
		}

		public void SetText(string text)
		{
			TextObject.SetText(text);
		}

		public void SetToggleOnChange(UnityAction<bool> action)
		{
			ToggleComponent.onValueChanged.AddListener(action);
		}
	}
}
