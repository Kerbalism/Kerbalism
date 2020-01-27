using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiIcon : KsmGuiBase, IKsmGuiIcon
	{
		public RawImage Image { get; private set; }
		public RectTransform IconTransform { get; private set; }

		/// <summary> force the size of the icon. This will ignore all layout constraints</summary>
		public void ForceIconSize(float width, float height) => IconTransform.sizeDelta = new Vector2(width, height);

		public KsmGuiIcon(KsmGuiBase parent, Texture2D texture, string tooltipText = null, int width = 16, int height = 16) : base(parent)
		{
			// we use a child gameobject because KsmGuiIcon can be used as a button (we need it as a child in this case)
			// we directly set its size trough anchors / sizeDelta instead of using layout components, this way it can be used
			// both standalone or as a button without having to mess with the layout component
			// We still set a min height/min size layout on the top object to make sure other objects in a group won't overlap

			GameObject icon = new GameObject("icon");
			IconTransform = icon.AddComponent<RectTransform>();
			icon.AddComponent<CanvasRenderer>();

			Image = TopObject.AddComponent<RawImage>();

			// make sure pivot is at the center
			IconTransform.pivot = new Vector2(0.5f, 0.5f);

			// set anchors to middle-center
			IconTransform.anchorMin = new Vector2(0.5f, 0.5f);
			IconTransform.anchorMax = new Vector2(0.5f, 0.5f);

			// anchor-pivot distance
			IconTransform.anchoredPosition = new Vector2(0f, 0f);

			SetIconTexture(texture, width, height);

			IconTransform.SetParentFixScale(TopTransform);

			if (tooltipText != null) SetTooltipText(tooltipText);
		}

		public void SetIconTexture(Texture2D texture, int width = 16, int height = 16)
		{
			SetLayoutElement(false, false, -1, -1, width, height);
			Image.texture = texture;
			IconTransform.sizeDelta = new Vector2(width, height);
		}

		public void SetIconColor(Color color)
		{
			Image.color = color;
		}

		public void SetIconColor(Lib.Kolor kolor)
		{
			Image.color = Lib.KolorToColor(kolor);
		}
	}
}
