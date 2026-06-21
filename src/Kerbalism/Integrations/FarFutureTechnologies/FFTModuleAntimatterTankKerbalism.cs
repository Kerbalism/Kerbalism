using KSP.Localization;
using System.Collections.Generic;

namespace KERBALISM
{
	public class FFTModuleAntimatterTankKerbalism : PartModule, IKerbalismModule
	{
		public static string brokerName = "FFTAntimatterTank";
		public static string brokerTitle = Localizer.Format("#KERBALISM_Brokers_AntimatterTank");

		private const string ProtoEcDeficitKey = "AntimatterEcDeficitSeconds";

		[KSPField(isPersistant = true)]
		public float ThermalFluxToAddOnLoad = 0f;

		[KSPField(isPersistant = true)]
		public bool ContainmentEnabled = true;

		[KSPField(isPersistant = true)]
		public float ContainmentCost = 0f;

		[KSPField(isPersistant = true)]
		public string FuelName = "Antimatter";

		[KSPField(isPersistant = true)]
		public float DetonationKJPerUnit = 0f;

		[KSPField(isPersistant = true)]
		public float DetonationRate = 0f;

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
			if (ThermalFluxToAddOnLoad > 0)
			{
				IntegrationUtils.Log("Antimatter containment for tank " + part.partInfo.title + " on vessel " + vessel.GetDisplayName() + " was turned off due to EC loss. " + ThermalFluxToAddOnLoad.ToString() + " KW of heat was added to part as a resut of antimatter detonation.");
				part.AddThermalFlux(ThermalFluxToAddOnLoad);
				ThermalFluxToAddOnLoad = 0f;
			}
		}

		double GetResourceAmount(string resourceName)
		{
			PartResource resource = part.Resources.Get(resourceName);
			return resource != null ? resource.amount : 0.0;
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			if (GetResourceAmount(FuelName) > 0.0 && ContainmentEnabled && ContainmentCost > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -ContainmentCost));
			return brokerTitle;
		}

		public static string BackgroundUpdate(Vessel v, ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot, PartModule proto_part_module, Part proto_part, Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s)
		{
			var tank = proto_part_module as FFTModuleAntimatterTankKerbalism;
			if (tank == null)
				return brokerTitle;

			if (Lib.Proto.GetBool(module_snapshot, "ContainmentEnabled"))
			{
				float containmentCost = tank.ContainmentCost;
				if (containmentCost > 0f)
				{
					double ecNeed = containmentCost * elapsed_s;
					double ec = KERBALISM.ResourceCache.Get(v).GetResource(v, "ElectricCharge").Amount;
					resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -containmentCost));

					if (ec < ecNeed)
					{
						if (FFTSettings.AntimatterBackgroundDetonation)
						{
							double deficit = Lib.Proto.GetFloat(module_snapshot, ProtoEcDeficitKey) + elapsed_s;
							Lib.Proto.Set(module_snapshot, ProtoEcDeficitKey, (float)deficit);

							if (deficit >= FFTSettings.AntimatterDetonationGraceSeconds)
								DisableContainment(v, module_snapshot);
						}
					}
					else
					{
						Lib.Proto.Set(module_snapshot, ProtoEcDeficitKey, 0f);
					}
				}
			}
			else if (FFTSettings.AntimatterBackgroundDetonation)
			{
				SimulateDetonation(v, module_snapshot, tank, elapsed_s);
			}

			return brokerTitle;
		}

		static void DisableContainment(Vessel v, ProtoPartModuleSnapshot module_snapshot)
		{
			Lib.Proto.Set(module_snapshot, "ContainmentEnabled", false);
			Lib.Proto.Set(module_snapshot, ProtoEcDeficitKey, 0f);
			Message.Post(
				Severity.danger,
				Localizer.Format("#KERBALISM_FFT_antimatterDetonation", v.GetDisplayName()));
		}

		static void SimulateDetonation(Vessel v, ProtoPartModuleSnapshot module_snapshot, FFTModuleAntimatterTankKerbalism tank, double elapsed_s)
		{
			float detonationKjPerUnit = tank.DetonationKJPerUnit;
			float detonationRate = tank.DetonationRate;
			string fuelName = tank.FuelName;

			ResourceInfo antimatter = KERBALISM.ResourceCache.GetResource(v, fuelName);
			double detonatedAmount = elapsed_s * detonationRate;
			if (FFTSettings.AntimatterMaxDetonationPerStep > 0.0)
				detonatedAmount = System.Math.Min(detonatedAmount, FFTSettings.AntimatterMaxDetonationPerStep);

			if (antimatter.Amount < detonatedAmount)
				detonatedAmount = antimatter.Amount;

			if (detonatedAmount <= 0.0)
				return;

			antimatter.Consume(detonatedAmount, KERBALISM.ResourceBroker.GetOrCreate(brokerName, KERBALISM.ResourceBroker.BrokerCategory.VesselSystem, brokerTitle));
			float thermalFluxToAddOnLoad = Lib.Proto.GetFloat(module_snapshot, "ThermalFluxToAddOnLoad");
			thermalFluxToAddOnLoad += (float)detonatedAmount * detonationKjPerUnit;
			Lib.Proto.Set(module_snapshot, "ThermalFluxToAddOnLoad", thermalFluxToAddOnLoad);
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (ContainmentEnabled && ContainmentCost > 0f)
			{
				ResourceInfo ec = KERBALISM.ResourceCache.GetResource(vessel, "ElectricCharge");
				double chargeRequest = ContainmentCost * TimeWarp.fixedDeltaTime;
				if (ec.Amount >= chargeRequest)
					ec.Consume(chargeRequest, KERBALISM.ResourceBroker.GetOrCreate(brokerName, KERBALISM.ResourceBroker.BrokerCategory.VesselSystem, brokerTitle));
				else
					ContainmentEnabled = false;
			}
			return brokerTitle;
		}

		public void FixedUpdate()
		{
			if (ContainmentEnabled && ContainmentCost > 0f)
			{
				ResourceInfo ec = KERBALISM.ResourceCache.GetResource(vessel, "ElectricCharge");
				double chargeRequest = ContainmentCost * TimeWarp.fixedDeltaTime;
				if (ec.Amount < chargeRequest)
					ContainmentEnabled = false;
			}
		}
	}
}
