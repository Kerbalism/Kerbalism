using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class VesselThermalData
	{
		public double coolantVolume;
		public double coolantTemperature;

		public void Reset()
		{
			coolantVolume = 0.0;
			coolantTemperature = 0.0;
		}
	}
}
