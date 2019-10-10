using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public static class KsmGuiStyle
	{
		public static readonly float defaultWindowOpacity = 0.8f;

		public static readonly Color textColor = Color.white;
		public static readonly TMP_FontAsset textFont = UISkinManager.TMPFont; // KSP default font : Noto-sans
		public static readonly float textSize = 12f;

		public static readonly Color tooltipBackgroundColor = Color.black;
		public static readonly Color tooltipBorderColor = Color.white; // new Color(1f, 0.82f, 0f); // yellow #FFD200

		public static readonly Color boxColor = new Color(0f, 0f, 0f, 0.2f);
		public static readonly Color selectedBoxColor = new Color(0f, 0f, 0f, 0.5f);

		public static readonly Color headerColor = Color.black;

		public static readonly float tooltipMaxWidth = 300f;

		public static readonly ColorBlock iconTransitionColorBlock = new ColorBlock()
		{
			normalColor = Color.white,
			highlightedColor = new Color(0.8f, 0.8f, 0.8f, 0.8f),
			pressedColor = Color.white,
			disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f),
			colorMultiplier = 1f,
			fadeDuration = 0.1f
		};

		public static readonly SpriteState buttonSpriteSwap = new SpriteState()
		{
			highlightedSprite = Textures.KsmGuiSpriteBtnHighlight,
			pressedSprite = Textures.KsmGuiSpriteBtnHighlight,
			disabledSprite = Textures.KsmGuiSpriteBtnDisabled
		};

	}
}
