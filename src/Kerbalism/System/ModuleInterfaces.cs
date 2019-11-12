using System;
namespace KERBALISM
{
	/// <summary>
	/// Modules can implement this interface in case they need to do something
	/// when a new vessel is rolled out to the launchpad / runway.
	/// </summary>
	public interface IModuleRollout
	{
		void OnRollout();
	}
}
