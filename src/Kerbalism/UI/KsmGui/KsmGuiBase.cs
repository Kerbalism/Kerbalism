using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiBase
	{
		public RectTransform TopTransform { get; private set; }
		public GameObject TopObject { get; private set; }
		public LayoutElement LayoutElement { get; private set; }
		public KsmGuiUpdateHandler UpdateHandler { get; private set; }
		private KsmGuiTooltip tooltip;

		public KsmGuiBase()
		{
			TopObject = new GameObject(Name);
			TopTransform = TopObject.AddComponent<RectTransform>();
			TopObject.AddComponent<CanvasRenderer>();
			TopObject.SetLayerRecursive(5);
		}

		public virtual string Name => GetType().Name;

		public bool Enabled
		{
			get => TopObject.activeSelf;
			set
			{
				TopObject.SetActive(value);
				// if enabling and update frequency is more than every update, update immediately
				if (value && UpdateHandler != null)
					UpdateHandler.UpdateASAP();
			}
		}

		/// <summary> callback that will be called on this object Update(). Won't be called if Enabled = false </summary>
		/// <param name="updateFrequency">amount of Update() frames skipped between each call. 50 =~ 1 sec </param>
		public void SetUpdateAction(Action action, int updateFrequency = 1)
		{
			if (UpdateHandler == null)
				UpdateHandler = TopObject.AddComponent<KsmGuiUpdateHandler>();

			UpdateHandler.updateAction = action;
			UpdateHandler.updateFrequency = updateFrequency;
			//UpdateHandler.UpdateASAP();
		}

		public void SetTooltipText(string text)
		{
			if (text == null)
				return;

			if (tooltip == null)
				tooltip = TopObject.AddComponent<KsmGuiTooltip>();

			tooltip.SetTooltipText(text);
		}

		/// <summary> Add sizing constraints trough a LayoutElement component</summary>
		public void SetLayoutElement(bool flexibleWidth = false, bool flexibleHeight = false, int preferredWidth = -1, int preferredHeight = -1, int minWidth = -1, int minHeight = -1)
		{
			if (LayoutElement == null)
				LayoutElement = TopObject.AddComponent<LayoutElement>();

			LayoutElement.flexibleWidth = flexibleWidth ? 1f : -1f;
			LayoutElement.flexibleHeight = flexibleHeight ? 1f : -1f;
			LayoutElement.preferredWidth = preferredWidth;
			LayoutElement.preferredHeight = preferredHeight;
			LayoutElement.minWidth = minWidth;
			LayoutElement.minHeight = minHeight;
		}

		public virtual void Add(KsmGuiBase elementToAdd)
		{
			elementToAdd.TopTransform.SetParentFixScale(TopTransform);
			elementToAdd.TopObject.SetLayerRecursive(5);
		}

		public virtual void AddFirst(KsmGuiBase elementToAdd)
		{
			elementToAdd.TopTransform.SetParentFixScale(TopTransform);
			elementToAdd.TopObject.SetLayerRecursive(5);
			elementToAdd.TopTransform.SetAsFirstSibling();
		}

		public virtual void AddAfter(KsmGuiBase afterThis, KsmGuiBase elementToAdd)
		{

		}


	}
}
