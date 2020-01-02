using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiVerticalSection : KsmGuiVerticalLayout
	{
		public KsmGuiVerticalSection(KsmGuiBase parent) : base(parent, 0, 5,5,5,5, TextAnchor.UpperLeft)
		{
			Image background = TopObject.AddComponent<Image>();
			background.color = KsmGuiStyle.boxColor;
		}

	}
}
