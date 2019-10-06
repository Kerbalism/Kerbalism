using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	// To avoid the huge performance hit of the dynamic layout being recalculated every frame,
	// we disable all the layout controller components, excepted ScrollRect (because it won't work otherwise)
	// To catch them all, we search for components implementing ILayoutController :
	// - ScrollRect
	// - AspectRatioFitter
	// - ContentSizeFitter
	// - Layout groups (Vertical, Horizontal, Grid)
	// This component is added to KsmGuiWindow, and a reference to it is kept by all KsmGuiBase elements
	// Not ideal, but it works.
	public class KsmGuiLayoutOptimizer : MonoBehaviour
	{
		private List<UIBehaviour> layoutControllers = new List<UIBehaviour>();
		private bool willRebuild;
		
		public void RebuildLayout()
		{
			if (willRebuild)
				return;

			willRebuild = true;

			layoutControllers.Clear();
			foreach (ILayoutController ILayoutController in GetComponentsInChildren<ILayoutController>(true))
			{
				if (ILayoutController is ScrollRect)
					continue;

				UIBehaviour UIBehaviour = (UIBehaviour)ILayoutController;
				layoutControllers.Add(UIBehaviour);
				UIBehaviour.enabled = true;
			}

			StartCoroutine(DisableLayoutAfterRebuild());
		}

		private IEnumerator DisableLayoutAfterRebuild()
		{
			// Unity needs components to be enabled at the beginning of the frame
			// This mean the layout will rebuild only in the next frame, then we can disable
			// the layout components in the following frame. So wait 2 frames :
			yield return StartCoroutine(WaitForFrames(2));

			foreach (UIBehaviour layoutController in layoutControllers)
			{
				layoutController.enabled = false;
			}

			willRebuild = false;
		}

		private IEnumerator WaitForFrames(int frameCount)
		{
			if (frameCount <= 0)
			{
				throw new ArgumentOutOfRangeException("frameCount", "Cannot wait for less that 1 frame");
			}

			while (frameCount > 0)
			{
				frameCount--;
				yield return null;
			}
		}
	}
}
