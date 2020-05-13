using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
Design goals :
- Temperature is tracked per part, but only on parts that have a "thermal enabled" module
- Thermal enabled modules :
	- Can emit "internal heat" : kerbal bodies, exothermic processes...
	- Can define an "operating temperature" that the thermal system will try to match
	- Can use the current temperature to do various things (killing kerbals, changing process output...)
- A part exchange heat with its environment :
  - In/out radiation, according to :
    - Stars/albedo/emmissive/background irradiance
	- The part emissivity / absorbtivity
	- The part surface temperature
  - Convection while in atmosphere
	- Simplified atmo temperature model on unloaded vessels and on loaded landed vessels
	- Blending in the stock part temperature on loaded flying vessels
  - No part <> part conduction, but excessive module-emitted heat is redistributed to
    parts that require a temperature increase.
- Active radiators remove excess heat vessel-wide
- A heater process produce heat by consuming EC if needed (should it be controllable ?)
- Thermal enabled parts "passive stats" can be editor tweaked :
	- Ideally trough a single slider (internal <> surface conduction ?)
	- Or maybe better, through selection between a few "thermal profile presets" with
	  different internal <> surface conduction, emissivity and absorbtivity values.

Implementation :
- The step sim compute a raw irradiance for the four "surface sections", each section surface coeficient and each section "average direction" vector :
	- section coeficient is derived from the sun/vessel/body angle, nothing fancy.
	- section 1 : exposed to the sun + main body
	- section 2 : exposed to the sun
	- section 3 : exposed to the main body
	- section 4 : exposed only to the void
	- Accuracy note : at high timewarp speeds, what to do with substeps ?
		- We fall into the same problem as with solar panels : sun/shadow min/max are taken away, making timewarp a bit "cheaty".
		  For solar panels this isn't too much of an issue, but for thermals the effect will likely be much more visible.
		- Computing the per part thermal stats for each substeps is likely out of reach from a performance standpoint if done
		  from the main thread.
		- Anyway, since the part temperature is dependant on the resource sim and is used as an input, we can't
		  work on a faster loop that the main vesseldata update loop, so I don't see any way to use a "between FUs" threaded pattern.
- On the VesselData update, for each thermal-enabled part :
	- Each section surface temperature is approximated by mixing together the following temperatures :
		- The part current internal temperature, scaled down by an internal <> surface thermal transfer coeficient. 
		  This could be made PAW-tweakable to introduce some level of "passive thermal design" at editor time.
		- The blackbody equilibrium temperature for the section current irradiance,
		  scaled down by the craft current angular velocity
		- External temperature while in atmosphere
		- The "stock" part skin temperature : only when loaded and flying through an atmosphere
		- Doing this is a **very** rough approximation, but any "actual physics" solution would involve rate-of-change
		  math is much more complex, involve approximatiosn anyway and would have issues at high timewarp speeds.
	- The in/out radiative flux for each surface section is computed by the blackbody equation according to :
		- the section surface (part surface * section coeficient)
		- the part emissivity / absorbtivity (again, should we allow customization ?)
		- the section surface temperature we just computed
	- The environment thermal flux is saved into PartData and computed by doing the sum of :
		- radiativeIn - radiativeOut
		- Atmospheric convective transfer (we should probably leverage the stock methods for that)
	- Each thermal enabled part hold a storage capacity for 2 virtual resources :
		- "BelowOperatingTempThermalEnergy" :
			- Capacity is "operating temp * part structural mass * a game-wide-constant specific heat value"
			- Resources mass will probably be ignored, altough it's probably not too hard to factor them in
			- Maybe some factor could be defined at the thermal enabled module level to allow some configuration, TBD.
		- "AboveOperatingTempThermalEnergy" :
			- Capacity is limited by the stock "part max internal temp" value
		- Those two resources are the thermal energy (joules) stored in the part, and is what determine the part temperature.
		- This allow the thermal system to leverage the resource sim. There are two resources because that allow us to
		  define freely the "target operating temperature" per part, by adjusting the "below" resource capacity, and to have
		  radiators/heaters automatically adjust and distribute their output : radiators only consume the "above" resource,
		  heaters only produce the "below" resource. The thermal target for every part (no matter the the actual target temperature)
		  is reached when "below" is full, and "above" is empty.
		- It will require a refactor of the virtual resources handling, so the behavior become similar to the "real" resources,
		  especially in regard to the handler <> individual parts amounts/capcities relation. Currently that synchronization is
		  only one way from the handler -> parts both for reads and writes, we need to make :
		  - read : part -> handler
		  - write : handler -> part
		  There is some issues with the current system anyway, this should handle them (and might prove useful for the "self consuming processes" too)
	- According to the environment thermal flux, and internal heat produced by the part modules implementing the "IThermalModule" interface,
	  change the two virtual resource amounts in the part (there is a bit of logic involved to properly handle the below/above limit, nothing too
	  complicated)
