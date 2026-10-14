# PowerShell 7; pass the Unity.Mathematics.dll supplied by KSPBurst v1.7.4.11.
# Executes current production methods with managed job buffers, outside Unity.
param([Parameter(Mandatory = $true)][string]$MathAssembly)
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$simulationSource = Get-Content -Raw (Join-Path $repoPath 'src/Kerbalism/Simulation/SubSteps/SubStepSimulation.cs')
function Get-ProductionMethod([string]$signature) {
    $start = $simulationSource.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production method: $signature" }
    $body = $simulationSource.IndexOf('{', $start)
    $depth = 1
    $end = $body + 1
    while ($depth -gt 0) {
        if ($simulationSource[$end] -eq '{') { $depth++ }
        if ($simulationSource[$end] -eq '}') { $depth-- }
        $end++
    }
    $simulationSource.Substring($start, $end - $start)
}
$signatures = @(
    'private static bool HasFiniteVesselResults(',
    'private static void ValidateBodyPositions(',
    'private static bool IsFinite(',
    'private static void EnsureCapacity<T>(',
    'private static bool TryCreateVesselSnapshot(',
    'private static SubStepBodyNative CreateBodySnapshot(',
    'private static SubStepOrbitNative CreateOrbitSnapshot(',
    'private static double3 ToDouble3('
)
$methods = ($signatures | ForEach-Object { Get-ProductionMethod $_ }) -join "`n"
$tolerance = [regex]::Match($simulationSource, 'private const double TimeMatchTolerance = [^;]+;').Value
$jobs = Get-Content -Raw (Join-Path $repoPath 'src/Kerbalism/Simulation/SubSteps/SubStepJobs.cs')
$jobs = [regex]::Replace($jobs, '(?m)^using [^\r\n]+;\s*', '')
$stubs = Get-Content -Raw (Join-Path $PSScriptRoot 'Stubs.cs')
$checks = Get-Content -Raw (Join-Path $PSScriptRoot 'Checks.cs')
[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $MathAssembly).Path) | Out-Null
Add-Type -TypeDefinition ($stubs + $jobs + $checks + "`nnamespace KERBALISM { public static partial class GeometryChecks {`n" + $tolerance + "`n" + $methods + "`n} }") -ReferencedAssemblies @(
    $MathAssembly,
    (Join-Path $PSHOME 'ref/mscorlib.dll'),
    (Join-Path $PSHOME 'ref/System.Runtime.dll'),
    (Join-Path $PSHOME 'ref/System.Collections.dll'),
    (Join-Path $PSHOME 'ref/System.Console.dll')
)
[KERBALISM.GeometryChecks]::Run()
