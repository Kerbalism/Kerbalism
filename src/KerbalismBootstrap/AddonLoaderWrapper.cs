using System;
using System.Linq;
using System.Reflection;

namespace KerbalismBootstrap
{
	public static class AddonLoaderWrapper
	{
		private static readonly MethodInfo Method__StartAddon;

		static AddonLoaderWrapper()
		{
			Method__StartAddon = typeof( AddonLoader ).GetMethods( BindingFlags.Instance | BindingFlags.NonPublic ).First( delegate ( MethodInfo method )
			{
				ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length != 4)
					return false;
				if (parameters[0].ParameterType != typeof( AssemblyLoader.LoadedAssembly ))
					return false;
				if (parameters[1].ParameterType != typeof( Type ))
					return false;
				if (parameters[2].ParameterType != typeof( KSPAddon ))
					return false;
				if (parameters[3].ParameterType != typeof( KSPAddon.Startup ))
					return false;
				return true;
			} );
		}

		public static void StartAddon( AssemblyLoader.LoadedAssembly assembly, Type addonType, KSPAddon addon, KSPAddon.Startup startup )
		{
			Method__StartAddon.Invoke( AddonLoader.Instance, new object[] { assembly, addonType, addon, startup } );
		}
	}
}
