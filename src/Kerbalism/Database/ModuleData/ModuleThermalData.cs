using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class ModuleThermalData
	{
		private const string NODENAME_THERMAL = "THERMAL";
		private const float defaultInsulation = 0.75f;

		private PartResourceData belowThEnergy;
		private PartResourceData aboveThEnergy;
		private double energyPerKelvin;
		public enum FlowState { allowBoth, allowCooling, allowHeating, deny }
		public FlowState flowState = FlowState.allowBoth;
		public float insulation = defaultInsulation;
		public double Temperature => temperature;
		private double temperature;
		private string pawInfoLabel; // 310.0/295.0K, +0.078 kWth //

		private double internalFlux;

		public IThermalModule thermalModule;

		public void Setup(PartData partData, IThermalModule thermalModule)
		{
			SetupThermalModule(thermalModule);
			SetupResources(partData);
			SetupPAW(partData);
		}


		public void SetupThermalModule(IThermalModule thermalModule)
		{
			this.thermalModule = thermalModule;
			energyPerKelvin = thermalModule.ThermalMass * PartThermalData2.partSpecificHeat;
		}

		public void SetupResources(PartData partData)
		{
			foreach (PartResourceData prd in partData.virtualResources)
			{
				if (prd.ContainerId != thermalModule.ModuleId)
				{
					continue;
				}

				if (prd.ResourceName == PartThermalData2.belowThDef.name)
				{
					belowThEnergy = prd;
					continue;
				}

				if (prd.ResourceName == PartThermalData2.aboveThDef.name)
				{
					aboveThEnergy = prd;
					continue;
				}
			}

			if (aboveThEnergy == null || belowThEnergy == null)
			{
				double targetTempEnergy = energyPerKelvin * thermalModule.OperatingTemperature;
				double maxEnergy = targetTempEnergy * 2.0; // we simulate the temperature up to 3x the operating temperature
				belowThEnergy = partData.virtualResources.AddResource(PartThermalData2.belowThDef.name, targetTempEnergy, targetTempEnergy, thermalModule.ModuleId);
				aboveThEnergy = partData.virtualResources.AddResource(PartThermalData2.aboveThDef.name, 0.0, maxEnergy, thermalModule.ModuleId);
			}

			temperature = (belowThEnergy.Amount + aboveThEnergy.Amount) / energyPerKelvin;
		}

		public void SetupPAW(PartData partData)
		{
			if (partData.LoadedPart == null)
				return;

			FieldInfo internalFluxField = typeof(ModuleThermalData).GetField(nameof(internalFlux), BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo pawInfoLabelField = typeof(ModuleThermalData).GetField(nameof(pawInfoLabel), BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo insulationField = typeof(ModuleThermalData).GetField(nameof(insulation));
			FieldInfo flowStateField = typeof(ModuleThermalData).GetField(nameof(flowState));

			BasePAWGroup pawGroup = new BasePAWGroup(thermalModule.ModuleId, thermalModule.ModuleId, false);

			BaseField pawInfoBF = new BaseField(new UI_Label(), pawInfoLabelField, this);
			pawInfoBF.guiActiveEditor = true;
			pawInfoBF.guiName = "T°";
			pawInfoBF.group = pawGroup;
			partData.LoadedPart.Fields.Add(pawInfoBF);

			BaseField internalFluxBF = new BaseField(new UI_Label(), internalFluxField, this);
			internalFluxBF.guiActiveEditor = true;
			internalFluxBF.guiName = "Internal production";
			internalFluxBF.guiFormat = "F3";
			internalFluxBF.guiUnits = " kWth";
			internalFluxBF.group = pawGroup;
			partData.LoadedPart.Fields.Add(internalFluxBF);

			UI_FloatRange insulationFR = new UI_FloatRange();
			insulationFR.minValue = 0.1f;
			insulationFR.maxValue = 0.9f;
			insulationFR.stepIncrement = 0.025f;
			//insulationFR.onFieldChanged = (a, b) => PlannerUpdate();
			BaseField insulationBF = new BaseField(insulationFR, insulationField, this);
			insulationBF.uiControlEditor = insulationFR;
			insulationBF.guiActiveEditor = true;
			insulationBF.guiName = "Internal insulation";
			insulationBF.guiFormat = "P1";
			insulationBF.group = pawGroup;
			partData.LoadedPart.Fields.Add(insulationBF);

			UI_Cycle flowStateCycle = new UI_Cycle();
			flowStateCycle.stateNames = new string[]
			{
					Lib.Color("enabled", Lib.Kolor.Green),
					Lib.Color("cooling only", Lib.Kolor.Cyan),
					Lib.Color("heating only", Lib.Kolor.Orange),
					Lib.Color("disabled", Lib.Kolor.Yellow)
			};

			flowStateCycle.onFieldChanged = (a, b) => SetFlow();
			BaseField flowStateBF = new BaseField(flowStateCycle, flowStateField, this);
			flowStateBF.uiControlEditor = flowStateCycle;
			flowStateBF.uiControlFlight = flowStateCycle;
			flowStateBF.guiActiveEditor = true;
			flowStateBF.guiName = "Thermal control";
			flowStateBF.group = pawGroup;
			partData.LoadedPart.Fields.Add(flowStateBF);
		}

		public void ClearPAW(PartData partData)
		{
			if (partData.LoadedPart == null)
				return;

			List<BaseField> fields = Lib.ReflectionValue<List<BaseField>>(partData.LoadedPart.Fields, "_fields");
			for (int i = fields.Count - 1; i >= 0; i--)
			{
				FieldInfo fieldInfo = fields[i].FieldInfo;
			//	if (fieldInfo == internalFluxField || fieldInfo == pawInfoLabelField || fieldInfo == insulationField)
			//	{
			//		fields.RemoveAt(i);
			//	}
			}

			if (partData.LoadedPart.PartActionWindow != null)
			{
				partData.LoadedPart.PartActionWindow.CreatePartList(true);
			} 
		}

		private void SetFlow()
		{
			switch (flowState)
			{
				case FlowState.allowBoth:
					belowThEnergy.flowState = true;
					aboveThEnergy.flowState = true;
					break;
				case FlowState.allowCooling:
					belowThEnergy.flowState = false;
					aboveThEnergy.flowState = true;
					break;
				case FlowState.allowHeating:
					belowThEnergy.flowState = true;
					aboveThEnergy.flowState = false;
					break;
				case FlowState.deny:
					belowThEnergy.flowState = false;
					aboveThEnergy.flowState = false;
					break;
			}
		}

		public void ResetTemperature()
		{
			belowThEnergy.Amount = belowThEnergy.Capacity;
			aboveThEnergy.Amount = 0.0;
			temperature = (belowThEnergy.Amount + aboveThEnergy.Amount) / energyPerKelvin;
		}

		public double Update(double elapsedSec, double skinSurface, double skinTemperature)
		{
			temperature = (belowThEnergy.Amount + aboveThEnergy.Amount) / energyPerKelvin;
			internalFlux = thermalModule.HeatProduction;

			double tempDiff = temperature - skinTemperature;
			bool isNeg = false;
			if (tempDiff < 0.0)
			{
				tempDiff *= -1.0;
				isNeg = true;
			}
			double conduction = (1.0 - thermalModule.ThermalData.insulation) * 0.1;
			double internalToSkinFlux = Math.Pow((skinSurface * thermalModule.SurfaceFactor * tempDiff) + 1.0, conduction) - 1.0;
			if (isNeg) internalToSkinFlux *= -1.0;

			double flux = internalFlux - internalToSkinFlux;
			double energyChange = flux * elapsedSec;
			double belowThEnergyChange = Math.Min(belowThEnergy.Capacity - belowThEnergy.Amount, energyChange + aboveThEnergy.Amount);
			double aboveThEnergyChange = energyChange - belowThEnergyChange;
			belowThEnergy.Amount += belowThEnergyChange;
			aboveThEnergy.Amount += aboveThEnergyChange;
			

			// 310.0/295.0K +0.078kWth
			pawInfoLabel = Temperature.ToString("0.0") + "/" + thermalModule.OperatingTemperature.ToString("0.0K") + ", " + flux.ToString("0.000kWth");
			return internalToSkinFlux;
		}

		public void Save(ConfigNode moduleDataNode)
		{
			ConfigNode thermalNode = moduleDataNode.AddNode(NODENAME_THERMAL);
			thermalNode.AddValue("insulation", insulation);
			thermalNode.AddValue("flowState", flowState);
		}

		public static void Load(ModuleThermalData thermalData, ConfigNode moduleDataNode)
		{
			ConfigNode thermalNode = moduleDataNode.GetNode(NODENAME_THERMAL);
			if (thermalNode == null)
				return;

			if (thermalData == null)
				thermalData = new ModuleThermalData();

			thermalData.insulation = Lib.ConfigValue(thermalNode, "insulation", defaultInsulation);
			thermalData.flowState = Lib.ConfigEnum(thermalNode, "flowState", FlowState.allowBoth);
		}
	}
}