- Radiators :
	- Operate in a similar way as solar panels in regard to local occlusion, pivot, etc
	- Have a tweakable "coolant loop" temperature that determine their effectiveness
	- Use the 4 sections "average direction" vectors to determine the radiator surface temperature
	- Translate the resulting negative flux in a "AboveOperatingTempThermalEnergy" consumption
- Heaters :
	- Are a normal process that consume EC to produce "BelowOperatingTempThermalEnergy"
	- Three options :
		- Make it a vessel-wide process with no capacity limit.
			- Advantage : no configs required, no extra stuff to take care for the player
			- Disadvantage : player has no control on it
		- Make it a process controller :
			- Advantage : full control for the player
			- Diadvantage : per part configs are required, more micromanagement for the player
		- Both :
			- Have a vessel wide "habitat heater" process whose capacity auto-adjust to the crew capacity
			- Have an extra option in the ECLSS parts for additional controllable/adjustable heating capabilities.
- Heat exchanger :
	- A vessel wide, non-controllable process that consume "AboveOperatingTempThermalEnergy" to produce "BelowOperatingTempThermalEnergy"
	- Automatically redistribute heat from "too hot" parts to "too cold" parts.
	- Should automatically have priority over radiators, as per the "processes have higher priority than modules" resource sim behaviour.
- The resource sim will then process everything as usual, redistributing equally the radiators/heater/heat exchanger result equally
  amongst all parts, allowing to know (at the beginning of the FU) what the resulting temperature for each part is.
- Habitat :
	- Has an internal heat production depending of the kerbal count
	- for each module, the difference between the target temperature and the current temperature is computed
	- the vessel-wide average difference can be used for the rule modifier
- ProcessController :
	- Can have an internal heat production (defined in the process ? in the module ?), scaled by the process utilization
	- Maybe a good idea to make a "ModuleKsmThermalProcessController" derivative instead of having that built-in in every process
	- Not sure about how to factor in the temperature. Maybe have two option :
		- An "operating temperature range" : when the part is outside the range, the process is disabled
		- A "temperature efficiency factor" : capacity is scaled by the current temperature
*/

namespace KERBALISM
{
	public class PartThermalData
	{
		public static double defaultEmissivity = 0.25;
		public static double defaultInternalTransfer = 0.1;

		public const string belowThEnergyResName = "belowThEnergyRes";
		public const string aboveThEnergyResName = "aboveThEnergyRes";

		// specific heats in kJ/t/K
		public const double specifHeatAluminum = 0.897 * 1e3;
		public const double specifHeatSteel = 0.466 * 1e3;
		public const double specifHeatTitanium = 0.523 * 1e3;
		public const double specifHeatPolyethylene = 2.303 * 1e3;
		public const double partSpecificHeat =
			specifHeatAluminum * 0.5
			+ specifHeatTitanium * 0.3
			+ specifHeatSteel * 0.1
			+ specifHeatPolyethylene * 0.1;

		private PartData partData;
		private PartResourceData aboveThEnergy;
		private PartResourceData belowThEnergy;
		private double energyPerKelvin;

		// TODO : persistence for those !
		public float insulation = 0.9f;
		public float emissivity = 0.25f;
		public string pawInfo;
		public double currentFlux;

		public double Temperature => temperature;
		private double temperature;

		public List<IThermalModule> thermalModules = new List<IThermalModule>();

		//debug
		public double envFlux;
		public double skinIrradiance;
		public double skinRadiosity;
		public double internalFlux;

		private PartThermalData(PartData partData)
		{
			this.partData = partData;
		}

