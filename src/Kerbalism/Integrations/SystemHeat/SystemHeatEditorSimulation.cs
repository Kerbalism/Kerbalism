using System;
using UnityEngine;

namespace KERBALISM
{
	public static class SystemHeatEditorSimulation
	{
		public const double MinEff = 1e-5;
		public const double MaxEff = 1.5;
		public const double HystFrac = 1e-3;

		public static bool IsEditorScene => HighLogic.LoadedSceneIsEditor;

		public static double EvaluateEfficiency(FloatCurve curve, float loopTemperatureK)
		{
			if (curve == null)
				return 1.0;

			double eff = curve.Evaluate(loopTemperatureK);
			return Math.Max(MinEff, Math.Min(MaxEff, eff));
		}

		public static double CalculateProcessEfficiency(FloatCurve curve, float loopTemperatureK, float heatPower, bool editorScene)
		{
			if (editorScene || heatPower <= 0f)
				return 1.0;

			double thermalEff = EvaluateEfficiency(curve, loopTemperatureK);

			const double bootstrapEff = 0.01;
			if (thermalEff < bootstrapEff)
				thermalEff = bootstrapEff;

			return thermalEff;
		}

		public static float CalculateTemperatureCoreDamage(float loopTemperatureK, float damageStartK, float fullMeltdownK)
		{
			if (damageStartK <= 0f || fullMeltdownK <= damageStartK || loopTemperatureK <= damageStartK)
				return 0f;

			if (loopTemperatureK >= fullMeltdownK)
				return 100f;

			float progress = (loopTemperatureK - damageStartK) / (fullMeltdownK - damageStartK);
			return Mathf.Clamp01(progress) * 100f;
		}

		public static float SyncCoreDamageFromTemperature(float loopTemperatureK, float damageStartK, float fullMeltdownK, float currentDamage)
		{
			float tempDamage = CalculateTemperatureCoreDamage(loopTemperatureK, damageStartK, fullMeltdownK);
			return Mathf.Clamp(Mathf.Max(currentDamage, tempDamage), 0f, 100f);
		}

		/// <summary>
		/// Integrate rate-based core damage (CoreDamageRate + optional curve) then apply instantaneous temperature floor.
		/// </summary>
		public static float AccumulateCoreDamage(
			float loopTemperatureK,
			float damageStartK,
			float fullMeltdownK,
			float currentDamage,
			float damageRatePerSecond,
			FloatCurve damageCurve,
			float elapsedSeconds)
		{
			float damage = currentDamage;
			if (damageRatePerSecond > 0f && elapsedSeconds > 0f && loopTemperatureK > damageStartK)
			{
				float curveMult = 1f;
				if (damageCurve != null && damageCurve.Curve.length > 0)
					curveMult = damageCurve.Evaluate(loopTemperatureK);
				damage = Mathf.Min(100f, damage + damageRatePerSecond * curveMult * elapsedSeconds * 100f);
			}

			return SyncCoreDamageFromTemperature(loopTemperatureK, damageStartK, fullMeltdownK, damage);
		}

		public static float GetCoreHealthPercent(float loopTemperatureK, float damageStartK, float fullMeltdownK, float coreDamage)
		{
			if (coreDamage >= 100f || ShouldInstantMeltdown(loopTemperatureK, fullMeltdownK))
				return 0f;

			float effectiveDamage = SyncCoreDamageFromTemperature(loopTemperatureK, damageStartK, fullMeltdownK, coreDamage);
			return Mathf.Clamp(100f - effectiveDamage, 0f, 100f);
		}

		public static bool ShouldInstantMeltdown(float loopTemperatureK, float fullMeltdownK)
		{
			return fullMeltdownK > 0f && loopTemperatureK >= fullMeltdownK;
		}
	}
}
