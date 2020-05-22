using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	/// <summary> Implemented by ModuleData derivatives that require their part to be radiation-evaluated </summary>
	public interface IRadiationReceiver
	{
		PartRadiationData RadiationData { get; }
	}

	/// <summary> Implemented by ModuleData derivatives that emit (or remove) radiation </summary>
	public interface IRadiationEmitter
	{
		/// <summary> radiation emitted in rad/s </summary>
		double RadiationRate { get; }
		int ModuleId { get; }
		bool IsActive { get; }
		PartRadiationData RadiationData { get; }
	}

	public interface IThermalModule
	{
		bool IsThermalEnabled { get; }
		string ModuleId { get; }
		double OperatingTemperature { get; } // Kelvin
		double HeatProduction { get; } // KiloWatts
		double ThermalMass { get; } // Tons
		// Estimate of the contact area proportion between that module and the part surface.
		// Unless a specific value is defined in configs, it can be guessed from the thermal module mass compared to the part mass.
		double SurfaceFactor { get; } 
		ModuleThermalData ThermalData { get; set; }
	}
}
