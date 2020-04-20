using KSP.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public static class PartPrefabsPostLoadCompilation
	{
		public static void Compile()
		{
			Dictionary<string, PartListTweak> editorPartListTweaks = GetPartListTweaks();

			foreach (AvailablePart ap in PartLoader.LoadedPartsList)
			{
				// tweak out parts icon size and order in the editors part list
				if (editorPartListTweaks.TryGetValue(ap.name, out PartListTweak partListTweak))
					partListTweak.Apply(ap);

				// add supply resources to pods
				Profile.SetupPod(ap);

				// recompile some part infos (this is normally done by KSP on loading, after each part prefab is compiled)
				// This is needed because :
				// - We can't check interdependent modules when OnLoad() is called, since the other modules may not be loaded yet
				// - The science DB needs the system/bodies to be instantiated, which is done after the part compilation
				bool partInfoNeedRecompile = false;

				foreach (PartModule module in ap.partPrefab.Modules)
				{
					if (ExperimentModuleDefinition.CompileModuleInfo(module))
					{
						partInfoNeedRecompile = true;
					}
					// auto balance hard drive capacity
					else if (module is ModuleKsmDrive hardDrive)
					{
						AutoAssignHardDriveCapacity(ap, hardDrive);
					}
					else if (module.moduleName == B9PartSwitch.moduleName)
					{
						foreach (B9PartSwitch.SubtypeWrapper subtype in B9PartSwitch.GetSubtypes(module))
						{
							foreach (B9PartSwitch.ModuleModifierWrapper moduleModifier in subtype.ModuleModifiers)
							{
								if (moduleModifier.DataNode == null)
									continue;

								// the experiment module implements ISwitchable but it's a special case since
								// we also need to store the module info in the science DB, and analyze the tech
								// at which the module becomes available.
								if (moduleModifier.PartModule is ModuleKsmExperiment switchedExperiment)
								{
									string details = ExperimentModuleDefinition.CompileModuleInfo(switchedExperiment, subtype, moduleModifier);
									if (moduleModifier.ModuleActive && !string.IsNullOrEmpty(details))
									{
										subtype.SetSubTypeDescriptionDetail(details);
									}
								}
								else if (moduleModifier.ModuleActive && moduleModifier.PartModule is ISwitchable switchedModule)
								{
									string description = switchedModule.GetSubtypeDescription(moduleModifier.DataNode);
									if (!string.IsNullOrEmpty(description))
									{
										subtype.SetSubTypeDescriptionDetail(description);
									}
									
								}
							}
						}
					}
				}

				// for some reason this crashes on the EVA kerbal parts
				if (partInfoNeedRecompile && !ap.name.StartsWith("kerbalEVA"))
				{
					ap.moduleInfos.Clear();
					ap.resourceInfos.Clear();
					try
					{
						Lib.ReflectionCall(PartLoader.Instance, "CompilePartInfo", new Type[] { typeof(AvailablePart), typeof(Part) }, new object[] { ap, ap.partPrefab });
					}
					catch (Exception ex)
					{
						Lib.Log("Could not recompile moduleInfo for part " + ap.name + " - " + ex.Message, Lib.LogLevel.Warning);
					}
				}
			}

			foreach (ExperimentModuleDefinition expDef in ScienceDB.ExperimentModuleDefinitions())
			{
				expDef.PopulateSpecIfUndefined();
			}

			foreach (ExperimentInfo expInfo in ScienceDB.ExperimentInfos)
			{
				expInfo.CompileModuleDefinitionsInfo();
			}
		}

		#region EDITOR PART LIST TWEAKS

		private static Dictionary<string, PartListTweak> GetPartListTweaks()
		{
			Dictionary<string, PartListTweak> tweaks = new Dictionary<string, PartListTweak>();

			tweaks.Add("kerbalism-container-inline-prosemian-full-0625", new PartListTweak(0, 0.6f));
			tweaks.Add("kerbalism-container-inline-prosemian-full-125", new PartListTweak(1, 0.85f));
			tweaks.Add("kerbalism-container-inline-prosemian-full-250", new PartListTweak(2, 1.1f));
			tweaks.Add("kerbalism-container-inline-prosemian-full-375", new PartListTweak(3, 1.33f));

			tweaks.Add("kerbalism-container-inline-prosemian-half-125", new PartListTweak(10, 0.85f));
			tweaks.Add("kerbalism-container-inline-prosemian-half-250", new PartListTweak(11, 1.1f));
			tweaks.Add("kerbalism-container-inline-prosemian-half-375", new PartListTweak(12, 1.33f));

			tweaks.Add("kerbalism-container-radial-box-prosemian-small", new PartListTweak(20, 0.6f));
			tweaks.Add("kerbalism-container-radial-box-prosemian-normal", new PartListTweak(21, 0.85f));
			tweaks.Add("kerbalism-container-radial-box-prosemian-large", new PartListTweak(22, 1.1f));

			tweaks.Add("kerbalism-container-radial-pressurized-prosemian-small", new PartListTweak(30, 0.6f));
			tweaks.Add("kerbalism-container-radial-pressurized-prosemian-medium", new PartListTweak(31, 0.85f));
			tweaks.Add("kerbalism-container-radial-pressurized-prosemian-big", new PartListTweak(32, 1.1f));
			tweaks.Add("kerbalism-container-radial-pressurized-prosemian-huge", new PartListTweak(33, 1.33f));

			tweaks.Add("kerbalism-solenoid-short-small", new PartListTweak(40, 0.85f));
			tweaks.Add("kerbalism-solenoid-long-small", new PartListTweak(41, 0.85f));
			tweaks.Add("kerbalism-solenoid-short-large", new PartListTweak(42, 1.33f));
			tweaks.Add("kerbalism-solenoid-long-large", new PartListTweak(43, 1.33f));

			tweaks.Add("kerbalism-greenhouse", new PartListTweak(50));
			tweaks.Add("kerbalism-gravityring", new PartListTweak(51));
			tweaks.Add("kerbalism-activeshield", new PartListTweak(52));
			tweaks.Add("kerbalism-chemicalplant", new PartListTweak(53));

			tweaks.Add("kerbalism-experiment-beep", new PartListTweak(60));
			tweaks.Add("kerbalism-experiment-ding", new PartListTweak(61));
			tweaks.Add("kerbalism-experiment-tick", new PartListTweak(62));
			tweaks.Add("kerbalism-experiment-wing", new PartListTweak(63));
			tweaks.Add("kerbalism-experiment-curve", new PartListTweak(64));

			return tweaks;
		}

		private class PartListTweak
		{
			private int listOrder;
			private float iconScale;

			public PartListTweak(float iconScale)
			{
				listOrder = -1;
				this.iconScale = iconScale;
			}

			public PartListTweak(int listOrder, float iconScale = 1f)
			{
				this.listOrder = listOrder;
				this.iconScale = iconScale;
			}

			public void Apply(AvailablePart ap)
			{
				if (iconScale != 1f)
				{
					ap.iconPrefab.transform.GetChild(0).localScale *= iconScale;
					ap.iconScale *= iconScale;
				}

				if (listOrder >= 0)
				{
					ap.title = Lib.BuildString("<size=1><color=#00000000>" + listOrder.ToString("000") + "</color></size>", ap.title);
				}
			}
		}

		#endregion

		/// <summary> Auto-Assign hard drive storage capacity based on the parts position in the tech tree and part cost </summary>
		private static void AutoAssignHardDriveCapacity(AvailablePart ap, ModuleKsmDrive hardDrive)
		{
			// don't touch drives assigned to an experiment
			if (!string.IsNullOrEmpty(hardDrive.experiment_id))
				return;

			// no auto-assigning necessary
			if (hardDrive.sampleCapacity != ModuleKsmDrive.CAPACITY_AUTO && hardDrive.dataCapacity != ModuleKsmDrive.CAPACITY_AUTO)
				return;

			// get cumulative science cost for this part
			double maxScienceCost = 0;
			double tier = 1.0;
			double maxTier = 1.0;

			// find start node and max. science cost
			ProtoRDNode node = null;
			ProtoRDNode maxNode = null;

			foreach (var n in AssetBase.RnDTechTree.GetTreeNodes())
			{
				if (n.tech.scienceCost > maxScienceCost)
				{
					maxScienceCost = n.tech.scienceCost;
					maxNode = n;
				}
				if (ap.TechRequired == n.tech.techID)
					node = n;
			}

			if (node == null)
			{
				Lib.Log($"{ap.partPrefab.partInfo.name}: part not found in tech tree, skipping auto assignment", Lib.LogLevel.Warning);
				return;
			}

			// add up science cost from start node and all the parents
			// (we ignore teh requirement to unlock multiple nodes before this one)
			while (node.parents.Count > 0)
			{
				tier++;
				node = node.parents[0];
			}

			// determine max science cost and max tier
			while (maxNode.parents.Count > 0)
			{
				maxTier++;
				maxNode = maxNode.parents[0];
			}

			// see https://www.desmos.com/calculator/9oiyzsdxzv
			//
			// f = (tier / max. tier)^3
			// capacity = f * max. capacity
			// max. capacity factor 3GB (remember storages can be tweaked to 4x the base size, deep horizons had 8GB)
			// with the variation effects, this caps out at about 10GB.

			// add some part variance based on part cost
			var t = tier - 1;
			t += (ap.cost - 5000) / 10000;

			double f = Math.Pow(t / maxTier, 3);
			double dataCapacity = f * 3000;

			dataCapacity = (int)(dataCapacity * 4) / 4.0; // set to a multiple of 0.25
			if (dataCapacity > 2)
				dataCapacity = (int)(dataCapacity * 2) / 2; // set to a multiple of 0.5
			if (dataCapacity > 5)
				dataCapacity = (int)(dataCapacity); // set to a multiple of 1
			if (dataCapacity > 25)
				dataCapacity = (int)(dataCapacity / 5) * 5; // set to a multiple of 5
			if (dataCapacity > 250)
				dataCapacity = (int)(dataCapacity / 25) * 25; // set to a multiple of 25
			if (dataCapacity > 250)
				dataCapacity = (int)(dataCapacity / 50) * 50; // set to a multiple of 50
			if (dataCapacity > 1000)
				dataCapacity = (int)(dataCapacity / 250) * 250; // set to a multiple of 250

			dataCapacity = Math.Max(dataCapacity, 0.25); // 0.25 minimum

			double sampleCapacity = tier / maxTier * 8;
			sampleCapacity = Math.Max(sampleCapacity, 1); // 1 minimum

			if (hardDrive.dataCapacity == ModuleKsmDrive.CAPACITY_AUTO)
			{
				hardDrive.dataCapacity = dataCapacity;
				Lib.Log($"{ap.partPrefab.partInfo.name}: tier {tier}/{maxTier} part cost {ap.cost.ToString("F0")} data cap. {dataCapacity.ToString("F2")}", Lib.LogLevel.Message);
			}
			if (hardDrive.sampleCapacity == ModuleKsmDrive.CAPACITY_AUTO)
			{
				hardDrive.sampleCapacity = (int)Math.Round(sampleCapacity);
				Lib.Log($"{ap.partPrefab.partInfo.name}: tier {tier}/{maxTier} part cost {ap.cost.ToString("F0")} sample cap. {hardDrive.sampleCapacity}", Lib.LogLevel.Message);
			}
		}
	}

}
