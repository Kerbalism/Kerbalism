# Windows / .NET Framework regression checks using the installed KSP 1.12
# ConfigNode, CRP and ModuleManager libraries. Does not modify the game install.
# Example: ./misc/tests/RunCryoTests.ps1 -GameRoot 'E:/Games/KSP'
param(
    [Parameter(Mandatory = $true)]
    [string] $GameRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$gamePath = (Resolve-Path -LiteralPath $GameRoot).Path
$managedPath = Join-Path $gamePath 'KSP_x64_Data/Managed'
$managerDll = @(Get-ChildItem -LiteralPath (Join-Path $gamePath 'GameData') -Filter 'ModuleManager.*.dll')
if ($managerDll.Count -ne 1) { throw 'Expected one ModuleManager DLL in GameData.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$outputExe = Join-Path ([System.IO.Path]::GetTempPath()) ('KerbalismCryoTests-' + [guid]::NewGuid() + '.exe')
try {
    & $compiler /nologo /target:exe /platform:x64 "/out:$outputExe" `
        "/reference:$(Join-Path $managedPath 'Assembly-CSharp.dll')" `
        "/reference:$(Join-Path $managedPath 'UnityEngine.dll')" `
        "/reference:$(Join-Path $managedPath 'UnityEngine.CoreModule.dll')" `
        "/reference:$(Join-Path $managedPath 'UnityEngine.UI.dll')" `
        "/reference:$($managerDll[0].FullName)" `
        (Join-Path $PSScriptRoot 'CryoTanksConfigTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $outputExe $repoRoot $gamePath
    if ($LASTEXITCODE -ne 0) { throw 'CryoTanks configuration tests failed.' }
}
finally {
    if (Test-Path -LiteralPath $outputExe) { Remove-Item -LiteralPath $outputExe -Force }
}
