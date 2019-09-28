using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM.KsmGui
{
	public class KsmGuiUpdateHandler : MonoBehaviour
	{
		private int updateCounter;
		public int updateFrequency = 1;
		public Action updateAction;

		public void UpdateASAP() => updateCounter = updateFrequency;

		void Start()
		{
			// always update on start
			updateCounter = updateFrequency;
		}

		void Update()
		{
			updateCounter++;
			if (updateCounter >= updateFrequency)
			{
				updateCounter = 0;
				updateAction();
			}
		}
	}
}
