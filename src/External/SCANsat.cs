using System;


namespace KERBALISM
{


	public static class SCANsat
	{
		static SCANsat()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "SCANsat")
				{
					SCANUtils = a.assembly.GetType("SCANsat.SCANUtil");
					RegisterSensor = SCANUtils.GetMethod("registerSensorExternal");
					UnregisterSensor = SCANUtils.GetMethod("unregisterSensorExternal");
					break;
				}
			}
		}

		// interrupt scanning of a SCANsat module
		// - v: vessel that own the module
		// - m: protomodule of a SCANsat or a resource scanner
		// - p: prefab of the part owning the module
		public static bool stopScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
		{
			return SCANUtils != null && (bool)UnregisterSensor.Invoke(null, new Object[] { v, m, part_prefab });
		}

		// resume scanning of a SCANsat module
		// - v: vessel that own the module
		// - m: protomodule of a SCANsat or a resource scanner
		// - p: prefab of the part owning the module
		public static bool resumeScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
		{
			return SCANUtils != null && (bool)RegisterSensor.Invoke(null, new Object[] { v, m, part_prefab });
		}

		// return scanner EC consumption per-second
		public static double EcConsumption(PartModule scanner)
		{
			foreach (ModuleResource res in scanner.resHandler.inputResources)
			{
				if (res.name == "ElectricCharge") return res.rate;
			}
			return 0.0;
		}

		// reflection type of SCANUtils static class in SCANsat assembly, if present
		static Type SCANUtils;
		static System.Reflection.MethodInfo RegisterSensor;
		static System.Reflection.MethodInfo UnregisterSensor;
	}


} // KERBALISM