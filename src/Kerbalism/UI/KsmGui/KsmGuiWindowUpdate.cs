using KSP.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
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
