using System;
using System.Text;
using System.Collections.Generic;

namespace KERBALISM
{

	public static class Archive
	{
		public static string list2str(List<string> list)
		{
			string result = "";
			foreach (var s in list)
			{
				if (result.Length > 0) result += ";";
				result += s.Replace(";", ",");
			}
			return result;
		}

		public static List<string> string2list(String str)
		{
			return Lib.Tokenize(str, ';');
		}
	}


} // KERBALISM

