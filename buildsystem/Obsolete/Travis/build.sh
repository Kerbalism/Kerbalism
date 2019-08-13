#!/bin/bash
ALLVERS="1.7.1 1.7.0 1.6.1 1.5.1 1.4.5"
ALLBINS="17 170 16 15 14"

bash "buildscripts/Travis/inc_buildnumber.sh"

if [ "$1" != "" ] && [ "$2" != "" ]; then
  echo "building for KSP Version $1, binary file $2"
  export KSPVERS=$1
  export KSPBINS=$2
else
  export KSPVERS=$ALLVERS
  export KSPBINS=$ALLBINS
fi

export TRAVIS_BUILD_DIR=${TRAVIS_BUILD_DIR:-$PWD}
IFS=' ' read -r -a _KSPVERS <<< "$KSPVERS"
IFS=' ' read -r -a _KSPBINS <<< "$KSPBINS"

nuget restore -Verbosity detailed "$TRAVIS_BUILD_DIR/packages.config" -SolutionDirectory "$TRAVIS_BUILD_DIR"
for element in "${!_KSPVERS[@]}"
do
	rm -rf "src/DLLs"
	current_kspvr="${_KSPVERS[$element]}"
	current_kspbin="${_KSPBINS[$element]}"
	echo "Building for $current_kspvr / $current_kspbin"
	mkdir "src/DLLs"
	mkdir "src/archives"
	filename="KSP-$current_kspvr.7z"
	if [ -f "src/archives/$filename" ]; then
		cp "src/archives/$filename" .
	else
		wget "https://img.steamport.xyz/$filename" || rm -f "$filename"
		cp "$filename" "src/archives"
	fi
	7za x $filename -osrc/DLLs -pgQn337XZBEFxzFuVwzKgc27ehZo7XLz485hh3erqF9
	msbuild /p:DefineConstants="KSP${current_kspbin}" Kerbalism.sln /t:Build /p:Configuration="Release"
	/bin/cp -rf "src/KerbalismBootstrap/obj/Release/KerbalismBootstrap.dll" "GameData/Kerbalism/KerbalismBootstrap.dll"
	/bin/cp -rf "src/Kerbalism/obj/Release/Kerbalism.dll" "GameData/Kerbalism/Kerbalism${current_kspbin}.kbin"
	rm -f $filename
done
cp $TRAVIS_BUILD_DIR/packages/Lib.Harmony.1.2.0.1/lib/net35/0Harmony.dll GameData/Kerbalism/0Harmony.1.2.0.1.dll
rm -f GameData/Kerbalism/Kerbalism.dll

if [ "$1" == "clean" ]; then
	rm -rf "src/archives"
fi
