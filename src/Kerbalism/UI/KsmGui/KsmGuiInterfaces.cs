using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace KERBALISM
{
	public interface IKsmGuiText
	{
		void SetText(string text);
	}

	public interface IKsmGuiInteractable
	{
		void SetInteractable(bool interactable);
	}

	public interface IKsmGuiButton
	{
		void SetButtonOnClick(UnityAction action);
	}


	public interface IKsmGuiIcon
	{
		void SetIconTexture(Texture2D texture, int width = 16, int height = 16);
	}
}
