using System;
namespace KERBALISM
{
	public static class SCANsat
	{
		private static readonly OptionalAssembly assembly = new OptionalAssembly("SCANsat");
		private static bool apiFailureLogged;

		public static bool Installed => assembly.Installed;
		public static bool APIAvailable => Installed
			&& SCANUtils != null
			&& RegisterSensor != null
			&& UnregisterSensor != null
			&& GetCoverage != null;

		static SCANsat()
		{
			try
			{
				foreach (var a in AssemblyLoader.loadedAssemblies)
				{
					if (a.name != "SCANsat")
						continue;

					SCANUtils = a.assembly.GetType("SCANsat.SCANUtil");
					if (SCANUtils != null)
					{
						RegisterSensor = SCANUtils.GetMethod("registerSensorExternal");
						UnregisterSensor = SCANUtils.GetMethod("unregisterSensorExternal");
						GetCoverage = SCANUtils.GetMethod("GetCoverage");
					}

					Type controllerType = a.assembly.GetType("SCANsat.SCANcontroller");
					ScanType = a.assembly.GetType("SCANsat.SCAN_Data.SCANtype");
					if (controllerType != null && ScanType != null)
					{
						Controller = controllerType.GetProperty(
							"controller",
							System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
						Type[] sensorParameters =
						{
							typeof(Vessel), ScanType, typeof(double), typeof(double),
							typeof(double), typeof(double), typeof(bool)
						};
						RegisterSensorExact = controllerType.GetMethod(
							"registerSensor",
							System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
							null,
							sensorParameters,
							null);
						UnregisterSensorExact = controllerType.GetMethod(
							"unregisterSensor",
							System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
							null,
							sensorParameters,
							null);
					}
					break;
				}

				if (Installed && !APIAvailable)
					WarnApiFailure("required SCANUtil API methods were not found");
			}
			catch (Exception ex)
			{
				SCANUtils = null;
				RegisterSensor = null;
				UnregisterSensor = null;
				GetCoverage = null;
				WarnApiFailure("initialization failed: " + ex);
			}
		}

		// interrupt scanning of a SCANsat module
		// - v: vessel that own the module
		// - m: protomodule of a SCANsat or a resource scanner
		// - p: prefab of the part owning the module
		public static bool StopScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
		{
			bool? exactResult = InvokeExactScannerMethod(false, v, m, part_prefab);
			if (exactResult.HasValue)
				return exactResult.Value;
			if (ScannerModuleCount(part_prefab) > 1)
				return false;
			return InvokeScannerMethod(UnregisterSensor, v, m, part_prefab);
		}

		// resume scanning of a SCANsat module
		// - v: vessel that own the module
		// - m: protomodule of a SCANsat or a resource scanner
		// - p: prefab of the part owning the module
		public static bool ResumeScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
		{
			bool? exactResult = InvokeExactScannerMethod(true, v, m, part_prefab);
			if (exactResult.HasValue)
				return exactResult.Value;
			if (ScannerModuleCount(part_prefab) > 1)
				return false;
			return InvokeScannerMethod(RegisterSensor, v, m, part_prefab);
		}

		// return the scanning coverage for a given sensor type on a give body
		// - sensor_type: the sensor type
		// - body: the body in question
		public static double Coverage(int sensor_type, CelestialBody body)
		{
			if (GetCoverage == null || body == null)
				return 0.0;

			try
			{
				return (double)GetCoverage.Invoke(null, new Object[] { sensor_type, body });
			}
			catch (Exception ex)
			{
				WarnApiFailure("GetCoverage failed: " + ex);
				return 0.0;
			}
		}

		public static bool IsScanning(PartModule scanner)
		{
			if (scanner == null)
				return false;
			try
			{
				return Lib.ReflectionValue<bool>(scanner, "scanning");
			}
			catch (Exception ex)
			{
				WarnApiFailure("reading scanner state failed: " + ex);
				return false;
			}
		}

		public static int SensorType(PartModule scanner)
		{
			return scanner != null ? ReflectionInt(scanner, "sensorType") : 0;
		}

		public static int ScienceSensorType(string experimentType)
		{
			switch (experimentType)
			{
				case "SCANsatAltimetryLoRes": return 1 << 0;
				case "SCANsatAltimetryHiRes": return 1 << 1;
				case "SCANsatBiomeAnomaly": return 1 << 3;
				case "SCANsatResources": return 1 << 8;
				case "SCANsatVisual": return 1 << 6;
				default: return 0;
			}
		}

		public static bool HasPowerProblem(PartModule scanner)
		{
			if (scanner == null)
				return false;
			try
			{
				Type type = scanner.GetType();
				while (type != null)
				{
					System.Reflection.FieldInfo field = type.GetField(
						"powerIsProblem",
						System.Reflection.BindingFlags.Instance
						| System.Reflection.BindingFlags.Public
						| System.Reflection.BindingFlags.NonPublic);
					if (field != null)
						return (bool)field.GetValue(scanner);
					type = type.BaseType;
				}
			}
			catch (Exception ex)
			{
				WarnApiFailure("reading scanner power state failed: " + ex);
			}
			return false;
		}

		public static void StopScan(PartModule scanner)
		{
			if (scanner == null)
				return;
			try
			{
				Lib.ReflectionCall(scanner, "stopScan");
			}
			catch (Exception ex)
			{
				WarnApiFailure("stopScan failed: " + ex);
			}
		}

		public static void StartScan(PartModule scanner)
		{
			if (scanner == null)
				return;
			try
			{
				Lib.ReflectionCall(scanner, "startScan");
			}
			catch (Exception ex)
			{
				WarnApiFailure("startScan failed: " + ex);
			}
		}

		public static PartModule FindScanner(Part part, string experimentType, int sensorType)
		{
			if (part == null)
				return null;

			int expectedExperiment = ExperimentTypeId(experimentType);
			int expectedScienceSensor = ScienceSensorType(experimentType);
			PartModule scienceSensorMatch = null;
			PartModule experimentMatch = null;
			PartModule sensorMatch = null;
			PartModule onlyCandidate = null;
			int candidateCount = 0;
			foreach (PartModule candidate in part.Modules)
			{
				if (!IsScannerModule(candidate.moduleName))
					continue;
				candidateCount++;
				onlyCandidate = candidate;

				int candidateExperiment = ReflectionInt(candidate, "experimentType");
				int candidateSensor = ReflectionInt(candidate, "sensorType");
				if (expectedScienceSensor != 0
					&& (candidateSensor & expectedScienceSensor) == expectedScienceSensor)
				{
					if (candidateSensor == expectedScienceSensor)
						return candidate;
					if (scienceSensorMatch == null)
						scienceSensorMatch = candidate;
				}
				if (expectedExperiment > 0 && candidateExperiment == expectedExperiment)
				{
					if (sensorType == 0 || candidateSensor == sensorType)
						return candidate;
					if (experimentMatch == null)
						experimentMatch = candidate;
				}
				if (sensorType != 0 && candidateSensor == sensorType && sensorMatch == null)
					sensorMatch = candidate;
			}

			if (scienceSensorMatch != null)
				return scienceSensorMatch;
			if (experimentMatch != null)
				return experimentMatch;
			if (sensorMatch != null)
				return sensorMatch;
			return candidateCount == 1 ? onlyCandidate : null;
		}

		public static ProtoPartModuleSnapshot FindScanner(ProtoPartSnapshot part, string experimentType, int sensorType)
		{
			if (part == null)
				return null;

			int expectedExperiment = ExperimentTypeId(experimentType);
			int expectedScienceSensor = ScienceSensorType(experimentType);
			ProtoPartModuleSnapshot scienceSensorMatch = null;
			ProtoPartModuleSnapshot experimentMatch = null;
			ProtoPartModuleSnapshot sensorMatch = null;
			ProtoPartModuleSnapshot onlyCandidate = null;
			int candidateCount = 0;
			foreach (ProtoPartModuleSnapshot candidate in part.modules)
			{
				if (!IsScannerModule(candidate.moduleName))
					continue;
				candidateCount++;
				onlyCandidate = candidate;

				int candidateExperiment = (int)Lib.Proto.GetUInt(candidate, "experimentType");
				int candidateSensor = (int)Lib.Proto.GetUInt(candidate, "sensorType");
				if (expectedScienceSensor != 0
					&& (candidateSensor & expectedScienceSensor) == expectedScienceSensor)
				{
					if (candidateSensor == expectedScienceSensor)
						return candidate;
					if (scienceSensorMatch == null)
						scienceSensorMatch = candidate;
				}
				if (expectedExperiment > 0 && candidateExperiment == expectedExperiment)
				{
					if (sensorType == 0 || candidateSensor == sensorType)
						return candidate;
					if (experimentMatch == null)
						experimentMatch = candidate;
				}
				if (sensorType != 0 && candidateSensor == sensorType && sensorMatch == null)
					sensorMatch = candidate;
			}

			if (scienceSensorMatch != null)
				return scienceSensorMatch;
			if (experimentMatch != null)
				return experimentMatch;
			if (sensorMatch != null)
				return sensorMatch;
			return candidateCount == 1 ? onlyCandidate : null;
		}

		private static bool InvokeScannerMethod(System.Reflection.MethodInfo method, Vessel vessel,
			ProtoPartModuleSnapshot module, Part partPrefab)
		{
			if (method == null || vessel == null || module == null || partPrefab == null)
				return false;
			try
			{
				object result = method.Invoke(null, new Object[] { vessel, module, partPrefab });
				return result is bool success && success;
			}
			catch (Exception ex)
			{
				WarnApiFailure(method.Name + " failed: " + ex);
				return false;
			}
		}

		private static bool? InvokeExactScannerMethod(bool register, Vessel vessel,
			ProtoPartModuleSnapshot module, Part partPrefab)
		{
			System.Reflection.MethodInfo method = register ? RegisterSensorExact : UnregisterSensorExact;
			if (method == null || Controller == null || ScanType == null
				|| vessel == null || module == null || partPrefab == null)
				return null;

			int sensorType = (int)Lib.Proto.GetUInt(module, "sensorType");
			string experimentType = ExperimentTypeName((int)Lib.Proto.GetUInt(module, "experimentType"));
			PartModule scanner = FindScanner(partPrefab, experimentType, sensorType);
			if (scanner == null || sensorType == 0)
				return null;

			try
			{
				object controller = Controller.GetValue(null, null);
				if (controller == null)
					return null;

				object scanType = Enum.ToObject(ScanType, (short)sensorType);
				method.Invoke(controller, new[]
				{
					vessel,
					scanType,
					(object)ReflectionDouble(scanner, "fov"),
					ReflectionDouble(scanner, "min_alt"),
					ReflectionDouble(scanner, "max_alt"),
					ReflectionDouble(scanner, "best_alt"),
					ReflectionBool(scanner, "requireLight")
				});
				Lib.Proto.Set(module, "scanning", register);
				return true;
			}
			catch (Exception ex)
			{
				WarnApiFailure(method.Name + " exact sensor control failed: " + ex);
				return false;
			}
		}

		private static int ScannerModuleCount(Part part)
		{
			if (part == null)
				return 0;
			int count = 0;
			foreach (PartModule module in part.Modules)
			{
				if (IsScannerModule(module.moduleName))
					count++;
			}
			return count;
		}

		private static int ReflectionInt(PartModule module, string fieldName)
		{
			try
			{
				return Lib.ReflectionValue<int>(module, fieldName);
			}
			catch
			{
				return 0;
			}
		}

		private static double ReflectionDouble(PartModule module, string fieldName)
		{
			try
			{
				System.Reflection.FieldInfo field = module.GetType().GetField(
					fieldName,
					System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.NonPublic);
				return field != null ? Convert.ToDouble(field.GetValue(module)) : 0.0;
			}
			catch
			{
				return 0.0;
			}
		}

		private static bool ReflectionBool(PartModule module, string fieldName)
		{
			try
			{
				System.Reflection.FieldInfo field = module.GetType().GetField(
					fieldName,
					System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.NonPublic);
				return field != null && Convert.ToBoolean(field.GetValue(module));
			}
			catch
			{
				return false;
			}
		}

		private static bool IsScannerModule(string moduleName)
		{
			return moduleName == "SCANsat" || moduleName == "ModuleSCANresourceScanner";
		}

		private static int ExperimentTypeId(string experimentType)
		{
			switch (experimentType)
			{
				case "SCANsatAltimetryLoRes": return 1;
				case "SCANsatAltimetryHiRes": return 2;
				case "SCANsatBiomeAnomaly": return 3;
				case "SCANsatResources": return 4;
				case "SCANsatVisual": return 5;
				default: return 0;
			}
		}

		private static string ExperimentTypeName(int experimentType)
		{
			switch (experimentType)
			{
				case 1: return "SCANsatAltimetryLoRes";
				case 2: return "SCANsatAltimetryHiRes";
				case 3: return "SCANsatBiomeAnomaly";
				case 4: return "SCANsatResources";
				case 5: return "SCANsatVisual";
				default: return string.Empty;
			}
		}

		private static void WarnApiFailure(string message)
		{
			if (apiFailureLogged)
				return;
			apiFailureLogged = true;
			Lib.Log("SCANsat integration disabled or degraded: " + message, Lib.LogLevel.Warning);
		}

		// reflection type of SCANUtils static class in SCANsat assembly, if present
		static Type SCANUtils;
		static System.Reflection.MethodInfo RegisterSensor;
		static System.Reflection.MethodInfo UnregisterSensor;
		static System.Reflection.MethodInfo GetCoverage;
		static Type ScanType;
		static System.Reflection.PropertyInfo Controller;
		static System.Reflection.MethodInfo RegisterSensorExact;
		static System.Reflection.MethodInfo UnregisterSensorExact;
	}
} // KERBALISM
