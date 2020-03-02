using System;
using System.Collections.Generic;
using System.Linq;

namespace KERBALISM
{
	public class CommHandlerEditor
	{
		public static Action<ConnectionInfoEditor> APIHandlerUpdate;
		public static bool APIHandlerEnabled = false;

		public static CommHandlerEditor GetHandler()
		{
			if (APIHandlerEnabled && APIHandlerUpdate != null)
				return new CommHandlerEditor();
			else if (RemoteTech.Installed)
				return new CommHandlerEditorRemoteTech();
			else if (CommNet.CommNetScenario.CommNetEnabled)
				return new CommHandlerEditorCommNet();
			else
				return null;
		}

		public void Update(ConnectionInfoEditor connection, double minHomeDistance, double maxHomeDistance)
		{
			connection.minDsnDistance = minHomeDistance;
			connection.maxDsnDistance = maxHomeDistance;

			double maxlevel = ScenarioUpgradeableFacilities.GetFacilityLevelCount(SpaceCenterFacility.TrackingStation);
			if (maxlevel <= 0.0) maxlevel = 2.0;
			connection.dsnLevel = (int)Math.Round(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) * maxlevel + 1);

			if (APIHandlerEnabled && APIHandlerUpdate != null)
				APIHandlerUpdate(connection);
			else if (CommNet.CommNetScenario.CommNetEnabled)
				UpdateConnection(connection);
		}

		public virtual void UpdateConnection(ConnectionInfoEditor connection) { }
	}
}
