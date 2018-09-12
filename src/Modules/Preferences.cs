using System;
using System.Reflection;
using KSP.IO;

namespace KerbalsMeanBusiness.Modules
{
	public class Settings : GameParameters.CustomParameterNode
	{
		private static Settings instance;

		public static Settings Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{
						instance = HighLogic.CurrentGame.Parameters.CustomParams<Settings>();
					}
				}
				return instance;
			}
		}

		[GameParameters.CustomParameterUI("Enable Reliability",
										 toolTip = "Component malfunctions and critical failures")]
		public bool enableReliability = true;

		[GameParameters.CustomParameterUI("Deployment requires EC",
		                                  toolTip = "Add EC cost to keep modules working.\nAdds EC cost to Extend/Retract landing gears")]
		public bool enableDeploy = true;

		[GameParameters.CustomParameterUI("Kerbalistic Science",
		                                  toolTip = "Replace stock science data storage, transmission and analysis")]
		public bool enableScience = true;

		[GameParameters.CustomParameterUI("Space Weather",
		                                  toolTip = "Simulate coronal mass ejections")]
		public bool enableSpaceWeather = true;

		[GameParameters.CustomParameterUI("Automation",
		                                  toolTip = "Control vessel components using an on-board computer")]
		public bool enableAutomation = true;

		public override GameParameters.GameMode GameMode
		{
			get
			{
				return GameParameters.GameMode.ANY;
			}
		}

		public override bool HasPresets
		{
			get
			{
				return false;
			}
		}

		public override string DisplaySection { get { return "Kerbalism"; } }

		public override string Section
		{
			get
			{
				return "Kerbalism";
			}
		}

		public override int SectionOrder
		{
			get
			{
				return 0;
			}
		}

		public override string Title
		{
			get
			{
				return "CONFIG";
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			instance = null;
		}

	}
}
