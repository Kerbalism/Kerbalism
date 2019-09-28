using System;
using System.Collections.Generic;
using TMPro;

namespace KERBALISM.KsmGui
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
