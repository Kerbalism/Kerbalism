using Harmony;
using System;
using System.Collections;
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

		string GetSubtypeDescription(ConfigNode subtypeDataNode);
	}

	public static class B9PartSwitch
	{
		public const string moduleName = "ModuleB9PartSwitch";
		private static Type moduleType;
		private static FieldInfo moduleSubtypesField;

		private static Type subTypeType;
		private static FieldInfo subtypeNameField;
		private static FieldInfo subtypeDescriptionDetailField;
		private static FieldInfo subtypeUpgradeRequiredField;
		private static FieldInfo subtypeModuleModifierInfosField;

		private static Type moduleModifierInfoType;
		private static FieldInfo moduleModifierIdentifierNodeField;
		private static FieldInfo moduleModifierDataNodeField;
		private static FieldInfo moduleModifierModuleActiveField;

		private static Type moduleMatcherType;
		private static ConstructorInfo moduleMatcherCtor;
		private static MethodInfo moduleMatcherFindModuleMethod;

		public static void Init(HarmonyInstance harmony)
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "B9PartSwitch")
				{
					moduleType = a.assembly.GetType("B9PartSwitch.ModuleB9PartSwitch");
					moduleSubtypesField = moduleType.GetField("subtypes");

					subTypeType = a.assembly.GetType("B9PartSwitch.PartSubtype");
					subtypeNameField = subTypeType.GetField("subtypeName");
					subtypeDescriptionDetailField = subTypeType.GetField("descriptionDetail");
					subtypeUpgradeRequiredField = subTypeType.GetField("upgradeRequired");
					subtypeModuleModifierInfosField = subTypeType.GetField("moduleModifierInfos");

					moduleModifierInfoType = a.assembly.GetType("B9PartSwitch.ModuleModifierInfo");
					moduleModifierIdentifierNodeField = moduleModifierInfoType.GetField("identifierNode");
					moduleModifierDataNodeField = moduleModifierInfoType.GetField("dataNode");
					moduleModifierModuleActiveField = moduleModifierInfoType.GetField("moduleActive");

					moduleMatcherType = a.assembly.GetType("B9PartSwitch.ModuleMatcher");

					// public ModuleMatcher(ConfigNode identifierNode)
					Type[] moduleMatcherCtorArgs = new Type[] { typeof(ConfigNode) };
					moduleMatcherCtor = moduleMatcherType.GetConstructor(new Type[] { typeof(ConfigNode) });

					// public PartModule FindModule(Part part)
					Type[] findModuleArgs = new Type[] { typeof(Part) };
					moduleMatcherFindModuleMethod = moduleMatcherType.GetMethod("FindModule", findModuleArgs);

					Type moduleDataHandlerBasic = a.assembly.GetType("B9PartSwitch.PartSwitch.PartModifiers.ModuleDataHandlerBasic");
					
					var activate = moduleDataHandlerBasic.GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic);
					var activatePatch = typeof(B9PartSwitch).GetMethod(nameof(ActivatePostfix));
					harmony.Patch(activate, null, new HarmonyMethod(activatePatch));

					var deactivate = moduleDataHandlerBasic.GetMethod("Deactivate", BindingFlags.Instance | BindingFlags.NonPublic);
					var deactivatePatch = typeof(B9PartSwitch).GetMethod(nameof(DeactivatePostfix));
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

		public static IEnumerable<SubtypeWrapper> GetSubtypes(PartModule moduleB9PartSwitch)
		{
			foreach (object subtype in GetSubTypes(moduleB9PartSwitch))
			{
				yield return new SubtypeWrapper(moduleB9PartSwitch, subtype);
			}
		}

		private static IList GetSubTypes(PartModule moduleB9PartSwitch)
		{
			return (IList)moduleSubtypesField.GetValue(moduleB9PartSwitch);
		}

		public class SubtypeWrapper
		{
			private object instance;
			private PartModule moduleB9PartSwitch;
			public string Name { get; private set; }
			public string TechRequired { get; private set; }

			public SubtypeWrapper(PartModule moduleB9PartSwitch, object subtype)
			{
				this.moduleB9PartSwitch = moduleB9PartSwitch;
				instance = subtype;
				Name = (string)subtypeNameField.GetValue(subtype);

				string upgradeName = (string)subtypeUpgradeRequiredField.GetValue(subtype);
				if (string.IsNullOrEmpty(upgradeName))
				{
					TechRequired = string.Empty;
				}
				else
				{
					PartUpgradeHandler.Upgrade upgrade = PartUpgradeManager.Handler.GetUpgrade(upgradeName);
					if (upgrade != null)
					{
						TechRequired = upgrade.techRequired;
					}
					else
					{
						TechRequired = string.Empty;
					}
				}
			}

			public IEnumerable<ModuleModifierWrapper> ModuleModifiers
			{
				get
				{
					foreach (object modifier in GetModuleModifiers())
					{
						yield return new ModuleModifierWrapper(moduleB9PartSwitch, modifier);
					}
				}
			}

			private IList GetModuleModifiers()
			{
				return (IList)subtypeModuleModifierInfosField.GetValue(instance);
			}

			public void SetSubTypeDescriptionDetail(string descriptionDetail)
			{
				subtypeDescriptionDetailField.SetValue(instance, descriptionDetail);
			}
		}

		public class ModuleModifierWrapper
		{
			public PartModule PartModule { get; private set; }
			public bool ModuleActive { get; private set; }
			public ConfigNode DataNode { get; private set; }

			public ModuleModifierWrapper(PartModule moduleB9PartSwitch, object moduleModifier)
			{
				ConfigNode identiferNode = (ConfigNode)moduleModifierIdentifierNodeField.GetValue(moduleModifier);

				// public ModuleMatcher(ConfigNode identifierNode)
				object[] moduleMatcherCtorParams = new object[] { identiferNode };
				object moduleMatcher = moduleMatcherCtor.Invoke(moduleMatcherCtorParams);

				// public PartModule FindModule(Part part)
				object[] moduleMatcherFindModuleMethodParams = new object[] { moduleB9PartSwitch.part };
				PartModule = (PartModule)moduleMatcherFindModuleMethod.Invoke(moduleMatcher, moduleMatcherFindModuleMethodParams);

				ModuleActive = (bool)moduleModifierModuleActiveField.GetValue(moduleModifier);
				DataNode = (ConfigNode)moduleModifierDataNodeField.GetValue(moduleModifier);
			}
		}
	}
}
