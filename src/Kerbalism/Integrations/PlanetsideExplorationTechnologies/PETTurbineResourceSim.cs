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

		private static bool windProbabilitiesCached;
		private static double probabilityHighWinds = 0.10;
		private static double probabilityMidWinds = 0.55;
		private static double probabilityLowWinds = 0.25;
		private static double probabilityNoWinds = 0.15;

		/// <summary>Broker id for ResourceUpdate / Background (must match ResourceBroker.WindTurbine.Id).</summary>
		public static string BrokerId => ResourceBroker.WindTurbine.Id;

		public static string BrokerTitle => Local.Brokers_WindTurbine;

		public static double GetLoadedRate(PartModule turbine, Vessel vessel)
		{
			if (turbine == null || vessel == null)
				return 0.0;

			if (!CanProduceEnvironment(vessel, turbine.part) || !IsGeneratingState(turbine))
				return 0.0;

			float chargeRate = PlanetsideExplorationTechnologies.Get(turbine, "chargeRate", 0f);
			float efficiencyCurve = PlanetsideExplorationTechnologies.Get(turbine, "efficiencyCurve", 0f);
			double rate = chargeRate * efficiencyCurve * GetAngleEfficiency(turbine);
			return rate > 0.0 ? rate : 0.0;
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
			double atmDensity = GetAtmosphericDensity(v);
			double rate = 0.0;

			if (envOk && genOk && atmDensity > double.Epsilon)
			{
				double atmFactor = atmCurve != null ? atmCurve.Evaluate((float)atmDensity) : 1.0;
				bool tracking = IsTracking(turbinePrefab);
				double effectiveWind = GetExpectedEffectiveWind(atmDensity, minWindSpeed, tracking);
				// PET's steady-state output is chargeRate * wind * atmFactor * angleEfficiency².
				rate = chargeRate * atmFactor * effectiveWind;
				if (rate < 0.0)
					rate = 0.0;
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

		private static bool IsTracking(PartModule turbinePrefab)
		{
			string turbineType = PlanetsideExplorationTechnologies.Get(turbinePrefab, "turbineType", string.Empty);
			string rotationPivotName = PlanetsideExplorationTechnologies.Get(turbinePrefab, "rotationPivotName", string.Empty);
			return string.Equals(turbineType, "Tracking", StringComparison.OrdinalIgnoreCase)
				|| !string.IsNullOrEmpty(rotationPivotName);
		}

		private static double GetExpectedEffectiveWind(double atmDensity, double minWindSpeed, bool tracking)
		{
			CacheWindProbabilities();

			double totalProbability = probabilityHighWinds + probabilityMidWinds + probabilityLowWinds + probabilityNoWinds;
			if (totalProbability <= double.Epsilon)
				return 0.0;

			// PET samples uniformly inside each non-zero wind band. Midpoints preserve the
			// existing approximation while evaluating the non-linear minimum-wind threshold
			// separately for each band.
			double weightedWind =
				probabilityHighWinds * GetWindContribution(1.45, atmDensity, minWindSpeed, tracking)
				+ probabilityMidWinds * GetWindContribution(0.975, atmDensity, minWindSpeed, tracking)
				+ probabilityLowWinds * GetWindContribution(0.65, atmDensity, minWindSpeed, tracking);

			return weightedWind / totalProbability;
		}

		private static double GetWindContribution(double wind, double atmDensity, double minWindSpeed, bool tracking)
		{
			if (wind <= double.Epsilon || atmDensity <= double.Epsilon)
				return 0.0;

			double minimumAlignment = minWindSpeed / (atmDensity * wind);
			if (tracking)
				return minimumAlignment <= 1.0 ? wind : 0.0;

			if (minimumAlignment >= 1.0)
				return 0.0;

			minimumAlignment = Math.Max(0.0, minimumAlignment);

			// For a fixed turbine and a uniformly random heading, angle efficiency is uniform
			// on [0, 1]. PET applies it twice, so E[a² * I(a >= minimumAlignment)] is
			// integral(a², minimumAlignment..1).
			double expectedSquaredAlignment = (1.0 - minimumAlignment * minimumAlignment * minimumAlignment) / 3.0;
			return wind * expectedSquaredAlignment;
		}

		private static void CacheWindProbabilities()
		{
			if (windProbabilitiesCached)
				return;

			windProbabilitiesCached = true;

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
								probabilityHighWinds = IntegrationReflection.GetFloat(node, "probabilityHighWinds", (float)probabilityHighWinds);
								probabilityMidWinds = IntegrationReflection.GetFloat(node, "probabilityMidWinds", (float)probabilityMidWinds);
								probabilityLowWinds = IntegrationReflection.GetFloat(node, "probabilityLowWinds", (float)probabilityLowWinds);
								probabilityNoWinds = IntegrationReflection.GetFloat(node, "probabilityNoWinds", (float)probabilityNoWinds);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Lib.LogDebug("PET wind difficulty read failed: " + e.Message);
			}
		}
	}
}
