using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.Planner;
using System.Reflection;

namespace KERBALISM
{

	// This runs afer the LoadShip Postfix.
	// The purpose of this patch is double :
	// 1. In the editor, create the PartData and ModuleData for every KsmPartModule
	//    This is in a Part.Start() Prefix because that will run after ShipConstruct.LoadShip()
	//    So any PartData/Moduledata that was instantiated by loading a shipconstruct is already here
	//    and won't be "erased", but by doing this in Part.Start() we are sure that any other part
	//    instantiation (picked from the list, alt-copy, symmetry...) is catched, and we are sure
	//    that ModuleData will be here when KsmPartModule.OnStart() is called from Part.Start() -> Part.ModulesOnStart()
	// 2. In flight, two cases :
	//   A. A (loaded) vessel has been loaded from an existing VesselData.
	//      From VesselData.OnLoad(), the PartData collection has been populated
	//      If moduledatas were found in the confignode, they have been loaded, otherwise new ones have been created
	//      In any case, flightIds have been populated.
	//      The Part and its PartModule also have their OnLoad called, so the flightIds are there too
	//      What only remains to do is to set the actual cross-references for the KsmPartModule to their ModuleData
	//   B. A (loaded) vessel has been loaded, but the ModuleData for one or more of its KsmPartModule can't be found.
	//      This can happen if the part configuration has been modified.
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("Start")]
	class Part_Start
	{
		static void Prefix(Part __instance)
		{
			if (Lib.IsEditor)
			{
				// PartData will already exist if created trough ShipConstruct.LoadShip()
				if (VesselDataShip.LoadedParts.Contains(__instance))
					return;

				// otherwise, this is a newly instantiated part, create the partdata
				PartData partData = new PartData(VesselDataShip.Instance, __instance);
				VesselDataShip.LoadedParts.Add(partData);

				// and create and link the ModuleData for every KsmPartModule
				for (int i = 0; i < __instance.Modules.Count; i++)
				{
					if (__instance.Modules[i] is KsmPartModule ksmPM)
					{
						ModuleData.New(ksmPM, i, partData, false);
					}
				}
			}
			else
			{
				for (int i = 0; i < __instance.Modules.Count; i++)
				{
					if (__instance.Modules[i] is KsmPartModule ksmPM && ksmPM.ModuleData == null)
					{
						ModuleData.GetOrCreateFlightModuleData(ksmPM, i);
					}
				}
			}
		}
	}


	[HarmonyPatch(typeof(ShipConstruct))]
	[HarmonyPatch("LoadShip")]
	[HarmonyPatch(new Type[] { typeof(ConfigNode), typeof(uint), typeof(bool), typeof(string)}, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
	class ShipConstruct_LoadShip
	{
		private static uint editorPreviousShipId;
		public static ConfigNode kerbalismDataNode;

		// See comment on the ShipConstruct.SaveShip patch. From my tests, having our extra node in the first
		// part node doesn't cause any issue, but just in case, better remove it.
		static void Prefix(ConfigNode root)
		{
			editorPreviousShipId = EditorLogic.fetch?.ship?.persistentId ?? 0u;

			if (root != null && root.CountNodes > 0)
			{
				kerbalismDataNode = root.nodes[0].GetNode(VesselDataBase.NODENAME_VESSEL);
			}

			if (kerbalismDataNode != null)
			{
				root.nodes[0].RemoveNode(VesselDataBase.NODENAME_VESSEL);
			}
		}

		static void Postfix(ShipConstruct __instance, bool __result)
		{
			// LoadShip can be in a chain of events that deleted all/some loaded parts, or not at all.
			// Exemple : in the editor, loading a ship will remove the current ship, but merging a ship won't.
			// There are just too many different cases, and our options for detecting them are very limited.
			// So we take a brute force approach and always verify that our PartData collection doesn't contain
			// a part that doesn't exist anymore, by iterating over Part.allParts (which seems reliable in that matter)
			if (Part.allParts.Count == 0)
			{
				VesselDataShip.LoadedParts.Clear();
			}
			else
			{
				HashSet<int> loadedPartsId = new HashSet<int>();
				for (int i = 0; i < Part.allParts.Count; i++)
				{
					loadedPartsId.Add(Part.allParts[i].GetInstanceID());
				}

				List<int> loadedPartDataIds = new List<int>(VesselDataShip.LoadedParts.AllInstanceIDs);
				foreach (int key in loadedPartDataIds)
				{
					if (!loadedPartsId.Contains(key))
					{
						VesselDataShip.LoadedParts.Remove(key);
					}
				}
			}

			// LoadShip will return false if an error happened
			if (!__result)
				return;

			Lib.LogDebug($"Loading VesselData for ship {__instance.shipName}");

			// we don't want to overwrite VesselData when loading a subassembly or when merging
			uint editorNewShipId = EditorLogic.fetch?.ship?.persistentId ?? 0u;
			bool isNewShip = editorPreviousShipId == 0 || editorNewShipId == 0 || editorPreviousShipId != editorNewShipId;

			VesselDataBase.LoadShipConstruct(__instance, kerbalismDataNode, isNewShip);

		}
	}

	// There is no "public" way to save non-module specific data into a shipconstruct.
	// So the purpose of this patch is to save our VesselDataShip in the shipconstruct confignode,
	// which is what KSP saves in *.craft files.
	// Unfortunately, this node only has values and all child nodes are expected to be PART nodes,
	// and KSP doesn't even check that they are named "PART", it just do a node.GetNodes(), so that cause
	// a crash if we put our node there, and there are other issues (like for showing the part count of
	// ship saves, it just do nodes.count on the root node.
	// So what we do is put our KERBALISMDATA node inside the first PART node, where there are multiple other
	// stock nodes with various names, so we can expect that it will never be confused by it.
	// For an additional safety, we remove that node in the ShipConstruct.LoadShip prefix
	[HarmonyPatch(typeof(ShipConstruct))]
	[HarmonyPatch("SaveShip")]
	class ShipConstruct_SaveShip
	{
		// For linking KsmPartModule and ModuleData in shipconstruct, we use an unique int id called "shipId"
		// We put it into a persistent KSPField on KsmPartModule.dataShipId, and in ModuleData.shipId
		// The purpose of the prefix patch is to create and assign this unique id before KSP saves the PartModule.
		static void Prefix(ShipConstruct __instance)
		{
			Lib.LogDebug($"Saving shipconstruct {__instance.shipName}...");

			HashSet<int> shipIds = new HashSet<int>();

			foreach (Part part in __instance.parts)
			{
				for (int i = 0; i < part.Modules.Count; i++)
				{
					if (part.Modules[i] is KsmPartModule ksmPM)
					{
						if (ksmPM.ModuleData == null)
						{
							// note : what happens here is that ShipConstruct.SaveShip is called to create the "auto-saved ship" 
							// in the editor just after the first part is instantiatiated when you pick it up from the part list
							// This happens before Part.Start(), meaning we won't have instantated the ModuleData yet.
							// It doesn't seem to have any consequence, since ShipConstruct.SaveShip will be called again latter
							// in all cases. I'm not sure why KSP does that super-early call.
							Lib.Log($"ModuleData for {ksmPM.GetType().Name} not found, skipping shipId affectation...");
							continue;
						}

						int shipId;
						do
							shipId = Guid.NewGuid().GetHashCode();
						while (shipIds.Contains(shipId) || shipId == 0);

						shipIds.Add(shipId);
						ksmPM.dataShipId = shipId;
						ksmPM.ModuleData.shipId = shipId;
					}
				}
			}
		}

		// In the postfix, we grab the ShipConstruct node that the method created, and put our data in the first PART node
		static void Postfix(ConfigNode __result)
		{
			if (__result.CountNodes == 0)
				return;

			ConfigNode firstPartNode = __result.nodes[0];
			if (firstPartNode == null)
				return;

			VesselDataShip.Instance.Save(firstPartNode);

		}
	}

	// This patch handle the creation of a new VesselData from a VesselDataShip.
	// When launching a new ship, KSP does the following :
	// - Call ShipConstruct.LoadShip() to create the parts
	// - Call ShipConstruct.SaveShip() (probably for the purpose of reverting to launch)
	// - Call ShipConstruction.AssembleForLaunch() to create the Vessel, and assign those parts to it.
	// Since we are patching ShipConstruct.LoadShip(), that mean all our PartData/ModuleData has been loaded.
	// Note that all of this happens after Kerbalism.OnLoad(), this is important because we need the global
	// dictionary of ModuleData flightIds to be populated so we can check the uniqueness of the flightIds
	// we are about to create for this new vessel.
	// So, what we are about to do :
	// - Get the confignode that was just loaded by the ShipConstruct.LoadShip() call
	// - Create a new VesselData with a specific ctor that :
	//   - Copy the PartDatas/ModuleDatas from the current PartDataCollectionShip to a new PartDataCollectionVessel
	//   - Assign flightId for every PartData/ModuleData
	//   - call VesselDataBase.Load() with that confignode we grabbed to load the data that is meant to be transfered
	//     from editor to flight. That call won't call VesselData.OnLoad(), but initialize the default values instead.
	// - Add the new VesselData to the DB.
	// We are doing it that way because that avoid a full serialization/deserialization cycle, but more importantly
	// because the "in between" ShipConstruct.SaveShip() call by KSP causes a new shipID to be affected to the currently
	// loaded parts, so we have lost any way to re-link trough ids the currently loaded parts and the last loaded confignode.
	[HarmonyPatch(typeof(ShipConstruction))]
	[HarmonyPatch("AssembleForLaunch")]
	[HarmonyPatch(new Type[] { typeof(ShipConstruct), typeof(string), typeof(string), typeof(string), typeof(Game),
		typeof(VesselCrewManifest), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(Orbit), typeof(bool), typeof(bool) })]
	class ShipConstruction_AssembleForLaunch
	{
		static void Postfix(Vessel __result)
		{
			Lib.LogDebug($"Assembling ship for launch: {__result.vesselName}");

			ConfigNode kerbalismDataNode = ShipConstruction.ShipConfig?.nodes[0]?.GetNode(VesselDataBase.NODENAME_VESSEL);

			DB.NewVesselDataFromShipConstruct(__result, kerbalismDataNode, VesselDataShip.LoadedParts);

			VesselDataShip.LoadedParts.Clear();
			VesselDataShip.Instance = null;
		}
	}
}
