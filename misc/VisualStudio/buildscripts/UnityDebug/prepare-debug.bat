rem Generate the MDB file needed by UnityVS and Monodevelop for debugging
rem For information on how to setup your debugging environment.
rem see https://github.com/ShotgunNinja/Kerbalism/tree/master/misc/VisualStudio/Readme.md

@echo off

rem get parameters that are passed by visual studio post build event
SET outDllPath=%1

rem make sure the initial working directory is the one containing the current script
SET scriptPath=%~dp0
SET initialWD=%CD%

echo Generating Unity Monodevelop Debug file...
echo Kerbalism.dll -^> %outDllPath%Kerbalism.dll.mdb
cd "%outDllPath%"
"%scriptPath%\pdb2mdb.exe" Kerbalism.dll

cd "%initialWD%"
