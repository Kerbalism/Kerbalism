/*
From the original author : Thanks to blowfish for guiding me through this!

Gotmachine 08-2019 : Complete refactor of KerbalismBootstrap.
- major code despaghettification
- removed the Utils.cs file and put the AddonLoaderWrapper static class in this file
- changed how kbins are assigned to a specific version of KSP

This now use a distributed "VersionConfig.xml" file to determine which bin must be used for which KSP version
The VersionConfig.xml is also used by the Kerbalism MSBuild based build system to update the assembly versions and the Kerbalism.version file.
However, in the context of KerbalismBootstrap, this is an example of the required values :

<KBinVersionConstant Include="17">
	<KSPMinMajor>1</KSPMinMajor>
	<KSPMinMinor>7</KSPMinMinor>
	<KSPMinBuild>0</KSPMinBuild>
	<KSPMaxMajor>1</KSPMaxMajor>
	<KSPMaxMinor>7</KSPMaxMinor>
	<KSPMaxBuild>99</KSPMaxBuild>
</KBinVersionConstant>
<KBinVersionConstant Include="15_16">
	<KSPMinMajor>1</KSPMinMajor>
	<KSPMinMinor>5</KSPMinMinor>
	<KSPMinBuild>0</KSPMinBuild>
	<KSPMaxMajor>1</KSPMaxMajor>
	<KSPMaxMinor>6</KSPMaxMinor>
	<KSPMaxBuild>1</KSPMaxBuild>
</KBinVersionConstant>

In this example, we must have 2 *.kbin files in the same folder as KerbalismBootstrap.dll :
- "Kerbalism15_16.kbin" will be loaded for KSP 1.5.0 to 1.6.1
- "Kerbalism17.kbin" will be loaded for KSP 1.7.0 to 1.7.99

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEngine;

namespace KerbalismBootstrap
{
	[KSPAddon( KSPAddon.Startup.Instantly, false )]
	public class Bootstrap : MonoBehaviour
	{
		const string assemblyName = "Kerbalism";

		public List<Kbin> Kbins { get; private set; }
		public Version KSPVersion { get; private set; }

		public class Kbin
		{
			public string KbinAssemblyName;
			public string KbinFilePath;
			public Version KSPVersionMin;
			public Version KSPVersionMax;
		}

		public int ParseKbinVersion(XmlNode KbinNode, string versionNode)
		{
			return int.Parse(KbinNode.SelectSingleNode(versionNode).InnerText);
		}

		public void Start()
		{
			// get assembly path
			string codeBase = Assembly.GetExecutingAssembly().CodeBase;
			UriBuilder uri = new UriBuilder(codeBase);
			string path = Uri.UnescapeDataString(uri.Path);
			string assemblyPath =  Path.GetDirectoryName(path);

			// check if the assembly exists as a dll file, or is already loaded. If it's the case, we assume it has priority over the kbins.
			if (File.Exists(Path.Combine(assemblyPath, assemblyName + ".dll")) || AssemblyLoader.loadedAssemblies.Any(p => p.name == assemblyName))
			{
				print("[KerbalismBootstrap] WARNING : " + assemblyName + ".dll exists, aborting! Note : this is normal if you are running a debug build)");
				return;
			}

			// Check that the KSP load method has been acquired
			if (!AddonLoaderWrapper.IsValid)
			{
				print("[KerbalismBootstrap] ERROR : the AddonLoader.StartAddon() method hasn't been found");
				return;
			}

			// load the xml version file but remove namespaces
			XmlDocument versionConfig = new XmlDocument();
			using (XmlTextReader textReader = new XmlTextReader(Path.Combine(assemblyPath, "VersionConfig.xml")))
			{
				textReader.Namespaces = false;
				versionConfig.Load(textReader);
			}

			// get the kbin nodes list (should match the *.kbin files present in the assemblyPath)
			XmlNodeList kbinXmlList = versionConfig.GetElementsByTagName("KBinVersionConstant");

			// parse each kbin xml node to get the name of the *.kbin file and the max / min KSP version this kbin is made for.
			Kbins = new List<Kbin>();
			foreach (XmlNode kbinXml in kbinXmlList)
			{
				Kbin kbin = new Kbin();
				string versionConstant = kbinXml.Attributes["Include"].Value;
				kbin.KbinAssemblyName = assemblyName + versionConstant;
				kbin.KbinFilePath = Path.Combine(assemblyPath, kbin.KbinAssemblyName + ".kbin");
				kbin.KSPVersionMin = new Version(ParseKbinVersion(kbinXml, "KSPMinMajor"), ParseKbinVersion(kbinXml, "KSPMinMinor"), ParseKbinVersion(kbinXml, "KSPMinBuild"));
				kbin.KSPVersionMax = new Version(ParseKbinVersion(kbinXml, "KSPMaxMajor"), ParseKbinVersion(kbinXml, "KSPMaxMinor"), ParseKbinVersion(kbinXml, "KSPMaxBuild"));

				if (File.Exists(kbin.KbinFilePath))
					Kbins.Add(kbin);
				else
					print("[KerbalismBootstrap] WARNING : Can't find '" + kbin.KbinFilePath + "' defined in VersionConfig.xml");
			}

			// get the KSP version
			KSPVersion = new Version(Versioning.version_major, Versioning.version_minor, Versioning.Revision);

			// get the Kbin matching the KSP version
			Kbin kbinToLoad = Kbins.Find(p => KSPVersion >= p.KSPVersionMin && KSPVersion <= p.KSPVersionMax);
			if (kbinToLoad == null)
			{
				print("[KerbalismBootstrap] ERROR : No *.kbin file available for KSP " + KSPVersion + " - " + assemblyName + " wasn't loaded. Check the supported versions in VersionConfig.xml");
				return;
			}

			// load the kbin in the KSP assembly loader
			AssemblyLoader.LoadPlugin(new FileInfo(kbinToLoad.KbinFilePath), kbinToLoad.KbinFilePath, null);
			AssemblyLoader.LoadedAssembly loadedKbin = AssemblyLoader.loadedAssemblies.FirstOrDefault(p => p.name == kbinToLoad.KbinAssemblyName);
			if (loadedKbin == null)
			{
				print("[KerbalismBootstrap] ERROR : kbin '" + kbinToLoad.KbinFilePath + "' failed to load!");
				return;
			}
			else
			{
				print("[KerbalismBootstrap] " + kbinToLoad.KbinAssemblyName + ".kbin for KSP version " + KSPVersion + " successfully loaded");
			}

			// Gotmachine 08-2019 : I'm not sure exactly how the following code work and I don't have time to investigate.
			// Shame on the guy that created this for not adding a single comment on a super hacky piece of code

			loadedKbin.Load();

			foreach (Type type in loadedKbin.assembly.GetTypes())
			{
				
				foreach (Type loadedType in AssemblyLoader.loadedTypes)
				{
					if (loadedType.IsAssignableFrom(type))
					{
						loadedKbin.types.Add(loadedType, type);
						PropertyInfo temp = typeof(AssemblyLoader.LoadedAssembly).GetProperty("typesDictionary");
						if (temp != null)
						{
							Dictionary<Type, Dictionary<String, Type>> dict = (Dictionary<Type, Dictionary<String, Type>>)temp.GetValue(loadedKbin, null);
							// Here is the only comment on this thing :
							// This is just so we have 1.3 compat!
							if (!dict.ContainsKey(loadedType))
							{
								dict[loadedType] = new Dictionary<string, Type>();
							}
							dict[loadedType][type.Name] = type;
						}
					}
				}

				if (type.IsSubclassOf(typeof(MonoBehaviour)))
				{
					KSPAddon addonAttribute = (KSPAddon)type.GetCustomAttributes(typeof(KSPAddon), true).FirstOrDefault();
					if (addonAttribute != null && addonAttribute.startup == KSPAddon.Startup.Instantly)
					{
						AddonLoaderWrapper.StartAddon(loadedKbin, type, addonAttribute, KSPAddon.Startup.Instantly);
					}
				}
			}
		}
	}

	// This class is a wrapper for the private AddonLoader.StartAddon() KSP method
	public static class AddonLoaderWrapper
	{
		private static readonly MethodInfo KSPStartAddon;
		public static bool IsValid => KSPStartAddon != null;

		static AddonLoaderWrapper()
		{
			KSPStartAddon = typeof(AddonLoader).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).FirstOrDefault(delegate (MethodInfo method)
			{
				ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length != 4)
					return false;
				if (parameters[0].ParameterType != typeof(AssemblyLoader.LoadedAssembly))
					return false;
				if (parameters[1].ParameterType != typeof(Type))
					return false;
				if (parameters[2].ParameterType != typeof(KSPAddon))
					return false;
				if (parameters[3].ParameterType != typeof(KSPAddon.Startup))
					return false;
				return true;
			});
		}

		public static void StartAddon(AssemblyLoader.LoadedAssembly assembly, Type addonType, KSPAddon addon, KSPAddon.Startup startup)
		{
			KSPStartAddon.Invoke(AddonLoader.Instance, new object[] { assembly, addonType, addon, startup });
		}
	}
}
