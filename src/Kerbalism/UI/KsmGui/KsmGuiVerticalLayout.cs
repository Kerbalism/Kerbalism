using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class KsmGuiVerticalLayout : KsmGuiBase
	{
		public VerticalLayoutGroup LayoutGroup { get; private set; }

		public KsmGuiVerticalLayout
			(
				int spacing = 0,
				int paddingLeft = 0,
				int paddingRight = 0,
				int paddingTop = 0,
				int paddingBottom = 0,
				TextAnchor childAlignement = TextAnchor.UpperLeft
			) : base()
		{
			LayoutGroup = TopObject.AddComponent<VerticalLayoutGroup>();
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
