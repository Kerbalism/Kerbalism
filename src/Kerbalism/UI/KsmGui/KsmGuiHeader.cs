using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiHeader : KsmGuiHorizontalLayout, IKsmGuiText
	{


		public KsmGuiText TextObject { get; private set; }

		public KsmGuiHeader(KsmGuiBase parent, string title, Color backgroundColor = default, int textPreferredWidth = -1)
			: base(parent, 2, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{
			// default : black background
			Image image = TopObject.AddComponent<Image>();
			if (backgroundColor == default)
				image.color = Color.black;
			else
				image.color = backgroundColor;


			TextObject = new KsmGuiText(this, title, null, TextAlignmentOptions.Center);
			TextObject.TextComponent.fontStyle = FontStyles.UpperCase;
			TextObject.SetLayoutElement(true, false, textPreferredWidth, -1, -1, 16);
		}

		public string Text
		{
			get => TextObject.Text;
			set => TextObject.Text = value;
		}
	}
}
