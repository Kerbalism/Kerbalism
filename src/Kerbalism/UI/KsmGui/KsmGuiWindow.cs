using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
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
		public HorizontalOrVerticalLayoutGroup LayoutGroup { get; private set; }

		public enum LayoutGroupType { Vertical, Horizontal }

		public KsmGuiWindow
			(
				LayoutGroupType topLayout,
				float opacity = 1f,
				bool isDraggable = false, int dragOffset = 0,
				TextAnchor groupAlignment = TextAnchor.UpperLeft,
				float groupSpacing = 0f,
				TextAnchor screenAnchor = TextAnchor.MiddleCenter,
				TextAnchor windowPivot = TextAnchor.MiddleCenter,
				int posX = 0, int posY = 0
			) : base(null)
		{
			TopTransform.SetAnchorsAndPosition(screenAnchor, windowPivot, posX, posY);
			TopTransform.SetParentFixScale(KsmGuiMasterController.Instance.KsmGuiTransform);
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

			if (topLayout == LayoutGroupType.Vertical)
				LayoutGroup = TopObject.AddComponent<VerticalLayoutGroup>();
			else
				LayoutGroup = TopObject.AddComponent<HorizontalLayoutGroup>();

			LayoutGroup.spacing = groupSpacing;
			LayoutGroup.padding = new RectOffset(5, 5, 5, 5);
			LayoutGroup.childControlHeight = true;
			LayoutGroup.childControlWidth = true;
			LayoutGroup.childForceExpandHeight = false;
			LayoutGroup.childForceExpandWidth = false;
			LayoutGroup.childAlignment = groupAlignment;

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
