using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM
{


	public interface ISpecifics
	{
		Specifics Specs();
	}

	public sealed class Specifics
	{
		public class Entry
		{
			public string label = string.Empty;
			public string value = string.Empty;
		}

		StringBuilder sb = new StringBuilder();
		public List<Entry> entries = new List<Entry>();

		public void Add(string label, string value = "")
		{
			Entry e = new Entry
			{
				label = label,
				value = value
			};
			entries.Add(e);
		}

		public string Info(string desc = "")
		{
			sb.Clear();
			if (desc.Length > 0)
			{
				sb.Append("<i>");
				sb.Append(desc);
				sb.Append("</i>\n\n");
			}

			for (int i = 0; i < entries.Count; i++)
			{
				Entry e = entries[i];

				if (i > 0)
					sb.Append("\n");

				sb.Append(e.label);
				if (e.value.Length > 0)
				{
					sb.Append(": <b>");
					sb.Append(e.value);
					sb.Append("</b>");
				}
			}
			return sb.ToString();
		}



		
	}


} // KERBALISM
