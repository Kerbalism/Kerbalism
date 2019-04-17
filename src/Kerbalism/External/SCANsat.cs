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
					GetCoverage = SCANUtils.GetMethod("GetCoverage");
					break;
				}
			}
		}

		// interrupt scanning of a SCANsat module
		// - v: vessel that own the module
		// - m: protomodule of a SCANsat or a resource scanner
		// - p: prefab of the part owning the module
		public static bool StopScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
		{
			return SCANUtils != null && (bool)UnregisterSensor.Invoke(null, new Object[] { v, m, part_prefab });
		}

		// resume scanning of a SCANsat module
		// - v: vessel that own the module
		// - m: protomodule of a SCANsat or a resource scanner
		// - p: prefab of the part owning the module
		public static bool ResumeScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
		{
			return SCANUtils != null && (bool)RegisterSensor.Invoke(null, new Object[] { v, m, part_prefab });
		}

		// return the scanning coverage for a given sensor type on a give body
		// - sensor_type: the sensor type
		// - body: the body in question
		public static double Coverage(int sensor_type, CelestialBody body)
		{
			if (SCANUtils == null) return 0;
			return (double)GetCoverage.Invoke(null, new Object[] { sensor_type, body });
		}

		public static bool IsScanning(PartModule scanner)
		{
			return Lib.ReflectionValue<bool>(scanner, "scanning");
		}

		public static void StopScan(PartModule scanner)
		{
			Lib.ReflectionCall(scanner, "stopScan");
		}

		public static void StartScan(PartModule scanner)
		{
			Lib.ReflectionCall(scanner, "startScan");
		}

		// reflection type of SCANUtils static class in SCANsat assembly, if present
		static Type SCANUtils;
		static System.Reflection.MethodInfo RegisterSensor;
		static System.Reflection.MethodInfo UnregisterSensor;
		static System.Reflection.MethodInfo GetCoverage;
	}


} // KERBALISM