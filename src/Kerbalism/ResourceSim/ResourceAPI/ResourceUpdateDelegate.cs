using System;
using System.Collections.Generic;
using System.Reflection;


namespace KERBALISM
{
	public class ResourceUpdateDelegate
	{
		private static Type[] signature = { typeof(Dictionary<string, double>), typeof(List<KeyValuePair<string, double>>) };

		internal PartModule module;

		internal MethodInfo methodInfo;
		private ResourceUpdateDelegate(MethodInfo methodInfo, PartModule module)
		{
			this.methodInfo = methodInfo;
			this.module = module;
		}

		public string invoke(Dictionary<string, double> availableRresources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			IKerbalismModule km = module as IKerbalismModule;
			if (km != null)
				return km.ResourceUpdate(availableRresources, resourceChangeRequest);

			var title = methodInfo.Invoke(module, new object[] { availableRresources, resourceChangeRequest });
			if (title == null) return module.moduleName;
			return title.ToString();
		}

		public static ResourceUpdateDelegate Instance(PartModule module)
		{
			MethodInfo methodInfo = null;
			var type = module.GetType();
			supportedModules.TryGetValue(type, out methodInfo);
			if (methodInfo != null) return new ResourceUpdateDelegate(methodInfo, module);

			if (unsupportedModules.Contains(type)) return null;

			methodInfo = module.GetType().GetMethod("ResourceUpdate", BindingFlags.Instance | BindingFlags.Public);
			if (methodInfo == null)
			{
				unsupportedModules.Add(type);
				return null;
			}

			supportedModules[type] = methodInfo;
			return new ResourceUpdateDelegate(methodInfo, module);
		}

		private static readonly Dictionary<Type, MethodInfo> supportedModules = new Dictionary<Type, MethodInfo>();
		private static readonly List<Type> unsupportedModules = new List<Type>();
	}
}
