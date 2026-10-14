using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace KERBALISM
{
	public class ResourceUpdateDelegate
	{
		private static Type[] signature = { typeof(Dictionary<string, double>), typeof(List<KeyValuePair<string, double>>) };

		internal PartModule module;

		internal MethodInfo methodInfo;

		private delegate string ResourceUpdateMethod(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest);

		// the method bound to the module, when it returns a string; it is called every physics step, where
		// MethodInfo.Invoke costs an argument array and the reflection call itself
		private readonly ResourceUpdateMethod method;
		private readonly object[] args;

		private ResourceUpdateDelegate(MethodInfo methodInfo, PartModule module)
		{
			this.methodInfo = methodInfo;
			this.module = module;
			method = (ResourceUpdateMethod)Delegate.CreateDelegate(typeof(ResourceUpdateMethod), module, methodInfo, false);
			if (method == null)
				args = new object[2];
		}

		public string invoke(Dictionary<string, double> availableRresources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			IKerbalismModule km = module as IKerbalismModule;
			if (km != null)
				return km.ResourceUpdate(availableRresources, resourceChangeRequest);

			if (method != null)
				return method(availableRresources, resourceChangeRequest) ?? module.moduleName;

			args[0] = availableRresources;
			args[1] = resourceChangeRequest;
			var title = methodInfo.Invoke(module, args);
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
