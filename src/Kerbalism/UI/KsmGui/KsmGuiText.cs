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
	public class KsmGuiText : KsmGuiBase, IKsmGuiText
	{
		public TextMeshProUGUI TextComponent { get; private set; }

		public KsmGuiText(string text, string tooltipText = null, TextAlignmentOptions alignement = TextAlignmentOptions.TopLeft) : base()
		{
			TextComponent = TopObject.AddComponent<TextMeshProUGUI>();
			TextComponent.color = KsmGuiStyle.textColor;
			TextComponent.font = KsmGuiStyle.textFont;
			TextComponent.fontSize = KsmGuiStyle.textSize;
			TextComponent.alignment = alignement;
			TextComponent.text = text;

			if (tooltipText != null) SetTooltipText(text);
		}

		public void SetText(string text)
		{
			TextComponent.text = text;
		}

	}
}