		public static PartThermalData Setup(PartData partData)
		{
			if (partData.volumeAndSurface == null)
				return null;

			PartThermalData thermalData = null;
			foreach (ModuleData moduleData in partData.modules)
			{
				if (moduleData is IThermalModule thModule)
				{
					if (thermalData == null)
						thermalData = new PartThermalData(partData);

					thermalData.thermalModules.Add(thModule);
				}
			}

			if (thermalData == null)
				return null;

			IThermalModule masterModule = null;
			thermalData.energyPerKelvin = 0.0;
			foreach (IThermalModule thModule in thermalData.thermalModules)
			{
				if (thModule.IsAlwaysMaster || masterModule == null)
					masterModule = thModule;

				thermalData.energyPerKelvin += thModule.ThermalMass * partSpecificHeat;
			}

			if (thermalData.energyPerKelvin <= 0.0)
				return null;

			if (partData.virtualResources.Contains(belowThEnergyResName))
			{
				partData.virtualResources.TryGet(belowThEnergyResName, out thermalData.belowThEnergy);
				partData.virtualResources.TryGet(aboveThEnergyResName, out thermalData.aboveThEnergy);
			}
			else
			{
				VesselVirtualPartResource belowThRes = partData.vesselData.ResHandler.GetOrCreateVirtualResource<VesselVirtualPartResource>(belowThEnergyResName);
				VesselVirtualPartResource aboveThRes = partData.vesselData.ResHandler.GetOrCreateVirtualResource<VesselVirtualPartResource>(aboveThEnergyResName);
				double targetTempEnergy = thermalData.energyPerKelvin * masterModule.OperatingTemperature;
				thermalData.belowThEnergy = partData.virtualResources.AddResource(belowThRes, targetTempEnergy, targetTempEnergy);
				thermalData.aboveThEnergy = partData.virtualResources.AddResource(aboveThRes, 0.0, targetTempEnergy * 3.0);
			}

			if (partData.LoadedPart != null)
			{
				BasePAWGroup pawGroup = new BasePAWGroup("Thermal", "Systems thermal control", true);
				System.Reflection.BindingFlags npFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

				Type dataType = typeof(PartThermalData);

				partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), dataType.GetField(nameof(envFlux)), thermalData));
				partData.LoadedPart.Fields[nameof(envFlux)].guiFormat = "F3";
				partData.LoadedPart.Fields[nameof(envFlux)].guiUnits = "kW";
				partData.LoadedPart.Fields[nameof(envFlux)].group = pawGroup;
				partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), dataType.GetField(nameof(skinIrradiance)), thermalData));
				partData.LoadedPart.Fields[nameof(skinIrradiance)].guiFormat = "F3";
				partData.LoadedPart.Fields[nameof(skinIrradiance)].guiUnits = "kW/m²";
				partData.LoadedPart.Fields[nameof(skinIrradiance)].group = pawGroup;
				partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), dataType.GetField(nameof(skinRadiosity)), thermalData));
				partData.LoadedPart.Fields[nameof(skinRadiosity)].guiFormat = "F3";
				partData.LoadedPart.Fields[nameof(skinRadiosity)].guiUnits = "kW/m²";
				partData.LoadedPart.Fields[nameof(skinRadiosity)].group = pawGroup;

				BaseField temperatureField = new BaseField(new UI_Label(), dataType.GetField(nameof(temperature), npFlags), thermalData);
				temperatureField.guiName = "Current T°";
				temperatureField.guiFormat = "F2";
				temperatureField.guiUnits = "K";
				temperatureField.group = pawGroup;
				partData.LoadedPart.Fields.Add(temperatureField);

				BaseField internalFluxField = new BaseField(new UI_Label(), dataType.GetField(nameof(internalFlux)), thermalData);
				internalFluxField.guiActiveEditor = true;
				internalFluxField.guiName = "Internal production";
				internalFluxField.guiFormat = "F3";
				internalFluxField.guiUnits = " kWth";
				internalFluxField.group = pawGroup;
				partData.LoadedPart.Fields.Add(internalFluxField);

				BaseField fluxField = new BaseField(new UI_Label(), dataType.GetField(nameof(currentFlux)), thermalData);
				fluxField.guiActiveEditor = true;
				fluxField.guiName = Lib.IsEditor ? "Estimated balance" : "Current balance";
				fluxField.guiFormat = "F3";
				fluxField.guiUnits = " kWth";
				fluxField.group = pawGroup;
				partData.LoadedPart.Fields.Add(fluxField);

				BaseField pawInfoField = new BaseField(new UI_Label(), dataType.GetField(nameof(pawInfo)), thermalData);
				pawInfoField.guiActiveEditor = true;
				pawInfoField.guiName = "Operating T°";
				pawInfoField.group = pawGroup;
				partData.LoadedPart.Fields.Add(pawInfoField);

				thermalData.pawInfo = Lib.BuildString(
					Lib.HumanReadableTemp(masterModule.OperatingTemperature), ", ",
					"Th. mass", ": ", Lib.HumanReadableMass(thermalData.energyPerKelvin / partSpecificHeat));

				UI_FloatRange insulationFR = new UI_FloatRange();
				insulationFR.minValue = 0.8f;
				insulationFR.maxValue = 0.975f;
				insulationFR.stepIncrement = 0.005f;
				insulationFR.onFieldChanged = (a, b) => thermalData.PlannerUpdate();
				BaseField insulationField = new BaseField(insulationFR, dataType.GetField(nameof(insulation)), thermalData);
				insulationField.uiControlEditor = insulationFR;
				insulationField.guiActiveEditor = true;
				insulationField.guiName = "Internal insulation";
				insulationField.guiFormat = "P1";
				insulationField.group = pawGroup;
				partData.LoadedPart.Fields.Add(insulationField);

				UI_FloatRange emissivityFR = new UI_FloatRange();
				emissivityFR.minValue = 0.10f;
				emissivityFR.maxValue = 0.75f;
				emissivityFR.stepIncrement = 0.01f;
				emissivityFR.onFieldChanged = (a, b) => thermalData.PlannerUpdate();
				BaseField emissivityField = new BaseField(emissivityFR, dataType.GetField(nameof(emissivity)), thermalData);
				emissivityField.uiControlEditor = emissivityFR;
				emissivityField.guiActiveEditor = true;
				emissivityField.guiName = "Surface emissivity";
				emissivityField.guiFormat = "P1";
				emissivityField.group = pawGroup;
				partData.LoadedPart.Fields.Add(emissivityField);


			}

			return thermalData;
		}

		public void PlannerUpdate()
		{
			ResetTemperature();
			Update(1.0);
		}

		private void ResetTemperature()
		{
			belowThEnergy.Amount = belowThEnergy.Capacity;
			aboveThEnergy.Amount = 0.0;
		}

		public void Update(double elapsedSec)
		{
			VesselDataBase vdb = partData.vesselData;

			temperature = (belowThEnergy.Amount + aboveThEnergy.Amount) / energyPerKelvin;

			
			double skinTemperature;
			double atmoConvectionFlux = 0.0;
			// for loaded vessels flying in atmosphere, plug into the stock temperature
			if (vdb.EnvInAtmosphere && !vdb.EnvLanded && vdb.LoadedOrEditor && partData.LoadedPart.vessel != null)
			{
				skinTemperature = (temperature + partData.LoadedPart.skinTemperature) * 0.5;
				double internalTempChange = partData.LoadedPart.temperature - partData.LoadedPart.ptd.previousTemperature;
				atmoConvectionFlux = energyPerKelvin * internalTempChange / TimeWarp.fixedDeltaTime;
				if (atmoConvectionFlux < 0.0 && temperature < partData.LoadedPart.temperature)
					atmoConvectionFlux = 0.0;
				else if (atmoConvectionFlux > 0.0 && temperature > partData.LoadedPart.temperature)
					atmoConvectionFlux = 0.0;
				atmoConvectionFlux = Math.Pow(atmoConvectionFlux, 0.5); // try to balance the stock stupidly high internal temperatures
			}
			// TODO : include our own atmospheric temperature model for conduction while landed
			else
			{
				skinTemperature = temperature;
			}

			// TODO : aggregate (but not here, at the sim level) "close" stars (Kopernicus binary systems handling)
			StarFlux star = vdb.MainStar;

			skinIrradiance = ((star.directFlux + star.bodiesAlbedoFlux + star.bodiesEmissiveFlux + vdb.IrradianceBodiesCore) * 0.25) + Sim.BackgroundFlux;

			double hottestSkinTemperature = skinTemperature + Sim.BlackBodyTemperature(skinIrradiance);
			double coldestSkinTemperature = Sim.BlackBodyTemperature(Sim.BackgroundFlux);
			skinRadiosity =
				  (0.250 * Sim.GreyBodyRadiosity(hottestSkinTemperature, emissivity))
				+ (0.375 * Sim.GreyBodyRadiosity(skinTemperature, emissivity))
				+ (0.375 * Sim.GreyBodyRadiosity(coldestSkinTemperature, emissivity));

			skinRadiosity /= 1000.0; // W/m² -> kW/m²
			skinIrradiance /= 1000.0; // W/m² -> kW/m² 

			envFlux =
				atmoConvectionFlux
				+ (skinIrradiance * partData.volumeAndSurface.surface)
				- (skinRadiosity * partData.volumeAndSurface.surface);

			envFlux *= 1f - insulation;

			internalFlux = 0.0;
			foreach (IThermalModule thModule in thermalModules)
			{
				internalFlux += thModule.InternalHeatProduction;
			}

			currentFlux = envFlux + internalFlux;

			double energyChange = currentFlux * elapsedSec;
			double belowThEnergyChange = Math.Min(belowThEnergy.Capacity - belowThEnergy.Amount, energyChange + aboveThEnergy.Amount);
			double aboveThEnergyChange = energyChange - belowThEnergyChange;
			belowThEnergy.Amount += belowThEnergyChange;
			aboveThEnergy.Amount += aboveThEnergyChange;

		}
	}
}
