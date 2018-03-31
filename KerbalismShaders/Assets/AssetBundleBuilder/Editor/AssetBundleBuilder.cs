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
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kerbalism.AssetBundleBuilder
{
	public class AssetBundleBuilder : MonoBehaviour
	{
		[MenuItem("Kerbalism/Build Kerbalism Assets")]
		static void BuildAssets()
		{
			var opts = BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ForceRebuildAssetBundle;
			// Put the bundles in a folder called "ABs" within the Assets folder.
			string[] target_names = { "windows", "osx", "linux" };
			BuildTarget[] targets = { BuildTarget.StandaloneWindows, BuildTarget.StandaloneOSXUniversal, BuildTarget.StandaloneLinux };

			for (int i = 0; i < target_names.Length; i++)
			{
				string target = target_names[i];
				string source_path = "Assets/AssetBundles/kshaders";
				string target_path = "Assets/AssetBundles/kshaders_" + target;
				if (Directory.Exists(target_path))
				{
					Directory.Delete(target_path, true);
				}
				Directory.CreateDirectory(target_path);
				if (File.Exists("Assets/AssetBundles/_" + target))
				{
					File.Delete("Assets/AssetBundles/_" + target);
				}
				foreach (string newPath in Directory.GetFiles(source_path, "*.*", SearchOption.AllDirectories))
				{
					File.Copy(newPath, newPath.Replace(source_path, target_path), true);
				}
				BuildPipeline.BuildAssetBundles(target_path, opts, targets[i]);
				File.Copy(target_path + "/kerbalism_shaders", "Assets/AssetBundles/_" + target);
				Directory.Delete(target_path, true);
			}
		}
	}
}