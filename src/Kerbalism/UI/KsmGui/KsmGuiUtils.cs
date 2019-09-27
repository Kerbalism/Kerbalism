using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public static class KsmGuiUtils
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

	}
}
