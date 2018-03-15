/*************************************************************************
 *  Copyright © 2017-2018 Mogoson. All rights reserved.
 *------------------------------------------------------------------------
 *  File         :  AssetBundleBuilder.cs
 *  Description  :  Config build options and build AssetBundles to
 *                  target path.
 *------------------------------------------------------------------------
 *  Author       :  Mogoson
 *  Version      :  0.1.0
 *  Date         :  3/7/2018
 *  Description  :  Initial development version.
 *************************************************************************/

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Developer.AssetBundleBuilder
{
	public class AssetBundleBuilder : EditorWindow
	{
		#region Field and Property
		private static AssetBundleBuilder instance;
		private const float ButtonWidth = 80;

		private string path = "Assets";
		private BuildTarget platform = BuildTarget.Android;

		private const string PathKey = "AssetBundleBuildPath";
		private const string PlatformKey = "AssetBundleTargetPlatform";
		#endregion

		#region Private Method
		[MenuItem("Tool/Asset Bundle Builder &B")]
		private static void ShowEditor()
		{
			instance = GetWindow<AssetBundleBuilder>("Asset Bundle");
			instance.Show();
		}

		private void OnEnable()
		{
			GetEditorPreferences();
		}

		private void OnGUI()
		{
			EditorGUILayout.BeginVertical("Window");

			EditorGUILayout.BeginHorizontal();
			path = EditorGUILayout.TextField("Path", path);
			if (GUILayout.Button("Browse", GUILayout.Width(ButtonWidth)))
				SelectBuildPath();
			EditorGUILayout.EndHorizontal();

			platform = (BuildTarget)EditorGUILayout.EnumPopup("Platform", platform);

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.Space();
			if (GUILayout.Button("Build", GUILayout.Width(ButtonWidth)))
				BuildAssetBundles();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
		}

		private void SelectBuildPath()
		{
			var selectPath = EditorUtility.OpenFolderPanel("Select Build Path", "Assets", string.Empty);
			if (selectPath == string.Empty)
				return;

			try { path = selectPath.Substring(selectPath.IndexOf("Assets")); }
			catch { path = selectPath; }
		}

		private void BuildAssetBundles()
		{
			if (Directory.Exists(path))
			{
				try
				{
					var opts = BuildAssetBundleOptions.DeterministicAssetBundle
						| BuildAssetBundleOptions.ForceRebuildAssetBundle;
					BuildPipeline.BuildAssetBundles(path, opts, platform);
				}
				catch (Exception e)
				{
					ShowNotification(new GUIContent(e.Message));
					return;
				}

				AssetDatabase.Refresh();
				SetEditorPreferences();
			}
			else
				ShowNotification(new GUIContent("The output path does not exist."));
		}

		private void SetEditorPreferences()
		{
			EditorPrefs.SetString(PathKey, path);
			EditorPrefs.SetInt(PlatformKey, (int)platform);
		}

		private void GetEditorPreferences()
		{
			path = EditorPrefs.GetString(PathKey, path);
			platform = (BuildTarget)EditorPrefs.GetInt(PlatformKey, (int)platform);
		}
		#endregion
	}
}