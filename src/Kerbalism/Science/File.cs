using System;

namespace KERBALISM
{
	public sealed class File
	{
		/// <summary>data size in Mb</summary>
		public double size;

		/// <summary>randomized result text</summary>
		public string resultText;

		/// <summary> will be true if the file was created by the hijacker. Force the stock crediting formula to be applied on recovery</summary>
		public bool useStockCrediting;

		//public double scienceValueRatio;
		//public double ScienceMaxValue => Math.Max((subjectData.ScienceMaxValue * scienceValueRatio) - subjectData.ScienceRetrievedInKSC, 0.0);
		//public double SciencePerMB => subjectData.SciencePerMB * scienceValueRatio;

		public SubjectData subjectData;

		public double transmitRate = 0.0;

		/// <summary>Optional SCANsat coverage delta applied to the map when this file is fully transmitted or recovered.</summary>
		public Int16[,] ScanCoverage { get; private set; }

		/// <summary>Body flightGlobalsIndex for ScanCoverage when subject body is unavailable.</summary>
		public int scanBodyIndex = -1;

		public bool HasScanPayload => !ScanGrid.IsEmpty(ScanCoverage);

		public File(SubjectData subjectData, double size = 0.0, bool useStockCrediting = false, string resultText = "")
		{
			this.subjectData = subjectData;
			this.size = size;
			if (double.IsNaN(size))
			{
				Lib.LogStack($"File has a NaN size on creation : {subjectData.DebugStateInfo}", Lib.LogLevel.Error);
				this.size = 0.0;
			}

			this.useStockCrediting = useStockCrediting;
			if (string.IsNullOrEmpty(resultText))
				this.resultText = ResearchAndDevelopment.GetResults(subjectData.StockSubjectId);
			else
				this.resultText = resultText;
		}

		public void MergeScanCoverage(Int16[,] payload, int bodyIndex = -1)
		{
			if (ScanGrid.IsEmpty(payload))
				return;

			if (ScanCoverage == null)
				ScanCoverage = ScanGrid.Create();

			ScanGrid.Or(ScanCoverage, payload);
			if (bodyIndex >= 0)
				scanBodyIndex = bodyIndex;
			else if (scanBodyIndex < 0 && subjectData?.Situation?.Body != null)
				scanBodyIndex = subjectData.Situation.Body.flightGlobalsIndex;
		}

		public void ClearScanPayload()
		{
			ScanCoverage = null;
			scanBodyIndex = -1;
		}

		private void LoadScanPayload(ConfigNode node)
		{
			scanBodyIndex = Lib.ConfigValue(node, "scanBodyIndex", -1);
			string blob = Lib.ConfigValue(node, "scanCoverage", string.Empty);
			ScanCoverage = ScanGrid.Decode(blob);
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
			if (double.IsNaN(size))
			{
				Lib.LogStack($"File has a NaN size on load : {subjectData.DebugStateInfo}", Lib.LogLevel.Error);
				return null;
			}

			string resultText = Lib.ConfigValue(node, "resultText", "");
			bool useStockCrediting = Lib.ConfigValue(node, "useStockCrediting", false);

			File file = new File(subjectData, size, useStockCrediting, resultText);
			file.LoadScanPayload(node);
			return file;
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("resultText", resultText);
			node.AddValue("useStockCrediting", useStockCrediting);

			if (subjectData is UnknownSubjectData)
				node.AddValue("stockSubjectId", subjectData.StockSubjectId);

			if (HasScanPayload)
			{
				node.AddValue("scanBodyIndex", scanBodyIndex);
				node.AddValue("scanCoverage", ScanGrid.Encode(ScanCoverage));
			}
		}

		public ScienceData ConvertToStockData()
		{
			return new ScienceData((float)size, 1.0f, 1.0f, subjectData.StockSubjectId, subjectData.FullTitle);
		}
	}


} // KERBALISM
