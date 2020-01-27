using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiHorizontalLayout : KsmGuiBase
	{

		public HorizontalLayoutGroup LayoutGroup { get; private set; }

		public KsmGuiHorizontalLayout
		(
			KsmGuiBase parent,
			int spacing = 0,
			int paddingLeft = 0,
			int paddingRight = 0,
			int paddingTop = 0,
			int paddingBottom = 0,
			TextAnchor childAlignement = TextAnchor.UpperLeft
		) : base(parent)
		{
			LayoutGroup = TopObject.AddComponent<HorizontalLayoutGroup>();
			LayoutGroup.spacing = spacing;
			LayoutGroup.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
			LayoutGroup.childAlignment = childAlignement;
			LayoutGroup.childControlHeight = true;
			LayoutGroup.childControlWidth = true;
			LayoutGroup.childForceExpandHeight = false;
			LayoutGroup.childForceExpandWidth = false;
		}
	}
}
