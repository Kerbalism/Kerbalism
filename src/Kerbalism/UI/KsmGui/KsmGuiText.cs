using System;
using System.Collections.Generic;
using TMPro;

namespace KERBALISM.KsmGui
{
	public class KsmGuiText : KsmGuiBase, IKsmGuiText
	{
		public TextMeshProUGUI TextComponent { get; private set; }
		

		private TextAlignmentOptions savedAlignement;

		public KsmGuiText(
			KsmGuiBase parent,
			string text,
			string tooltipText = null,
			TextAlignmentOptions alignement = TextAlignmentOptions.TopLeft,
			bool wordWrap = true,
			TextOverflowModes overflowMode = TextOverflowModes.Overflow
			) : base(parent)
		{
			savedAlignement = alignement;
			TextComponent = TopObject.AddComponent<TextMeshProUGUI>();
			TextComponent.color = KsmGuiStyle.textColor;
			TextComponent.font = KsmGuiStyle.textFont;
			TextComponent.fontSize = KsmGuiStyle.textSize;
			TextComponent.alignment = alignement;
			TextComponent.enableWordWrapping = wordWrap;
			TextComponent.overflowMode = overflowMode;
			TextComponent.text = text;
			SetLayoutElement(true);
			//TextComponent.raycastTarget = false;

			if (tooltipText != null) SetTooltipText(tooltipText);
		}

		public string Text
		{
			get => TextComponent.text;
			set => TextComponent.SetText(value);
		}

		// workaround for a textmeshpro bug :
		// https://forum.unity.com/threads/textmeshprougui-alignment-resets-when-enabling-disabling-gameobject.549784/#post-3901597
		public override bool Enabled
		{
			get => base.Enabled;
			set
			{
				base.Enabled = value;
				TextComponent.alignment = savedAlignement;
			}
		}



	}
}
