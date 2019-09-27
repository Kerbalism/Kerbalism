using KSP.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM
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
				if (value && UpdateHandler != null && UpdateHandler.updateFrequency > 1)
					UpdateHandler.updateAction();
			}
		}

		public void SetUpdateAction(Action action, int updateFrequency = 1)
		{
			if (UpdateHandler == null)
				UpdateHandler = TopObject.AddComponent<KsmGuiUpdateHandler>();

			UpdateHandler.updateAction = action;
			UpdateHandler.updateFrequency = updateFrequency;
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
			elementToAdd.TopTransform.SetParentAtOneScale(TopTransform);
			elementToAdd.TopObject.SetLayerRecursive(5);
			//ApplyCanvasScalerScale(elementToAdd.TopTransform);
		}

		public virtual void AddFirst(KsmGuiBase elementToAdd)
		{
			elementToAdd.TopTransform.SetParentAtOneScale(TopTransform);
			elementToAdd.TopObject.SetLayerRecursive(5);
			elementToAdd.TopTransform.SetAsFirstSibling();
			//ApplyCanvasScalerScale(elementToAdd.TopTransform);
		}

		public virtual void AddAfter(KsmGuiBase afterThis, KsmGuiBase elementToAdd)
		{

		}


		// objects instantiated at runtime under a CanvasScaler have their scale
		// automagically altered to "compensate" the scaling factor of the CanvasScaler
		// this is silly and annoying (and took me 2 days to figure out the problem)
		// so here is the brute-force solution
		public static void ApplyCanvasScalerScale(RectTransform transform)
		{
			transform.localScale = Vector3.one;

			foreach (RectTransform rt in transform.GetComponentsInChildren<RectTransform>())
				rt.localScale = Vector3.one;
		}


	}
}
