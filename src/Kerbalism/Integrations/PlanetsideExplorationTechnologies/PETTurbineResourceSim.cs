using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Shared EC rate logic for Planetside Exploration Technologies wind turbines.
	/// Loaded vessels reuse PET's per-frame efficiency; unloaded vessels use expected wind.
	/// </summary>
	internal static class PETTurbineResourceSim
	{
		private const string ResourceEc = "ElectricCharge";

		private static bool expectedWindCached;
		private static double expectedWind = 0.8;
		private static bool loadedRateDiagLogged;
		private static bool loadedPositiveRateLogged;
		private static float nextBackgroundTraceTime;

		/// <summary>Broker id for ResourceUpdate / Background (must match ResourceBroker.WindTurbine.Id).</summary>
		public static string BrokerId => ResourceBroker.WindTurbine.Id;

		public static string BrokerTitle => Local.Brokers_WindTurbine;

		public static double GetLoadedRate(PartModule turbine, Vessel vessel)
		{
			if (turbine == null || vessel == null)
				return 0.0;

			bool envOk = CanProduceEnvironment(vessel, turbine.part);
			bool genOk = IsGeneratingState(turbine);
			float chargeRate = PlanetsideExplorationTechnologies.Get(turbine, "chargeRate", 0f);
			float efficiencyCurve = PlanetsideExplorationTechnologies.Get(turbine, "efficiencyCurve", 0f);
			float trueEfficiencyAngle = GetAngleEfficiency(turbine);
			object deployState = PlanetsideExplorationTechnologies.Get<object>(turbine, "deployState", null);
			bool isActive = PlanetsideExplorationTechnologies.Get(turbine, "isActive", false);
			double rate = (envOk && genOk) ? chargeRate * efficiencyCurve * trueEfficiencyAngle : 0.0;
			if (rate < 0.0)
				rate = 0.0;

			// One-shot Release log so StockChinese KSP.log shows why Monitor gets (or misses) EC.
			if (!loadedRateDiagLogged)
			{
				loadedRateDiagLogged = true;
				Lib.Log(string.Format(
					"PET turbine ResourceUpdate: vessel={0} landed={1} splashed={2} atm={3:F4} waterContact={4} deployState={5} isActive={6} envOk={7} genOk={8} chargeRate={9:F3} efficiencyCurve={10:F4} trueEfficiencyAngle={11:F4} rate={12:F3} EC/s",
					vessel.vesselName,
					vessel.Landed,
					vessel.Splashed,
					vessel.atmDensity,
					turbine.part != null && turbine.part.WaterContact,
					deployState != null ? deployState.ToString() : "null",
					isActive,
					envOk,
					genOk,
					chargeRate,
					efficiencyCurve,
					trueEfficiencyAngle,
					rate));
			}

			if (rate > 0.0 && !loadedPositiveRateLogged)
			{
				loadedPositiveRateLogged = true;
				Lib.Log(string.Format(
					"PET turbine production active: vessel={0} rate={1:F3} EC/s broker={2}",
					vessel.vesselName,
					rate,
					BrokerId));
			}

			return rate;
		}

		public static float GetAngleEfficiency(PartModule turbine)
		{
			// Newer PET source caches this factor, but the currently distributed DLL computes
			// it directly into efficiencyAngle. Support both binary layouts.
			float factor = PlanetsideExplorationTechnologies.Get(turbine, "trueEfficiencyAngle", float.NaN);
			if (float.IsNaN(factor))
				factor = PlanetsideExplorationTechnologies.Get(turbine, "efficiencyAngle", 0f);

			return Mathf.Clamp01(factor);
		}

		public static double GetBackgroundRate(Vessel v, ProtoPartModuleSnapshot turbineProto, PartModule turbinePrefab)
		{
			if (v == null || turbineProto == null || turbinePrefab == null)
				return 0.0;

			bool envOk = CanProduceEnvironment(v, null);
			bool genOk = IsGeneratingState(turbineProto);
			float chargeRate = PlanetsideExplorationTechnologies.Get(turbinePrefab, "chargeRate", 0f);
			float minWindSpeed = PlanetsideExplorationTechnologies.Get(turbinePrefab, "minWindSpeed", 0.25f);
			FloatCurve atmCurve = PlanetsideExplorationTechnologies.Get<FloatCurve>(turbinePrefab, "atmEfficiencyCurve", null);
			double alignment = GetAlignmentFactor(turbinePrefab);
			double wind = GetExpectedWindMultiplier();
			double atmDensity = GetAtmosphericDensity(v);
			double localWindSpeed = atmDensity * wind * alignment;
			double rate = 0.0;

			if (envOk && genOk && localWindSpeed >= minWindSpeed && atmDensity > double.Epsilon)
			{
				double atmFactor = atmCurve != null ? atmCurve.Evaluate((float)atmDensity) : 1.0;
				// Steady-state tracking approximation of PET's loaded formula
				rate = chargeRate * wind * atmFactor * alignment;
				if (rate < 0.0)
					rate = 0.0;
			}

			if (Time.realtimeSinceStartup >= nextBackgroundTraceTime)
			{
				nextBackgroundTraceTime = Time.realtimeSinceStartup + 2.0f;
				Lib.Log(string.Format(
					"PET_TRACE BACKGROUND vessel={0} altitude={1:R} vesselAtmDensity={2:R} calculatedAtmDensity={3:R} deploy={4} active={5} envOk={6} genOk={7} expectedWind={8:R} alignment={9:R} localWind={10:R} minWind={11:R} rate={12:R}",
					v.vesselName,
					v.altitude,
					v.atmDensity,
					atmDensity,
					GetDeployState(turbineProto),
					Lib.Proto.GetBool(turbineProto, "isActive"),
					envOk,
					genOk,
					wind,
					alignment,
					localWindSpeed,
					minWindSpeed,
					rate));
			}

			return rate;
		}

		public static void AddRate(List<KeyValuePair<string, double>> resourceChangeRequest, double rate)
		{
			if (resourceChangeRequest == null || rate <= 0.0)
				return;

			resourceChangeRequest.Add(new KeyValuePair<string, double>(ResourceEc, rate));
		}

		public static bool CanProduceEnvironment(Vessel v, Part part)
		{
			if (v == null)
				return false;

			// Match PET: only water contact / splash kills wind; do not require Landed
			// (surface bases can report Landed=false while PET PAW still shows output).
			if (v.loaded)
			{
				if (v.Splashed)
					return false;
				if (part != null && part.WaterContact)
					return false;
			}
			else
			{
				if (v.protoVessel == null || v.protoVessel.splashed)
					return false;
			}

			return GetAtmosphericDensity(v) > 1e-6;
		}

		private static double GetAtmosphericDensity(Vessel v)
		{
			if (v == null)
				return 0.0;

			if (v.loaded)
				return Math.Max(0.0, v.atmDensity);

			CelestialBody body = v.mainBody;
			double altitude = Math.Max(0.0, v.altitude);
			if (body == null || !body.atmosphere || altitude >= body.atmosphereDepth)
				return 0.0;

			double pressure = body.GetPressure(altitude);
			double temperature = body.GetTemperature(altitude);
			return Math.Max(0.0, body.GetDensity(pressure, temperature));
		}

		public static bool IsGeneratingState(PartModule turbine)
		{
			if (turbine == null)
				return false;

			object deployState = PlanetsideExplorationTechnologies.Get<object>(turbine, "deployState", null);
			if (deployState == null || !string.Equals(deployState.ToString(), "EXTENDED", StringComparison.Ordinal))
				return false;

			return PlanetsideExplorationTechnologies.Get(turbine, "isActive", false);
		}

		public static bool IsGeneratingState(ProtoPartModuleSnapshot turbineProto)
		{
			if (turbineProto == null)
				return false;

			string deployState = Lib.Proto.GetString(turbineProto, "deployState");
			if (!string.Equals(deployState, "EXTENDED", StringComparison.Ordinal))
				return false;

			return Lib.Proto.GetBool(turbineProto, "isActive");
		}

		public static string GetDeployState(PartModule turbine)
		{
			object deployState = PlanetsideExplorationTechnologies.Get<object>(turbine, "deployState", null);
			return deployState != null ? deployState.ToString() : "RETRACTED";
		}

		public static string GetDeployState(ProtoPartModuleSnapshot turbineProto)
		{
			return Lib.Proto.GetString(turbineProto, "deployState", "RETRACTED");
		}

		public static bool IsDeployable(PartModule turbine)
		{
			if (turbine == null)
				return false;

			string animationName = PlanetsideExplorationTechnologies.Get(turbine, "animationName", string.Empty);
			return !string.IsNullOrEmpty(animationName);
		}

		public static bool IsBroken(PartModule turbine)
		{
			return string.Equals(GetDeployState(turbine), "BROKEN", StringComparison.Ordinal);
		}

		public static bool IsBroken(ProtoPartModuleSnapshot turbineProto)
		{
			return string.Equals(GetDeployState(turbineProto), "BROKEN", StringComparison.Ordinal);
		}

		public static void Extend(PartModule turbine)
		{
			if (turbine == null || IsBroken(turbine))
				return;
			PlanetsideExplorationTechnologies.Call(turbine, "Extend");
		}

		public static void Retract(PartModule turbine)
		{
			if (turbine == null || IsBroken(turbine))
				return;
			PlanetsideExplorationTechnologies.Call(turbine, "Retract");
		}

		public static void SetActive(PartModule turbine, bool value)
		{
			if (turbine == null || IsBroken(turbine))
				return;
			PlanetsideExplorationTechnologies.Set(turbine, "isActive", value);
		}

		public static void ProtoSetDeployed(ProtoPartModuleSnapshot turbineProto, bool deployed)
		{
			if (turbineProto == null || IsBroken(turbineProto))
				return;

			if (deployed)
			{
				Lib.Proto.Set(turbineProto, "deployState", "EXTENDED");
				Lib.Proto.Set(turbineProto, "savedAnimationTime", 1f);
				Lib.Proto.Set(turbineProto, "isActive", true);
			}
			else
			{
				Lib.Proto.Set(turbineProto, "deployState", "RETRACTED");
				Lib.Proto.Set(turbineProto, "savedAnimationTime", 0f);
				Lib.Proto.Set(turbineProto, "isActive", false);
			}
		}

		public static void ProtoSetActive(ProtoPartModuleSnapshot turbineProto, bool value)
		{
			if (turbineProto == null || IsBroken(turbineProto))
				return;
			Lib.Proto.Set(turbineProto, "isActive", value);
		}

		public static ProtoPartModuleSnapshot FindTurbineProto(ProtoPartSnapshot partSnapshot)
		{
			return IntegrationUtils.TryFindPartModuleSnapshot(partSnapshot, PlanetsideExplorationTechnologies.TurbineModuleName);
		}

		public static PartModule FindTurbinePrefab(Part partPrefab)
		{
			return PlanetsideExplorationTechnologies.FindTurbineModule(partPrefab);
		}

		private static double GetAlignmentFactor(PartModule turbinePrefab)
		{
			string turbineType = PlanetsideExplorationTechnologies.Get(turbinePrefab, "turbineType", string.Empty);
			string rotationPivotName = PlanetsideExplorationTechnologies.Get(turbinePrefab, "rotationPivotName", string.Empty);
			bool tracking = string.Equals(turbineType, "Tracking", StringComparison.OrdinalIgnoreCase)
				|| !string.IsNullOrEmpty(rotationPivotName);
			return tracking ? 1.0 : 0.75;
		}

		private static double GetExpectedWindMultiplier()
		{
			if (expectedWindCached)
				return expectedWind;

			expectedWindCached = true;
			expectedWind = ComputeExpectedWindFromDifficulty();
			return expectedWind;
		}

		private static double ComputeExpectedWindFromDifficulty()
		{
			// Defaults match PET DifficultyWindProbability field initializers
			double pHigh = 0.10;
			double pMid = 0.55;
			double pLow = 0.25;
			double pNone = 0.15;

			try
			{
				if (HighLogic.CurrentGame != null)
				{
					Type difficultyType = AccessTools.TypeByName("PlanetsideExplorationTechnologies.DifficultySettings.DifficultyWindProbability");
					if (difficultyType != null)
					{
						// GameParameters.CustomParams<T>() — invoke via MakeGenericMethod
						MethodInfo generic = typeof(GameParameters).GetMethod("CustomParams", Type.EmptyTypes);
						if (generic != null && generic.IsGenericMethodDefinition)
						{
							object node = generic.MakeGenericMethod(difficultyType).Invoke(HighLogic.CurrentGame.Parameters, null);
							if (node != null)
							{
								pHigh = IntegrationReflection.GetFloat(node, "probabilityHighWinds", (float)pHigh);
								pMid = IntegrationReflection.GetFloat(node, "probabilityMidWinds", (float)pMid);
								pLow = IntegrationReflection.GetFloat(node, "probabilityLowWinds", (float)pLow);
								pNone = IntegrationReflection.GetFloat(node, "probabilityNoWinds", (float)pNone);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Lib.LogDebug("PET wind difficulty read failed: " + e.Message);
			}

			double total = pHigh + pMid + pLow + pNone;
			if (total <= double.Epsilon)
				return 0.8;

			// Midpoints of PET GenerateWindSpeed ranges
			double highMid = (1.1 + 1.8) * 0.5;
			double midMid = (0.9 + 1.05) * 0.5;
			double lowMid = (0.5 + 0.8) * 0.5;
			return (pHigh * highMid + pMid * midMid + pLow * lowMid) / total;
		}
	}
}
