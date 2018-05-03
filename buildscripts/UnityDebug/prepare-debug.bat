rem Generate the MDB file needed by UnityVS and Monodevelop for debugging
rem For information on how to setup your debugging environment.
rem see https://github.com/MoreRobustThanYou/Kerbalism/tree/master/CONTRIBUTING.md

@echo off

rem get parameters that are passed by visual studio post build event
SET TargetName=%1

rem make sure the initial working directory is the one containing the current script
SET scriptPath=%~dp0

echo Generating Unity Monodevelop Debug file...
echo %TargetName%.dll -^> %TargetName%.dll.mdb
"%scriptPath%\pdb2mdb.exe" %TargetName%.dll
