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
}
