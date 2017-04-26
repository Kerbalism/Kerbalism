rem Generate the Kerbalism.zip Release package.
rem For information on how to setup your environment.
rem see https://github.com/ShotgunNinja/Kerbalism/tree/master/misc/VisualStudio/Readme.md

@echo off

rem get parameters that are passed by visual studio post build event
SET outDllPath=%1

rem make sure the initial working directory is the one containing the current script
SET scriptPath=%~dp0
SET initialWD=%CD%

echo Generating Kerbalism Release Package...
cd "%scriptPath%..\..\..\..\"
xcopy /y "%outDllPath%Kerbalism.dll" GameData\Kerbalism\Kerbalism.dll*

IF EXIST package\ rd /s /q package
mkdir package
cd package
mkdir GameData
cd GameData
mkdir Kerbalism
cd Kerbalism

xcopy /y /e ..\..\..\GameData\Kerbalism\* .
xcopy /y ..\..\..\CHANGELOG.md .
xcopy /y ..\..\..\License .
xcopy /y ..\..\..\README.md .

echo.
echo Compressing Kerbalism Release Package...
IF EXIST ..\..\..\Kerbalism.zip del ..\..\..\Kerbalism.zip
"%scriptPath%7za.exe" a ..\..\..\Kerbalism.zip ..\..\GameData

cd ..\..\..\
rd /s /q package

cd "%initialWD%"
