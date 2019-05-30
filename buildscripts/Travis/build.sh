#!/bin/bash
export KSPVERS=${KSPVERS:-"1.7.1 1.6.1 1.5.1 1.4.5"}
export KSPBINS=${KSPBINS:-"17 16 15 14"}
export TRAVIS_BUILD_DIR=${TRAVIS_BUILD_DIR:-$PWD}
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
	mkdir "src/DLLs"
	7za x $filename -osrc/DLLs -pgQn337XZBEFxzFuVwzKgc27ehZo7XLz485hh3erqF9
	bash "buildscripts/Travis/avc_to_assembly.sh"
	msbuild /p:DefineConstants="KSP${current_kspbin}" Kerbalism.sln /t:Build /p:Configuration="Release"
	/bin/cp -rf "src/KerbalismBootstrap/obj/Release/KerbalismBootstrap.dll" "GameData/Kerbalism/KerbalismBootstrap.dll"
	/bin/cp -rf "src/Kerbalism/obj/Release/Kerbalism.dll" "GameData/Kerbalism/Kerbalism${current_kspbin}.bin"
	rm -rf $filename
done
rm -f GameData/Kerbalism/Kerbalism.dll
