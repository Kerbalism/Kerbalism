using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public interface ISwitchable
	{
		void OnSwitchActivate();

		void OnSwitchDeactivate();
	}

	public static class B9PSPatcher
	{
		public static void Init(HarmonyInstance harmony)
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "B9PartSwitch")
				{
					Type moduleDataHandlerBasic = a.assembly.GetType("B9PartSwitch.PartSwitch.PartModifiers.ModuleDataHandlerBasic");
					
					var activate = moduleDataHandlerBasic.GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic);
					var activatePatch = typeof(B9PSPatcher).GetMethod(nameof(ActivatePostfix));
					harmony.Patch(activate, null, new HarmonyMethod(activatePatch));

					var deactivate = moduleDataHandlerBasic.GetMethod("Deactivate", BindingFlags.Instance | BindingFlags.NonPublic);
					var deactivatePatch = typeof(B9PSPatcher).GetMethod(nameof(DeactivatePostfix));
					harmony.Patch(deactivate, null, new HarmonyMethod(deactivatePatch));

					break;
				}
			}
		}

		public static void ActivatePostfix(PartModule ___module)
		{
			if (___module is ISwitchable switchableModule)
			{
				Lib.Log($"B9PS : activating {___module.GetType().Name}, id={___module.GetInstanceID()}");
				switchableModule.OnSwitchActivate();
			}
		}

		public static void DeactivatePostfix(PartModule ___module)
		{
			if (___module is ISwitchable switchableModule)
			{
				Lib.Log($"B9PS : deactivating {___module.GetType().Name}, id={___module.GetInstanceID()}");
				switchableModule.OnSwitchDeactivate();
			}
		}
	}
}
