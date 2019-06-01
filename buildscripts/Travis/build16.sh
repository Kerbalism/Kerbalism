#!/bin/bash


nuget restore -Verbosity detailed "packages.config" -SolutionDirectory .
rm -rf "src/DLLs"
current_kspvr="1.6.1"
current_kspbin="16"
echo "Building for $current_kspvr / $current_kspbin"
filename="KSP-$current_kspvr.7z"
wget "https://img.steamport.xyz/$filename"
mkdir "src/DLLs"
cp packages/Lib.Harmony.1.2.0.1/lib/net35/0Harmony.dll src/DLLs
7za x $filename -osrc/DLLs -pgQn337XZBEFxzFuVwzKgc27ehZo7XLz485hh3erqF9
bash "buildscripts/Travis/avc_to_assembly.sh"
msbuild /p:DefineConstants="KSP${current_kspbin}" Kerbalism.sln /t:Build /p:Configuration="Release"
/bin/cp -rf "src/KerbalismBootstrap/obj/Release/KerbalismBootstrap.dll" "GameData/Kerbalism/KerbalismBootstrap.dll"
/bin/cp -rf "src/Kerbalism/obj/Release/Kerbalism.dll" "GameData/Kerbalism/Kerbalism${current_kspbin}.bin"
rm -rf $filename

rm -f GameData/Kerbalism/Kerbalism.dll
