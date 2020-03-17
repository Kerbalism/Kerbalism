using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
public class KsmModuleRadiationCoil : KsmPartModule<KsmModuleRadiationCoil, ModuleRadiationCoilData>
{
		[KSPField(guiActive = true, guiActiveEditor = true)]
		[UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 100f, stepIncrement = 1f)]
		public float coilPower;

		public override void OnStart(StartState state)
		{
			coilPower = moduleData.coilPower;
		}

		public void Update()
		{
			moduleData.coilPower = coilPower;
		}
	}

public class ModuleRadiationCoilData : ModuleData<KsmModuleRadiationCoil, ModuleRadiationCoilData>
{
	public float coilPower;

	// this will be called by VesselData.Evaluate() (in flight, loaded or not)
	public override void OnVesselDataUpdate(VesselData vd)
	{
		//vd.scienceTransmitted += coilPower;
	}

	// this will be called by VesselDataShip.Evaluate() (in the editor)
	public override void OnEditorDataUpdate(VesselDataShip vd)
	{
		//vd.atmoFactor += coilPower;
	}

	public override void OnLoad(ConfigNode node)
	{
		coilPower = Lib.ConfigValue(node, "coilPower", 0f);
	}

	public override void OnSave(ConfigNode node)
	{
		node.AddValue("coilPower", coilPower);
	}
}

	public abstract class KsmPartModule : PartModule
	{
		public const string VALUENAME_SHIPID = "dataShipId";
		public const string VALUENAME_FLIGHTID = "dataFlightId";

		[KSPField(isPersistant = true)]
		public int dataShipId = 0;

		[KSPField(isPersistant = true)]
		public int dataFlightId = 0;

		public abstract ModuleData ModuleData { get; set; }

		public abstract Type ModuleDataType { get; }
	}

	public class KsmPartModule<TModule, TData> : KsmPartModule
		where TModule : KsmPartModule<TModule, TData>
		where TData : ModuleData<TModule, TData>
	{
		protected TData moduleData;

		public override ModuleData ModuleData { get => moduleData; set => moduleData = (TData)value; }

		public override Type ModuleDataType => typeof(TData);

		public void OnDestroy()
		{
			// clear loaded module reference to avoid memory leaks
			if (moduleData != null)
				moduleData.partModule = null;
		}
	}

	public abstract class ModuleData<TModule, TData> : ModuleData
		where TModule : KsmPartModule<TModule, TData>
		where TData : ModuleData<TModule, TData>
	{
		public TModule partModule;

		public override KsmPartModule PartModule { get => partModule; set => partModule = (TModule)value; }
	}


	public abstract class ModuleData
	{
		public const string VALUENAME_FLIGHTID = "flightId";
		public const string VALUENAME_SHIPID = "shipId";

		public int flightId;

		public int shipId;

		public PartData partData;

		public abstract KsmPartModule PartModule { get; set; }

		public virtual void OnVesselDataUpdate(VesselData vd) { }

		public virtual void OnEditorDataUpdate(VesselDataShip vd) { }

		public virtual void OnLoad(ConfigNode node) { }

		public virtual void OnSave(ConfigNode node) { }

		public virtual void SetInstantiateDefaults() { }

		/// <summary> for every ModuleData derived class name, the constructor delegate </summary>
		private static Dictionary<string, Func<ModuleData>> activatorsByModuleData = new Dictionary<string, Func<ModuleData>>();

		/// <summary> for every KsmPartModule derived class name, the corresponding ModuleData constructor delegate </summary>
		private static Dictionary<string, Func<ModuleData>> activatorsByKsmPartModule = new Dictionary<string, Func<ModuleData>>();

		/// <summary> dictionary of all moduleDatas game-wide, keys are unique</summary>
		private static Dictionary<int, ModuleData> flightModuleDatas = new Dictionary<int, ModuleData>();

		/// <summary>
		/// Compile activator delegates for every child class of ModuleData.
		/// This is to avoid the performance hit of Activator.CreateInstance(), the delegate is ~10 times faster.
		/// </summary>
		public static void Init()
		{
			Type moduleDataBaseType = typeof(ModuleData);
			Type ksmPartModuleBaseType = typeof(KsmPartModule);
			Dictionary<string, string> ksmModulesAndDataTypes = new Dictionary<string, string>();
			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
			{
				if (type.IsClass && !type.IsAbstract && moduleDataBaseType.IsAssignableFrom(type))
				{
					ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
					NewExpression newExp = Expression.New(ctor);
					LambdaExpression lambda = Expression.Lambda(typeof(Func<ModuleData>), newExp);
					activatorsByModuleData.Add(type.Name, (Func<ModuleData>)lambda.Compile());
				}

				if (type.IsClass && !type.IsAbstract && ksmPartModuleBaseType.IsAssignableFrom(type))
				{
					Type moduleDataType = type.GetField("moduleData", BindingFlags.Instance | BindingFlags.NonPublic).FieldType;

					ksmModulesAndDataTypes.Add(moduleDataType.Name, type.Name);
				}
			}

			foreach (KeyValuePair<string, Func<ModuleData>> activator in activatorsByModuleData)
			{
				activatorsByKsmPartModule.Add(ksmModulesAndDataTypes[activator.Key], activator.Value);
			}
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

		public static void GetOrCreateFlightModuleData(KsmPartModule ksmPartModule)
		{
			if (flightModuleDatas.TryGetValue(ksmPartModule.dataFlightId, out ModuleData moduleData))
			{
				Lib.LogDebug($"Linking {ksmPartModule.GetType().Name} and it's ModuleData, flightId={ksmPartModule.dataFlightId}");
				ksmPartModule.ModuleData = moduleData;
				moduleData.PartModule = ksmPartModule;
			}
			else
			{
				Lib.LogDebugStack($"Flight Moduledata for {ksmPartModule.GetType().Name} hasn't been found after load, instatiating a new one");
				PartDataCollectionVessel partDatas = ksmPartModule.vessel.KerbalismData().Parts;
				PartData partData;
				if (!partDatas.TryGet(ksmPartModule.part.flightID, out partData))
				{
					partData = partDatas.Add(ksmPartModule.part);
				}
				New(ksmPartModule, partData, true);
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
			moduleData.PartModule.dataFlightId = flightId;
		}

		public static void New(KsmPartModule module, PartData partData, bool inFlight)
		{
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
			moduleData.PartModule = module;
			partData.modules.Add(moduleData);
			moduleData.SetInstantiateDefaults();

			Lib.LogDebug($"Instantiated new : {module.ModuleDataType.Name}, flightId={flightId}, shipId={moduleData.PartModule.dataShipId} for part {partData.PartInfo.title}");
		}

		public static void NewFromNode(KsmPartModule module, PartData partData, ConfigNode moduleDataNode)
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
			moduleData.PartModule = module;
			partData.modules.Add(moduleData);
			moduleData.OnLoad(moduleDataNode);

			Lib.LogDebug($"Instantiated from ConfigNode : {module.ModuleDataType.Name}, flightId={flightId}, shipId={moduleData.PartModule.dataShipId} for part {partData.PartInfo.title}");
		}

		public static bool New(ProtoPartModuleSnapshot protoModule, PartData partData)
		{
			if (!activatorsByKsmPartModule.TryGetValue(protoModule.moduleName, out Func<ModuleData> activator))
				return false;

			ModuleData moduleData = activator.Invoke();

			int flightId = NewFlightId(moduleData);
			moduleData.flightId = flightId;
			Lib.Proto.Set(protoModule, VALUENAME_FLIGHTID, flightId);

			moduleData.partData = partData;
			partData.modules.Add(moduleData);
			moduleData.SetInstantiateDefaults();

			Lib.LogDebug($"Instantiated new {moduleData.GetType().Name} (unloaded), flightId={flightId} for PartData {partData.PartInfo.title}");
			return true;
		}

		public static bool NewFromNode(ProtoPartModuleSnapshot protoModule, PartData partData, ConfigNode moduleDataNode, int flightId = 0)
		{
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
			partData.modules.Add(moduleData);
			moduleData.OnLoad(moduleDataNode);

			Lib.LogDebug($"Instantiated from confignode : {moduleData.GetType().Name} (unloaded), flightId={flightId} for PartData {partData.PartInfo.title}");
			return true;
		}

		public static void SaveModuleDatas(IEnumerable<PartData> partDatas, ConfigNode vesselDataNode)
		{
			ConfigNode topNode = vesselDataNode.AddNode(VesselDataBase.NODENAME_MODULE);

			foreach (PartData partData in partDatas)
			{
				foreach (ModuleData moduleData in partData.modules)
				{
					ConfigNode moduleDataNode = topNode.AddNode(Lib.BuildString(partData.PartInfo.name, "@", moduleData.GetType().Name));

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

					moduleData.OnSave(moduleDataNode);
				}
			}
		}
	}
}
