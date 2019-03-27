using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class Changelog : MonoBehaviour
	{
		public void Start()
		{
			string cl = Path.Combine(AssemblyDirectory(Assembly.GetExecutingAssembly()), "CHANGELOG.md");
			string vf = Path.Combine(AssemblyDirectory(Assembly.GetExecutingAssembly()), ".lastversion");
			string ver = Lib.Version();
			bool display_cl = false;
			if(!System.IO.File.Exists(vf) || System.IO.File.ReadAllText(vf) != ver)
				display_cl = true;
			if (display_cl == true && System.IO.File.Exists(cl))
			//if (System.IO.File.Exists(cl))
			{
				System.IO.File.WriteAllText(vf, ver);
				//PopupDialog PopupDialog.SpawnPopupDialog(Vector2 anchorMin, Vector2 anchorMax,
				//string dialogName, string title, string message, string buttonMessage, bool persistAcrossScenes,
				//UISkinDef skin, bool isModal = true, string titleExtra = "")
				string changelog = System.IO.File.ReadAllText(cl);
				PopupDialog.SpawnPopupDialog(
					new Vector2(0.5f, 0.5f),
					new Vector2(0.5f, 0.5f),
					new MultiOptionDialog(
						"KerbalismCL",
						"",
						"Kerbalism Changelog",
						HighLogic.UISkin,
						new Rect(0.5f, 0.5f, 450f, 550f),
						new DialogGUIVerticalLayout(
							new DialogGUITextInput(changelog, true, changelog.Length, (string s) => { return string.Empty; }, -1),
							new DialogGUIHorizontalLayout(
								new DialogGUIButton("Close", () => { }, 140.0f, 30.0f, true)
							)
						)
					),
					false,
					HighLogic.UISkin
				);
			}
		}

		internal string AssemblyDirectory(Assembly a)
		{
			string codeBase = Assembly.GetExecutingAssembly().CodeBase;
			UriBuilder uri = new UriBuilder(codeBase);
			string path = Uri.UnescapeDataString(uri.Path);
			return Path.GetDirectoryName(path);
		}
	}
}
