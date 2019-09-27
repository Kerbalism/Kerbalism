using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KERBALISM
{
	public class KsmGuiTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		private string tooltipText;

		public void OnPointerEnter(PointerEventData eventData)
		{
			KsmGuiTooltipController.Instance.ShowTooltip(tooltipText);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			KsmGuiTooltipController.Instance.HideTooltip();
		}

		public void SetTooltipText(string text)
		{
			tooltipText = text;
			KsmGuiTooltipController.Instance.SetTooltipText(text);
		}
	}
}
