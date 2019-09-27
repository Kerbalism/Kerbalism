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

		void Start()
		{
			updateCounter = 0;
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
