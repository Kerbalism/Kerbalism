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
		public enum VesselState { Loaded, Unloaded, EditorInit, EditorStep, EditorFinalize}
		private VesselState currentState;

		public VesselResource ElectricCharge { get; protected set; }

		public Dictionary<string, double> APIResources = new Dictionary<string, double>();

		private static List<string> allResourceNames;
		private static List<string> AllResourceNames
		{
			get
			{
				if (allResourceNames == null)
				{
					allResourceNames = new List<string>();
					foreach (PartResourceDefinition resDefinition in PartResourceLibrary.Instance.resourceDefinitions)
						if (!allResourceNames.Contains(resDefinition.name))
							allResourceNames.Add(resDefinition.name);
				}
				return allResourceNames;
			}
		}

		private List<Recipe> recipes = new List<Recipe>(4);
		private Dictionary<string, IResource> resources = new Dictionary<string, IResource>(32);
		private Dictionary<string, ResourceWrapper> resourceWrappers = new Dictionary<string, ResourceWrapper>(32);

		public VesselResHandler(object vesselOrProtoVessel, VesselState state)
		{
			currentState = state;

			switch (state)
			{
				case VesselState.Loaded:
					foreach (string resourceName in AllResourceNames)
					{
						ResourceWrapper resourceWrapper = new LoadedResourceWrapper(resourceName);
						resourceWrappers.Add(resourceName, resourceWrapper);
						resources.Add(resourceName, new VesselResource(resourceWrapper));
					}
					break;
				case VesselState.Unloaded:
					foreach (string resourceName in AllResourceNames)
					{
						ResourceWrapper resourceWrapper = new ProtoResourceWrapper(resourceName);
						resourceWrappers.Add(resourceName, resourceWrapper);
						resources.Add(resourceName, new VesselResource(resourceWrapper));
					}
					break;
				case VesselState.EditorStep:
				case VesselState.EditorInit:
				case VesselState.EditorFinalize:
					foreach (string resourceName in AllResourceNames)
					{
						ResourceWrapper resourceWrapper = new EditorResourceWrapper(resourceName);
						resourceWrappers.Add(resourceName, resourceWrapper);
						resources.Add(resourceName, new VesselResource(resourceWrapper));
					}
					break;
			}

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
					SyncEditorPartResources(EditorLogic.fetch.ship.parts);
					break;
			}

			foreach (IResource resource in resources.Values)
			{
				resource.Init();
				if (resource.Name == "ElectricCharge")
					ElectricCharge = (VesselResource)resource;

				APIResources.Add(resource.Name, resource.Amount);
			}
		}

		/// <summary>return the VesselResource object for this resource or create a virtual resource if the resource doesn't exists</summary>
		public IResource GetResource(string resourceName)
		{
			// try to get existing entry if any
			IResource resource;
			if (resources.TryGetValue(resourceName, out resource))
				return resource;

			resource = new VirtualResource(resourceName);

			// remember new entry
			resources.Add(resourceName, resource);

			// return new entry
			return resource;
		}

		/// <summary>
		/// Get all virtual resources that exist on the vessel. Quite slow, don't use this in update/fixedupdate.
		/// Note that it isn't garanteed that these resources are still present/used on the vessel.
		/// </summary>
		public List<VirtualResource> GetVirtualResources()
		{
			List<VirtualResource> virtualResources = new List<VirtualResource>();
			foreach (IResource res in resources.Values)
				if (res is VirtualResource) virtualResources.Add((VirtualResource)res);

			return virtualResources;
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

		public void ResourceUpdate(object vesselOrProtoVessel, VesselState state, double elapsed_s)
		{
			Vessel vessel = null;
			ProtoVessel protoVessel = null;

			// execute all recorded recipes
			switch (state)
			{
				case VesselState.Loaded:
					vessel = (Vessel)vesselOrProtoVessel;
					Recipe.ExecuteRecipes(this, recipes, vessel);
					break;
				case VesselState.Unloaded:
					protoVessel = (ProtoVessel)vesselOrProtoVessel;
					Recipe.ExecuteRecipes(this, recipes, protoVessel.vesselRef);
					break;
				case VesselState.EditorStep:
					Recipe.ExecuteRecipes(this, recipes, null);
					break;
			}

			// forget the recipes
			recipes.Clear();

			// if the vessel state has changed between loaded and unloaded, rebuild the resource wrappers
			if ((state == VesselState.Loaded || state == VesselState.Unloaded) && state != currentState)
			{
				Lib.LogDebug($"State changed for {(vessel != null ? vessel.vesselName : protoVessel?.vesselName)} from {currentState.ToString()} to {state.ToString()}, rebuilding resource wrappers");
				currentState = state;
				foreach (string resourceName in AllResourceNames)
				{
					ResourceWrapper oldWrapper = resourceWrappers[resourceName];
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

					resourceWrappers[resourceName] = newWrapper;
					((VesselResource)resources[resourceName]).SetWrapper(newWrapper);
				}
			}
			// else just reset amount, capacity, and the part/protopart resource object references,
			// excepted when this is an editor simulation step
			else
			{
				foreach (ResourceWrapper resourceWrapper in resourceWrappers.Values)
					resourceWrapper.ClearPartResources(state != VesselState.EditorStep);
			}

			switch (state)
			{
				case VesselState.Loaded:
					SyncPartResources(vessel.parts);
					break;
				case VesselState.Unloaded:
					SyncPartResources(protoVessel.protoPartSnapshots);
					break;
				case VesselState.EditorInit:
					SyncEditorPartResources(EditorLogic.fetch.ship.parts);
					break;
				case VesselState.EditorFinalize:
					SyncEditorPartResources(EditorLogic.fetch.ship.parts);
					return;
			}

			// apply all deferred requests and synchronize to vessel
			foreach (IResource resource in resources.Values)
			{
				// note : we try to exclude resources that aren't relevant here to save some
				// performance, but this might have minor side effects, like brokers not being reset
				// after a vessel part count change for example. 
				if (!resource.NeedUpdate)
					continue;

				if (resource.ExecuteAndSyncToParts(elapsed_s) && vessel != null && vessel.loaded)
					CoherencyWarning(vessel, resource.Name);

				APIResources[resource.Name] = resource.Amount;
			}

		}


		private void SyncEditorPartResources(List<Part> parts)
		{
			foreach (Part p in parts)
			{
				foreach (PartResource r in p.Resources)
				{
#if DEBUG_RESOURCES
					// Force view all resource in Debug Mode
					r.isVisible = true;
#endif

					if (!r.flowState)
						continue;

					((EditorResourceWrapper)resourceWrappers[r.resourceName]).AddPartresources(r);
				}
			}
		}

		private void SyncPartResources(List<Part> parts)
		{
			foreach (Part p in parts)
			{
				foreach (PartResource r in p.Resources)
				{
#if DEBUG_RESOURCES
					// Force view all resource in Debug Mode
					r.isVisible = true;
#endif

					if (!r.flowState)
						continue;

					((LoadedResourceWrapper)resourceWrappers[r.resourceName]).AddPartresources(r);
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

					((ProtoResourceWrapper)resourceWrappers[r.resourceName]).AddPartresources(r);
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
