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
		public static double defaultEmissivity = 0.4;
		public static double defaultAbsorptance = 0.3;

		public const string belowThEnergyResName = "belowThEnergyRes";
		public const string aboveThEnergyResName = "aboveThEnergyRes";

		// specific heats in J/t/K
		public const double specifHeatAluminum = 0.897 * 1e6;
		public const double specifHeatSteel = 0.466 * 1e6;
		public const double specifHeatTitanium = 0.523 * 1e6;
		public const double specifHeatPolyethylene = 2.303 * 1e6;
		public const double partSpecificHeat =
			specifHeatAluminum * 0.5
			+ specifHeatTitanium * 0.3
			+ specifHeatSteel * 0.1
			+ specifHeatPolyethylene * 0.1;

		private PartData partData;
		private PartResourceData aboveThEnergy;
		private PartResourceData belowThEnergy;
		private double energyPerKelvin;

		//private double defaultEmissivity = 0.3; // [0;1] factor for the out flux in the grey body equation
		//private double defaultAbsorptance = 0.3; // [0,1] factor for proportion of external radiation transmitted to interiorof the vessel

		public double Temperature => temperature;
		private double temperature;

		private List<IThermalModule> thermalModules = new List<IThermalModule>();

		//debug
		public double sunAndBodyFaceSkinTemp;
		public double bodiesFaceSkinTemp;
		public double sunFaceSkinTemp;
		public double darkFaceSkinTemp;
		public double envFlux;

		public PartThermalData(PartData partData)
		{
			this.partData = partData;
			energyPerKelvin = partData.PartPrefab.mass * partSpecificHeat;
		}

		private double SurfaceTemperature(double irradiance, double angularVelFactor, double averageTemperature)
		{
			double insulation = 1.0 - defaultAbsorptance;
			return ((Sim.BlackBodyTemperature(irradiance) * angularVelFactor) + (averageTemperature * insulation)) / (angularVelFactor + insulation);
		}

		public void Update(double elapsedSec)
		{
			VesselDataBase vdb = partData.vesselData;

			if (belowThEnergy == null || aboveThEnergy == null)
			{
				if (partData.virtualResources.Contains(belowThEnergyResName))
				{
					partData.virtualResources.TryGet(belowThEnergyResName, out belowThEnergy);
					partData.virtualResources.TryGet(aboveThEnergyResName, out aboveThEnergy);
				}
				else
				{
					VesselVirtualPartResource belowThRes = partData.vesselData.ResHandler.CreateVirtualResource<VesselVirtualPartResource>(belowThEnergyResName);
					VesselVirtualPartResource aboveThRes = partData.vesselData.ResHandler.CreateVirtualResource<VesselVirtualPartResource>(aboveThEnergyResName);
					double targetTempEnergy = energyPerKelvin * 273.0; // temporary
					belowThEnergy = partData.virtualResources.AddResource(belowThRes, targetTempEnergy, targetTempEnergy);
					double maxAboveTempEnergy = (energyPerKelvin * partData.PartPrefab.maxTemp) - targetTempEnergy;
					aboveThEnergy = partData.virtualResources.AddResource(aboveThRes, 0.0, maxAboveTempEnergy);
				}

				if (partData.LoadedPart != null)
				{
					partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(temperature), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), this));
					partData.LoadedPart.Fields[nameof(temperature)].guiName = "Temperature";
					partData.LoadedPart.Fields[nameof(temperature)].guiFormat = "F2";
					partData.LoadedPart.Fields[nameof(temperature)].guiUnits = "K";
					partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(envFlux)), this));
					partData.LoadedPart.Fields[nameof(envFlux)].guiFormat = "F2";
					partData.LoadedPart.Fields[nameof(envFlux)].guiUnits = "W";
					partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(sunAndBodyFaceSkinTemp)), this));
					partData.LoadedPart.Fields[nameof(sunAndBodyFaceSkinTemp)].guiFormat = "F2";
					partData.LoadedPart.Fields[nameof(sunAndBodyFaceSkinTemp)].guiUnits = "K";
					partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(bodiesFaceSkinTemp)), this));
					partData.LoadedPart.Fields[nameof(bodiesFaceSkinTemp)].guiFormat = "F2";
					partData.LoadedPart.Fields[nameof(bodiesFaceSkinTemp)].guiUnits = "K";
					partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(sunFaceSkinTemp)), this));
					partData.LoadedPart.Fields[nameof(sunFaceSkinTemp)].guiFormat = "F2";
					partData.LoadedPart.Fields[nameof(sunFaceSkinTemp)].guiUnits = "K";
					partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(darkFaceSkinTemp)), this));
					partData.LoadedPart.Fields[nameof(darkFaceSkinTemp)].guiFormat = "F2";
					partData.LoadedPart.Fields[nameof(darkFaceSkinTemp)].guiUnits = "K";
				}
			}

			temperature = (belowThEnergy.Amount + aboveThEnergy.Amount) / energyPerKelvin;

			// for loaded vessels flying in atmosphere, blend in the stock skin temperature
			double baseSkinTemperature;
			double atmoConvectionFlux = 0.0;
			if (vdb.EnvInAtmosphere && !vdb.EnvLanded && vdb.LoadedOrEditor && partData.LoadedPart.vessel != null)
			{
				// TODO : include our own atmospheric temperature model for conduction while landed

				baseSkinTemperature = (temperature + partData.LoadedPart.skinTemperature) * 0.5;
				double internalTempChange = partData.LoadedPart.temperature - partData.LoadedPart.ptd.previousTemperature;
				atmoConvectionFlux = energyPerKelvin * internalTempChange / TimeWarp.fixedDeltaTime;
				if (atmoConvectionFlux < 0.0 && temperature < partData.LoadedPart.temperature)
					atmoConvectionFlux = 0.0;
				else if (atmoConvectionFlux > 0.0 && temperature > partData.LoadedPart.temperature)
					atmoConvectionFlux = 0.0;
				atmoConvectionFlux = Math.Pow(atmoConvectionFlux, 0.5); // try to balance the stock stupidly high internal temperatures
			}
			else
			{
				baseSkinTemperature = temperature;
			}

			// 5°/s = max factor
			double angularVelFactor = 1.0 - Math.Min(vdb.AngularVelocity * 0.0873, 1.0);
			double perStarBodiesCoreIrradiance = vdb.IrradianceBodiesCore / vdb.StarsIrradiance.Length;
			
			// TODO : aggregate (but not here, at the sim level) "close" stars (Kopernicus binary systems handling)
			StarFlux star = vdb.MainStar;

			// irradiance for the portion of the 360° angle that is exposed to both the sun and the main body
			double sunAndBodyFaceEe = star.directFlux + star.bodiesAlbedoFlux + star.bodiesEmissiveFlux + perStarBodiesCoreIrradiance + Sim.BackgroundFlux;
			sunAndBodyFaceSkinTemp = SurfaceTemperature(sunAndBodyFaceEe, angularVelFactor, baseSkinTemperature);
			double sunAndBodyFaceJe = Sim.GreyBodyRadiosity(sunAndBodyFaceSkinTemp, defaultEmissivity);

			// irradiance for the portion of the 360° angle that is exposed only to the main body
			// Note : ideally we should still use Sim.BackgroundFlux here but scale it down with the body distance.
			// But it doesn't matter much and I'm lazy.
			double bodiesFaceEe = star.bodiesAlbedoFlux + star.bodiesEmissiveFlux + perStarBodiesCoreIrradiance;
			bodiesFaceSkinTemp = SurfaceTemperature(bodiesFaceEe, angularVelFactor, baseSkinTemperature);
			double bodiesFaceJe = Sim.GreyBodyRadiosity(bodiesFaceSkinTemp, defaultEmissivity);

			// irradiance for the portion of the 360° angle that is exposed only to the sun
			double sunFaceEe = star.directFlux + Sim.BackgroundFlux;
			sunFaceSkinTemp = SurfaceTemperature(sunFaceEe, angularVelFactor, baseSkinTemperature);
			double sunFaceJe = Sim.GreyBodyRadiosity(sunFaceSkinTemp, defaultEmissivity);

			// irradiance for the portion of the 360° angle that is neither exposed to the sun nor to the main body
			double darkFaceEe = Sim.BackgroundFlux;
			darkFaceSkinTemp = SurfaceTemperature(darkFaceEe, angularVelFactor, baseSkinTemperature);
			double darkFaceJe = Sim.GreyBodyRadiosity(darkFaceSkinTemp, defaultEmissivity);

			double surface;
			if (partData.volumeAndSurface != null)
				surface = partData.volumeAndSurface.GetBestSurface();
			else
				surface = PartVolumeAndSurface.PartBoundsSurface(partData.PartPrefab, true); // temporary for testing

			double SBAndDSurfFactor = (1.0 - star.mainBodyVesselStarAngle) * 0.5;
			double SAndBSurfFactor = star.mainBodyVesselStarAngle * 0.5;

			envFlux =
				((
					  (sunAndBodyFaceEe * SBAndDSurfFactor)
					+ (bodiesFaceEe * SAndBSurfFactor)
					+ (sunFaceEe * SAndBSurfFactor)
					+ (darkFaceEe * SBAndDSurfFactor)
				) * defaultAbsorptance * surface)
				-
				((
					  (sunAndBodyFaceJe * SBAndDSurfFactor)
					+ (bodiesFaceJe * SAndBSurfFactor)
					+ (sunFaceJe * SAndBSurfFactor)
					+ (darkFaceJe * SBAndDSurfFactor)
				) * surface)
				+
				atmoConvectionFlux;

			double energyChange = envFlux * elapsedSec;
			double belowThEnergyChange = Math.Min(belowThEnergy.Capacity - belowThEnergy.Amount, energyChange + aboveThEnergy.Amount);
			double aboveThEnergyChange = energyChange - belowThEnergyChange;
			belowThEnergy.Amount += belowThEnergyChange;
			aboveThEnergy.Amount += aboveThEnergyChange;
		}
	}
}
