using System;
using System.Collections.Generic;
using TMPro;

namespace KERBALISM.KsmGui
{
	public class KsmGuiTextBox : KsmGuiVerticalSection, IKsmGuiText
	{
		public KsmGuiText TextObject { get; private set; }

		public KsmGuiTextBox(KsmGuiBase parent, string text, string tooltipText = null, TextAlignmentOptions alignement = TextAlignmentOptions.TopLeft) : base(parent)
		{
			SetLayoutElement(true, true);
			TextObject = new KsmGuiText(this, text, null, alignement);

			if (tooltipText != null) SetTooltipText(text);
		}

		public string Text
		{
			get => TextObject.Text;
			set => TextObject.Text = value;
		}
	}
}
