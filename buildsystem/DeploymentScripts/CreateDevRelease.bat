@echo off

for /f "tokens=*" %%i in ('C:\Users\Got\Source\Repos\Kerbalism\Kerbalism\buildsystem\Utility\win_vswhere\vswhere.exe -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe') do set msbuild=%%i


"%msbuild%"
@echo on