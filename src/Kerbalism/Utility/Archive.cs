using System;
using System.Text;


namespace KERBALISM
{


	public class ReadArchive
	{
		public ReadArchive(string data)
		{
			this.data = data;
		}

		public void Load(out int integer)
		{
			integer = data[index] - 32;
			++index;
		}

		public void Load(out string text)
		{
			int len;
			Load(out len);
			text = data.Substring(index, len);
			index += len;
		}

		public void Load(out double value)
		{
			string s;
			Load(out s);
			value = Lib.Parse.ToDouble(s);
		}

		string data;
		int index;
	}


	public class WriteArchive
	{
		public void Save(int integer)
		{
			integer = Lib.Clamp(integer + 32, 32, 255);
			sb.Append((char)integer);
		}

		public void Save(string text)
		{
			Save(text.Length);
			sb.Append(text.Substring(0, Math.Min(255 - 32, text.Length)));
		}

		public void Save(double value)
		{
			Save(value.ToString());
		}

		public string Serialize()
		{
			return sb.ToString();
		}

		StringBuilder sb = new StringBuilder();
	}


} // KERBALISM

