using KERBALISM.Planner;

namespace KERBALISM
{
	public interface IPlannerModule
	{
		/// <summary> This will be called by Kerbalism in the editor (VAB/SPH), you can implement it to consume or produce resources</summary>
		void PlannerUpdate(VesselResHandler resHandler, EnvironmentAnalyzer environment, VesselAnalyzer vessel);
	}
}
