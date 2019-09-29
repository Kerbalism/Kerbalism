using System;
using System.Collections.Generic;
using UnityEngine;

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

	}
}
