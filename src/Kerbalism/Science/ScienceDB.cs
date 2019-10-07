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

		public class BiomesSubjects : KeyValueList<int, List<SubjectData>> { }
		public class SituationsBiomesSubjects : KeyValueList<ScienceSituation, BiomesSubjects> { }
		public class BodiesSituationsBiomesSubjects : KeyValueList<int, SituationsBiomesSubjects>
		{
			public void AddSubject(int bodyIndex, ScienceSituation scienceSituation, int biomeIndex, SubjectData subjectData)
			{
				SituationsBiomesSubjects situationsBiomesSubjects;
				if (!TryGetValue(bodyIndex, out situationsBiomesSubjects))
				{
					situationsBiomesSubjects = new SituationsBiomesSubjects();
					Add(bodyIndex, situationsBiomesSubjects);
				}

				BiomesSubjects biomesSubjects;
				if (!situationsBiomesSubjects.TryGetValue(scienceSituation, out biomesSubjects))
				{
					biomesSubjects = new BiomesSubjects();
					situationsBiomesSubjects.Add(scienceSituation, biomesSubjects);
				}

				List<SubjectData> subjects;
				if (!biomesSubjects.TryGetValue(biomeIndex, out subjects))
				{
					subjects = new List<SubjectData>();
					biomesSubjects.Add(biomeIndex, subjects);
				}

				subjects.Add(subjectData);
			}

			public void RemoveSubject(int bodyIndex, ScienceSituation scienceSituation, int biomeIndex, SubjectData subjectData)
			{
				SituationsBiomesSubjects situationsBiomesSubjects;
				if (!TryGetValue(bodyIndex, out situationsBiomesSubjects))
					return;

				BiomesSubjects biomesSubjects;
				if (!situationsBiomesSubjects.TryGetValue(scienceSituation, out biomesSubjects))
					return;

				List<SubjectData> subjects;
				if (!biomesSubjects.TryGetValue(biomeIndex, out subjects))
					return;

				subjects.Remove(subjectData);
			}
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

				subjectDataList.Add(subjectData);
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

		/// <summary>
		/// For every body index, situation index, biome index, the corresponding SubjectDatas.
		/// Used to get/display all subjects for a given situation
		/// <para/> body indexes are from the FlightGlobals.Bodies list, and match the CelestialBody.flightGlobalsIndex value
		/// <para/> situation indexes are the ScienceSituation enum value
		/// <para/> biome indexes are from the CelestialBody.BiomeMap.Attribute. The -1 index correspond to the biome-agnostic situation.
		/// </summary>
		private static readonly BodiesSituationsBiomesSubjects
			bodiesSituationsBiomesSubjects = new BodiesSituationsBiomesSubjects();

		// HashSet of all subjects using the stock string id as key, used for RnD subjects synchronization
		private static readonly HashSet<string> knownStockSubjectsId = new HashSet<string>();

		private static readonly List<MultiSubjectData> multiSubjectDataList = new List<MultiSubjectData>();

		public static double uncreditedScience;

		/// <summary>
		/// List of subjects that should be persisted in our own DB
		/// </summary>
		public static readonly UniqueValuesList<SubjectData> persistedSubjects = new UniqueValuesList<SubjectData>();

		public static void Init()
		{


			// get our extra defintions
			ConfigNode[] expDefNodes = GameDatabase.Instance.GetConfigNodes("EXPERIMENT_DEFINITION");

			// create our subject database
			// Note : GetExperimentIDs will force the creation of all ScienceExperiment objects,
			// no matter if the RnD instance is null or not because the ScienceExperiment dictionary is static.
			foreach (string experimentId in ResearchAndDevelopment.GetExperimentIDs())
			{
				// the recovery experiment (created at runtime by KSP) uses a non standard situation system and a ton of custom handling, so we just ignore it
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

				ExperimentInfo expInfo = new ExperimentInfo(ResearchAndDevelopment.GetExperiment(experimentId), kerbalismExpNode);

				experiments.Add(experimentId, expInfo);
				subjectByExpThenSituationId.Add(expInfo, new Dictionary<int, SubjectData>());


				for (int bodyIndex = 0; bodyIndex < FlightGlobals.Bodies.Count; bodyIndex++)
				{
					CelestialBody body = FlightGlobals.Bodies[bodyIndex];

					if (!expInfo.ExpBodyConditions.IsBodyAllowed(body))
						continue;

					// ScienceSituationUtils.validSituations is all situations in the enum, apart from the "None" value
					foreach (ScienceSituation scienceSituation in ScienceSituationUtils.validSituations)
					{
						// test the ScienceExperiment situation mask
						if (!scienceSituation.IsAvailableForExperiment(expInfo))
							continue;

						// don't add impossible body / situation combinations
						if (!scienceSituation.IsAvailableOnBody(body))
							continue;

						// if the body has no biomes, a single biome (?) or if the experiment is biome agnostic for this situation, generate the global situation
						if (body.BiomeMap == null || body.BiomeMap.Attributes.Length <= 1 || !scienceSituation.IsBiomesRelevantForExperiment(expInfo))
						{
							VesselSituation vesselSituation = new VesselSituation(bodyIndex, scienceSituation);
							SubjectData subjectData = new SubjectData(expInfo, vesselSituation);

							subjectByExpThenSituationId[expInfo].Add(vesselSituation.Id, subjectData);
							expBodiesSituationsBiomesSubject.AddSubject(expInfo, bodyIndex, scienceSituation, -1, subjectData);
							bodiesSituationsBiomesSubjects.AddSubject(bodyIndex, scienceSituation, -1, subjectData);

							knownStockSubjectsId.Add(subjectData.StockSubjectId);
						}
						else
						{
							for (int biomeIndex = 0; biomeIndex < body.BiomeMap.Attributes.Length; biomeIndex++)
							{
								VesselSituation vesselSituation = new VesselSituation(bodyIndex, scienceSituation, biomeIndex);
								SubjectData subjectData = new SubjectData(expInfo, vesselSituation);

								subjectByExpThenSituationId[expInfo].Add(vesselSituation.Id, subjectData);
								expBodiesSituationsBiomesSubject.AddSubject(expInfo, bodyIndex, scienceSituation, biomeIndex, subjectData);
								bodiesSituationsBiomesSubjects.AddSubject(bodyIndex, scienceSituation, biomeIndex, subjectData);

								knownStockSubjectsId.Add(subjectData.StockSubjectId);
							}
						}
					}
				}
			}
		}

		public static void Load(ConfigNode node)
		{
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

			// RnD subjects don't exists in sandbox
			if (!Science.GameHasRnD)
				return;

			// remove multiSubjects (asteroid samples) from the database
			foreach (MultiSubjectData subjectData in multiSubjectDataList)
			{
				int bodyIndex;
				int scienceSituation;
				int biomeIndex;

				VesselSituation.IdToFields(subjectData.Situation.Id, out bodyIndex, out scienceSituation, out biomeIndex);

				expBodiesSituationsBiomesSubject.RemoveSubject(subjectData.ExpInfo, bodyIndex, (ScienceSituation)scienceSituation, biomeIndex, subjectData);
				bodiesSituationsBiomesSubjects.RemoveSubject(bodyIndex, (ScienceSituation)scienceSituation, biomeIndex, subjectData);
			}

			// clear the list
			multiSubjectDataList.Clear();


			// find them again
			foreach (ScienceSubject stockSubject in ResearchAndDevelopment.GetSubjects())
				if (!knownStockSubjectsId.Contains(stockSubject.id))
					GetSubjectDataFromStockId(stockSubject.id, stockSubject);


		}

		public static void Save(ConfigNode node)
		{
			// save uncredited science (transmission buffer)
			node.AddValue("uncreditedScience", uncreditedScience);

			// save science subjects persisted data
			var subjectsNode = node.AddNode("subjectData");
			foreach (SubjectData subject in persistedSubjects)
			{
				subject.Save(subjectsNode.AddNode(DB.To_safe_key(subject.Id)));
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
		public static SubjectData GetSubjectData(ExperimentInfo expInfo, VesselSituation situation)
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
		public static SubjectData GetSubjectData(ExperimentInfo expInfo, VesselSituation situation, out int situationId)
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
				Lib.Log("Could not parse the SubjectData from subjectId '" + stockSubjectId + "' : the body '" + bodyAndBiome[0] + "' doesn't exist");
				return null;
			}

			ScienceSituation scienceSituation = ScienceSituationUtils.ScienceSituationDeserialize(situation);

			int biomeIndex = -1;
			if (bodyAndBiome.Length == 2 && ScienceSituationUtils.IsBiomesRelevantForExperiment(scienceSituation, expInfo) && subjectBody.BiomeMap != null)
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
			VesselSituation vesselSituation = new VesselSituation(bodyIndex, scienceSituation, biomeIndex);

			SubjectData subjectData = null;
			// if the subject isn't an asteroid sample or another kind of "more than one by situation" subject, we should have it in the DB, unless there has been a config change
			if (extraSituationInfo == string.Empty)
				subjectData = GetSubjectData(expInfo, vesselSituation);

			// if it's an asteroid sample, or if it wasn't found, add it to the subject archives, but not to the available subjects dictionary
			// and keep track of them in multiSubjectDataList so we can clear and reload them on load
			if (subjectData == null)
			{
				MultiSubjectData multiSubjectData = new MultiSubjectData(expInfo, vesselSituation, stockSubjectId, RnDSubject, extraSituationInfo);
				subjectData = multiSubjectData;
				multiSubjectDataList.Add(multiSubjectData);
				expBodiesSituationsBiomesSubject.AddSubject(subjectData.ExpInfo, bodyIndex, scienceSituation, biomeIndex, multiSubjectData);
				bodiesSituationsBiomesSubjects.AddSubject(bodyIndex, scienceSituation, biomeIndex, multiSubjectData);
			}
				
			return subjectData;
		}

		public static BodiesSituationsBiomesSubject GetSubjectsForExperiment(ExperimentInfo expInfo)
		{
			return expBodiesSituationsBiomesSubject[expInfo];
		}
	}
}
