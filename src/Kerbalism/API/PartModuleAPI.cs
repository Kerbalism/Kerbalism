using System;
using System.Collections.Generic;
using System.Reflection;

namespace KERBALISM
{
	public static class PartModuleAPI
	{
		/*
		### PARTMODULE BACKGROUND PROCESSING ###

		Add the following method to any partmodule to get background processing for this module :

		public void KerbalismBackgroundUpdate(Vessel v, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, double elapsed_s)

		v : reference to the vessel
		protoPart : the ProtoPartSnapshot the protomodule is on
		protoModule : the ProtoPartModuleSnapshot reference. You can access persisted fields from the ProtoPartModuleSnapshot.moduleValues ConfigNode
		elapsed_s : seconds elapsed since last simulation step

		When the vessel is unloaded, for every persisted ProtoPartModuleSnapshot, Kerbalism will call this method on the module prefab.
		When using the module / part fields or properties from this method, remember that you are using the default values on the prefab :
		- never modify a module or part field / property, only read them
		- be aware that the prefab fields won't be affected by stock upgrades or tweakscale (if you want to use upgrades on a field, it must be persistent)
		- you can read/write persisted fields from the ProtoPartModuleSnapshot.moduleValues
		- you can do resource consumption/production trough the API ConsumeResource() and ProduceResource() methods

		### PARTMODULE PLANNER PROCESSING ###

		The purpose of this method is to allow your module to register a resource production / consumption in the
		Kerbalism planner. It will be called in the editor when the vessel is modified, for every instance of the partmodule.
		See the Planner* methods in the main API to produce/consume resource from this method

		public static void KerbalismPlannerUpdate(PartModule pm, CelestialBody body, double altitude, bool sunlight)

		pm : the instance of your partmodule. You can safely cast it to your module type.
		body : the currently selected body in the planner
		altitude : the simulated vessel altitude
		sunlight : true if the simulated vessel is in sunlight
		*/

		private static Type partModuleType = typeof(PartModule);
		private static Type[] backgroundUpdateSignature = { typeof(Vessel), typeof(ProtoPartSnapshot), typeof(ProtoPartModuleSnapshot), typeof(double) };
		private static Type[] plannerUpdateSignature = { typeof(PartModule), typeof(CelestialBody), typeof(double), typeof(bool) };

		public static Dictionary<PartModule, Action<Vessel, ProtoPartSnapshot, ProtoPartModuleSnapshot, double>> backgroundModules
			= new Dictionary<PartModule, Action<Vessel, ProtoPartSnapshot, ProtoPartModuleSnapshot, double>>();

		public static Dictionary<Type, Action<PartModule, CelestialBody, double, bool>> plannerModules
			= new Dictionary<Type, Action<PartModule, CelestialBody, double, bool>>();

		public static void Init()
		{
			List<Type> backgroundModulesTypes = new List<Type>();
			foreach (Type type in AssemblyLoader.loadedTypes)
			{
				if (partModuleType.IsAssignableFrom(type))
				{
					MethodInfo backgroundMethod = type.GetMethod("KerbalismBackgroundUpdate", backgroundUpdateSignature);
					if (backgroundMethod != null)
					{
						backgroundModulesTypes.Add(type);
					}

					MethodInfo plannerMethod = type.GetMethod("KerbalismPlannerUpdate", plannerUpdateSignature);
					if (plannerMethod != null)
					{
						Action<PartModule, CelestialBody, double, bool> action =
							(Action<PartModule, CelestialBody, double, bool>)Delegate.CreateDelegate(
								typeof(Action<PartModule, CelestialBody, double, bool>), null, "KerbalismPlannerUpdate");

						plannerModules.Add(type, action);
					}
				}
			}

			foreach (AvailablePart ap in PartLoader.LoadedPartsList)
			{
				foreach (PartModule pm in ap.partPrefab.Modules)
				{
					Type pmType = pm.GetType();
					foreach (Type bmType in backgroundModulesTypes)
					{
						if (bmType.IsAssignableFrom(pmType))
						{
							Action<Vessel, ProtoPartSnapshot, ProtoPartModuleSnapshot, double> action =
								(Action<Vessel, ProtoPartSnapshot, ProtoPartModuleSnapshot, double>)Delegate.CreateDelegate(
									typeof(Action<Vessel, ProtoPartSnapshot, ProtoPartModuleSnapshot, double>), pm, "KerbalismBackgroundUpdate");

							backgroundModules.Add(pm, action);
							break;
						}
					}
				}
			}

		}
	}
}
