using KSP.Localization;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// Kerbalism resource routing for FFT antimatter tanks. Native ModuleAntimatterTank is kept
	/// on the part for UI/state; this updater owns EC accounting and background containment logic.
	/// </summary>
	public class FFTAntimatterTankKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "FFTAntimatterTank";
		public static string brokerTitle = Localizer.Format("#KERBALISM_Brokers_AntimatterTank");

		private const string NativeModuleName = "ModuleAntimatterTank";
		private const string ProtoEcDeficitKey = "AntimatterEcDeficitSeconds";

		[KSPField(isPersistant = true)]
		public float ThermalFluxToAddOnLoad = 0f;

		[KSPField(isPersistant = true)]
		public float ecDeficitSeconds = 0f;

		private PartModule nativeTank;
		private bool nativeResolved;

		private PartModule NativeTank
		{
			get
			{
				if (!nativeResolved)
				{
					nativeResolved = true;
					nativeTank = FindNativeTank(part);
				}
				return nativeTank;
			}
		}

		public override void OnAwake()
		{
			base.OnAwake();
			if (Lib.IsFlight())
				GameEvents.onPartUnpack.Add(new EventData<Part>.OnEvent(GoOffRails));
		}

		void OnDestroy()
		{
			GameEvents.onPartUnpack.Remove(GoOffRails);
		}

		public virtual void GoOffRails(Part p)
		{
			if (ThermalFluxToAddOnLoad <= 0f)
				return;

			PartModule tank = NativeTank;
			if (tank != null && GetContainmentEnabled(tank) && !FarFutureTechnologies.Get(tank, "DetonationOccuring", false))
			{
				ThermalFluxToAddOnLoad = 0f;
				return;
			}

			IntegrationUtils.Log("Antimatter containment for tank " + part.partInfo.title + " on vessel " + vessel.GetDisplayName() + " was turned off due to EC loss. " + ThermalFluxToAddOnLoad.ToString() + " KW of heat was added to part as a resut of antimatter detonation.");
			part.AddThermalFlux(ThermalFluxToAddOnLoad);
			ThermalFluxToAddOnLoad = 0f;
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			PartModule tank = NativeTank;
			if (tank == null)
				return brokerTitle;

			if (GetResourceAmount(tank) > 0.0 && GetContainmentEnabled(tank) && GetContainmentCost(tank) > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -GetContainmentCost(tank)));
			return brokerTitle;
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			PartModule tank = NativeTank;
			if (tank == null)
				return brokerTitle;

			float containmentCost = GetContainmentCost(tank);
			if (containmentCost <= 0f)
				return brokerTitle;

			string fuelName = GetFuelName(tank);
			bool hasAntimatter = GetResourceAmount(tank) > 0.0;
			bool containmentEnabled = GetContainmentEnabled(tank);
			ResourceInfo ec = KERBALISM.ResourceCache.GetResource(vessel, "ElectricCharge");
			ResourceBroker broker = KERBALISM.ResourceBroker.GetOrCreate(brokerName, KERBALISM.ResourceBroker.BrokerCategory.VesselSystem, brokerTitle);
			double elapsed_s = TimeWarp.fixedDeltaTime;

			if (!containmentEnabled && hasAntimatter && ec.Amount >= containmentCost)
			{
				FarFutureTechnologies.Set(tank, "ContainmentEnabled", true);
				FarFutureTechnologies.Set(tank, "DetonationOccuring", false);
				ecDeficitSeconds = 0f;
				ThermalFluxToAddOnLoad = 0f;
				containmentEnabled = true;
			}

			if (containmentEnabled && hasAntimatter)
			{
				bool powered = ec.Amount >= containmentCost;
				ec.Consume(containmentCost * elapsed_s, broker);
				SyncNativePoweredState(tank, powered);

				if (!powered)
				{
					if (FFTSettings.AntimatterBackgroundDetonation)
					{
						ecDeficitSeconds += (float)elapsed_s;
						if (ecDeficitSeconds >= FFTSettings.AntimatterDetonationGraceSeconds)
							DisableContainmentLoaded(tank);
					}
				}
				else
				{
					ecDeficitSeconds = 0f;
				}
			}
			else if (!containmentEnabled && hasAntimatter && FFTSettings.AntimatterBackgroundDetonation)
			{
				SimulateDetonationLoaded(tank, fuelName, elapsed_s, broker);
			}

			return brokerTitle;
		}

		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);
			if (!Lib.IsFlight())
				return;

			PartModule tank = NativeTank;
			if (tank == null)
				return;

			float containmentCost = GetContainmentCost(tank);
			if (!GetContainmentEnabled(tank) || containmentCost <= 0f)
				return;

			ResourceInfo ec = KERBALISM.ResourceCache.GetResource(vessel, "ElectricCharge");
			SyncNativePoweredState(tank, ec.Amount >= containmentCost);
		}

		void DisableContainmentLoaded(PartModule tank)
		{
			FarFutureTechnologies.Set(tank, "ContainmentEnabled", false);
			ecDeficitSeconds = 0f;
			SyncNativePoweredState(tank, false);
			Message.Post(
				Severity.danger,
				Localizer.Format("#KERBALISM_FFT_antimatterDetonation", vessel.GetDisplayName()));
		}

		void SimulateDetonationLoaded(PartModule tank, string fuelName, double elapsed_s, ResourceBroker broker)
		{
			float detonationKjPerUnit = GetDetonationKjPerUnit(tank);
			float detonationRate = GetDetonationRate(tank);
			ResourceInfo antimatter = KERBALISM.ResourceCache.GetResource(vessel, fuelName);
			double detonatedAmount = elapsed_s * detonationRate;

			if (FFTSettings.AntimatterMaxDetonationPerStep > 0.0)
				detonatedAmount = System.Math.Min(detonatedAmount, FFTSettings.AntimatterMaxDetonationPerStep);

			if (antimatter.Amount < detonatedAmount)
				detonatedAmount = antimatter.Amount;

			if (detonatedAmount <= 0.0)
				return;

			antimatter.Consume(detonatedAmount, broker);
			part.AddThermalFlux((float)(detonatedAmount * detonationKjPerUnit));
			ecDeficitSeconds = 0f;
		}

		private static void SyncNativePoweredState(PartModule tank, bool powered)
		{
			FarFutureTechnologies.SetPoweredState(tank, powered);
			if (powered && FarFutureTechnologies.Get(tank, "ContainmentEnabled", true))
				FarFutureTechnologies.Set(tank, "DetonationOccuring", false);
		}

		public static string BackgroundUpdate(Vessel v, ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot, PartModule proto_part_module, Part proto_part, Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s)
		{
			ProtoPartModuleSnapshot tankSnapshot = IntegrationUtils.FindPartModuleSnapshot(part_snapshot, NativeModuleName);
			PartModule tankPrefab = FindNativeTank(proto_part);
			if (tankSnapshot == null || tankPrefab == null)
				return brokerTitle;

			float containmentCost = GetContainmentCost(tankPrefab);
			if (containmentCost <= 0f)
				return brokerTitle;

			string fuelName = GetFuelName(tankPrefab);
			ResourceInfo ec = KERBALISM.ResourceCache.Get(v).GetResource(v, "ElectricCharge");
			ResourceInfo antimatter = KERBALISM.ResourceCache.GetResource(v, fuelName);
			bool hasAntimatter = antimatter.Amount > 0.0;
			bool containmentEnabled = Lib.Proto.GetBool(tankSnapshot, "ContainmentEnabled");

			if (!containmentEnabled && hasAntimatter && ec.Amount >= containmentCost)
			{
				Lib.Proto.Set(tankSnapshot, "ContainmentEnabled", true);
				Lib.Proto.Set(tankSnapshot, "DetonationOccuring", false);
				Lib.Proto.Set(module_snapshot, ProtoEcDeficitKey, 0f);
				Lib.Proto.Set(module_snapshot, "ThermalFluxToAddOnLoad", 0f);
				containmentEnabled = true;
			}

			if (containmentEnabled && hasAntimatter)
			{
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -containmentCost));

				// Fail only when EC cannot pay ~1s of containment, not the full accumulated interval.
				if (ec.Amount < containmentCost)
				{
					if (FFTSettings.AntimatterBackgroundDetonation)
					{
						double deficit = Lib.Proto.GetFloat(module_snapshot, ProtoEcDeficitKey) + elapsed_s;
						Lib.Proto.Set(module_snapshot, ProtoEcDeficitKey, (float)deficit);

						if (deficit >= FFTSettings.AntimatterDetonationGraceSeconds)
							DisableContainment(v, tankSnapshot, module_snapshot);
					}
				}
				else
				{
					Lib.Proto.Set(module_snapshot, ProtoEcDeficitKey, 0f);
				}
			}
			else if (!containmentEnabled && hasAntimatter && FFTSettings.AntimatterBackgroundDetonation)
			{
				SimulateDetonation(v, module_snapshot, tankPrefab, fuelName, elapsed_s);
			}

			return brokerTitle;
		}

		static void DisableContainment(Vessel v, ProtoPartModuleSnapshot tankSnapshot, ProtoPartModuleSnapshot updaterSnapshot)
		{
			Lib.Proto.Set(tankSnapshot, "ContainmentEnabled", false);
			if (updaterSnapshot != null)
				Lib.Proto.Set(updaterSnapshot, ProtoEcDeficitKey, 0f);
			Message.Post(
				Severity.danger,
				Localizer.Format("#KERBALISM_FFT_antimatterDetonation", v.GetDisplayName()));
		}

		static void SimulateDetonation(Vessel v, ProtoPartModuleSnapshot updaterSnapshot, PartModule tankPrefab, string fuelName, double elapsed_s)
		{
			float detonationKjPerUnit = GetDetonationKjPerUnit(tankPrefab);
			float detonationRate = GetDetonationRate(tankPrefab);

			ResourceInfo antimatter = KERBALISM.ResourceCache.GetResource(v, fuelName);
			double detonatedAmount = elapsed_s * detonationRate;
			if (FFTSettings.AntimatterMaxDetonationPerStep > 0.0)
				detonatedAmount = System.Math.Min(detonatedAmount, FFTSettings.AntimatterMaxDetonationPerStep);

			if (antimatter.Amount < detonatedAmount)
				detonatedAmount = antimatter.Amount;

			if (detonatedAmount <= 0.0)
				return;

			antimatter.Consume(detonatedAmount, KERBALISM.ResourceBroker.GetOrCreate(brokerName, KERBALISM.ResourceBroker.BrokerCategory.VesselSystem, brokerTitle));
			float thermalFluxToAddOnLoad = Lib.Proto.GetFloat(updaterSnapshot, "ThermalFluxToAddOnLoad");
			thermalFluxToAddOnLoad += (float)detonatedAmount * detonationKjPerUnit;
			Lib.Proto.Set(updaterSnapshot, "ThermalFluxToAddOnLoad", thermalFluxToAddOnLoad);
			Lib.Proto.Set(updaterSnapshot, ProtoEcDeficitKey, 0f);
		}

		internal static PartModule FindNativeTank(Part part)
		{
			if (part == null)
				return null;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (FarFutureTechnologies.IsAntimatterTank(module) || module.moduleName == NativeModuleName)
					return module;
			}

			return null;
		}

		static double GetResourceAmount(PartModule tank)
		{
			string fuelName = GetFuelName(tank);
			PartResource resource = tank.part.Resources.Get(fuelName);
			return resource != null ? resource.amount : 0.0;
		}

		static float GetContainmentCost(PartModule tank) => FarFutureTechnologies.Get(tank, "ContainmentCost", 0f);

		static bool GetContainmentEnabled(PartModule tank) => FarFutureTechnologies.Get(tank, "ContainmentEnabled", true);

		static string GetFuelName(PartModule tank) => FarFutureTechnologies.Get(tank, "FuelName", "Antimatter");

		static float GetDetonationRate(PartModule tank) => FarFutureTechnologies.Get(tank, "DetonationRate", 0f);

		static float GetDetonationKjPerUnit(PartModule tank) => FarFutureTechnologies.Get(tank, "DetonationKJPerUnit", 0f);

		internal static bool HasUpdater(Part part) => part != null && part.FindModuleImplementing<FFTAntimatterTankKerbalismUpdater>() != null;
	}
}
