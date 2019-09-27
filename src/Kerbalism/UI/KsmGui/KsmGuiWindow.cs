using System;
using System.Collections.Generic;
using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KERBALISM
{



	public class KsmGuiWindow : KsmGuiBase
	{
		public class KsmGuiInputLock : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
		{
			private string inputLockId;
			public RectTransform rectTransform;
			private bool isLocked = false;

			private ControlTypes inputLocks =
				ControlTypes.MANNODE_ADDEDIT |
				ControlTypes.MANNODE_DELETE |
				ControlTypes.MAP_UI | // not sure this is necessary, and might cause infinite loop of adding/removing the lock
				ControlTypes.TARGETING |
				ControlTypes.VESSEL_SWITCHING |
				ControlTypes.TWEAKABLES |
				//ControlTypes.EDITOR_UI |
				ControlTypes.EDITOR_SOFT_LOCK |
				//ControlTypes.UI |
				ControlTypes.CAMERACONTROLS;

			void Awake()
			{
				inputLockId = "KsmGuiInputLock:" + gameObject.GetInstanceID();
			}

			public void OnPointerEnter(PointerEventData pointerEventData)
			{
				if (!isLocked)
				{
					global::InputLockManager.SetControlLock(inputLocks, inputLockId);
					isLocked = true;
				}
			}

			public void OnPointerExit(PointerEventData pointerEventData)
			{
				global::InputLockManager.RemoveControlLock(inputLockId);
				isLocked = false;
			}

			// this handle disabling and destruction
			void OnDisable()
			{
				global::InputLockManager.RemoveControlLock(inputLockId);
			}
		}

		public KsmGuiInputLock InputLockManager { get; private set; }
		public bool IsDraggable { get; private set; }
		public DragPanel DragPanel { get; private set; }
		public ContentSizeFitter SizeFitter { get; private set; }
		public Action OnClose { get; set; }
		public HorizontalOrVerticalLayoutGroup TopLayout { get; private set; }

		public enum TopLayoutType { Vertical, Horizontal }

		public KsmGuiWindow
			(
				TopLayoutType topLayout,
				float opacity = 1f,
				bool isDraggable = false, int dragOffset = 0,
				TextAnchor layoutAlignment = TextAnchor.UpperLeft,
				TextAnchor screenAnchor = TextAnchor.MiddleCenter,
				TextAnchor windowPivot = TextAnchor.MiddleCenter,
				float posX = 0f, float posY = 0f
			) : base()
		{
			// set the anchor (origin point) on the parent (screen) that will be used as reference in anchoredPosition
			switch (screenAnchor)
			{
				case TextAnchor.UpperLeft:    TopTransform.anchorMin = new Vector2(0.0f, 1.0f); TopTransform.anchorMax = new Vector2(0.0f, 1.0f); break;
				case TextAnchor.UpperCenter:  TopTransform.anchorMin = new Vector2(0.5f, 1.0f); TopTransform.anchorMax = new Vector2(0.5f, 1.0f); break;
				case TextAnchor.UpperRight:   TopTransform.anchorMin = new Vector2(1.0f, 1.0f); TopTransform.anchorMax = new Vector2(1.0f, 1.0f); break;
				case TextAnchor.MiddleLeft:   TopTransform.anchorMin = new Vector2(0.0f, 0.5f); TopTransform.anchorMax = new Vector2(0.0f, 0.5f); break;
				case TextAnchor.MiddleCenter: TopTransform.anchorMin = new Vector2(0.5f, 0.5f); TopTransform.anchorMax = new Vector2(0.5f, 0.5f); break;
				case TextAnchor.MiddleRight:  TopTransform.anchorMin = new Vector2(1.0f, 0.5f); TopTransform.anchorMax = new Vector2(1.0f, 0.5f); break;
				case TextAnchor.LowerLeft:    TopTransform.anchorMin = new Vector2(0.0f, 0.0f); TopTransform.anchorMax = new Vector2(0.0f, 0.0f); break;
				case TextAnchor.LowerCenter:  TopTransform.anchorMin = new Vector2(0.5f, 0.0f); TopTransform.anchorMax = new Vector2(0.5f, 0.0f); break;
				case TextAnchor.LowerRight:   TopTransform.anchorMin = new Vector2(1.0f, 0.0f); TopTransform.anchorMax = new Vector2(1.0f, 0.0f); break;
			}

			// set the pivot (destination point) on the window that will be used as reference in anchoredPosition
			switch (windowPivot)
			{
				case TextAnchor.UpperLeft:    TopTransform.pivot = new Vector2(0.0f, 1.0f); break;
				case TextAnchor.UpperCenter:  TopTransform.pivot = new Vector2(0.5f, 1.0f); break;
				case TextAnchor.UpperRight:   TopTransform.pivot = new Vector2(1.0f, 1.0f); break;
				case TextAnchor.MiddleLeft:   TopTransform.pivot = new Vector2(0.0f, 0.5f); break;
				case TextAnchor.MiddleCenter: TopTransform.pivot = new Vector2(0.5f, 0.5f); break;
				case TextAnchor.MiddleRight:  TopTransform.pivot = new Vector2(1.0f, 0.5f); break;
				case TextAnchor.LowerLeft:    TopTransform.pivot = new Vector2(0.0f, 0.0f); break;
				case TextAnchor.LowerCenter:  TopTransform.pivot = new Vector2(0.5f, 0.0f); break;
				case TextAnchor.LowerRight:   TopTransform.pivot = new Vector2(1.0f, 0.0f); break;
			}

			// distance in pixels between the anchor and the pivot
			TopTransform.anchoredPosition = new Vector2(posX, posY);

			TopTransform.SetParentAtOneScale(Kerbalism.uitransform, false);
			TopTransform.localScale = Vector3.one;

			// our custom lock manager
			InputLockManager = TopObject.AddComponent<KsmGuiInputLock>();
			InputLockManager.rectTransform = TopTransform;

			// if draggable, add the stock dragpanel component
			IsDraggable = isDraggable;
			if (IsDraggable)
			{
				DragPanel = TopObject.AddComponent<DragPanel>();
				DragPanel.edgeOffset = dragOffset;
			}

			Image img = TopObject.AddComponent<Image>();
			img.sprite = Textures.KsmGuiSpriteBackground;
			img.type = Image.Type.Sliced;
			img.color = new Color(1.0f, 1.0f, 1.0f, opacity);

			SizeFitter = TopObject.AddComponent<ContentSizeFitter>();
			SizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			SizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			if (topLayout == TopLayoutType.Vertical)
				TopLayout = TopObject.AddComponent<VerticalLayoutGroup>();
			else
				TopLayout = TopObject.AddComponent<HorizontalLayoutGroup>();

			TopLayout.padding = new RectOffset(5, 5, 5, 5);
			TopLayout.childControlHeight = true;
			TopLayout.childControlWidth = true;
			TopLayout.childForceExpandHeight = false;
			TopLayout.childForceExpandWidth = false;
			TopLayout.childAlignment = layoutAlignment;

			// close on scene changes
			GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
		}

		public virtual void OnSceneChange(GameScenes data) => Close();

		public void Close()
		{
			if (OnClose != null) OnClose();
			KsmGuiTooltipController.Instance.HideTooltip();
			TopObject.DestroyGameObject();
		}

		private void OnDestroy()
		{
			GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);
			KsmGuiTooltipController.Instance.HideTooltip();
		}
	}
}
