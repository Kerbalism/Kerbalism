using System;
using System.Collections.Generic;

namespace KERBALISM
{


	public sealed class Supply
	{
		public Supply(ConfigNode node)
		{
			resource = Lib.ConfigValue(node, "resource", string.Empty);
			on_pod = Lib.ConfigValue(node, "on_pod", 0.0);
			on_eva = Lib.ConfigValue(node, "on_eva", 0.0);
			on_rescue = Lib.ConfigValue(node, "on_rescue", Lib.ConfigValue(node, "on_resque", 0.0)); //< old typo, pre 1.1.9
			empty = Lib.ConfigValue(node, "empty", false);

			low_threshold = Lib.ConfigValue(node, "low_threshold", 0.15);
			low_message = Lib.ConfigValue(node, "low_message", string.Empty);
			empty_message = Lib.ConfigValue(node, "empty_message", string.Empty);
			refill_message = Lib.ConfigValue(node, "refill_message", string.Empty);

			if (low_message.Length > 0 && low_message[0] == '#') Lib.Log("Broken translation: " + low_message);
			if (empty_message.Length > 0 && empty_message[0] == '#') Lib.Log("Broken translation: " + empty_message);
			if (refill_message.Length > 0 && refill_message[0] == '#') Lib.Log("Broken translation: " + refill_message);

			// check that resource is specified
			if (resource.Length == 0) throw new Exception("skipping resource-less supply");

			// check that resource exist
			if (Lib.GetDefinition(resource) == null) throw new Exception("resource " + resource + " doesn't exist");
		}


		public void CheckMessages(Vessel v, VesselData vd, List<ProtoCrewMember> crew)
		{
			// get resource handler
			VesselResource res = vd.ResHandler.GetResource(resource);

			// get data from db
			SupplyData sd = vd.Supply(resource);

			// message obey user config
			bool show_msg = resource == "ElectricCharge" ? vd.cfg_ec : vd.cfg_supply;

			// messages are shown only if there is some capacity and the vessel is manned
			// special case: ElectricCharge related messages are shown for unmanned vessels too
			if (res.Capacity > double.Epsilon && (crew.Count > 0 || resource == "ElectricCharge"))
			{
				// manned/probe message variant
				uint variant = crew.Count > 0 ? 0 : 1u;

				// manage messages
				if (res.Level <= double.Epsilon && sd.message < 2)
				{
					if (empty_message.Length > 0 && show_msg) Message.Post(Severity.danger, Lib.ExpandMsg(empty_message, v, null, variant));
					sd.message = 2;
				}
				else if (res.Level < low_threshold && sd.message < 1)
				{
					if (low_message.Length > 0 && show_msg) Message.Post(Severity.warning, Lib.ExpandMsg(low_message, v, null, variant));
					sd.message = 1;
				}
				else if (res.Level > low_threshold && sd.message > 0)
				{
					if (refill_message.Length > 0 && show_msg) Message.Post(Severity.relax, Lib.ExpandMsg(refill_message, v, null, variant));
					sd.message = 0;
				}
			}
		}


		public void SetupPod(AvailablePart p)
		{
			// get prefab
			Part prefab = p.partPrefab;

			// avoid problems with some parts that don't have a resource container (like flags)
			if (prefab.Resources == null) return;

			// do nothing if no resource on pod
			if (on_pod <= double.Epsilon) return;

			// do nothing for EVA kerbals, that have now CrewCapacity
			if (prefab.FindModuleImplementing<KerbalEVA>() != null) return;

			// do nothing if not manned
			if (prefab.CrewCapacity == 0) return;

			// do nothing if this isn't a command pod
			if (prefab.FindModuleImplementing<ModuleCommand>() == null) return;

			// calculate quantity
			double quantity = on_pod * (double)prefab.CrewCapacity;

			// add the resource
			Lib.AddResource(prefab, resource, empty ? 0.0 : quantity, quantity);

			// add resource cost
			p.cost += (float)(Lib.GetDefinition(resource).unitCost * (empty ? 0.0 : quantity));
		}


		public void SetupEva(Part p)
		{
			// do nothing if no resource on eva
			if (on_eva <= double.Epsilon) return;

			// create new resource capacity in the eva kerbal
			Lib.AddResource(p, resource, 0.0, on_eva);
		}


		public void SetupRescue(VesselData vd)
		{
			// do nothing if no resource on rescue
			if (on_rescue <= double.Epsilon) return;

			// if the vessel has no capacity
			if (vd.ResHandler.GetResource(resource).Capacity <= 0.0)
			{
				// find the first useful part
				Part p = vd.Vessel.parts.Find(k => k.CrewCapacity > 0 || k.FindModuleImplementing<KerbalEVA>() != null);

				// add capacity
				Lib.AddResource(p, resource, 0.0, on_rescue);
			}

			// add resource to the vessel
			vd.ResHandler.Produce(resource, on_rescue, ResourceBroker.Generic);
		}



		public string resource;                           // name of resource
		public double on_pod;                             // how much resource to add to manned parts, per-kerbal
		public double on_eva;                             // how much resource to take on eva, if any
		public double on_rescue;                          // how much resource to gift to rescue missions
		public bool empty;                              // set initial amount to zero

		public double low_threshold;                      // threshold of resource level used to show low messages and yellow status color
		public string low_message;                        // messages shown on threshold crossings
		public string empty_message;                      // .
		public string refill_message;                     // .
	}


} // KERBALISM
