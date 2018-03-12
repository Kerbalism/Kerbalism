rem Generate the Kerbalism.zip Release package.
rem For information on how to setup your environment.
rem see https://github.com/ShotgunNinja/Kerbalism/tree/master/misc/VisualStudio/Readme.md

@echo off

rem get parameters that are passed by visual studio post build event
SET TargetName=%1
SET Dllversion=%2

rem make sure the initial working directory is the one containing the current script
SET scriptPath=%~dp0
SET rootPath=%scriptPath%..\..\..\..\
SET initialWD=%CD%

echo Generating %TargetName% Release Package...
cd "%rootPath%"
xcopy /y "%initialWD%\%TargetName%.dll" GameData\%TargetName%\%TargetName%.dll*

IF EXIST package\ rd /s /q package
mkdir package
cd package
mkdir GameData
cd GameData
mkdir "%TargetName%"
cd "%TargetName%"

xcopy /y /e "..\..\..\GameData\%TargetName%\*" .
xcopy /y ..\..\..\CHANGELOG.md .
xcopy /y ..\..\..\License .
xcopy /y ..\..\..\README.md .

echo.
echo Compressing %TargetName% Release Package...
IF EXIST "%rootPath%%TargetName%*.zip" del "%rootPath%%TargetName%*.zip"
"%scriptPath%7za.exe" a "..\..\..\%TargetName%%Dllversion%.zip" ..\..\GameData

cd "%rootPath%"
rd /s /q package

cd "%initialWD%"
