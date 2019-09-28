using System;
using System.Collections.Generic;
using TMPro;

namespace KERBALISM.KsmGui
{
	public class KsmGuiTextBox : KsmGuiVerticalSection, IKsmGuiText
	{
		public KsmGuiText TextObject { get; private set; }

		public KsmGuiTextBox(string text, string tooltipText = null, TextAlignmentOptions alignement = TextAlignmentOptions.TopLeft) : base()
		{
			SetLayoutElement(true, true);
			TextObject = new KsmGuiText(text, null, alignement);
			Add(TextObject);

			if (tooltipText != null) SetTooltipText(text);
		}

		public void SetText(string text)
		{
			TextObject.SetText(text);
		}
	}
}
