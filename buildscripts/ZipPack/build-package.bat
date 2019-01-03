rem Generate the zip Release package.
rem For information on how to setup your environment.
rem see https://github.com/steamp0rt/Kerbalism/tree/master/CONTRIBUTING.md

@echo off

rem get parameters that are passed by visual studio post build event
SET TargetName=%1
SET Dllversion=%~n2
SET KSPversion=%3

rem make sure the initial working directory is the one containing the current script
SET scriptPath=%~dp0
SET rootPath=%scriptPath%..\..\
SET initialWD=%CD%

echo Generating %TargetName% Bootstrap files...
cd "%rootPath%"
rem copy Bootstrap dll from build directory to GameData
IF EXIST "%initialWD%\%TargetName%Bootstrap.dll" xcopy /y "%initialWD%\%TargetName%Bootstrap.dll" "GameData\%TargetName%\*" > nul
rem rename dll to KSP version specific bin
echo %TargetName%.dll -^> %TargetName%%KSPversion%.bin
move /y "%initialWD%\%TargetName%.dll" "%initialWD%\%TargetName%%KSPversion%.bin" > nul
rem if built version is greater than KSP 1.3.1 then we assume latest version and copy built bin for the other compatible versions
IF %KSPversion% GTR 13 xcopy /y "%initialWD%\%TargetName%%KSPversion%.bin" "%initialWD%\%TargetName%14.bin" > nul
IF %KSPversion% GTR 13 xcopy /y "%initialWD%\%TargetName%%KSPversion%.bin" "%initialWD%\%TargetName%15.bin" > nul
rem copy Bootstrap bins from build directory to GameData
xcopy /y "%initialWD%\%TargetName%*.bin" "GameData\%TargetName%\*" > nul

echo Generating %TargetName% Release Package...
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

echo.
echo Compressing %TargetName% Release Package...
IF EXIST "%rootPath%%TargetName%*.zip" del "%rootPath%%TargetName%*.zip"
"%scriptPath%7za.exe" a "..\..\..\%TargetName%%Dllversion%.zip" ..\..\..\GameData

rem check all bootstrap files exist
cd "%rootPath%"
IF NOT EXIST "package\GameData\%TargetName%\%TargetName%Bootstrap.dll" echo **WARNING** %TargetName%Bootstrap.dll is missing
IF NOT EXIST "package\GameData\%TargetName%\%TargetName%*.bin" echo **WARNING** %TargetName% bin is missing

rem remove temp files
rd /s /q package

cd "%initialWD%"
