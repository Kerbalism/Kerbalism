/*
 * Thanks to blowfish for guiding me through this!
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KerbalismBootstrap
{
	[KSPAddon( KSPAddon.Startup.Instantly, false )]
	public class Bootstrap : MonoBehaviour
	{

		public void Start()
		{
			if (Util.IsDllLoaded || (Util.FindKerbalismBin() != null))
				print( "[KerbalismBootstrap] WARNING: KERBALISM HAS ALREADY LOADED BEFORE US!" );

			string our_bin = Path.Combine( AssemblyDirectory( Assembly.GetExecutingAssembly() ), Util.BinName + ".bin" );
			string possible_dll = Path.Combine( AssemblyDirectory( Assembly.GetExecutingAssembly() ), "Kerbalism.dll" );

			if (File.Exists( our_bin ))
			{
				print( "[KerbalismBootstrap] Found Kerbalism bin file at '" + our_bin + "'" );
				if (File.Exists( possible_dll ))
				{
					try
					{
						File.Delete( possible_dll );
						print( "[KerbalismBootstrap] Deleted non-bin DLL at '" + possible_dll + "'" );
					}
					catch
					{
						print( "[KerbalismBootstrap] Could not delete non-bin DLL at '" + possible_dll + "'" );
					}
				}
			}
			else
			{
				print( "[KerbalismBootstrap] ERROR: COULD NOT FIND KERBALISM BIN FILE (" + Util.BinName + ".bin" + ")! Ditching!" );
				return;
			}

			if (Util.IsDllLoaded)
			{
				print( "[KerbalismBootstrap] Kerbalism non-bin DLL already loaded! Ditching!" );
				return;
			}

			AssemblyLoader.LoadPlugin( new FileInfo( our_bin ), our_bin, null );
			AssemblyLoader.LoadedAssembly loadedAssembly = Util.FindKerbalismBin();
			if (loadedAssembly == null)
			{
				print( "[KerbalismBootstrap] Kerbalism failed to load! Ditching!" );
				return;
			}
			else
			{
				print( "[KerbalismBootstrap] Kerbalism loaded!" );
			}

			loadedAssembly.Load();

			foreach (Type type in loadedAssembly.assembly.GetTypes())
			{
				foreach (Type loadedType in AssemblyLoader.loadedTypes)
				{
					if (loadedType.IsAssignableFrom( type ))
					{
						loadedAssembly.types.Add( loadedType, type );
						PropertyInfo temp = typeof( Dictionary<Type, Dictionary<String, Type>> ).GetProperty( "typesDictionary" );
						if (temp != null)
						{
							Dictionary<Type, Dictionary<String, Type>> dict = (Dictionary<Type, Dictionary<String, Type>>) temp.GetValue( loadedAssembly, null );
							Util.AddToLoadedTypesDict( ref dict, loadedType, type );
						}

					}
				}

				if (type.IsSubclassOf( typeof( MonoBehaviour ) ))
				{
					KSPAddon addonAttribute = (KSPAddon) type.GetCustomAttributes( typeof( KSPAddon ), true ).FirstOrDefault();
					if (addonAttribute != null && addonAttribute.startup == KSPAddon.Startup.Instantly)
					{
						AddonLoaderWrapper.StartAddon( loadedAssembly, type, addonAttribute, KSPAddon.Startup.Instantly );
					}
				}
			}
		}

		public string AssemblyDirectory( Assembly a )
		{
			string codeBase = Assembly.GetExecutingAssembly().CodeBase;
			UriBuilder uri = new UriBuilder( codeBase );
			string path = Uri.UnescapeDataString( uri.Path );
			return Path.GetDirectoryName( path );
		}
	}
}
