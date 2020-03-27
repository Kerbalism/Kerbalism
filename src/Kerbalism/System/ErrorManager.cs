using KSP.UI;
using KSP.UI.Screens.DebugToolbar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public static class ErrorManager
	{
		public class Error
		{
			public bool fatal;
			public bool log;
			public string title;
			public string text;

			public Error(bool fatal, string title, string text = "", bool log = true)
			{
				this.fatal = fatal;
				this.log = log;
				this.title = title;
				this.text = text;
			}
		}

		private static List<Error> errors = new List<Error>();

		public static void AddError(bool fatal, string title, string errorDetails = "", bool logError = true)
		{
			errors.Add(new Error(fatal, title, errorDetails, logError));
		}

		public static void AddError(Error error)
		{
			errors.Add(error);
		}

		public static void CheckErrors(bool forceCloseOnFatal = true)
		{
			if (errors.Count == 0)
				return;

			// fatal errors first
			errors.Sort((x, y) => y.fatal.CompareTo(x.fatal));
			bool fatal = errors[0].fatal;

			StringBuilder sb = new StringBuilder();

			string title;
			if (fatal)
			{
				title = "KERBALISM FATAL ERROR";
				sb.Append("Kerbalism has encountered an unrecoverable error and ");
				sb.AppendKSPLine(Lib.Color("KSP must be closed", Lib.Kolor.Orange, true));
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Color("<size=14>LOADING YOUR SAVE MAY CORRUPT IT</size>", Lib.Kolor.Orange, true));
				sb.AppendKSPNewLine();
			}
			else
			{
				title = "KERBALISM WARNING";
				sb.AppendKSPLine("Kerbalism has encountered a serious error.");
				sb.AppendKSPLine(Lib.Color("This may cause savegame corruption or unpredictable behaviour", Lib.Kolor.Orange, true));
				sb.AppendKSPNewLine();
			}

			Lib.Log(title, Lib.LogLevel.Error);

			sb.Append("Please take a screenshot of this message, or better : click on the ");
			sb.Append(Lib.Color("Create bug report", Lib.Kolor.Orange, true));
			sb.AppendKSPLine(" button and send us the zip file.");
			sb.AppendKSPNewLine();

			bool tooManyErrors = false;
			int lineCount = 0;
			int shownErrorCount = 0;

			foreach (Error error in errors)
			{
				if (error.fatal)
				{
					error.title = "[FATAL]: " + error.title;
				}

				if (error.log)
				{
					Lib.Log(error.title, Lib.LogLevel.Warning);
					Lib.Log(error.text, Lib.LogLevel.Warning);
				}

				if (tooManyErrors)
					continue;

				shownErrorCount++;
				lineCount += 2;
				lineCount += Regex.Matches(error.text, "\\n").Count;
				sb.Append("<size=14>");
				sb.Append(Lib.Color(error.title, Lib.Kolor.Orange));
				sb.Append("</size>");
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(error.text);

				if (lineCount > 30)
				{
					tooManyErrors = true;
					sb.AppendKSPNewLine();
					sb.AppendKSPLine($"{errors.Count - shownErrorCount} more errors...");
				}
			}

			errors.Clear();

			Callback gotToGithub = delegate
			{
				System.Diagnostics.Process.Start(@"https://github.com/Kerbalism/Kerbalism/wiki");
			};
			Callback gotToDiscord = delegate
			{
				System.Diagnostics.Process.Start(@"https://discord.gg/AwzsWju");
			};
			Callback gotToForums = delegate
			{
				System.Diagnostics.Process.Start(@"https://forum.kerbalspaceprogram.com/index.php?/topic/190382-15-19-kerbalism-37/");
			};

			string closeBtn;
			Callback closeDelegate;
			if (fatal && forceCloseOnFatal)
			{
				closeBtn = "Quit KSP";
				closeDelegate = delegate { Application.Quit(); };
			}
			else
			{
				closeBtn = "Close";
				closeDelegate = null;
			}

			PopupDialog.SpawnPopupDialog(new Vector2(1.0f, 1.0f),
				new Vector2(1.0f, 1.0f),
				new MultiOptionDialog(
					title,
					sb.ToString(),
					title,
					HighLogic.UISkin,
					new Rect(0.9f, 0.9f, 750f, 60f),
					new DialogGUIFlexibleSpace(),
					new DialogGUIHorizontalLayout(
						new DialogGUIFlexibleSpace(),
						new DialogGUIButton("Create bug report", BugReportDialog, 140.0f, 30.0f, false),
						new DialogGUIButton("Go to Github", gotToGithub, 140.0f, 30.0f, false),
						new DialogGUIButton("Go to Discord", gotToDiscord, 140.0f, 30.0f, false),
						new DialogGUIButton("Go to KSP forums", gotToForums, 140.0f, 30.0f, false),
						new DialogGUIButton(closeBtn, closeDelegate, 140.0f, 30.0f, true),
						new DialogGUIFlexibleSpace()
					)
				),
				fatal,
				HighLogic.UISkin,
				true);

		}

		private static void BugReportDialog()
		{
			if (!CreateReport(out string zipFileName, out string zipFileFolderPath, out string zipFilePath))
				return;
			
			PopupDialog.SpawnPopupDialog(new Vector2(0.0f, 1.0f),
				new Vector2(0.0f, 1.0f),
				new MultiOptionDialog(
					"bugreportcreated",
					$"<size=14>Zip file : {Lib.Color(zipFileName, Lib.Kolor.Orange, true)}</size>\n<size=14>In folder : {Lib.Color(zipFileFolderPath, Lib.Kolor.Orange, true)}</size>\n\n" ,
					"Bug report created",
					HighLogic.UISkin,
					new Rect(0.1f, 0.9f, 400, 60f),
					new DialogGUIButton("Open folder containing zip file", delegate { System.Diagnostics.Process.Start(zipFileFolderPath); }, 200.0f, 30.0f, false),
					new DialogGUIButton("Open zip file", delegate { System.Diagnostics.Process.Start(zipFilePath); }, 200.0f, 30.0f, false),
					new DialogGUIButton("Close", null, 200.0f, 30.0f, true)
				),
				true,
				HighLogic.UISkin,
				false);
		}

		public static IEnumerator InjectBugReportInDebugScreen()
		{
			// force the creation of the ALT+F12 debug screen, so our custom options are added last and not first
			DebugScreenSpawner.ShowDebugScreen();

			// doesn't work if we do it right away, we have to wait one frame
			yield return null;
			DebugScreen screen = Lib.ReflectionValue<DebugScreen>(DebugScreenSpawner.Instance, "screen");
			screen.Hide();

			yield return null;
			UITreeView.Item parent = DebugScreen.AddContentItem(null, "bugreport", "Bug report", null, null);
			DebugScreen.AddContentItem(parent, "createreport", "Create report", null, CreateReportFromDebugScreen);
			DebugScreen.AddContentItem(parent, "openreport", "Open last report", null, OpenReportFromDebugScreen);
			DebugScreen.AddContentItem(parent, "openfolder", "Open report folder", null, OpenFolderFromDebugScreen);
		}

		private static string lastReportPath = string.Empty;
		private static string lastReportFolderPath = string.Empty;

		private static void CreateReportFromDebugScreen()
		{
			if (CreateReport(out string zipFileName, out lastReportFolderPath, out lastReportPath))
			{
				ScreenMessages.PostScreenMessage($"Bug report :\n{Lib.Color(zipFileName, Lib.Kolor.Orange, true)}\ncreated in folder :\n{Lib.Color(lastReportFolderPath, Lib.Kolor.Orange, true)}", 10f, ScreenMessageStyle.UPPER_CENTER, true);
			}
			else
			{
				ScreenMessages.PostScreenMessage($"Error while creating bug report", 3f, ScreenMessageStyle.UPPER_CENTER, Color.red);
			}
		}

		private static void OpenReportFromDebugScreen()
		{
			if (!string.IsNullOrEmpty(lastReportPath) && System.IO.File.Exists(lastReportPath))
			{
				System.Diagnostics.Process.Start(lastReportPath);
			}
			else
			{
				ScreenMessages.PostScreenMessage($"No bug report to open, please create one first.", 3f, ScreenMessageStyle.UPPER_CENTER, Color.red);
			}
		}

		private static void OpenFolderFromDebugScreen()
		{
			if (!string.IsNullOrEmpty(lastReportFolderPath) && Directory.Exists(lastReportFolderPath))
			{
				System.Diagnostics.Process.Start(lastReportFolderPath);
			}
			else
			{
				ScreenMessages.PostScreenMessage($"No bug report to open, please create one first.", 3f, ScreenMessageStyle.UPPER_CENTER, Color.red);
			}
		}

		private static bool CreateReport(out string zipFileName, out string zipFileFolderPath, out string zipFilePath)
		{
			string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KSP.log");
			string mmCacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameData", "ModuleManager.ConfigCache");
			zipFileFolderPath = System.Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			if (string.IsNullOrEmpty(zipFileFolderPath))
			{
				zipFileFolderPath = AppDomain.CurrentDomain.BaseDirectory;
			}

			zipFileFolderPath = Path.Combine(zipFileFolderPath, "KSPBugReports");

			try
			{
				Directory.CreateDirectory(zipFileFolderPath);
			}
			catch (Exception e)
			{
				Lib.Log($"Could not create directory {zipFileFolderPath}\n{e.ToString()}", Lib.LogLevel.Warning);
				zipFileName = zipFilePath = string.Empty;
				return false;
			}

			string fileName = "KSPBugReport_" + DateTime.Now.ToString(@"yyyy-MM-dd_HHmmss");
			zipFileName = fileName + ".zip";
			zipFilePath = Path.Combine(zipFileFolderPath, zipFileName);

			using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
			{
				try
				{
					if (System.IO.File.Exists(logFilePath))
					{
						using (Stream fileStream = System.IO.File.Open(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
						{
							ZipArchiveEntry zipArchiveEntry = archive.CreateEntry("KSP.log", System.IO.Compression.CompressionLevel.Optimal);
							using (Stream entryStream = zipArchiveEntry.Open())
							{
								fileStream.CopyTo(entryStream);
							}
						}
					}
				}
				catch (Exception e)
				{
					Lib.Log($"Could not zip KSP.log\n{e.ToString()}", Lib.LogLevel.Warning);
					archive.Dispose();
					return false;
				}

				try
				{
					if (System.IO.File.Exists(mmCacheFilePath))
					{
						using (Stream fileStream = System.IO.File.Open(mmCacheFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
						{
							ZipArchiveEntry zipArchiveEntry = archive.CreateEntry("ModuleManager.ConfigCache", System.IO.Compression.CompressionLevel.Optimal);
							using (Stream entryStream = zipArchiveEntry.Open())
							{
								fileStream.CopyTo(entryStream);
							}
						}
					}
				}
				catch (Exception e)
				{
					Lib.Log($"Could not zip ModuleManager.ConfigCache\n{e.ToString()}", Lib.LogLevel.Warning);
				}

				try
				{
					if (HighLogic.CurrentGame != null && Lib.IsGameRunning)
					{
						Game game = HighLogic.CurrentGame.Updated();
						game.startScene = GameScenes.SPACECENTER;
						ConfigNode configNode = new ConfigNode();
						game.Save(configNode);
						configNode = configNode.nodes[0];

						ZipArchiveEntry zipArchiveEntry = archive.CreateEntry(HighLogic.SaveFolder + ".sfs", System.IO.Compression.CompressionLevel.Optimal);
						using (Stream entryStream = zipArchiveEntry.Open())
						{
							using (StreamWriter writer = new StreamWriter(entryStream))
							{
								writer.Write(configNode.ToString());
								writer.Flush();
							}
						}
					}
				}
				catch (Exception e)
				{
					Lib.Log($"Could not zip savegame\n{e.ToString()}", Lib.LogLevel.Warning);
				}
			}
			return true;
		}
	}
}
