using System.Reflection;

namespace KERBALISM
{
	internal static class ProcessControllerUiHelper
	{
		private static readonly FieldInfo DumpValveField =
			typeof(ProcessController).GetField("dumpValve", BindingFlags.Instance | BindingFlags.NonPublic);

		internal static string DumpValveTitle(ProcessController module)
		{
			if (module == null)
				return string.Empty;

			DumpSpecs.ActiveValve valve = DumpValveField?.GetValue(module) as DumpSpecs.ActiveValve;
			return valve?.ValveTitle ?? string.Empty;
		}

		internal static void RefreshDumpValveLabel(ProcessController module)
		{
			if (module?.Events == null || !module.Events.Contains("DumpValve"))
				return;

			BaseEvent dumpEvent = module.Events["DumpValve"];
			if (dumpEvent == null || !dumpEvent.active)
				return;

			dumpEvent.guiName = Lib.StatusToggle(Local.ProcessController_Dump, DumpValveTitle(module));
		}
	}
}
