using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>Global cache for storing and accessing VesselResHandler (and each VesselResource) in all vessels, with shortcut for common methods</summary>
	public static class EditorResourceHandler
	{
		static VesselResHandler editorHandler;
		static uint editorHandlerId;

		public static VesselResHandler GetHandler(ShipConstruct ship)
		{
			if (editorHandler == null || ship.persistentId != editorHandlerId)
				editorHandler = new VesselResHandler(ship, VesselResHandler.VesselState.Editor);

			return editorHandler;
		}
	}
}
