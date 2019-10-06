using System;

namespace KERBALISM
{
	public sealed class File
	{
		/// <summary>data size in Mb</summary>
		public double size;

		/// <summary>randomized result text</summary>
		public string resultText;

		/// <summary> will be true if the file was created by the hijacker. Force the stock crediting formula to be applied </summary>
		public bool useStockCrediting;

		public SubjectData subjectData;

		public double transmitRate = 0.0;

		public File(SubjectData subjectData, double size = 0.0, bool useStockCrediting = false, string resultText = "")
		{
			this.subjectData = subjectData;
			this.size = size;
			this.useStockCrediting = useStockCrediting;
			if (string.IsNullOrEmpty(resultText))
				this.resultText = ResearchAndDevelopment.GetResults(subjectData.StockSubjectId);
			else
				this.resultText = resultText;
		}

		public static File Load(string integerSubjectId, ConfigNode node)
		{
			SubjectData subjectData;
			string stockSubjectId = Lib.ConfigValue(node, "stockSubjectId", string.Empty);
			// the stock subject id is stored only if this is an asteroid sample, or a non-standard subject id
			if (stockSubjectId != string.Empty)
				subjectData = ScienceDB.GetSubjectDataFromStockId(stockSubjectId);
			else
				subjectData = ScienceDB.GetSubjectData(integerSubjectId);

			if (subjectData == null)
				return null;

			double size = Lib.ConfigValue(node, "size", 0.0);
			string resultText = Lib.ConfigValue(node, "resultText", "");
			bool useStockCrediting = Lib.ConfigValue(node, "useStockCrediting", false);

			return new File(subjectData, size, useStockCrediting, resultText);
		}

		// this is a fallback loading method for pre 3.1 / pre build 7212 files saved used the stock subject id
		public static File LoadOldFormat(string stockSubjectId, ConfigNode node)
		{
			SubjectData subjectData = ScienceDB.GetSubjectDataFromStockId(stockSubjectId);

			if (subjectData == null)
				return null;

			double size = Lib.ConfigValue(node, "size", 0.0);
			string resultText = Lib.ConfigValue(node, "resultText", "");
			bool useStockCrediting = Lib.ConfigValue(node, "useStockCrediting", false);

			return new File(subjectData, size, useStockCrediting, resultText);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("resultText", resultText);
			node.AddValue("useStockCrediting", useStockCrediting);

			if (subjectData is MultiSubjectData)
				node.AddValue("stockSubjectId", subjectData.StockSubjectId);
		}

		public ScienceData ConvertToStockData()
		{
			return new ScienceData((float)size, 1.0f, 1.0f, subjectData.StockSubjectId, subjectData.FullTitle);
		}
	}


} // KERBALISM
