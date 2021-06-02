using System.Reflection;
using SentinelMission;
using HarmonyLib;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
	public class KerbalismSentinel : SentinelModule, IContractObjectiveModule
	{
		// ec consumed per-second
		[KSPField] public double ec_rate = 0.0;

		// required comms data rate in Mb/s
		[KSPField] public double comms_rate = 0.0;

		// track user activation independentely of the current isTracking state,
		// in order to have the stock SentinelMission scenario still work while we can
		// alter isTracking to reflect the powered/connected state of the module.
		[KSPField(isPersistant = true)]
		public bool isTrackingEnabled;

		// trick the stock generator check by making the sentinel module itself a generator contract objective
		public string GetContractObjectiveType()
		{
			return "Generator";
		}

		public bool CheckContractObjectiveValidity()
		{
			return true;
		}

		public override void OnStart(StartState state)
		{
			if (isTrackingEnabled)
			{
				base.Events["StartTracking"].active = false;
				base.Events["StopTracking"].active = true;
			}
			else
			{
				base.Events["StartTracking"].active = true;
				base.Events["StopTracking"].active = false;
			}
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, KerbalismSentinel prefab, VesselData vd, ResourceInfo ec, double elapsed_s)
		{
			if (Lib.Proto.GetBool(m, "isTrackingEnabled"))
			{
				if (!vd.Connection.linked || vd.Connection.rate < prefab.comms_rate)
				{
					Lib.Proto.Set(m, "isTracking", false);
					return;
				}

				ec.Consume(prefab.ec_rate * elapsed_s, ResourceBroker.Scanner);

				if (ec.Amount <= double.Epsilon)
				{
					Lib.Proto.Set(m, "isTracking", false);
					return;
				}

				Lib.Proto.Set(m, "isTracking", true);
			}
		}

		public static void ApplyHarmonyPatches(Harmony harmonyInstance)
		{
			if (!Features.Science)
			{
				return;
			}

			MethodInfo startTracking = typeof(SentinelModule).GetMethod("StartTracking", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo startTrackingPostfix = typeof(KerbalismSentinel).GetMethod("StartTrackingPostfix", BindingFlags.Static | BindingFlags.NonPublic);
			harmonyInstance.Patch(startTracking, null, new HarmonyMethod(startTrackingPostfix));

			MethodInfo stopTracking = typeof(SentinelModule).GetMethod("StopTracking", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo stopTrackingPostfix = typeof(KerbalismSentinel).GetMethod("StopTrackingPostfix", BindingFlags.Static | BindingFlags.NonPublic);
			harmonyInstance.Patch(stopTracking, null, new HarmonyMethod(stopTrackingPostfix));

			MethodInfo fixedUpdate = typeof(SentinelModule).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo fixedUpdatePrefix = typeof(KerbalismSentinel).GetMethod("FixedUpdatePrefix", BindingFlags.Static | BindingFlags.NonPublic);
			harmonyInstance.Patch(fixedUpdate, new HarmonyMethod(fixedUpdatePrefix));

			MethodInfo telescopeCanActivate = typeof(SentinelModule).GetMethod("TelescopeCanActivate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo telescopeCanActivatePrefix = typeof(KerbalismSentinel).GetMethod("TelescopeCanActivatePrefix", BindingFlags.Static | BindingFlags.NonPublic);
			harmonyInstance.Patch(telescopeCanActivate, new HarmonyMethod(telescopeCanActivatePrefix));
		}

		private static void StartTrackingPostfix(KerbalismSentinel __instance)
		{
			if (__instance.isTracking)
			{
				__instance.isTrackingEnabled = true;
				__instance.isTracking = false;
			}
			else
			{
				__instance.isTrackingEnabled = false;
			}
		}

		private static void StopTrackingPostfix(KerbalismSentinel __instance)
		{
			__instance.isTrackingEnabled = false;
		}

		private static bool FixedUpdatePrefix(KerbalismSentinel __instance)
		{
			if (__instance.isTrackingEnabled)
			{
				VesselData vd = __instance.vessel.KerbalismData();
				if (!vd.Connection.linked || vd.Connection.rate < __instance.comms_rate)
				{
					__instance.isTracking = false;
					__instance.status = "Comms connection too weak";
					return false;
				}

				ResourceInfo ec = ResourceCache.GetResource(__instance.vessel, "ElectricCharge");
				ec.Consume(__instance.ec_rate * Kerbalism.elapsed_s, ResourceBroker.Scanner);

				if (ec.Amount <= double.Epsilon)
				{
					__instance.isTracking = false;
					__instance.status = Local.Module_Experiment_issue4; // "no Electricity"
					return false;
				}

				__instance.isTracking = true;

			}

			return true;
		}

		private static bool TelescopeCanActivatePrefix(KerbalismSentinel __instance, ref bool __result)
		{
			if (__instance.vessel.orbit.referenceBody != Planetarium.fetch.Sun)
			{
				string msg = Localizer.Format("#autoLOC_6002295", SentinelUtilities.SentinelPartTitle);
				ScreenMessages.PostScreenMessage(msg, SentinelUtilities.CalculateReadDuration(msg), ScreenMessageStyle.UPPER_CENTER);
				__result = false;
			}
			__result = true;

			return false;
		}

		public override string GetModuleDisplayName()
		{
			return Localizer.Format("#autoLOC_6002283");
		}

		public override string GetInfo()
		{
			Specifics specs = new Specifics();
			specs.Add(Lib.Color(Local.Module_Experiment_Specifics_info8, Lib.Kolor.Cyan, true));//"Needs:"
			if (ec_rate > 0.0) specs.Add(Local.Module_Experiment_Specifics_info9, Lib.HumanReadableRate(ec_rate));//"EC"
			if (comms_rate > 0.0) specs.Add(Local.Module_Experiment_Specifics_info2, Lib.HumanReadableDataRate(comms_rate)); // "Data rate"

			return specs.Info();
		}
	}
}
