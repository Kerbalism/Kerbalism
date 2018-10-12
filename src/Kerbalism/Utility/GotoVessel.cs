namespace KERBALISM
{
	public static class GotoVessel
	{
		[KSPField(isPersistant = true)] public static int version = 0;
		public static void JumpToVessel(Vessel v)
		{
			if (Lib.IsFlight())
			{
				FlightGlobals.SetActiveVessel(v);
			}
			else
			{
				int _idx = HighLogic.CurrentGame.flightState.protoVessels.FindLastIndex(pv => pv.vesselID == v.id);

				string _saveGame = GamePersistence.SaveGame("Goto_" + version.ToString(), HighLogic.SaveFolder, SaveMode.OVERWRITE);
				// Keep until 3 backups of Goto
				if (version++ > 3) version = 0;
				if (_idx != -1)
				{
					FlightDriver.StartAndFocusVessel(_saveGame, _idx);
				}
				else
				{
					Lib.Log("Invalid vessel Id:" + _idx);
				}
			}
		}
	}
}
