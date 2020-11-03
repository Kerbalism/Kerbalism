using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace KERBALISM
{
	/*
	 OVERVIEW OF THE RESOURCE SIM :
	- For each vessel or shipconstruct, a VesselResHandler is instantiated
	- It contains a dictionary of all individual resource handlers (one vessel-wide handler per resource)
	- Resource handlers are either a Virtualresource or VesselResource. The IResource interface hide the implementation details
	- The IResource interface provide Consume() and Produce() methods for direct consumptions/productions
	- The VesselResHandler provide a AddRecipe() method for defining input/output processes
	- Each resource handler store consumption/production sum in a "deferred" variable

	IMPLEMENTATION DETAILS :
	- VesselResHandler and VesselResource rely on a PartResourceWrapper abstract class that is used to hide
	  the difference between the KSP PartResource and ProtoPartResourceSnapshot classes
	- One of the reason this implementation is spread over multiple classes is because the KSP PartResourceList class is actually
	  a dictionary, meaning that every iteration over the Part.Resources property instantiate a new list from the
	  dictionary values, which is a huge performance/memory garbage issue since we need at least 2
	  "foreach resource > foreach part > foreach partresource" loops (first to get amount/capacity, then to update amounts).
	  To solve this, we do a single "foreach part > foreach partresource" loop and save a List of PartResource object references,
	  then iterate over that list.
	- Another constraint is that the VesselResHandler must be available from PartModule.Start()/OnStart(), and that
	  the VesselResHandler and resource handlers references must stay the same when the vessel is changing state (loaded <> unloaded)
	

	OVERVIEW OF A SIMULATION STEP :
	- Direct Consume()/Produce() calls from partmodules and other parts of Kerbalism are accumulated in the resource handler "deferred"
	- Recipe objects are created trough AddRecipe() from partmodules and other parts of Kerbalism
	- VesselResHandler.Update() is called : 
	  - All Recipes are executed, and the inputs/outputs added in each resource "deferred".
	  - Each resource amount/capacity (from previous step) is saved
	  - All parts are iterated upon, the KSP part resource object reference is saved in each resource handler,
	    and the new amount and capacity for each resource is calculated from each part resource object.
	  - If amount has changed, this mean there is non-Kerbalism producers/consumers on the vessel
	  - If non-Kerbalism producers are detected on a loaded vessel, we prevent high timewarp rates
	  - For each resource handler :
	    - clamp "deferred" to total amount/capacity
		- distribute "deferred" amongst all part resource
		- add "deferred" to total amount
		- calculate rate of change per-second
		- calculate resource level
		- reset "deferred"

	NOTE
	It is impossible to guarantee coherency in resource simulation of loaded vessels,
	if consumers/producers external to the resource cache exist in the vessel (#96).
	The effect is that the whole resource simulation become dependent on timestep again.
	From the user point-of-view, there are two cases:
	- (A) the timestep-dependent error is smaller than capacity
	- (B) the timestep-dependent error is bigger than capacity
	In case [A], there are no consequences except a slightly wrong computed level and rate.
	In case [B], the simulation became incoherent and from that point anything can happen,
	like for example insta-death by co2 poisoning or climatization.
	To avoid the consequences of [B]:
	- we hacked the solar panels to use the resource cache (SolarPanelFixer)
	- we detect incoherency on loaded vessels, and forbid the two highest warp speeds
	*/

	public class VesselResHandler
	{
		private static Dictionary<string, int> allResourcesIds = new Dictionary<string, int>();
		private static HashSet<int> usedIds = new HashSet<int>();
		private static HashSet<string> allKSPResources = new HashSet<string>();
		private static bool isSetupDone = false;

		public static void SetupDefinitions()
		{
			// note : KSP resources ids are garanteed to be unique because the are stored in a dictionary in
			// PartResourceDefinitionList, but that id is obtained by calling GetHashCode() on the resource name,
			// and there is no check for the actual uniqueness of it. If that happen, the Dictionary.Add() call
			// will just throw an exception, there isn't any handling of it.
			foreach (PartResourceDefinition resDefinition in PartResourceLibrary.Instance.resourceDefinitions)
			{
				if (!allResourcesIds.ContainsKey(resDefinition.name))
				{
					allResourcesIds.Add(resDefinition.name, resDefinition.id);
					allKSPResources.Add(resDefinition.name);
					usedIds.Add(resDefinition.id);
				}
			}

			foreach (VirtualResourceDefinition vResDefinition in VirtualResourceDefinition.definitions.Values)
			{
				do vResDefinition.id = Lib.RandomInt();
				while (usedIds.Contains(vResDefinition.id));

				allResourcesIds.Add(vResDefinition.name, vResDefinition.id);
				usedIds.Add(vResDefinition.id);
			}

			isSetupDone = true;
		}

		public static int GetVirtualResourceId(string resName)
		{
			// make sure we don't affect an id before we have populated the KSP resources ids
			if (!isSetupDone)
				return 0;

			int id;
			do id = Lib.RandomInt();
			while (usedIds.Contains(id));

			allResourcesIds.Add(resName, id);
			usedIds.Add(id);

			return id;
		}

		public enum VesselState { Loaded, Unloaded, EditorInit, EditorStep, EditorFinalize}
		private VesselState currentState;

		public VesselKSPResource ElectricCharge { get; protected set; }

		public Dictionary<string, double> APIResources = new Dictionary<string, double>();

		private List<Recipe> recipes = new List<Recipe>(4);
		private Dictionary<string, VesselResource> resources = new Dictionary<string, VesselResource>(16);
		private Dictionary<int, ResourceWrapper> resourceWrappers = new Dictionary<int, ResourceWrapper>(16);

		public VesselResHandler(object vesselOrProtoVessel, VesselState state)
		{
			currentState = state;

			switch (state)
			{
				case VesselState.Loaded:
					SyncPartResources(((Vessel)vesselOrProtoVessel).parts);
					break;
				case VesselState.Unloaded:
					SyncPartResources(((ProtoVessel)vesselOrProtoVessel).protoPartSnapshots);
					break;
				case VesselState.EditorStep:
				case VesselState.EditorInit:
				case VesselState.EditorFinalize:
					SyncEditorPartResources(EditorLogic.fetch?.ship.parts);
					break;
			}

			if (!resources.TryGetValue("ElectricCharge", out VesselResource ecRes))
			{
				AddKSPResource("ElectricCharge");
				ecRes = resources["ElectricCharge"];
			}
			ElectricCharge = (VesselKSPResource)ecRes;

			foreach (VesselResource resource in resources.Values)
			{
				resource.Init();
				APIResources.Add(resource.Name, resource.Amount);
			}
		}

		/// <summary>return the VesselResource for this resource or create a VesselVirtualResource if the resource doesn't exists</summary>
		public VesselResource GetResource(string resourceName)
		{
			// try to get the resource
			if (resources.TryGetValue(resourceName, out VesselResource resource))
			{
				return resource;
			}
			// if not found, and it's a KSP resource, create it
			if (allKSPResources.Contains(resourceName))
			{
				AddKSPResource(resourceName);
				resource = resources[resourceName];
			}
			// otherwise create a virtual resource
			else
			{
				resource = AutoCreateVirtualResource(resourceName);
			}

			return resource;
		}

		private ResourceWrapper AddKSPResource(string resourceName)
		{
			ResourceWrapper wrapper;
			switch (currentState)
			{
				case VesselState.Loaded:
					wrapper = new LoadedResourceWrapper();
					break;
				case VesselState.Unloaded:
					wrapper = new ProtoResourceWrapper();
					break;
				case VesselState.EditorStep:
				case VesselState.EditorInit:
				case VesselState.EditorFinalize:
					wrapper = new EditorResourceWrapper();
					break;
				default:
					wrapper = null;
					break;
			}

			int id = allResourcesIds[resourceName];
			resourceWrappers.Add(id, wrapper);
			resources.Add(resourceName, new VesselKSPResource(resourceName, id, wrapper));
			return wrapper;
		}

		private VesselResource AutoCreateVirtualResource(string name)
		{
			if (!VirtualResourceDefinition.definitions.TryGetValue(name, out VirtualResourceDefinition definition))
			{
				definition = VirtualResourceDefinition.GetOrCreateDefinition(name, false, VirtualResourceDefinition.ResType.VesselResource);
			}

			switch (definition.resType)
			{
				case VirtualResourceDefinition.ResType.PartResource:
					return GetOrCreateVirtualResource<VesselVirtualPartResource>(name);
				case VirtualResourceDefinition.ResType.VesselResource:
					return GetOrCreateVirtualResource<VesselVirtualResource>(name);
			}
			return null;
		}

		/// <summary>Get the VesselResource for this resource, returns false if that resource doesn't exist or isn't of the asked type</summary>
		public bool TryGetResource<T>(string resourceName, out T resource) where T : VesselResource
		{
			if (resources.TryGetValue(resourceName, out VesselResource baseResource))
			{
				resource = baseResource as T;
				return resource != null;
			}
			resource = null;
			return false;
		}

		/// <summary> Get-or-create a VesselVirtualPartResource or VesselVirtualResource with a random unique name </summary>
		public T GetOrCreateVirtualResource<T>() where T : VesselResource
		{
			string id;
			do id = Guid.NewGuid().ToString();
			while (allResourcesIds.ContainsKey(id));

			return GetOrCreateVirtualResource<T>(id);
		}

		/// <summary> Get-or-create a VesselVirtualPartResource or VesselVirtualResource with the specified name </summary>
		public T GetOrCreateVirtualResource<T>(string name) where T : VesselResource
		{
			if (resources.TryGetValue(name, out VesselResource baseExistingResource))
			{
				if (!(baseExistingResource is T existingResource))
				{
					Lib.Log($"Can't create the {typeof(T).Name} `{name}`, a VesselResource of type {baseExistingResource.GetType().Name} with that name exists already", Lib.LogLevel.Error);
					return null;
				}
				else
				{
					return existingResource;
				}
			}
			else
			{
				if (typeof(T) == typeof(VesselVirtualResource))
				{
					VesselResource resource = new VesselVirtualResource(name);
					resources.Add(name, resource);
					return (T)resource;
				}
				else
				{
					VirtualResourceWrapper wrapper = new VirtualResourceWrapper();
					VesselVirtualPartResource partResource = new VesselVirtualPartResource(wrapper, name);
					resourceWrappers.Add(partResource.Definition.id, wrapper);
					resources.Add(name, partResource);
					return (T)(VesselResource)partResource;
				}
			}
		}

		/// <summary> record deferred production of a resource (shortcut) </summary>
		/// <param name="broker">short ui-friendly name for the producer</param>
		public void Produce(string resource_name, double quantity, ResourceBroker broker)
		{
			GetResource(resource_name).Produce(quantity, broker);
		}

		/// <summary> record deferred consumption of a resource (shortcut) </summary>
		/// <param name="broker">short ui-friendly name for the consumer</param>
		public void Consume(string resource_name, double quantity, ResourceBroker broker)
		{
			GetResource(resource_name).Consume(quantity, broker);
		}

		/// <summary> record deferred execution of a recipe (shortcut) </summary>
		public void AddRecipe(Recipe recipe)
		{
			recipes.Add(recipe);
		}

		public void ResourceUpdate(VesselDataBase vd, object vesselOrProtoVessel, VesselState state, double elapsed_s)
		{
			Vessel vessel = null;
			ProtoVessel protoVessel = null;
			switch (state)
			{
				case VesselState.Loaded:
					vessel = (Vessel)vesselOrProtoVessel;
					break;
				case VesselState.Unloaded:
					protoVessel = (ProtoVessel)vesselOrProtoVessel;
					break;
			}


			// if the vessel state has changed between loaded and unloaded, rebuild the resource wrappers
			if ((state == VesselState.Loaded || state == VesselState.Unloaded) && state != currentState)
			{
				Lib.LogDebug($"State changed for {(vessel != null ? vessel.vesselName : protoVessel?.vesselName)} from {currentState.ToString()} to {state.ToString()}, rebuilding resource wrappers");
				currentState = state;
				foreach (string resourceName in allKSPResources)
				{
					int resId = allResourcesIds[resourceName];
					if (!resourceWrappers.TryGetValue(resId, out ResourceWrapper oldWrapper))
						continue;

					ResourceWrapper newWrapper;
					switch (state)
					{
						case VesselState.Loaded:
							newWrapper = new LoadedResourceWrapper(oldWrapper);
							break;
						case VesselState.Unloaded:
							newWrapper = new ProtoResourceWrapper(oldWrapper);
							break;
						default:
							newWrapper = null;
							break;
					}

					resourceWrappers[resId] = newWrapper;
					((VesselKSPResource)resources[resourceName]).SetWrapper(newWrapper);
				}
			}
			// else just reset amount, capacity, and the part/protopart resource object references,
			// excepted when this is an editor simulation step
			else
			{
				foreach (ResourceWrapper resourceWrapper in resourceWrappers.Values)
					resourceWrapper.ClearPartResources(state != VesselState.EditorStep);
			}

			// note : editor handling here is quite a mess :
			// - we reset "real" resource on finalize step because we want the craft amounts to be displayed, instead of the amounts resulting from the simulation
			// - we don't synchronize "real" ressources on simulation steps so the sim can be accurate
			// - but we do it for virtual resources because the thermal system rely on setting the per-part amount between each step
			// To solve this, we should work on a copy of the part resources, some sort of "simulation snapshot".
			// That would allow to remove all the special handling, and ensure that the editor sim is accurate.
			switch (state)
			{
				case VesselState.Loaded:
					SyncVirtualResources(vd);
					SyncPartResources(vessel.parts);
					break;
				case VesselState.Unloaded:
					SyncVirtualResources(vd);
					SyncPartResources(protoVessel.protoPartSnapshots);
					break;
				case VesselState.EditorStep:
					SyncVirtualResources(vd); 
					break;
				case VesselState.EditorInit:
					SyncVirtualResources(vd);
					SyncEditorPartResources(EditorLogic.fetch.ship.parts);
					break;
				case VesselState.EditorFinalize:
					SyncVirtualResources(vd);
					SyncEditorPartResources(EditorLogic.fetch.ship.parts);
					return;
			}

			// execute all recorded recipes
			switch (state)
			{
				case VesselState.Loaded:
					Recipe.ExecuteRecipes(this, recipes, vessel);
					break;
				case VesselState.Unloaded:
					Recipe.ExecuteRecipes(this, recipes, protoVessel.vesselRef);
					break;
				case VesselState.EditorStep:
					Recipe.ExecuteRecipes(this, recipes, null);
					break;
			}

			// forget the recipes
			recipes.Clear();

			// apply all deferred requests and synchronize to vessel
			foreach (VesselResource resource in resources.Values)
			{
				// note : we try to exclude resources that aren't relevant here to save some
				// performance, but this might have minor side effects, like brokers not being reset
				// after a vessel part count change for example. 
				if (!resource.NeedUpdate)
					continue;

				if (resource.ExecuteAndSyncToParts(vd, elapsed_s) && vessel != null && vessel.loaded)
					CoherencyWarning(vessel, resource.Name);

				APIResources[resource.Name] = resource.Amount;
			}

		}

		public void ConvertShipHandlerToVesselHandler()
		{
			foreach (string resourceName in allKSPResources)
			{
				int resId = allResourcesIds[resourceName];
				if (resourceWrappers.TryGetValue(resId, out ResourceWrapper oldWrapper))
				{
					ResourceWrapper newWrapper = new LoadedResourceWrapper(oldWrapper);
					resourceWrappers[resId] = newWrapper;
					((VesselKSPResource)resources[resourceName]).SetWrapper(newWrapper);
				}
			}
		}


		private void SyncEditorPartResources(List<Part> parts)
		{
			if (parts == null)
				return;

			foreach (Part p in parts)
			{
				// note : due to the list/dict hybrid implementation of PartResourceList,
				// don't use foreach on it to avoid heap memory allocs
				for (int i = 0; i < p.Resources.Count; i++)
				{
					PartResource r = p.Resources[i];
#if DEBUG_RESOURCES
					// Force view all resource in Debug Mode
					r.isVisible = true;
#endif

					if (!r.flowState)
						continue;

					if (!resourceWrappers.TryGetValue(r.info.id, out ResourceWrapper wrapper))
						wrapper = AddKSPResource(r.resourceName);

					((EditorResourceWrapper)wrapper).AddPartresources(r);
				}
			}
		}

		private void SyncPartResources(List<Part> parts)
		{
			foreach (Part p in parts)
			{
				// note : due to the list/dict hybrid implementation of PartResourceList,
				// don't use foreach on it to avoid heap memory allocs
				for (int i = 0; i < p.Resources.Count; i++)
				{
					PartResource r = p.Resources[i];
#if DEBUG_RESOURCES
					// Force view all resource in Debug Mode
					r.isVisible = true;
#endif

					if (!r.flowState)
						continue;

					if (!resourceWrappers.TryGetValue(r.info.id, out ResourceWrapper wrapper))
						wrapper = AddKSPResource(r.resourceName);

					((LoadedResourceWrapper)wrapper).AddPartresources(r);
				}
			}
		}

		// note : this can fail if called on a vessel with a resource that was removed (mod uninstalled),
		// since the resource is serialized on the protovessel, but doesn't exist in the resource library
		private void SyncPartResources(List<ProtoPartSnapshot> protoParts)
		{
			foreach (ProtoPartSnapshot p in protoParts)
			{
				foreach (ProtoPartResourceSnapshot r in p.resources)
				{
					if (!r.flowState)
						continue;

					if (!resourceWrappers.TryGetValue(r.definition.id, out ResourceWrapper wrapper))
						wrapper = AddKSPResource(r.resourceName);

					((ProtoResourceWrapper)wrapper).AddPartresources(r);
				}
			}
		}

		private void SyncVirtualResources(VesselDataBase vd)
		{
			foreach (PartData pd in vd.Parts)
			{
				foreach (PartResourceData prd in pd.virtualResources)
				{
					if (!prd.FlowState)
						continue;

					if (prd.resourceId == null || !resourceWrappers.TryGetValue((int)prd.resourceId, out ResourceWrapper wrapper))
					{
						VesselVirtualPartResource res = GetOrCreateVirtualResource<VesselVirtualPartResource>(prd.ResourceName);
						prd.resourceId = res.Definition.id;
						wrapper = resourceWrappers[res.Definition.id];
					}

					((VirtualResourceWrapper)wrapper).AddPartresources(prd);
				}
			}
		}

		private void CoherencyWarning(Vessel v, string resourceName)
		{
			if (v == null)
				return;

			Message.Post
			(
				Severity.warning,
				Lib.BuildString
				(
				!v.isActiveVessel ? Lib.BuildString("On <b>", v.vesselName, "</b>\na ") : "A ",
				"producer of <b>", resourceName, "</b> has\n",
				"incoherent behavior at high warp speed.\n",
				"<i>Unload the vessel before warping</i>"
				)
			);
			Lib.StopWarp(5);
		}
	}
}
