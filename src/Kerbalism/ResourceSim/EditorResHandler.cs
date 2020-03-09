using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary> Access to the editor resource handler</summary>
	public static class EditorResHandler
	{
		static VesselResHandler editorHandler;
		static uint editorHandlerId;

		public static VesselResHandler Handler
		{
			get
			{
				if (editorHandler == null || EditorLogic.fetch.ship.persistentId != editorHandlerId)
				{
					editorHandler = new VesselResHandler(null, VesselResHandler.VesselState.EditorStep);
					editorHandlerId = EditorLogic.fetch.ship.persistentId;
				}

				return editorHandler;
			}
		}
	}
}
