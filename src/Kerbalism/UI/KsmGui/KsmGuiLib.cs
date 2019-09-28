using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM.KsmGui
{
	public static class KsmGuiLib
	{
		public static void SetParentAtOneScale(this Transform child, Transform parent)
		{
			child.SetParent(parent);
			child.localScale = Vector3.one;
		}

		public static void SetParentAtOneScale(this Transform child, Transform parent, bool worldPositionStay)
		{
			child.SetParent(parent, worldPositionStay);
			child.localScale = Vector3.one;
		}

		public static void ForceOneScale(RectTransform transform)
		{
			transform.localScale = Vector3.one;

			foreach (RectTransform rt in transform.GetComponentsInChildren<RectTransform>())
				rt.localScale = Vector3.one;
		}

	}
}
