using System.Collections;
using System.Collections.Generic;
using KERBALISM;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
	internal static class FusionReactorResourceSim
	{
		// Must match FarFutureTechnologies.ChargeState { Ready, Charging, Running }.
		private const int ChargeStateReady = 0;
		private const int ChargeStateCharging = 1;
		private const int ChargeStateRunning = 2;

		private static bool CanSyncFusionUi(PartModule reactor)
		{
			if (reactor == null || !FarFutureTechnologies.IsFusionEngine(reactor))
				return true;

			object engines = IntegrationReflection.GetField<object>(reactor, "engines");
			return engines != null;
		}

		internal static bool IsFusionUiReady(PartModule reactor) => CanSyncFusionUi(reactor);

		private static IList GetModes(PartModule reactor)
		{
			return IntegrationReflection.GetField<IList>(reactor, "modes");
		}

		private static FusionModeData GetMode(PartModule reactor, int modeIndex, List<FusionModeData> parsedModes)
		{
			if (parsedModes != null && modeIndex >= 0 && modeIndex < parsedModes.Count)
				return parsedModes[modeIndex];

			IList modes = GetModes(reactor);
			if (modes == null || modeIndex < 0 || modeIndex >= modes.Count)
				return null;

			object mode = modes[modeIndex];
			if (mode == null)
				return null;

			var data = new FusionModeData(null);
			data.powerGeneration = IntegrationReflection.GetFloat(mode, "powerGeneration");
			object inputs = IntegrationReflection.GetField<object>(mode, "inputs");
			if (inputs is IList inputList)
			{
				for (int i = 0; i < inputList.Count; i++)
				{
					if (inputList[i] is ResourceRatio ratio)
						data.inputs.Add(ratio);
				}
			}
			return data;
		}

		private static float GetThrottle(PartModule reactor)
		{
			return IntegrationReflection.GetFloat(reactor, "reactorThrottle", 1f);
		}

		private static int GetChargeState(PartModule reactor)
		{
			object value = IntegrationReflection.GetField<object>(reactor, "chargeState");
			return value != null ? (int)value : ChargeStateCharging;
		}

		private static void SetChargeStateUI(PartModule reactor, int newState)
		{
			if (reactor == null || !CanSyncFusionUi(reactor) || GetChargeState(reactor) == newState)
				return;

			FarFutureTechnologies.SetChargeStateUI(reactor, newState);
		}

		internal static void SyncLoadedChargeUI(PartModule reactor, bool powerDelivered)
		{
			if (reactor == null || !CanSyncFusionUi(reactor))
				return;

			if (FarFutureTechnologies.Get(reactor, "Enabled", false))
			{
				if (GetChargeState(reactor) != ChargeStateRunning)
					SetChargeStateUI(reactor, ChargeStateRunning);
				return;
			}

			bool charging = FarFutureTechnologies.Get(reactor, "Charging", false);
			bool charged = FarFutureTechnologies.Get(reactor, "Charged", false);
			float currentCharge = FarFutureTechnologies.Get(reactor, "CurrentCharge", 0f);
			float chargeGoal = FarFutureTechnologies.Get(reactor, "ChargeGoal", 0f);

			if (charging && !charged)
			{
				if (currentCharge >= chargeGoal)
				{
					FarFutureTechnologies.Set(reactor, "CurrentCharge", chargeGoal);
					FarFutureTechnologies.Set(reactor, "Charged", true);
					FarFutureTechnologies.Set(reactor, "ChargeStatus", Localizer.Format("#LOC_FFT_ModuleFusionReactor_Field_ChargeStatus_Ready"));
					SetChargeStateUI(reactor, ChargeStateReady);
				}
				else if (powerDelivered)
				{
					FarFutureTechnologies.Set(reactor, "ChargeStatus", Localizer.Format(
						"#LOC_FFT_ModuleFusionReactor_Field_ChargeStatus_Normal",
						(currentCharge / chargeGoal * 100.0f).ToString("F1")));
					SetChargeStateUI(reactor, ChargeStateCharging);
				}
				else
				{
					FarFutureTechnologies.Set(reactor, "ChargeStatus", Localizer.Format("#LOC_FFT_ModuleFusionReactor_Field_ChargeStatus_NoPower"));
					SetChargeStateUI(reactor, ChargeStateCharging);
				}
				return;
			}

			if (!charging && currentCharge <= 0f)
				FarFutureTechnologies.Set(reactor, "ChargeStatus", Localizer.Format("#LOC_FFT_ModuleFusionReactor_Field_ChargeStatus_NotCharging"));
			else if (!FarFutureTechnologies.Get(reactor, "Enabled", false) && currentCharge >= chargeGoal)
			{
				FarFutureTechnologies.Set(reactor, "Charged", true);
				FarFutureTechnologies.Set(reactor, "ChargeStatus", Localizer.Format("#LOC_FFT_ModuleFusionReactor_Field_ChargeStatus_Ready"));
				SetChargeStateUI(reactor, ChargeStateReady);
			}
		}

		internal static void SetLoadedCharge(PartModule reactor, float charge)
		{
			if (reactor != null)
				FarFutureTechnologies.Set(reactor, "CurrentCharge", charge);
		}

		internal static void SetProtoCharge(ProtoPartModuleSnapshot reactor, float charge)
		{
			Lib.Proto.Set(reactor, "CurrentCharge", charge);
		}

		internal static void UpdateLoadedThrottle(PartModule reactor)
		{
			if (reactor == null || !FarFutureTechnologies.Get(reactor, "Enabled", false))
				return;

			IList modes = GetModes(reactor);
			int modeIndex = FarFutureTechnologies.Get(reactor, "currentModeIndex", 0);
			if (modes == null || modes.Count == 0 || modeIndex < 0 || modeIndex >= modes.Count)
				return;

			float powerGeneration = IntegrationReflection.GetFloat(modes[modeIndex], "powerGeneration");
			reactor.part.GetConnectedResourceTotals(PartResourceLibrary.ElectricityHashcode, out double shipEC, out double shipMaxEC, true);
			float requestedFramePower = (float)(shipMaxEC - shipEC);
			float minPower = powerGeneration * TimeWarp.fixedDeltaTime * FarFutureTechnologies.Get(reactor, "MinimumReactorPower", 0.1f);
			float clampedFramePower = Mathf.Clamp(requestedFramePower, minPower, powerGeneration * TimeWarp.fixedDeltaTime);

			float requestedReactorThrottle = clampedFramePower / (powerGeneration * TimeWarp.fixedDeltaTime);
			float currentThrottle = GetThrottle(reactor);
			currentThrottle = Mathf.MoveTowards(currentThrottle, requestedReactorThrottle, 0.1f);
			IntegrationReflection.SetField(reactor, "reactorThrottle", currentThrottle);
			SyncProtoFusionStatus(reactor, currentThrottle);
		}

		internal static bool HasChargeOperatingPower(PartModule reactor, Vessel v)
		{
			if (reactor == null || v == null)
				return false;

			float chargeRate = FarFutureTechnologies.Get(reactor, "ChargeRate", 0f);
			if (chargeRate <= 0f)
				return true;

			return KERBALISM.ResourceCache.GetResource(v, "ElectricCharge").Amount >= chargeRate;
		}

		internal static bool UpdateLoadedCharge(PartModule reactor, Vessel v, string brokerName, string brokerTitle)
		{
			if (reactor == null || FarFutureTechnologies.Get(reactor, "Enabled", false) || !FarFutureTechnologies.Get(reactor, "Charging", false)
				|| FarFutureTechnologies.Get(reactor, "Charged", false) || FarFutureTechnologies.Get(reactor, "ChargeRate", 0f) <= 0f)
				return false;

			float chargeRate = FarFutureTechnologies.Get(reactor, "ChargeRate", 0f);
			ResourceInfo ec = KERBALISM.ResourceCache.GetResource(v, "ElectricCharge");
			if (ec.Amount < chargeRate)
			{
				SyncLoadedChargeUI(reactor, false);
				return true;
			}

			double chargeRequest = chargeRate * TimeWarp.fixedDeltaTime;
			ec.Consume(chargeRequest, KERBALISM.ResourceBroker.GetOrCreate(brokerName, KERBALISM.ResourceBroker.BrokerCategory.Converter, brokerTitle));

			float chargeGoal = FarFutureTechnologies.Get(reactor, "ChargeGoal", 0f);
			float currentCharge = FarFutureTechnologies.Get(reactor, "CurrentCharge", 0f);
			float gained = Mathf.Min((float)chargeRequest, chargeGoal - currentCharge);
			FarFutureTechnologies.Set(reactor, "CurrentCharge", currentCharge + gained);
			if (FarFutureTechnologies.Get(reactor, "CurrentCharge", 0f) >= chargeGoal)
			{
				FarFutureTechnologies.Set(reactor, "CurrentCharge", chargeGoal);
				FarFutureTechnologies.Set(reactor, "Charged", true);
			}

			SyncLoadedChargeUI(reactor, true);
			return true;
		}

		internal static string AddPlannerRates(
			PartModule reactor,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			string brokerTitle,
			float maxEcGeneration,
			int modeIndex,
			List<FusionModeData> modes)
		{
			if (reactor == null)
				return brokerTitle;

			if (!FarFutureTechnologies.Get(reactor, "Enabled", false) && FarFutureTechnologies.Get(reactor, "Charging", false)
				&& !FarFutureTechnologies.Get(reactor, "Charged", false) && FarFutureTechnologies.Get(reactor, "ChargeRate", 0f) > 0f)
			{
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -FarFutureTechnologies.Get(reactor, "ChargeRate", 0f)));
				return brokerTitle;
			}

			if (maxEcGeneration > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", maxEcGeneration));

			FusionModeData mode = GetMode(reactor, modeIndex, modes);
			if (mode?.inputs != null)
			{
				foreach (ResourceRatio ratio in mode.inputs)
					resourceChangeRequest.Add(new KeyValuePair<string, double>(ratio.ResourceName, -ratio.Ratio));
			}

			return brokerTitle;
		}

		internal static string AddLoadedRates(PartModule reactor, List<KeyValuePair<string, double>> resourceChangeRequest, string brokerTitle)
		{
			if (reactor == null || !FarFutureTechnologies.Get(reactor, "Enabled", false))
				return brokerTitle;

			int modeIndex = FarFutureTechnologies.Get(reactor, "currentModeIndex", 0);
			FusionModeData mode = GetMode(reactor, modeIndex, null);
			if (mode == null)
				return brokerTitle;

			float throttle = GetThrottle(reactor);
			float power = mode.powerGeneration * throttle;
			if (power > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", power));

			foreach (ResourceRatio input in mode.inputs)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio * throttle));

			SyncProtoFusionStatus(reactor, throttle);
			return brokerTitle;
		}

		internal static void AddBackgroundRates(
			Vessel v,
			ProtoPartSnapshot partSnapshot,
			ProtoPartModuleSnapshot reactor,
			Part protoPart,
			List<FusionModeData> modes,
			int fallbackModeIndex,
			float fallbackMaxEcGeneration,
			string brokerName,
			string brokerTitle,
			double elapsed_s)
		{
			if (v == null || partSnapshot == null || reactor == null || !Lib.Proto.GetBool(reactor, "Enabled"))
				return;

			int modeIndex = Lib.Proto.GetInt(reactor, "currentModeIndex", fallbackModeIndex);
			FusionModeData mode = GetProtoMode(protoPart, reactor, modeIndex, modes);
			float throttle = Mathf.Clamp01(Lib.Proto.GetFloat(reactor, "reactorThrottle", 1f));
			float powerGeneration = mode != null ? mode.powerGeneration : fallbackMaxEcGeneration;
			if (throttle <= 0f && powerGeneration <= 0f)
				return;

			VesselResources resources = KERBALISM.ResourceCache.Get(v);
			bool needToStopReactor = false;
			ResourceRecipe recipe = new ResourceRecipe(KERBALISM.ResourceBroker.GetOrCreate(
				brokerName,
				KERBALISM.ResourceBroker.BrokerCategory.Converter,
				brokerTitle));

			if (mode?.inputs != null)
			{
				foreach (ResourceRatio input in mode.inputs)
				{
					recipe.AddInput(input.ResourceName, input.Ratio * throttle * elapsed_s);
					if (resources.GetResource(v, input.ResourceName).Amount < double.Epsilon)
						needToStopReactor = true;
				}
			}

			float ecGeneration = powerGeneration * throttle;
			if (ecGeneration > 0f)
				recipe.AddOutput("ElectricCharge", ecGeneration * elapsed_s, dump: true);

			resources.AddRecipe(recipe);

			if (needToStopReactor)
			{
				Lib.Proto.Set(reactor, "Enabled", false);
				SetProtoCharge(reactor, 0f);
				Lib.Proto.Set(reactor, "Charged", false);
			}
		}

		private static FusionModeData GetProtoMode(Part protoPart, ProtoPartModuleSnapshot reactor, int modeIndex, List<FusionModeData> parsedModes)
		{
			if (parsedModes != null && modeIndex >= 0 && modeIndex < parsedModes.Count)
				return parsedModes[modeIndex];

			PartModule prefab = FindFusionPrefab(protoPart, reactor);
			return prefab != null ? GetMode(prefab, modeIndex, null) : null;
		}

		private static PartModule FindFusionPrefab(Part protoPart, ProtoPartModuleSnapshot reactor)
		{
			string moduleId = Lib.Proto.GetString(reactor, "ModuleID");
			if (reactor.moduleName == "ModuleFusionEngine" || reactor.moduleName == "FusionEngine")
				return FarFutureTechnologies.FindFusionEngine(protoPart, moduleId);
			return FarFutureTechnologies.FindFusionReactor(protoPart, moduleId);
		}

		private static void SyncProtoFusionStatus(PartModule reactor, float throttle)
		{
			ProtoPartSnapshot protoPart = reactor.part?.protoPartSnapshot;
			if (protoPart == null)
				return;

			string moduleId = FarFutureTechnologies.Get(reactor, "ModuleID", "");
			ProtoPartModuleSnapshot fallback = null;
			foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
			{
				if (protoModule.moduleName != reactor.moduleName)
					continue;

				if (fallback == null)
					fallback = protoModule;

				string protoModuleId = Lib.Proto.GetString(protoModule, "ModuleID");
				if (!string.IsNullOrEmpty(moduleId) && protoModuleId != moduleId)
					continue;

				SyncProtoFusionStatus(protoModule, reactor, throttle);
				return;
			}

			if (fallback != null)
				SyncProtoFusionStatus(fallback, reactor, throttle);
		}

		private static void SyncProtoFusionStatus(ProtoPartModuleSnapshot protoModule, PartModule reactor, float throttle)
		{
			Lib.Proto.Set(protoModule, "Enabled", FarFutureTechnologies.Get(reactor, "Enabled", false));
			Lib.Proto.Set(protoModule, "reactorThrottle", throttle);
			Lib.Proto.Set(protoModule, "currentModeIndex", FarFutureTechnologies.Get(reactor, "currentModeIndex", 0));
			Lib.Proto.Set(protoModule, "CurrentCharge", FarFutureTechnologies.Get(reactor, "CurrentCharge", Lib.Proto.GetFloat(protoModule, "CurrentCharge")));
			Lib.Proto.Set(protoModule, "Charged", FarFutureTechnologies.Get(reactor, "Charged", Lib.Proto.GetBool(protoModule, "Charged")));
			Lib.Proto.Set(protoModule, "Charging", FarFutureTechnologies.Get(reactor, "Charging", Lib.Proto.GetBool(protoModule, "Charging")));
		}

		internal static void ValidateLoadedReactor(PartModule reactor, Vessel v)
		{
			if (reactor == null || !FarFutureTechnologies.Get(reactor, "Enabled", false) || v == null)
				return;

			int modeIndex = FarFutureTechnologies.Get(reactor, "currentModeIndex", 0);
			FusionModeData mode = GetMode(reactor, modeIndex, null);
			if (mode?.inputs == null)
				return;

			VesselResources resources = KERBALISM.ResourceCache.Get(v);
			foreach (ResourceRatio input in mode.inputs)
			{
				if (resources.GetResource(v, input.ResourceName).Amount < double.Epsilon)
				{
					StopLoadedReactorForFuel(reactor);
					return;
				}
			}
		}

		internal static void StopLoadedReactorForFuel(PartModule reactor)
		{
			if (reactor == null || !FarFutureTechnologies.Get(reactor, "Enabled", false))
				return;

			ScreenMessages.PostScreenMessage(new ScreenMessage(
				Localizer.Format("#LOC_FFT_ModuleFusionReactor_Message_OutOfFuel", reactor.part.partInfo.title),
				10.0f,
				ScreenMessageStyle.UPPER_CENTER));
			FarFutureTechnologies.ReactorDeactivated(reactor);
			SyncLoadedChargeUI(reactor, false);
		}

		internal static void BackgroundCharge(
			Vessel v,
			ProtoPartModuleSnapshot reactor,
			Part prefab,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsed_s)
		{
			if (Lib.Proto.GetBool(reactor, "Enabled"))
				return;
			if (!Lib.Proto.GetBool(reactor, "Charging") || Lib.Proto.GetBool(reactor, "Charged"))
				return;

			float chargeRate = Lib.Proto.GetFloat(reactor, "ChargeRate");
			if (chargeRate <= 0f)
				return;

			resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -chargeRate));

			double ec = KERBALISM.ResourceCache.Get(v).GetResource(v, "ElectricCharge").Amount;
			if (ec < chargeRate)
				return;

			float chargeGoal = GetChargeGoal(prefab);
			float currentCharge = Lib.Proto.GetFloat(reactor, "CurrentCharge");
			currentCharge += chargeRate * (float)elapsed_s;
			if (currentCharge >= chargeGoal)
			{
				SetProtoCharge(reactor, chargeGoal);
				Lib.Proto.Set(reactor, "Charged", true);
			}
			else
			{
				SetProtoCharge(reactor, currentCharge);
			}
		}

		private static float GetChargeGoal(Part prefab)
		{
			PartModule module = FarFutureTechnologies.FindFusionReactor(prefab, "");
			return module != null ? FarFutureTechnologies.Get(module, "ChargeGoal", 500000f) : 500000f;
		}
	}
}
