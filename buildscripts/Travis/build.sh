#!/bin/bash
IFS=' ' read -r -a _KSPVERS <<< "$KSPVERS"
IFS=' ' read -r -a _KSPBINS <<< "$KSPBINS"

for element in "${!_KSPVERS[@]}"
do
	rm -rf "src/DLLs"
	current_kspvr="${_KSPVERS[$element]}"
	current_kspbin="${_KSPBINS[$element]}"
    echo "Building for $current_kspvr / $current_kspbin"
	filename="KSP-$current_kspvr.7z"
	wget "https://img.steamport.xyz/$filename"
	#cp ~/Downloads/$filename .
	mkdir "src/DLLs"
	7za x $filename -osrc/DLLs -pgQn337XZBEFxzFuVwzKgc27ehZo7XLz485hh3erqF9
	bash "buildscripts/Travis/avc_to_assembly.sh"
	msbuild /p:DefineConstants="KSP${current_kspbin}" Kerbalism.sln /t:Build /p:Configuration="Release"
	/bin/cp -rf "src/KerbalismBootstrap/obj/Release/KerbalismBootstrap.dll" "GameData/Kerbalism/KerbalismBootstrap.dll"
	/bin/cp -rf "src/Kerbalism/obj/Release/Kerbalism.dll" "GameData/Kerbalism/Kerbalism${current_kspbin}.bin"
	rm -rf $filename
done