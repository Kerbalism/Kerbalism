#if !KSP15_16
using Expansions.Serenity.DeployedScience.Runtime;

namespace KERBALISM
{
	public class CommHandlerCommNetSerenity : CommHandlerCommNetBase
	{
		private DeployedScienceCluster cluster;

		protected override void UpdateInputs(ConnectionInfo connection)
		{
			connection.transmitting = vd.filesTransmitted.Count > 0;
			connection.storm = vd.EnvStorm;

			if (cluster == null)
				cluster = Serenity.GetScienceCluster(vd.Vessel);

			connection.ec = 0.0;
			connection.ec_idle = 0.0;

			if (cluster == null)
			{
				baseRate = 0.0;
				connection.powered = false;
			}
			else
			{
				baseRate = Settings.DataRateSurfaceExperiment;
				connection.powered = cluster.IsPowered;
			}
		}
	}
}
#endif
