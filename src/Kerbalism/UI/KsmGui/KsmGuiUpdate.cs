using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM.KsmGui
{
	public class KsmGuiUpdateCoroutine : IEnumerable
	{
		Func<IEnumerator> updateMethod;
		public KsmGuiUpdateCoroutine(Func<IEnumerator> updateMethod) => this.updateMethod = updateMethod;
		public IEnumerator GetEnumerator() => updateMethod();
	}

	public class KsmGuiUpdateHandler : MonoBehaviour
	{
		private int updateCounter;
		public int updateFrequency = 1;
		public Action updateAction;
		public KsmGuiUpdateCoroutine coroutineFactory;
		public IEnumerator currentCoroutine;

		public void UpdateASAP() => updateCounter = updateFrequency;

		void Start()
		{
			// always update on start
			updateCounter = updateFrequency;
		}

		void Update()
		{
			if (updateAction != null)
			{
				updateCounter++;
				if (updateCounter >= updateFrequency)
				{
					updateCounter = 0;
					updateAction();
				}
			}

			if (coroutineFactory != null)
			{
				if (currentCoroutine == null || !currentCoroutine.MoveNext())
					currentCoroutine = coroutineFactory.GetEnumerator();
			}
		}

		public void ForceExecuteCoroutine(bool fromStart = false)
		{
			if (coroutineFactory == null)
				return;

			if (fromStart || currentCoroutine == null || !currentCoroutine.MoveNext())
				currentCoroutine = coroutineFactory.GetEnumerator();

			while (currentCoroutine.MoveNext()) { }
		}

	}
}
