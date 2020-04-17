using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public abstract class ModuleData<TModule, TData> : ModuleData
		where TModule : KsmPartModule<TModule, TData>
		where TData : ModuleData<TModule, TData>
	{
		public TModule loadedModule;

		public override KsmPartModule LoadedModuleBase => loadedModule;

		public TModule modulePrefab;

		public override KsmPartModule PrefabModuleBase => modulePrefab;

		public VesselDataBase VesselData => partData.vesselData;

		public bool IsLoaded => loadedModule != null;

		public override void SetPartModuleReferences(PartModule prefab, KsmPartModule loadedModule)
		{
			modulePrefab = (TModule)prefab;
			if (loadedModule != null)
			{
				this.loadedModule = (TModule)loadedModule;
			}
		}
	}


	public abstract class ModuleData
	{
		public const string VALUENAME_FLIGHTID = "flightId";
		public const string VALUENAME_SHIPID = "shipId";

		public bool moduleIsEnabled;

		public int flightId;

		public int shipId;

		public int ID => flightId != 0 ? flightId : shipId;

		public PartData partData;

		public abstract KsmPartModule LoadedModuleBase { get; }

		public abstract KsmPartModule PrefabModuleBase { get; }

		public abstract void SetPartModuleReferences(PartModule prefab, KsmPartModule loadedModule);

		public virtual void VesselDataUpdate()
		{
			if (!moduleIsEnabled)
				return;

			OnVesselDataUpdate();
		}

		public virtual void Load(ConfigNode node)
		{
			moduleIsEnabled = Lib.ConfigValue(node, "moduleIsEnabled", true);

			OnLoad(node);
		}

		public virtual void Save(ConfigNode node)
		{
			node.AddValue("moduleIsEnabled", moduleIsEnabled);

			OnSave(node);
		}


		public virtual void FirstInstantiate(KsmPartModule module)
		{
			moduleIsEnabled = module.isEnabled;

			OnFirstInstantiate(null, null);
		}

		public virtual void FirstInstantiate(ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule)
		{
			moduleIsEnabled = Lib.Proto.GetBool(protoModule, "isEnabled", true);

			OnFirstInstantiate(protoModule, protoPart);
		}

		public void PartWillDie()
		{
			OnPartWillDie();
			flightModuleDatas.Remove(flightId);
		}

		public virtual void OnFixedUpdate(double elapsedSec) { }

		public virtual void OnVesselDataUpdate() { }

		public virtual void OnLoad(ConfigNode node) { }

		public virtual void OnSave(ConfigNode node) { }

		/// <summary>
		/// This is called when the ModuleData is instantiated : <br/>
		/// - After Load/OnLoad and after all the Part/Module references have been set <br/>
		/// - After VesselData instantiation and loading, after the first VesselData evaluation <br/>
		/// - On loaded parts, after the PartModule Awake() but before its OnStart() <br/>
		/// - On loaded parts, there is no garantee the other parts / other modules will be initialized when this is called
		/// </summary>
		public virtual void OnStart() { }

		/// <summary>
		/// Called when the PartData is instantiated for the first time. <br/>
		/// Override it to initialize the persisted fields according to the prefab configuration.<br/>
		/// This will usually be called in the editor, but it can also happen in flight (ex : EVA kerbals, KIS added part...), <br/>
		/// or even on an unloaded vessel (rescue missions, asteroids...)
		/// </summary>
		/// <param name="protoModule">Only available if the part was created unloaded (rescue, asteroids...)</param>
		/// <param name="protoPart">Only available if the part was created unloaded (rescue, asteroids...)</param>
		public virtual void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule = null, ProtoPartSnapshot protoPart = null) { }

		public virtual void OnPartWillDie() { }

		/// <summary> for every ModuleData derived class name, the constructor delegate </summary>
		private static Dictionary<string, Func<ModuleData>> activatorsByModuleData = new Dictionary<string, Func<ModuleData>>();

		/// <summary> for every KsmPartModule derived class name, the corresponding ModuleData constructor delegate </summary>
		private static Dictionary<string, Func<ModuleData>> activatorsByKsmPartModule = new Dictionary<string, Func<ModuleData>>();

		/// <summary> dictionary of all moduleDatas game-wide, by flightId</summary>
		private static Dictionary<int, ModuleData> flightModuleDatas = new Dictionary<int, ModuleData>();

		/// <summary>
		/// Compile activator delegates for every child class of ModuleData.
		/// This is to avoid the performance hit of Activator.CreateInstance(), the delegate is ~10 times faster.
		/// Must be called by all plugins that contain modules derived from KsmPartModule
		/// </summary>
		public static void Init(Assembly executingAssembly)
		{
			Type moduleDataBaseType = typeof(ModuleData);
			Type ksmPartModuleBaseType = typeof(KsmPartModule);
			Dictionary<string, string> ksmModulesAndDataTypes = new Dictionary<string, string>();
			foreach (Type type in executingAssembly.GetTypes())
			{
				if (type.IsClass && !type.IsAbstract)
				{
					if (moduleDataBaseType.IsAssignableFrom(type))
					{
						ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
						NewExpression newExp = Expression.New(ctor);
						LambdaExpression lambda = Expression.Lambda(typeof(Func<ModuleData>), newExp);
						activatorsByModuleData.Add(type.Name, (Func<ModuleData>)lambda.Compile());
					}

					if (ksmPartModuleBaseType.IsAssignableFrom(type))
					{
						Type moduleDataType = type.GetField("moduleData", BindingFlags.Instance | BindingFlags.Public).FieldType;
						ksmModulesAndDataTypes.Add(moduleDataType.Name, type.Name);
					}
				}
			}

			// add the activators found in this assembly to our registry
			// activatorsByModuleData is static and may contain activators for modules loaded from other assemblies
			foreach (KeyValuePair<string, Func<ModuleData>> activator in activatorsByModuleData)
			{
				if (ksmModulesAndDataTypes.ContainsKey(activator.Key))
				{
					activatorsByKsmPartModule.Add(ksmModulesAndDataTypes[activator.Key], activator.Value);
				}
			}
		}

		public static bool ExistsInFlight(int flightId) => flightModuleDatas.ContainsKey(flightId);

		public static bool TryGetModuleData<TModule, TData>(int flightId, out TData moduleData)
			where TModule : KsmPartModule<TModule, TData>
			where TData : ModuleData<TModule, TData>
		{
			if (flightModuleDatas.TryGetValue(flightId, out ModuleData moduleDataBase))
			{
				moduleData = (TData)moduleDataBase;
				return true;
			}

			moduleData = null;
			return false;
		}

		public static bool TryGetModuleData<TModule, TData>(ProtoPartModuleSnapshot protoModule, out TData moduleData)
			where TModule : KsmPartModule<TModule, TData>
			where TData : ModuleData<TModule, TData>
		{
			int flightId = Lib.Proto.GetInt(protoModule, KsmPartModule.VALUENAME_FLIGHTID, 0);
			
			if (flightModuleDatas.TryGetValue(flightId, out ModuleData moduleDataBase))
			{
				moduleData = (TData)moduleDataBase;
				return true;
			}

			moduleData = null;
			return false;
		}

		public static bool IsKsmPartModule(ProtoPartModuleSnapshot protoModule)
		{
			return activatorsByKsmPartModule.ContainsKey(protoModule.moduleName);
		}

		/// <summary> must be called in OnLoad(), before VesselData are loaded</summary>
		public static void ClearOnLoad()
		{
			flightModuleDatas.Clear();
		}

		// this called by the Part.Start() prefix patch. It's a "catch all" method for all the situations were a
		// part is instantiated in flight. It will ensure that the PartData and ModuleData exists and that the
		// module <> data cross references are set. Common cases :
		// - loaded vessel(s) instantiated after a scene load
		// - previously unloaded vessel entering physics range
		// - KIS created parts
		public static void GetOrCreateFlightModuleData(PartData partData, KsmPartModule ksmPartModule, int moduleIndex)
		{
			if (flightModuleDatas.TryGetValue(ksmPartModule.dataFlightId, out ModuleData moduleData))
			{
				Lib.LogDebug($"Linking {ksmPartModule.GetType().Name} and it's ModuleData, flightId={ksmPartModule.dataFlightId}");
				//moduleData.partData.SetLoadedPartReference(ksmPartModule.part);
				ksmPartModule.ModuleData = moduleData;
				moduleData.SetPartModuleReferences(partData.PartPrefab.Modules[moduleIndex], ksmPartModule);
				Lib.LogDebug($"Starting {moduleData.GetType().Name} on {partData.vesselData.VesselName}");
				moduleData.OnStart();
			}
			else
			{
				Lib.LogDebug($"Flight Moduledata for {ksmPartModule.GetType().Name} hasn't been found after load, instatiating a new one");
				//if (!ksmPartModule.vessel.TryGetVesselData(out VesselData vd))
				//{
				//	Lib.LogDebugStack($"VesselData doesn't exists for vessel {ksmPartModule.vessel.vesselName}, can't instantiate ModuleData !", Lib.LogLevel.Error);
				//	return;
				//}

				//if (!vd.Parts.TryGet(ksmPartModule.part.flightID, out PartData partData))
				//{
				//	partData = vd.Parts.Add(ksmPartModule.part);
				//}
				New(ksmPartModule, moduleIndex, partData, true);
			}
		}

		private static int NewFlightId(ModuleData moduleData)
		{
			int flightId = 0;
			do
			{
				flightId = Guid.NewGuid().GetHashCode();
			}
			while (flightModuleDatas.ContainsKey(flightId) || flightId == 0);

			flightModuleDatas.Add(flightId, moduleData);

			return flightId;
		}

		public static void AssignNewFlightId(ModuleData moduleData)
		{
			int flightId = NewFlightId(moduleData);
			moduleData.flightId = flightId;
			moduleData.LoadedModuleBase.dataFlightId = flightId;
		}

		public static void New(KsmPartModule module, int moduleIndex, PartData partData, bool inFlight)
		{
			Lib.LogDebug($"Constructing moduleData for {module.ModuleDataType.Name} of {module.GetType().Name}");
			ModuleData moduleData = activatorsByModuleData[module.ModuleDataType.Name].Invoke();

			int flightId = 0;
			if (inFlight)
			{
				flightId = NewFlightId(moduleData);
				moduleData.flightId = flightId;
				module.dataFlightId = flightId;
			}

			module.ModuleData = moduleData;
			moduleData.partData = partData;
			moduleData.SetPartModuleReferences(partData.PartPrefab.Modules[moduleIndex], module);
			partData.modules.Add(moduleData);

			moduleData.FirstInstantiate(module);

			Lib.LogDebug($"Starting {module.GetType().Name} on {partData.vesselData.VesselName}");
			moduleData.OnStart();

			Lib.LogDebug($"Instantiated new : {module.ModuleDataType.Name}, flightId={flightId}, shipId={moduleData.LoadedModuleBase.dataShipId} for part {partData.Title}");
		}

		public static void NewFromNode(KsmPartModule module, int moduleIndex, PartData partData, ConfigNode moduleDataNode)
		{
			ModuleData moduleData = activatorsByModuleData[module.ModuleDataType.Name].Invoke();

			int flightId = Lib.ConfigValue(moduleDataNode, VALUENAME_FLIGHTID, 0);
			if (flightId != 0)
			{
				flightModuleDatas.Add(flightId, moduleData);
				moduleData.flightId = flightId;
				module.dataFlightId = flightId;
			}

			module.ModuleData = moduleData;
			moduleData.partData = partData;
			moduleData.SetPartModuleReferences(partData.PartPrefab.Modules[moduleIndex], module);
			partData.modules.Add(moduleData);
			moduleData.Load(moduleDataNode);

			Lib.LogDebug($"Starting {module.GetType().Name} on {partData.vesselData.VesselName}");
			moduleData.OnStart();

			Lib.LogDebug($"Instantiated from ConfigNode : {module.ModuleDataType.Name}, flightId={flightId}, shipId={moduleData.LoadedModuleBase.dataShipId} for part {partData.Title}");
		}

		public static bool New(ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, PartData partData)
		{
			if (!Lib.TryFindModulePrefab(protoPart, protoModule, out PartModule modulePrefab))
				return false;

			if (!activatorsByKsmPartModule.TryGetValue(protoModule.moduleName, out Func<ModuleData> activator))
				return false;

			ModuleData moduleData = activator.Invoke();

			int flightId = NewFlightId(moduleData);
			moduleData.flightId = flightId;
			Lib.Proto.Set(protoModule, VALUENAME_FLIGHTID, flightId);

			moduleData.partData = partData;
			moduleData.SetPartModuleReferences(modulePrefab, null);
			partData.modules.Add(moduleData);

			moduleData.FirstInstantiate(protoPart, protoModule);

			Lib.LogDebug($"Instantiated new {moduleData.GetType().Name} (unloaded), flightId={flightId} for PartData {partData.Title}");
			return true;
		}

		public static bool NewFromNode(ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, PartData partData, ConfigNode moduleDataNode, int flightId = 0)
		{
			if (!Lib.TryFindModulePrefab(protoPart, protoModule, out PartModule modulePrefab))
				return false;

			if (!activatorsByKsmPartModule.TryGetValue(protoModule.moduleName, out Func<ModuleData> activator))
				return false;

			if (flightId == 0)
			{
				flightId = Lib.ConfigValue(moduleDataNode, VALUENAME_FLIGHTID, 0);

				if (flightId == 0)
					return false;
			}

			ModuleData moduleData = activator.Invoke();

			flightModuleDatas.Add(flightId, moduleData);
			moduleData.flightId = flightId;
			moduleData.partData = partData;
			moduleData.SetPartModuleReferences(modulePrefab, null);
			partData.modules.Add(moduleData);
			moduleData.Load(moduleDataNode);

			Lib.LogDebug($"Instantiated from confignode : {moduleData.GetType().Name} (unloaded), flightId={flightId} for PartData {partData.Title}");
			return true;
		}

		public static void SaveModuleDatas(PartDataCollectionBase partDatas, ConfigNode vesselDataNode)
		{
			ConfigNode topNode = vesselDataNode.AddNode(VesselDataBase.NODENAME_MODULE);

			foreach (PartData partData in partDatas)
			{
				foreach (ModuleData moduleData in partData.modules)
				{
					ConfigNode moduleDataNode = topNode.AddNode(Lib.BuildString(partData.Name, "@", moduleData.GetType().Name));

					if (moduleData.flightId != 0)
					{
						moduleDataNode.AddValue(VALUENAME_FLIGHTID, moduleData.flightId);
					}
					else if (moduleData.shipId != 0)
					{
						moduleDataNode.AddValue(VALUENAME_SHIPID, moduleData.shipId);
					}
					else
					{
						Lib.Log($"Can't save ModuleData, both flightId and shipId aren't defined !", Lib.LogLevel.Warning);
						continue;
					}

					moduleData.Save(moduleDataNode);
				}
			}
		}
	}
}
