using System;
using System.Collections;
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
		private KsmGuiLayoutOptimizer layoutOptimizer;

		/// <summary>
		/// transform that will be used as parent for child KsmGui objects.
		/// override this if you have an internal object hierarchy where child
		/// objects must be parented to a specific transform (ex : scroll view)
		/// </summary>
		public virtual RectTransform ParentTransformForChilds => TopTransform;

		public KsmGuiBase(KsmGuiBase parent)
		{
			TopObject = new GameObject(Name);
			TopTransform = TopObject.AddComponent<RectTransform>();
			TopObject.AddComponent<CanvasRenderer>();

			if (parent != null)
			{
				layoutOptimizer = parent.layoutOptimizer;
				TopTransform.SetParentFixScale(parent.ParentTransformForChilds);
			}
			else
			{
				layoutOptimizer = TopObject.AddComponent<KsmGuiLayoutOptimizer>();
			}

			TopObject.SetLayerRecursive(5);
		}

		public virtual string Name => GetType().Name;

		public virtual bool Enabled
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

		/// <summary> coroutine-like (IEnumerable) method that will be called repeatedly as long as Enabled = true </summary>
		public void SetUpdateCoroutine(KsmGuiUpdateCoroutine coroutineFactory)
		{
			if (UpdateHandler == null)
				UpdateHandler = TopObject.AddComponent<KsmGuiUpdateHandler>();

			UpdateHandler.coroutineFactory = coroutineFactory;
		}

		public void ForceExecuteCoroutine(bool fromStart = false)
		{
			if (UpdateHandler != null)
				UpdateHandler.ForceExecuteCoroutine(fromStart);
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

		public void RebuildLayout() => layoutOptimizer.RebuildLayout();

		public void MoveAsFirstChild()
		{
			TopTransform.SetAsFirstSibling();
		}

		public void MoveAfter(KsmGuiBase afterThis)
		{
			
		}
	}
}
