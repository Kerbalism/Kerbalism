using System;
using System.Collections.Generic;
using System.Reflection;

namespace KERBALISM
{
	public class BackgroundDelegate
	{
		private static Type[] signature = { typeof(Vessel), typeof(ProtoPartSnapshot), typeof(ProtoPartModuleSnapshot), typeof(PartModule), typeof(Part), typeof(Dictionary<string, double>), typeof(List<KeyValuePair<string, double>>), typeof(double) };

#if KSP18
		// non-generic actions are too new to be used in pre-KSP18
		internal Func<Vessel, ProtoPartSnapshot, ProtoPartModuleSnapshot, PartModule, Part, Dictionary<string, double>, List<KeyValuePair<string, double>>, double, string> function;
#else
			internal MethodInfo methodInfo;
#endif
		private BackgroundDelegate(MethodInfo methodInfo)
		{
#if KSP18
			function = (Func<Vessel, ProtoPartSnapshot, ProtoPartModuleSnapshot, PartModule, Part, Dictionary<string, double>, List<KeyValuePair<string, double>>, double, string>)Delegate.CreateDelegate(typeof(Func<Vessel, ProtoPartSnapshot, ProtoPartModuleSnapshot, PartModule, Part, Dictionary<string, double>, List<KeyValuePair<string, double>>, double, string>), methodInfo);
#else
				this.methodInfo = methodInfo;
#endif
		}

		public string invoke(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule module_prefab, Part part_prefab, Dictionary<string, double> availableRresources, List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s)
		{
			// TODO optimize this for performance
#if KSP18
			var result = function(v, p, m, module_prefab, part_prefab, availableRresources, resourceChangeRequest, elapsed_s);
			if (string.IsNullOrEmpty(result)) result = module_prefab.moduleName;
			return result;
#else
				var result = methodInfo.Invoke(null, new object[] { v, p, m, module_prefab, part_prefab, availableRresources, resourceChangeRequest, elapsed_s });
				if(result == null) return module_prefab.moduleName;
				return result.ToString();
#endif
		}

		public static BackgroundDelegate Instance(PartModule module_prefab)
		{
			BackgroundDelegate result = null;

			var type = module_prefab.GetType();
			supportedModules.TryGetValue(type, out result);
			if (result != null) return result;

			if (unsupportedModules.Contains(type)) return null;

			MethodInfo methodInfo = type.GetMethod("BackgroundUpdate", signature);
			if (methodInfo == null)
			{
				unsupportedModules.Add(type);
				return null;
			}

			result = new BackgroundDelegate(methodInfo);
			supportedModules[type] = result;
			return result;
		}

		private static readonly Dictionary<Type, BackgroundDelegate> supportedModules = new Dictionary<Type, BackgroundDelegate>();
		private static readonly List<Type> unsupportedModules = new List<Type>();
	}
}
