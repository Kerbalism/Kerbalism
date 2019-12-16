using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	/// <summary>
	/// We're always going to call you for resource handling, You tell us what
	// to produce or consume.
	/// </summary>
	public interface IKerbalismModule
	{
		// For background updates, define the following static method. It will
		// be called when the vessel is NOT loaded.
		//
		// - vessel: the vessel (unloaded)
		// -proto_part: proto part snapshot (contains all non-persistant KSPFields)
		// -proto_module: proto part module snapshot (contains all non-persistant KSPFields)
		// -partModule: proto part module snapshot (contains all non-persistant KSPFields)
		// -part: proto part snapshot (contains all non-persistant KSPFields)
		// -availableResources: key-value pair containing all available resources and their
		//  currently available amount on the vessel. if the resource is not in there, it's
		//  not available
		// -resourceChangeRequest: key-value pair that contains the resource names and the
		//   units per second that you want to produce/consume
		//   (produce: positive, consume: negative)
		// -elapsed_s: how much time elapsed since the last time. note this can be very
		// long, up to minutes and hours depending on warp speed
		//
		// return the title to be displayed in the resource tooltip. please be short.
		//
		// static string BackgroundUpdate(Vessel vessel, ProtoPartSnapshot proto_part,
		//	 ProtoPartModuleSnapshot proto_module, PartModule partModule, Part part,
		//	 Dictionary<string, double> availableResources,
		//	 List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s);

		/// <summary>
		/// We're also always going to call you when you're loaded.  Since you're loaded,
		/// this will be your PartModule, just like you'd expect in KSP. Will only be
		/// called while in flight, not in the editor
		/// </summary>
		/// <param name="availableResources">key-value pair containing all available
		///		resources and their currently available amount on the vessel. if the
		///		resource is not in there, it's not available</param>
		/// <param name="resourceChangeRequest">key-value pair that contains the
		///		resource names and the units per second that you want to produce/consume
		///		(produce: positive, consume: negative)</param>
		/// <returns>the title to be displayed in the resource tooltip. please be short.</returns>
		string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest);

		/// <summary>
		/// This will be called by Kerbalism in the editor (VAB/SPH), possibly several
		/// times after a change to the vessel.
		///
		/// The Kerbalism Planner allows to select different situations and bodies,
		/// and will update the simulated environment accordingly. This simulated
		/// environment is passed into this method:
		///
		/// - body: the currently selected body
		/// - environment: a string to double dictionary, currently containing:
		///   - altitude: the altitude of the vessel above the body
		///   - orbital_period: the duration of a circular equitorial orbit at
		///		the given altitude
		///   - shadow_period: the duration of that orbit that will be in the
		///		planets shadow
		///   - albedo_flux
		///   - solar_flux
		///   - sun_dist: distance to the sun
		///   - temperature
		///   - total_flux
		/// </summary>
		/// <param name="resourceChangeRequest">A list of resource names and production/consumption rates.
		/// Production is a positive rate, consumption is negatvie. Add all resources your module is going to produce/consume.</param>
		/// <param name="body">The currently selected body in the Kerbalism planner</param>
		/// <param name="environment">Environment variables guesstimated by Kerbalism, based on the current selection of body and vessel situation. See above.</param>
		/// <returns>The title to display in the tooltip of the planner UI. Please be short.</returns>
		string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment);
	}
}
