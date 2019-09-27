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
	public class KsmGuiVerticalSection : KsmGuiVerticalLayout
	{
		public KsmGuiVerticalSection() : base(0,5,5,5,5, TextAnchor.UpperLeft)
		{
			Image background = TopObject.AddComponent<Image>();
			background.color = KsmGuiStyle.boxColor;
		}

	}
}
