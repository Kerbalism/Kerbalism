@echo off

for /f "tokens=*" %%i in ('..\Utility\win_vswhere\vswhere.exe -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe') do set msbuild=%%i

echo The path to your MSBuild executable is :
echo --------------------------------------------------------------------
echo %msbuild%
echo --------------------------------------------------------------------
echo Add the folder that contains it to your PATH environment variable to 
echo be able to use the "msbuild" command directly from the command line,
echo and to be able to run the *.bat scripts
PAUSE