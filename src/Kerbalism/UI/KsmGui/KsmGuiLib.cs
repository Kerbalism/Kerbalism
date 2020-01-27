using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public static class KsmGuiLib
	{
		public static void SetParentFixScale(this Transform child, Transform parent)
		{
			child.SetParent(parent, false);
		}

		public static void ForceOneScale(RectTransform transform)
		{
			transform.localScale = Vector3.one;

			foreach (RectTransform rt in transform.GetComponentsInChildren<RectTransform>())
				rt.localScale = Vector3.one;
		}

		/// <summary>
		/// Set the anchorMin, anchorMax, pivot and anchoredPosition values of the RectTransform in a simplified way.
		/// Note : the values will be overridden if the parent is a horizontal/vertical layout and/or if SetLayout values have been defined.
		/// </summary>
		/// <param name="originInParent"> Point on the parent transform that will be used as origin for deltaX/Y values. Sets anchorMin & anchorMax </param>
		/// <param name="destinationPivot"> Point on this transform that will be used as destination for deltaX/Y values. Sets pivot</param>
		/// <param name="deltaX"> Distance in pixels between originInParent and destinationPivot</param>
		/// <param name="deltaY"> Distance in pixels between originInParent and destinationPivot</param>
		public static void SetAnchorsAndPosition(this RectTransform transform, TextAnchor originInParent, TextAnchor destinationPivot, int deltaX = 0, int deltaY = 0)
		{
			// set the anchor (origin point) on the parent (screen) that will be used as reference in anchoredPosition
			switch (originInParent)
			{
				case TextAnchor.UpperLeft:    transform.anchorMin = new Vector2(0.0f, 1.0f); transform.anchorMax = new Vector2(0.0f, 1.0f); break;
				case TextAnchor.UpperCenter:  transform.anchorMin = new Vector2(0.5f, 1.0f); transform.anchorMax = new Vector2(0.5f, 1.0f); break;
				case TextAnchor.UpperRight:   transform.anchorMin = new Vector2(1.0f, 1.0f); transform.anchorMax = new Vector2(1.0f, 1.0f); break;
				case TextAnchor.MiddleLeft:   transform.anchorMin = new Vector2(0.0f, 0.5f); transform.anchorMax = new Vector2(0.0f, 0.5f); break;
				case TextAnchor.MiddleCenter: transform.anchorMin = new Vector2(0.5f, 0.5f); transform.anchorMax = new Vector2(0.5f, 0.5f); break;
				case TextAnchor.MiddleRight:  transform.anchorMin = new Vector2(1.0f, 0.5f); transform.anchorMax = new Vector2(1.0f, 0.5f); break;
				case TextAnchor.LowerLeft:    transform.anchorMin = new Vector2(0.0f, 0.0f); transform.anchorMax = new Vector2(0.0f, 0.0f); break;
				case TextAnchor.LowerCenter:  transform.anchorMin = new Vector2(0.5f, 0.0f); transform.anchorMax = new Vector2(0.5f, 0.0f); break;
				case TextAnchor.LowerRight:   transform.anchorMin = new Vector2(1.0f, 0.0f); transform.anchorMax = new Vector2(1.0f, 0.0f); break;
			}

			// set the pivot (destination point) on the window that will be used as reference in anchoredPosition
			switch (destinationPivot)
			{
				case TextAnchor.UpperLeft:    transform.pivot = new Vector2(0.0f, 1.0f); break;
				case TextAnchor.UpperCenter:  transform.pivot = new Vector2(0.5f, 1.0f); break;
				case TextAnchor.UpperRight:   transform.pivot = new Vector2(1.0f, 1.0f); break;
				case TextAnchor.MiddleLeft:   transform.pivot = new Vector2(0.0f, 0.5f); break;
				case TextAnchor.MiddleCenter: transform.pivot = new Vector2(0.5f, 0.5f); break;
				case TextAnchor.MiddleRight:  transform.pivot = new Vector2(1.0f, 0.5f); break;
				case TextAnchor.LowerLeft:    transform.pivot = new Vector2(0.0f, 0.0f); break;
				case TextAnchor.LowerCenter:  transform.pivot = new Vector2(0.5f, 0.0f); break;
				case TextAnchor.LowerRight:   transform.pivot = new Vector2(1.0f, 0.0f); break;
			}

			// distance in pixels between the anchor and the pivot
			transform.anchoredPosition = new Vector2(deltaX, deltaY);
		}

		/// <summary>
		/// Sets the sizeDelta values of the rectTransform. Note : setting sizeDelta needs a non-stretch anchors setting.
		/// Also, the values will be overridden if the parent is a horizontal/vertical layout and/or if SetLayout values have been defined.
		/// </summary>
		/// <param name="transform"></param>
		/// <param name="sizeX"></param>
		/// <param name="sizeY"></param>
		public static void SetSizeDelta(this RectTransform transform, int sizeX, int sizeY)
		{
			transform.sizeDelta = new Vector2(sizeX, sizeY);
		}

		public static void AddImageComponentWithColor(this KsmGuiBase ksmGuiBase, Color color)
		{
			// Unity will trhow an exception, but it doesn't hurt to know why
			if (ksmGuiBase.TopObject.GetComponent<Graphic>() != null)
				Lib.Log("KsmGui error : can't add a background color to " + ksmGuiBase.Name + ", the GameObject already has a graphic component");

			Image image = ksmGuiBase.TopObject.AddComponent<Image>();
			image.color = color;
		}

	}
}
