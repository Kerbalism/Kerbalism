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

		public KsmGuiHeader(string title)
			: base(0, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{
			// black background
			Image image = TopObject.AddComponent<Image>();
			image.color = Color.black;

			KsmGuiText TextObject = new KsmGuiText(title, null, TextAlignmentOptions.Center);
			TextObject.TextComponent.fontStyle = FontStyles.UpperCase;
			TextObject.SetLayoutElement(true, false, -1, -1, -1, 16);
			Add(TextObject);
		}

		public void SetText(string text)
		{
			TextObject.SetText(text);
		}
	}
}
