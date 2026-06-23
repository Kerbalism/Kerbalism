using System.Collections.Generic;
using KERBALISM;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
	internal static class CapacitorResourceSim
	{
		internal static bool HasChargeHeadroom(PartModule capacitor)
		{
			if (capacitor == null)
				return false;

			PartResource stored = FindStoredChargeResource(capacitor.part);
			float maximumCharge = NearFutureElectrical.Get(capacitor, "MaximumCharge", 0f);
			float currentCharge = NearFutureElectrical.Get(capacitor, "CurrentCharge", 0f);
			if (stored == null)
				return currentCharge < maximumCharge;

			float capacity = Mathf.Min((float)stored.maxAmount, maximumCharge);
			return stored.amount < capacity - 1e-4f;
		}

		internal static bool IsCharging(PartModule capacitor)
		{
			return capacitor != null
				&& NearFutureElectrical.Get(capacitor, "Enabled", false)
				&& !NearFutureElectrical.Get(capacitor, "Discharging", false)
				&& NearFutureElectrical.Get(capacitor, "ChargeRate", 0f) > 0f
				&& HasChargeHeadroom(capacitor);
		}

		internal static bool ShouldPlannerReportCharging(PartModule capacitor)
		{
			if (capacitor == null || !NearFutureElectrical.Get(capacitor, "Enabled", false) || NearFutureElectrical.Get(capacitor, "Discharging", false))
				return false;

			if (NearFutureElectrical.Get(capacitor, "ChargeRate", 0f) <= 0f)
				return false;

			if (Lib.IsEditor())
				return true;

			return HasChargeHeadroom(capacitor);
		}

		internal static bool IsDischarging(PartModule capacitor)
		{
			return capacitor != null
				&& NearFutureElectrical.Get(capacitor, "Discharging", false)
				&& NearFutureElectrical.Get(capacitor, "CurrentCharge", 0f) > 1e-6f;
		}

		internal static bool HasChargeOperatingPower(PartModule capacitor, Vessel v)
		{
			float chargeRate = NearFutureElectrical.Get(capacitor, "ChargeRate", 0f);
			if (chargeRate <= 0f)
				return true;
			if (v == null)
				return false;
			return KERBALISM.ResourceCache.GetResource(v, "ElectricCharge").Amount >= chargeRate;
		}

		internal static void AddPlannerRates(PartModule capacitor, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (IsDischarging(capacitor))
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", NearFutureElectrical.Get(capacitor, "dischargeActual", 0f)));
			else if (ShouldPlannerReportCharging(capacitor))
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -NearFutureElectrical.Get(capacitor, "ChargeRate", 0f)));
		}

		internal static string UpdateLoaded(PartModule capacitor, Vessel v, string brokerName, string brokerTitle)
		{
			if (capacitor == null || v == null)
				return brokerTitle;

			float dischargeRate = NearFutureElectrical.Get(capacitor, "DischargeRate", 0f);
			float dischargeMin = NearFutureElectrical.Get(capacitor, "DischargeRateMinimumScalar", 0f);
			float dischargeActual = NearFutureElectrical.Get(capacitor, "dischargeActual", 0f);
			dischargeActual = Mathf.Clamp(dischargeActual, dischargeRate * dischargeMin, dischargeRate);
			NearFutureElectrical.Set(capacitor, "dischargeActual", dischargeActual);

			float dt = TimeWarp.fixedDeltaTime;
			KERBALISM.ResourceBroker broker = KERBALISM.ResourceBroker.GetOrCreate(brokerName, KERBALISM.ResourceBroker.BrokerCategory.Converter, brokerTitle);

			if (IsDischarging(capacitor))
			{
				double request = dischargeActual * dt;
				double removed = RemoveStoredCharge(capacitor.part, request);
				if (removed > double.Epsilon)
					KERBALISM.ResourceCache.GetResource(v, "ElectricCharge").Produce(removed, broker);

				if (NearFutureElectrical.Get(capacitor, "DischargeGeneratesHeat", false) && TimeWarp.CurrentRate <= 100f)
					capacitor.part.AddThermalFlux(NearFutureElectrical.Get(capacitor, "HeatRate", 0f));

				if (NearFutureElectrical.Get(capacitor, "CurrentCharge", 0f) <= 1e-6f)
					NearFutureElectrical.Set(capacitor, "Discharging", false);
			}
			else if (IsCharging(capacitor))
			{
				float chargeRate = NearFutureElectrical.Get(capacitor, "ChargeRate", 0f);
				double request = chargeRate * dt;
				ResourceInfo ec = KERBALISM.ResourceCache.GetResource(v, "ElectricCharge");
				if (ec.Amount >= chargeRate)
				{
					ec.Consume(request, broker);
					AddStoredCharge(capacitor.part, request * NearFutureElectrical.Get(capacitor, "ChargeRatio", 1f), NearFutureElectrical.Get(capacitor, "MaximumCharge", 0f));
					NearFutureElectrical.Set(capacitor, "lastUpdateTime", Planetarium.GetUniversalTime());
				}
			}

			UpdateCapacitorStatus(capacitor);
			SyncColorChanger(capacitor);
			return brokerTitle;
		}

		private static void UpdateCapacitorStatus(PartModule capacitor)
		{
			if (capacitor == null)
				return;

			float dischargeRate = NearFutureElectrical.Get(capacitor, "DischargeRate", 0f);
			float dischargeMin = NearFutureElectrical.Get(capacitor, "DischargeRateMinimumScalar", 0f);
			float dischargeActual = NearFutureElectrical.Get(capacitor, "dischargeActual", 0f);
			dischargeActual = Mathf.Clamp(dischargeActual, dischargeRate * dischargeMin, dischargeRate);
			NearFutureElectrical.Set(capacitor, "dischargeActual", dischargeActual);

			if (IsDischarging(capacitor))
			{
				NearFutureElectrical.Set(capacitor, "CapacitorStatus", Localizer.Format(
					"#LOC_NFElectrical_ModuleDischargeCapacitor_Field_Status_Discharging",
					dischargeActual.ToString("F2")));
			}
			else if (IsCharging(capacitor))
			{
				if (HasChargeOperatingPower(capacitor, capacitor.vessel))
				{
					NearFutureElectrical.Set(capacitor, "CapacitorStatus", Localizer.Format(
						"#LOC_NFElectrical_ModuleDischargeCapacitor_Field_Status_Charging",
						NearFutureElectrical.Get(capacitor, "ChargeRate", 0f).ToString("F2")));
				}
				else
				{
					NearFutureElectrical.Set(capacitor, "CapacitorStatus", Localizer.Format(
						"#LOC_NFElectrical_ModuleDischargeCapacitor_Field_Status_NoPower"));
				}
			}
			else if (NearFutureElectrical.Get(capacitor, "Enabled", false) && !NearFutureElectrical.Get(capacitor, "Discharging", false) && HasChargeHeadroom(capacitor))
			{
				NearFutureElectrical.Set(capacitor, "CapacitorStatus", Localizer.Format(
					"#LOC_NFElectrical_ModuleDischargeCapacitor_Field_Status_NoPower"));
			}
			else if (NearFutureElectrical.Get(capacitor, "CurrentCharge", 0f) <= 1e-6f)
			{
				NearFutureElectrical.Set(capacitor, "CapacitorStatus", Localizer.Format(
					"#LOC_NFElectrical_ModuleDischargeCapacitor_Field_Status_Empty"));
			}
			else
			{
				NearFutureElectrical.Set(capacitor, "CapacitorStatus", Localizer.Format(
					"#LOC_NFElectrical_ModuleDischargeCapacitor_Field_Status_Ready"));
			}
		}

		private static void SyncColorChanger(PartModule capacitor)
		{
			string moduleId = NearFutureElectrical.Get(capacitor, "ModuleID", "");
			float maximumCharge = NearFutureElectrical.Get(capacitor, "MaximumCharge", 0f);
			if (capacitor == null || string.IsNullOrEmpty(moduleId) || maximumCharge <= 0f)
				return;

			foreach (ModuleColorChanger colorChanger in capacitor.part.GetComponents<ModuleColorChanger>())
			{
				if (colorChanger.moduleID == moduleId)
				{
					colorChanger.SetScalar(NearFutureElectrical.Get(capacitor, "CurrentCharge", 0f) / maximumCharge);
					break;
				}
			}
		}

		internal static void SyncCapacitorVisuals(PartModule capacitor)
		{
			UpdateCapacitorStatus(capacitor);
			SyncColorChanger(capacitor);
		}

		internal static string BackgroundUpdate(
			Vessel v,
			ProtoPartSnapshot partSnapshot,
			ProtoPartModuleSnapshot capacitorSnapshot,
			Part prefab,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsed_s)
		{
			if (Lib.Proto.GetBool(capacitorSnapshot, "Discharging"))
			{
				float dischargeRate = Lib.Proto.GetFloat(capacitorSnapshot, "dischargeActual");
				if (dischargeRate > 0f)
				{
					resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", dischargeRate));
					RemoveStoredCharge(partSnapshot, dischargeRate * elapsed_s);
					if (GetStoredCharge(partSnapshot) <= 1e-6)
						Lib.Proto.Set(capacitorSnapshot, "Discharging", false);
				}
			}
			else if (Lib.Proto.GetBool(capacitorSnapshot, "Enabled") && !Lib.Proto.GetBool(capacitorSnapshot, "Discharging"))
			{
				PartModule prefabModule = NearFutureElectrical.FindCapacitorModule(prefab);
				if (prefabModule == null || NearFutureElectrical.Get(prefabModule, "ChargeRate", 0f) <= 0f)
					return NFECapacitorKerbalismUpdater.brokerTitle;

				float maximumCharge = NearFutureElectrical.Get(prefabModule, "MaximumCharge", 0f);
				if (GetStoredCharge(partSnapshot) >= maximumCharge)
					return NFECapacitorKerbalismUpdater.brokerTitle;

				float chargeRate = NearFutureElectrical.Get(prefabModule, "ChargeRate", 0f);
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -chargeRate));

				double ec = KERBALISM.ResourceCache.Get(v).GetResource(v, "ElectricCharge").Amount;
				double chargeRequest = chargeRate * elapsed_s;
				if (ec >= chargeRate)
					AddStoredCharge(partSnapshot, chargeRequest * NearFutureElectrical.Get(prefabModule, "ChargeRatio", 1f), maximumCharge);
			}

			return NFECapacitorKerbalismUpdater.brokerTitle;
		}

		private static PartResource FindStoredChargeResource(Part part)
		{
			if (part == null)
				return null;

			for (int i = 0; i < part.Resources.Count; i++)
			{
				PartResource resource = part.Resources[i];
				if (resource.resourceName == "StoredCharge")
					return resource;
			}
			return null;
		}

		private static double RemoveStoredCharge(Part part, double amount)
		{
			PartResource stored = FindStoredChargeResource(part);
			if (stored == null || amount <= 0.0)
				return 0.0;

			double removed = System.Math.Min(stored.amount, amount);
			stored.amount -= removed;
			return removed;
		}

		private static void AddStoredCharge(Part part, double amount, float maximumCharge)
		{
			PartResource stored = FindStoredChargeResource(part);
			if (stored == null || amount <= 0.0)
				return;

			stored.amount = System.Math.Min(maximumCharge, stored.amount + amount);
		}

		private static double GetStoredCharge(ProtoPartSnapshot partSnapshot)
		{
			ProtoPartResourceSnapshot stored = FindResource(partSnapshot, "StoredCharge");
			return stored != null ? stored.amount : 0.0;
		}

		private static double RemoveStoredCharge(ProtoPartSnapshot partSnapshot, double amount)
		{
			ProtoPartResourceSnapshot stored = FindResource(partSnapshot, "StoredCharge");
			if (stored == null || amount <= 0.0)
				return 0.0;

			double removed = System.Math.Min(stored.amount, amount);
			stored.amount -= removed;
			return removed;
		}

		private static void AddStoredCharge(ProtoPartSnapshot partSnapshot, double amount, float maximumCharge)
		{
			ProtoPartResourceSnapshot stored = FindResource(partSnapshot, "StoredCharge");
			if (stored == null || amount <= 0.0)
				return;

			stored.amount = System.Math.Min(maximumCharge, stored.amount + amount);
		}

		private static ProtoPartResourceSnapshot FindResource(ProtoPartSnapshot partSnapshot, string resourceName)
		{
			for (int i = 0; i < partSnapshot.resources.Count; i++)
			{
				if (partSnapshot.resources[i].resourceName == resourceName)
					return partSnapshot.resources[i];
			}
			return null;
		}
	}
}
