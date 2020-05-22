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
	public class PartThermalData2
	{
		public const string NODENAME_THERMAL = "THERMAL";

		public static VirtualResourceDefinition belowThDef;
		public static VirtualResourceDefinition aboveThDef;

		public static void SetupVirtualResources()
		{
			belowThDef = VirtualResourceDefinition.GetOrCreateDefinition(Settings.belowThEnergyRes, true, VirtualResourceDefinition.ResType.PartResource, "Heating control");
			aboveThDef = VirtualResourceDefinition.GetOrCreateDefinition(Settings.aboveThEnergyRes, true, VirtualResourceDefinition.ResType.PartResource, "Cooling control");
		}

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

		public const double surfaceThickness = 0.03;
		public const double surfaceDensity = 2.7;

		private PartData partData;
		private double energyPerKelvin;
		private double storedEnergy = -1.0;

		public float emissivity = 0.35f;

		public double Temperature => temperature;
		private double temperature;

		public string pawInfo; // Surface: 295.0K, -0.045 kWth

		public List<IThermalModule> thermalModules = new List<IThermalModule>();

		//debug
		public double envFlux;
		public double irradiance;
		public double radiosity;
		public double internalFlux;

		public PartThermalData2(PartData partData)
		{
			this.partData = partData;
		}

		public static PartThermalData2 Setup(PartData partData)
		{
			if (!Features.Thermal)
				return null;

			if (partData.volumeAndSurface == null)
				return null;

			PartThermalData2 thermalData = partData.thermalData;
			foreach (ModuleData moduleData in partData.modules)
			{
				if (moduleData is IThermalModule thModule)
				{
					if (thermalData == null)
						thermalData = new PartThermalData2(partData);

					if (!thModule.IsThermalEnabled)
						continue;

					if (thModule.ThermalData == null)
						thModule.ThermalData = new ModuleThermalData();

					thModule.ThermalData.Setup(partData, thModule);

					thermalData.thermalModules.Add(thModule);
				}
			}

			if (thermalData == null)
				return null;

			thermalData.energyPerKelvin = partData.volumeAndSurface.surface * surfaceThickness * surfaceDensity * specifHeatAluminum;
			if (thermalData.storedEnergy < 0.0)
			{
				thermalData.storedEnergy = 295.0 * thermalData.energyPerKelvin;
			}

			if (partData.LoadedPart != null)
			{
				BasePAWGroup pawGroup = new BasePAWGroup("Thermal", "Systems thermal control", true);
				System.Reflection.BindingFlags npFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

				Type dataType = typeof(PartThermalData2);

				partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), dataType.GetField(nameof(envFlux)), thermalData));
				partData.LoadedPart.Fields[nameof(envFlux)].guiActiveEditor = true;
				partData.LoadedPart.Fields[nameof(envFlux)].guiFormat = "F3";
				partData.LoadedPart.Fields[nameof(envFlux)].guiUnits = "kW";
				partData.LoadedPart.Fields[nameof(envFlux)].group = pawGroup;
				partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), dataType.GetField(nameof(irradiance)), thermalData));
				partData.LoadedPart.Fields[nameof(irradiance)].guiActiveEditor = true;
				partData.LoadedPart.Fields[nameof(irradiance)].guiFormat = "F3";
				partData.LoadedPart.Fields[nameof(irradiance)].guiUnits = "kW";
				partData.LoadedPart.Fields[nameof(irradiance)].group = pawGroup;
				partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), dataType.GetField(nameof(radiosity)), thermalData));
				partData.LoadedPart.Fields[nameof(radiosity)].guiActiveEditor = true;
				partData.LoadedPart.Fields[nameof(radiosity)].guiFormat = "F3";
				partData.LoadedPart.Fields[nameof(radiosity)].guiUnits = "kW";
				partData.LoadedPart.Fields[nameof(radiosity)].group = pawGroup;

				BaseField temperatureField = new BaseField(new UI_Label(), dataType.GetField(nameof(temperature), npFlags), thermalData);
				temperatureField.guiActiveEditor = true;
				temperatureField.guiName = "Hull T°";
				temperatureField.guiFormat = "F2";
				temperatureField.guiUnits = "K";
				temperatureField.group = pawGroup;
				partData.LoadedPart.Fields.Add(temperatureField);

				UI_FloatRange emissivityFR = new UI_FloatRange();
				emissivityFR.minValue = 0.10f;
				emissivityFR.maxValue = 0.75f;
				emissivityFR.stepIncrement = 0.01f;
				emissivityFR.onFieldChanged = (a, b) => thermalData.PlannerUpdate();
				BaseField emissivityField = new BaseField(emissivityFR, dataType.GetField(nameof(emissivity)), thermalData);
				emissivityField.uiControlEditor = emissivityFR;
				emissivityField.guiActiveEditor = true;
				emissivityField.guiName = "Hull emissivity";
				emissivityField.guiFormat = "P1";
				emissivityField.group = pawGroup;
				partData.LoadedPart.Fields.Add(emissivityField);


			}

			return thermalData;
		}

		static double shadowCorrectionFactor = 1.3;
		public void PlannerUpdate()
		{
			temperature = 295.0;
			storedEnergy = temperature * energyPerKelvin;

			foreach (IThermalModule thermalModule in thermalModules)
			{
				thermalModule.ThermalData.ResetTemperature();
			}

			double oldTemperature;
			double timestep = 60.0;
			
			VesselDataShip vds = (VesselDataShip)partData.vesselData;
			double correctedShadowTime = Math.Min(vds.shadowTime * shadowCorrectionFactor, 1.0);
			do
			{
				oldTemperature = temperature;
				UpdateSkinTemperature(timestep * (1.0 - correctedShadowTime), false);
				UpdateSkinTemperature(timestep * correctedShadowTime, true);

				if (Math.Abs(oldTemperature - temperature) < 0.01)
					timestep -= 1.0;
				
			} while (timestep > 0.0);

			UpdateModules(1.0);

		}



		private void UpdateSkinTemperature(double elapsedSec, bool forceInShadow = false)
		{
			VesselDataBase vdb = partData.vesselData;

			// TODO : aggregate (but not here, at the sim level) "close" stars (Kopernicus binary systems handling)
			StarFlux star = vdb.MainStar;
			//skinIrradiance = ((star.directFlux + star.bodiesAlbedoFlux + star.bodiesEmissiveFlux + vdb.IrradianceBodiesCore) * 0.25) + Sim.BackgroundFlux;
			////temperature = Math.Pow(skinIrradiance / (PhysicsGlobals.StefanBoltzmanConstant * emissivity), 0.25);
			////skinRadiosity = Sim.GreyBodyRadiosity(temperature, emissivity);
			//double hottestSkinTemperature = temperature + Sim.BlackBodyTemperature(skinIrradiance);
			//double coldestSkinTemperature = Sim.BlackBodyTemperature(Sim.BackgroundFlux);
			//skinRadiosity =
			//	  (0.250 * Sim.GreyBodyRadiosity(hottestSkinTemperature, emissivity))
			//	+ (0.375 * Sim.GreyBodyRadiosity(temperature, emissivity))
			//	+ (0.375 * Sim.GreyBodyRadiosity(coldestSkinTemperature, emissivity));

			double reflectivity = 1.0 - emissivity;
			double skinIrradiance;
			if (forceInShadow)
			{
				skinIrradiance = (star.bodiesEmissiveFlux + vdb.IrradianceBodiesCore) * reflectivity;
			}
			else
			{
				skinIrradiance = (star.directFlux + star.bodiesAlbedoFlux + star.bodiesEmissiveFlux + vdb.IrradianceBodiesCore) * reflectivity;
			}

			double hottestSkinTemperature = (temperature + Sim.BlackBodyTemperature(skinIrradiance)) * 0.5;
			double coldestSkinTemperature = (temperature + Sim.BlackBodyTemperature(Sim.BackgroundFlux)) * 0.5;
			double skinRadiosity =
				  (0.25 * Sim.GreyBodyRadiosity(hottestSkinTemperature, emissivity))
				+ (0.50 * Sim.GreyBodyRadiosity(temperature, emissivity))
				+ (0.25 * Sim.GreyBodyRadiosity(coldestSkinTemperature, emissivity));

			irradiance = ((skinIrradiance * 0.25) + (Sim.BackgroundFlux * reflectivity)) * 1e-3 * partData.volumeAndSurface.surface; // skinIrradiance W/m² -> kW/m²
			radiosity = skinRadiosity * 1e-3 * partData.volumeAndSurface.surface; // skinRadiosity W/m² -> kW/m²

			double skinConvectionFlux = 0.0; // TODO : atmo transfers
			//if (vdb.EnvInAtmosphere && !vdb.EnvLanded && vdb.LoadedOrEditor && partData.LoadedPart.vessel != null)
			//{
			//	skinTemperature = (temperature + partData.LoadedPart.skinTemperature) * 0.5;
			//	double internalTempChange = partData.LoadedPart.temperature - partData.LoadedPart.ptd.previousTemperature;
			//	atmoConvectionFlux = energyPerKelvin * internalTempChange / TimeWarp.fixedDeltaTime;
			//	if (atmoConvectionFlux < 0.0 && temperature < partData.LoadedPart.temperature)
			//		atmoConvectionFlux = 0.0;
			//	else if (atmoConvectionFlux > 0.0 && temperature > partData.LoadedPart.temperature)
			//		atmoConvectionFlux = 0.0;
			//	atmoConvectionFlux = Math.Pow(atmoConvectionFlux, 0.5); // try to balance the stock stupidly high internal temperatures
			//}

			envFlux = skinConvectionFlux + irradiance - radiosity;

			double flux = envFlux * elapsedSec;
			storedEnergy = Math.Max(storedEnergy + flux, 0.0);
			temperature = storedEnergy / energyPerKelvin;

		}

		private void UpdateModules(double elapsedSec)
		{
			double internalToSkinTotal = 0.0;
			foreach (IThermalModule thermalModule in thermalModules)
			{
				internalToSkinTotal += thermalModule.ThermalData.Update(elapsedSec, partData.volumeAndSurface.surface, temperature);
			}
		}

		public void Update(double elapsedSec)
		{
			UpdateSkinTemperature(elapsedSec);
			UpdateModules(elapsedSec);
		}

		public static void Load(PartData partData, ConfigNode partDataNode)
		{
			ConfigNode thermalNode = partDataNode.GetNode(NODENAME_THERMAL);
			if (thermalNode == null)
				return;

			if (partData.thermalData == null)
				partData.thermalData = new PartThermalData2(partData);

			partData.thermalData.emissivity = Lib.ConfigValue(thermalNode, "emissivity", 0.25f);
			partData.thermalData.storedEnergy = Lib.ConfigValue(thermalNode, "storedEnergy", -1.0);
		}

		public static bool Save(PartData partData, ConfigNode partDataNode)
		{
			if (partData.thermalData == null)
				return false;

			ConfigNode thermalNode = partDataNode.AddNode(NODENAME_THERMAL);
			thermalNode.AddValue("emissivity", partData.thermalData.emissivity);
			thermalNode.AddValue("storedEnergy", partData.thermalData.storedEnergy);
			return true;
		}
	}
}
