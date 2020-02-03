using System;
using System.Collections;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KERBALISM
{
	public static class ScienceDB
	{
		public class UniqueValuesList<T>
		{
			private List<T> values = new List<T>();
			private HashSet<T> valuesHashs = new HashSet<T>();

			public void Add(T value)
			{
				if (valuesHashs.Contains(value))
					return;

				valuesHashs.Add(value);
				values.Add(value);
			}

			public IEnumerator<T> GetEnumerator() => values.GetEnumerator();

			public void Clear()
			{
				values.Clear();
				valuesHashs.Clear();
			}
		}


		// Dictionary-like class intended for a small number of elements that need to be iterated over
		// - small memory footprint
		// - iterating (foreach) should be as fast as on a list, and generate no garbage
		// - key lookups are O(n) and are done by iterating and checking equality
		// - other operations all have the O(n) lookup overhead, but no garbage generation
		public class KeyValueList<TKey, TValue>
		{
			private readonly List<ObjectPair<TKey, TValue>> keyValuePairs = new List<ObjectPair<TKey, TValue>>();

			public TValue this[TKey key]
			{
				get { return keyValuePairs[keyValuePairs.FindIndex(p => p.Key.Equals(key))].Value; }
				set { keyValuePairs[keyValuePairs.FindIndex(p => p.Key.Equals(key))] = new ObjectPair<TKey, TValue>(key, value); }
			}

			public void Add(TKey key, TValue value)
			{
				if (key == null)
					throw new ArgumentNullException();

				if (ContainsKey(key))
					throw new ArgumentException();

				keyValuePairs.Add(new ObjectPair<TKey, TValue>(key, value));
			}

			public bool ContainsKey(TKey key) => keyValuePairs.Exists(p => p.Key.Equals(key));

			public int Count => keyValuePairs.Count;

			public void Clear() => keyValuePairs.Clear();

			public IEnumerator<ObjectPair<TKey, TValue>> GetEnumerator() => keyValuePairs.GetEnumerator();

			public bool Remove(TKey key)
			{
				int index = keyValuePairs.FindIndex(p => p.Key.Equals(key));
				if (index == -1)
					return false;

				keyValuePairs.RemoveAt(index);
				return true;
			}

			public bool TryGetValue(TKey key, out TValue value)
			{
				int index = keyValuePairs.FindIndex(p => p.Key.Equals(key));
				if (index == -1)
				{
					value = default;
					return false;
				}

				value = keyValuePairs[index].Value;
				return true;
			}

			public void ForEach(Action<ObjectPair<TKey, TValue>> action) => keyValuePairs.ForEach(action);
		}

		/// <summary> KeyValueList of ObjectPair&lt;int, List&lt;SubjectData&gt;&gt;, int = biome index </summary>
		public class BiomesSubject : KeyValueList<int, List<SubjectData>> { }
		/// <summary> KeyValueList of ObjectPair&lt;ScienceSituation, BiomesSubject&gt; </summary>
		public class SituationsBiomesSubject : KeyValueList<ScienceSituation, BiomesSubject> { }
		/// <summary> KeyValueList of ObjectPair&lt;int, SituationsBiomesSubject&gt; , int = body index </summary>
		public class BodiesSituationsBiomesSubject : KeyValueList<int, SituationsBiomesSubject> { }
		/// <summary> Dictionary of KeyBaluePair&lt;ExperimentInfo, BodiesSituationsBiomesSubject&gt; </summary>
		public class ExpBodiesSituationsBiomesSubject : Dictionary<ExperimentInfo, BodiesSituationsBiomesSubject>
		{
			public void AddSubject(ExperimentInfo expInfo, int bodyIndex, ScienceSituation scienceSituation, int biomeIndex, SubjectData subjectData)
			{

				BodiesSituationsBiomesSubject bodiesSituationsBiomesSubject;
				if (!TryGetValue(expInfo, out bodiesSituationsBiomesSubject))
				{
					bodiesSituationsBiomesSubject = new BodiesSituationsBiomesSubject();
					Add(expInfo, bodiesSituationsBiomesSubject);
				}

				SituationsBiomesSubject situationsBiomesSubject;
				if (!bodiesSituationsBiomesSubject.TryGetValue(bodyIndex, out situationsBiomesSubject))
				{
					situationsBiomesSubject = new SituationsBiomesSubject();
					bodiesSituationsBiomesSubject.Add(bodyIndex, situationsBiomesSubject);
				}

				BiomesSubject biomesSubject;
				if (!situationsBiomesSubject.TryGetValue(scienceSituation, out biomesSubject))
				{
					biomesSubject = new BiomesSubject();
					situationsBiomesSubject.Add(scienceSituation, biomesSubject);
				}

				List<SubjectData> subjectDataList;
				if (!biomesSubject.TryGetValue(biomeIndex, out subjectDataList))
				{
					subjectDataList = new List<SubjectData>();
					biomesSubject.Add(biomeIndex, subjectDataList);
				}

				if (subjectData != null)
				{
					subjectDataList.Add(subjectData);
				}
				
			}

			public void RemoveSubject(ExperimentInfo expInfo, int bodyIndex, ScienceSituation scienceSituation, int biomeIndex, SubjectData subjectData)
			{
				BodiesSituationsBiomesSubject bodiesSituationsBiomesSubject;
				if (!TryGetValue(expInfo, out bodiesSituationsBiomesSubject))
					return;

				SituationsBiomesSubject situationsBiomesSubject;
				if (!bodiesSituationsBiomesSubject.TryGetValue(bodyIndex, out situationsBiomesSubject))
					return;

				BiomesSubject biomesSubject;
				if (!situationsBiomesSubject.TryGetValue(scienceSituation, out biomesSubject))
					return;

				List<SubjectData> subjectDataList;
				if (!biomesSubject.TryGetValue(biomeIndex, out subjectDataList))
					return;

				subjectDataList.Remove(subjectData);
			}
		}

		/// <summary> All ExperimentInfos, accessible by experimentId </summary>
		private static readonly Dictionary<string, ExperimentInfo>
			experiments = new Dictionary<string, ExperimentInfo>();

		/// <summary> All ExperimentInfos </summary>
		public static IEnumerable<ExperimentInfo> ExperimentInfos => experiments.Values;

		/// <summary>
		/// For every ExperimentInfo, for every VesselSituation id, the corresponding SubjectData.
		/// used to get the subject, or to test if a situation is available for a given experiment
		/// </summary>
		private static readonly Dictionary<ExperimentInfo, Dictionary<int, SubjectData>>
			subjectByExpThenSituationId = new Dictionary<ExperimentInfo, Dictionary<int, SubjectData>>();

		/// <summary>
		/// For every ExperimentInfo, body index, situation index, biome index, the corresponding SubjectData.
		/// Note : to handle the stock possibility of multiple subjects per unique situation (asteroids), the value is an array of SubjectDatas.
		/// Used to get/display all subjects for a given experiment.
		/// <para/> body indexes are from the FlightGlobals.Bodies list, and match the CelestialBody.flightGlobalsIndex value
		/// <para/> situation indexes are the ScienceSituation enum value
		/// <para/> biome indexes are from the CelestialBody.BiomeMap.Attribute. The -1 index correspond to the biome-agnostic situation.
		/// <para/> Ex : expBodiesSituationsBiomesSubject[expInfo][2][ScienceSituation.InnerBelt][4][0] return the first subject for expInfo, bodyIndex 2, inner belt situation, biome index 4
		/// </summary>
		private static readonly ExpBodiesSituationsBiomesSubject
			expBodiesSituationsBiomesSubject = new ExpBodiesSituationsBiomesSubject();

		// HashSet of all subjects using the stock string id as key, used for RnD subjects synchronization
		private static readonly HashSet<string> knownStockSubjectsId = new HashSet<string>();

		private static readonly Dictionary<string, SubjectData> unknownSubjectDatas = new Dictionary<string, SubjectData>();

		public static readonly Dictionary<string, ScienceSubject> sandboxSubjects = new Dictionary<string, ScienceSubject>();

		/// <summary>
		/// List of subjects that should be persisted in our own DB
		/// </summary>
		public static readonly UniqueValuesList<SubjectData> persistedSubjects = new UniqueValuesList<SubjectData>();

		public static void Init()
		{
			Lib.Log("ScienceDB init started");
			int subjectCount = 0;
			double totalScience = 0.0;

			// get our extra defintions
			ConfigNode[] expDefNodes = GameDatabase.Instance.GetConfigNodes("EXPERIMENT_DEFINITION");

			// create our subject database
			// Note : GetExperimentIDs will force the creation of all ScienceExperiment objects,
			// no matter if the RnD instance is null or not because the ScienceExperiment dictionary is static.
			foreach (string experimentId in ResearchAndDevelopment.GetExperimentIDs())
			{
				if (experimentId == "recovery")
					continue;

				ConfigNode kerbalismExpNode = null;
				foreach (ConfigNode expDefNode in expDefNodes)
				{
					string id = string.Empty;
					if (expDefNode.TryGetValue("id", ref id) && id == experimentId)
					{
						kerbalismExpNode = expDefNode.GetNode("KERBALISM_EXPERIMENT"); // return null if not found
						break;
					}
				}

				ScienceExperiment stockDef = ResearchAndDevelopment.GetExperiment(experimentId);
				if (stockDef == null)
				{
					Lib.Log("ScienceExperiment is null for experiment Id=" + experimentId + ", skipping...", Lib.LogLevel.Warning);
					continue;
				}

				ExperimentInfo expInfo = new ExperimentInfo(stockDef, kerbalismExpNode);
				if (!experiments.ContainsKey(experimentId))
					experiments.Add(experimentId, expInfo);
				if (!subjectByExpThenSituationId.ContainsKey(expInfo))
					subjectByExpThenSituationId.Add(expInfo, new Dictionary<int, SubjectData>());

				for (int bodyIndex = 0; bodyIndex < FlightGlobals.Bodies.Count; bodyIndex++)
				{
					CelestialBody body = FlightGlobals.Bodies[bodyIndex];

					if (!expInfo.IgnoreBodyRestrictions && !expInfo.ExpBodyConditions.IsBodyAllowed(body))
						continue;

					// ScienceSituationUtils.validSituations is all situations in the enum, apart from the "None" value
					foreach (ScienceSituation scienceSituation in ScienceSituationUtils.validSituations)
					{
						// test the ScienceExperiment situation mask
						if (!scienceSituation.IsAvailableForExperiment(expInfo))
							continue;

						// don't add impossible body / situation combinations
						if (!expInfo.IgnoreBodyRestrictions && !scienceSituation.IsAvailableOnBody(body))
							continue;

						// virtual biomes always have priority over normal biomes :
						if (scienceSituation.IsVirtualBiomesRelevantForExperiment(expInfo))
						{
							foreach (VirtualBiome virtualBiome in expInfo.VirtualBiomes)
							{
								if (!virtualBiome.IsAvailableOnBody(body))
									continue;

								SubjectData subjectData = null;
								if (expInfo.HasDBSubjects)
								{
									Situation situation = new Situation(bodyIndex, scienceSituation, (int)virtualBiome);
									subjectData = new SubjectData(expInfo, situation);
									subjectByExpThenSituationId[expInfo].Add(situation.Id, subjectData);
									knownStockSubjectsId.Add(subjectData.StockSubjectId);
									subjectCount++;
									totalScience += subjectData.ScienceMaxValue;
								}
								
								expBodiesSituationsBiomesSubject.AddSubject(expInfo, bodyIndex, scienceSituation, (int)virtualBiome, subjectData);
							}
						}
						// if the biome mask says the situation is biome dependant :
						else if (scienceSituation.IsBodyBiomesRelevantForExperiment(expInfo) && body.BiomeMap != null && body.BiomeMap.Attributes.Length > 1)
						{
							for (int biomeIndex = 0; biomeIndex < body.BiomeMap.Attributes.Length; biomeIndex++)
							{
								SubjectData subjectData = null;
								if (expInfo.HasDBSubjects)
								{
									Situation situation = new Situation(bodyIndex, scienceSituation, biomeIndex);
									subjectData = new SubjectData(expInfo, situation);
									subjectByExpThenSituationId[expInfo].Add(situation.Id, subjectData);
									knownStockSubjectsId.Add(subjectData.StockSubjectId);
									subjectCount++;
									totalScience += subjectData.ScienceMaxValue;
								}

								expBodiesSituationsBiomesSubject.AddSubject(expInfo, bodyIndex, scienceSituation, biomeIndex, subjectData);
							}
						}
						// else generate the global, biome agnostic situation
						else
						{
							SubjectData subjectData = null;
							if (expInfo.HasDBSubjects)
							{
								Situation situation = new Situation(bodyIndex, scienceSituation);
								subjectData = new SubjectData(expInfo, situation);
								subjectByExpThenSituationId[expInfo].Add(situation.Id, subjectData);
								knownStockSubjectsId.Add(subjectData.StockSubjectId);
								subjectCount++;
								totalScience += subjectData.ScienceMaxValue;
							}

							expBodiesSituationsBiomesSubject.AddSubject(expInfo, bodyIndex, scienceSituation, -1, subjectData);
						}
					}
				}
			}

			// cache that call
			IEnumerable<ExperimentInfo> experimentInfosCache = ExperimentInfos;

			// first parse all the IncludeExperiment configs
			foreach (ExperimentInfo experimentInfo in experimentInfosCache)
				experimentInfo.ParseIncludedExperiments();

			// then check for infinite recursion from bad configs
			List<ExperimentInfo> chainedExperiments = new List<ExperimentInfo>();
			foreach (ExperimentInfo experimentInfo in experimentInfosCache)
			{
				chainedExperiments.Clear();
				ExperimentInfo.CheckIncludedExperimentsRecursion(experimentInfo, chainedExperiments);
			}
				
			// now we are sure all the include experiment chains are valid
			foreach (ExperimentInfo experimentInfo in experimentInfosCache)
			{
				// populate the included experiments chains at the subject level
				foreach (KeyValuePair<int, SubjectData> subjectInfo in subjectByExpThenSituationId[experimentInfo])
				{
					foreach (ExperimentInfo includedInfo in experimentInfo.IncludedExperiments)
					{
						SubjectData subjectToInclude = GetSubjectData(includedInfo, subjectInfo.Key);

						if (subjectToInclude != null)
							subjectInfo.Value.IncludedSubjects.Add(subjectToInclude);
					}
				}

				// Get the experiment description that will be shown in the science archive by calling GetInfo() on the first found partmodule using it
				// TODO: this isn't ideal, if there are several modules with different values (ex : data rate, ec rate...), the archive info will use the first found one.
				// Ideally we should revamp the whole handling of that (because it's a mess from the partmodule side too)
				experimentInfo.CompileModuleInfos();
			}

			Lib.Log($"ScienceDB init done : {subjectCount} subjects found, total science points : {totalScience.ToString("F1")}");
		}

		public static void Load(ConfigNode node)
		{
			// RnD subjects don't exists in sandbox
			if (!Science.GameHasRnD)
			{
				// load sandbox science subjects
				sandboxSubjects.Clear();
				if (node.HasNode("sandboxScienceSubjects"))
				{
					foreach (var subjectNode in node.GetNode("sandboxScienceSubjects").GetNodes())
					{
						ScienceSubject subject = new ScienceSubject(subjectNode);
						sandboxSubjects.Add(subject.id, subject);
					}
				}
			}
			else
			{
				// Load API subjects (require RnD)
				subjectsReceivedBuffer.Clear();
				subjectsReceivedValueBuffer.Clear();
				ConfigNode APISubjects = new ConfigNode();
				if (node.TryGetNode("APISubjects", ref APISubjects))
				{
					foreach (ConfigNode subjectNode in APISubjects.GetNodes("Subject"))
					{
						string subjectId = Lib.ConfigValue(subjectNode, "subjectId", string.Empty);
						ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(subjectId);
						if (subject == null)
						{
							Lib.Log($"Warning : API subject '{subjectId}' not found in ResearchAndDevelopment");
							continue;
						}
						subjectsReceivedBuffer.Add(subject);
						subjectsReceivedValueBuffer.Add(Lib.ConfigValue(subjectNode, "science", 0.0));
					}
				}
			}

			// load uncredited science (transmission buffer)
			uncreditedScience = Lib.ConfigValue(node, "uncreditedScience", 0.0);

			// Rebuild the list of persisted subjects
			persistedSubjects.Clear();
			foreach (ExperimentInfo expInfo in experiments.Values)
			{
				foreach (SubjectData subjectData in subjectByExpThenSituationId[expInfo].Values)
				{
					subjectData.CheckRnD();
					subjectData.ClearDataCollectedInFlight();
				}
			}

			// load science subjects persisted data
			if (node.HasNode("subjectData"))
			{
				foreach (var subjectNode in node.GetNode("subjectData").GetNodes())
				{
					string integerSubjectId = DB.From_safe_key(subjectNode.name);
					SubjectData subjectData = GetSubjectData(integerSubjectId);
					if (subjectData != null)
						subjectData.Load(subjectNode);
				}
			}

			//if (ResearchAndDevelopment.Instance == null)
			//	Lib.Log("ERROR : ResearchAndDevelopment.Instance is null on subjects load !");

			// remove unknown subjects from the database
			foreach (SubjectData subjectData in unknownSubjectDatas.Values)
			{
				int bodyIndex;
				int scienceSituation;
				int biomeIndex;

				Situation.IdToFields(subjectData.Situation.Id, out bodyIndex, out scienceSituation, out biomeIndex);

				expBodiesSituationsBiomesSubject.RemoveSubject(subjectData.ExpInfo, bodyIndex, (ScienceSituation)scienceSituation, biomeIndex, subjectData);
			}

			// clear the list
			unknownSubjectDatas.Clear();


			// find them again
			IEnumerable<ScienceSubject> stockSubjects;
			if (Science.GameHasRnD)
				stockSubjects = ResearchAndDevelopment.GetSubjects();
			else
				stockSubjects = sandboxSubjects.Values;

			foreach (ScienceSubject stockSubject in stockSubjects)
				if (!knownStockSubjectsId.Contains(stockSubject.id))
					GetSubjectDataFromStockId(stockSubject.id, stockSubject);
		}

		public static void Save(ConfigNode node)
		{
			// RnD subjects don't exists in sandbox, so we have our own subject persistence
			if (!Science.GameHasRnD)
			{
				ConfigNode sandboxSubjectsNode = node.AddNode("sandboxScienceSubjects");
				foreach (ScienceSubject subject in sandboxSubjects.Values)
					subject.Save(sandboxSubjectsNode.AddNode("subject"));
			}
			else
			{
				// save API subjects (only exists if game has RnD)
				if (subjectsReceivedBuffer.Count > 0)
				{
					ConfigNode APISubjects = node.AddNode("APISubjects");
					for (int i = 0; i < subjectsReceivedBuffer.Count; i++)
					{
						ConfigNode subjectNode = APISubjects.AddNode("Subject");
						subjectNode.AddValue("subjectId", subjectsReceivedBuffer[i].id);
						subjectNode.AddValue("science", subjectsReceivedValueBuffer[i]);
					}
				}
			}

			// save uncredited science (transmission buffer)
			node.AddValue("uncreditedScience", uncreditedScience);

			// save science subjects persisted data
			var subjectsNode = node.AddNode("subjectData");
			foreach (SubjectData subject in persistedSubjects)
			{
				subject.Save(subjectsNode.AddNode(DB.To_safe_key(subject.Id)));
			}
		}

		// Use a buffer because AddScience is VERY slow
		// We don't use "TransactionReasons.ScienceTransmission" because AddScience fire multiple events not meant to be fired continuously
		// this avoid many side issues (ex : chatterer transmit sound playing continously, strategia "+0.0 science" popup...)
		public static double uncreditedScience;

		// this is for the onSubjectsReceived API event
		public static List<ScienceSubject> subjectsReceivedBuffer = new List<ScienceSubject>();
		public static List<double> subjectsReceivedValueBuffer = new List<double>();

		private static double realTimeElapsed = 0.0;
		private static double realTimeElapsedAPI = 0.0;
		private static double gameTimeElapsedAPI = 0.0;
		public static void CreditScienceBuffers(double elapsed_s)
		{
			if (!Science.GameHasRnD)
				return;

			if (!API.preventScienceCrediting)
			{
				realTimeElapsed += TimeWarp.CurrentRate > 0f ? elapsed_s / TimeWarp.CurrentRate : 0f;
				if (uncreditedScience > 0.1 && realTimeElapsed > 2.5)
				{
					ResearchAndDevelopment.Instance.AddScience((float)uncreditedScience, TransactionReasons.None);
					uncreditedScience = 0.0;
					realTimeElapsed = 0.0;
				}
			}

			if (API.subjectsReceivedEventEnabled)
			{
				gameTimeElapsedAPI += elapsed_s;
				realTimeElapsedAPI += TimeWarp.CurrentRate > 0f ? elapsed_s / TimeWarp.CurrentRate : 0f;

				if (subjectsReceivedBuffer.Count > 0 && (gameTimeElapsedAPI > API.subjectsReceivedEventGameTimeInterval || realTimeElapsedAPI > API.subjectsReceivedEventRealTimeInterval))
				{
					API.onSubjectsReceived.Fire(subjectsReceivedBuffer, subjectsReceivedValueBuffer);
					subjectsReceivedBuffer.Clear();
					subjectsReceivedValueBuffer.Clear();
					gameTimeElapsedAPI = 0.0;
					realTimeElapsedAPI = 0.0;
				}
			}
		}

		public static ExperimentInfo GetExperimentInfo(string experimentId)
		{
			ExperimentInfo expInfo;
			if (!experiments.TryGetValue(experimentId, out expInfo))
				return null;

			return expInfo;
		}

		/// <summary> return the subject information for the given experiment and situation, or null if the situation isn't available. </summary>
		public static SubjectData GetSubjectData(ExperimentInfo expInfo, Situation situation)
		{
			int situationId;
			if (!situation.ScienceSituation.IsBiomesRelevantForExperiment(expInfo))
				situationId = situation.GetBiomeAgnosticId();
			else
				situationId = situation.Id;

			SubjectData subjectData;
			if (!subjectByExpThenSituationId[expInfo].TryGetValue(situationId, out subjectData))
				return null;

			return subjectData;
		}

		/// <summary> return the subject information for the given experiment and situation, or null if the situation isn't available. </summary>
		public static SubjectData GetSubjectData(ExperimentInfo expInfo, Situation situation, out int situationId)
		{
			if (!situation.ScienceSituation.IsBiomesRelevantForExperiment(expInfo))
				situationId = situation.GetBiomeAgnosticId();
			else
				situationId = situation.Id;

			SubjectData subjectData;
			if (!subjectByExpThenSituationId[expInfo].TryGetValue(situationId, out subjectData))
				return null;

			return subjectData;
		}

		/// <summary> return the subject information for the given experiment and situation id, or null if the situation isn't available. </summary>
		public static SubjectData GetSubjectData(ExperimentInfo expInfo, int situationId)
		{
			SubjectData subjectData;
			if (!subjectByExpThenSituationId[expInfo].TryGetValue(situationId, out subjectData))
				return null;

			return subjectData;
		}

		/// <summary> return the subject information for the given experiment and situation, or null if the situation isn't available. </summary>
		public static SubjectData GetSubjectData(string integerSubjectId)
		{
			string[] expAndSit = integerSubjectId.Split('@');

			if (expAndSit.Length != 2)
			{
				Lib.Log("Could not get the SubjectData from subject '" + integerSubjectId + "' : bad format");
				return null;
			}

			ExperimentInfo expInfo = GetExperimentInfo(expAndSit[0]);
			if (expInfo == null)
			{
				Lib.Log("Could not get the SubjectData from subject '" + integerSubjectId + "' : the experiment id '" + expAndSit[0] + "' doesn't exists");
				return null;
			}

			int situationId;
			if (!int.TryParse(expAndSit[1], out situationId))
			{
				Lib.Log("Could not get the SubjectData from subject '" + integerSubjectId + "' : the situation id '" + expAndSit[1] + "' isn't a valid integer");
				return null;
			}

			SubjectData subjectData;
			if (!subjectByExpThenSituationId[expInfo].TryGetValue(situationId, out subjectData))
			{
				Lib.Log("Could not get the SubjectData from subject '" + integerSubjectId + "' : the situation id '" + expAndSit[1] + "' isn't valid");
				return null;
			}

			return subjectData;

		}

		/// <summary>
		/// Create our SubjectData by parsing the stock "experiment@situation" subject id string.
		/// Used for asteroid samples, for compatibility with RnD archive data of removed mods and for converting stock ScienceData into SubjectData
		/// </summary>
		public static SubjectData GetSubjectDataFromStockId(string stockSubjectId, ScienceSubject RnDSubject = null)
		{
			SubjectData subjectData = null;

			if (unknownSubjectDatas.TryGetValue(stockSubjectId, out subjectData))
				return subjectData;

			string[] expAndSit = stockSubjectId.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);

			if (expAndSit.Length != 2)
			{
				Lib.Log("Could not parse the SubjectData from subjectId '" + stockSubjectId + "' : bad format");
				return null;
			}

			// the recovery experiment subject are created in ResearchAndDevelopment.reverseEngineerRecoveredVessel, called on the vessel recovery event
			// it use a non-standard situation system ("body" + a situation enum "RecoverySituations"). We ignore those.
			if (expAndSit[0] == "recovery")
				return null;

			ExperimentInfo expInfo = GetExperimentInfo(expAndSit[0]);
			if (expInfo == null)
			{
				Lib.Log("Could not parse the SubjectData from subjectId '" + stockSubjectId + "' : the experiment id '" + expAndSit[0] + "' doesn't exists");
				return null;
			}

			// for subject ids created with the ResearchAndDevelopment.GetExperimentSubject overload that take a "sourceUId" string,
			// the sourceUId is added to the situation after a "_"
			// in stock this seems to be used only for asteroids, and I don't think any mod use it.
			string extraSituationInfo = string.Empty;
			if (expAndSit[1].Contains("_"))
			{
				string[] sitAndAsteroid = expAndSit[1].Split('_');
				// remove
				expAndSit[1] = sitAndAsteroid[0];
				// asteroid are saved as "part.partInfo.name + part.flightID", and the part name always end with a randomly generated "AAA-000" string
				extraSituationInfo = Regex.Match(sitAndAsteroid[1], ".*?-[0-9][0-9][0-9]").Value;
				// if no match, just use the unmodified string
				if (extraSituationInfo == string.Empty)
					extraSituationInfo = sitAndAsteroid[1];
			}

			string[] bodyAndBiome = expAndSit[1].Split(ScienceSituationUtils.validSituationsStrings, StringSplitOptions.RemoveEmptyEntries);
			string situation;
			
			if (bodyAndBiome.Length == 1)
				situation = expAndSit[1].Substring(bodyAndBiome[0].Length);
			else if (bodyAndBiome.Length == 2)
				situation = expAndSit[1].Substring(bodyAndBiome[0].Length, expAndSit[1].Length - bodyAndBiome[0].Length - bodyAndBiome[1].Length);
			else
			{
				Lib.Log("Could not parse the SubjectData from subjectId '" + stockSubjectId + "' : the situation doesn't exists");
				return null;
			}

			CelestialBody subjectBody = null;
			foreach (CelestialBody body in FlightGlobals.Bodies)
			{
				if (body.name == bodyAndBiome[0])
				{
					subjectBody = body;
					break;
				}
			}

			if (subjectBody == null)
			{
				// TODO : DMOS asteroid experiments are doing : "magScan@AsteroidInSpaceLowCarbonaceous7051371", those subjects will be discarded entirely here
				// because the body "Asteroid" doesn't exists, consequently it's impossible to create the Situation object.
				// To handle that, maybe we could implement a derived class "UnknownSituation" from Situation that can handle a completely random subject format
				Lib.Log("Could not parse the SubjectData from subjectId '" + stockSubjectId + "' : the body '" + bodyAndBiome[0] + "' doesn't exist");
				return null;
			}

			ScienceSituation scienceSituation = ScienceSituationUtils.ScienceSituationDeserialize(situation);

			int biomeIndex = -1;
			if (bodyAndBiome.Length == 2 && ScienceSituationUtils.IsBodyBiomesRelevantForExperiment(scienceSituation, expInfo) && subjectBody.BiomeMap != null)
			{
				for (int i = 0; i < subjectBody.BiomeMap.Attributes.Length; i++)
				{
					// Note : a stock subject has its spaces in the biome name removed but prior versions of kerbalism didn't do that,
					// so we try to fix it, in order not to create duplicates in the RnD archives.

					// TODO : also, we need to remove the "reentry" subjects, as stock is failing to parse them, altough this is in a try/catch block and handled gracefully.
					string sanitizedBiome = bodyAndBiome[1].Replace(" ", string.Empty);
					if (RnDSubject != null && extraSituationInfo == string.Empty && sanitizedBiome != bodyAndBiome[1])
					{
						string correctedSubjectId = expAndSit[0] + "@" + bodyAndBiome[0] + situation + sanitizedBiome;
						RnDSubject.id = correctedSubjectId;

						Dictionary<string, ScienceSubject> stockSubjects = Lib.ReflectionValue<Dictionary<string, ScienceSubject>>(ResearchAndDevelopment.Instance, "scienceSubjects");
						if (stockSubjects.Remove(stockSubjectId) && !stockSubjects.ContainsKey(correctedSubjectId))
						{
							stockSubjects.Add(correctedSubjectId, RnDSubject);
						}

						Lib.Log("RnD subject load : misformatted subject '" + stockSubjectId + "' was corrected to '" + correctedSubjectId + "'");
					}

					if (subjectBody.BiomeMap.Attributes[i].name.Replace(" ", string.Empty).Equals(sanitizedBiome, StringComparison.OrdinalIgnoreCase))
					{
						biomeIndex = i;
						break;
					}
				}
			}

			int bodyIndex = subjectBody.flightGlobalsIndex;
			Situation vesselSituation = new Situation(bodyIndex, scienceSituation, biomeIndex);
			
			// if the subject is a "doable" subject, we should have it in the DB.
			if (extraSituationInfo == string.Empty)
				subjectData = GetSubjectData(expInfo, vesselSituation);

			// else create the subjectdata. this can happen either because :
			// - it's a subject using the stock "extra id" system (asteroid samples)
			// - the subject was created in RnD prior to an experiment definition config change
			// - it was created by a mod that does things in a non-stock way (ex : DMOS anomaly scans uses the anomaly name as biomes)
			if (subjectData == null)
			{
				if (bodyAndBiome.Length == 2 && bodyAndBiome[1] != string.Empty && string.IsNullOrEmpty(extraSituationInfo))
				{
					extraSituationInfo = bodyAndBiome[1];
				}

				UnknownSubjectData unknownSubjectData = new UnknownSubjectData(expInfo, vesselSituation, stockSubjectId, RnDSubject, extraSituationInfo);
				subjectData = unknownSubjectData;
				unknownSubjectDatas.Add(stockSubjectId, unknownSubjectData);
				expBodiesSituationsBiomesSubject.AddSubject(subjectData.ExpInfo, bodyIndex, scienceSituation, biomeIndex, subjectData);
			}
				
			return subjectData;
		}

		public static BodiesSituationsBiomesSubject GetSubjectsForExperiment(ExperimentInfo expInfo)
		{
			BodiesSituationsBiomesSubject result;
			expBodiesSituationsBiomesSubject.TryGetValue(expInfo, out result);
			return result;
		}
	}
}
