# Exercise the current production science allocation and requirements methods.
# The fixtures replace KSP state, resource stores and drives; they do not replace
# the allocation, cap, recording or sunlight algorithms under test.
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$experimentSource = Get-Content -Raw (Join-Path $repoPath 'src/Kerbalism/Modules/Experiment.cs')
$requirementSource = Get-Content -Raw (Join-Path $repoPath 'src/Kerbalism/Science/ExperimentRequirements.cs')
$intervalSource = Get-Content -Raw (Join-Path $repoPath 'src/Kerbalism/Simulation/SubSteps/SubStepData.cs')

function Get-ProductionBlock([string] $source, [string] $signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production block: $signature" }
    $body = $source.IndexOf('{', $start)
    $depth = 1
    $end = $body + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw "Unterminated production block: $signature" }
    return $source.Substring($start, $end - $start)
}

$experimentSignatures = @(
    'private sealed class SubjectDuration',
    'private static double RunningUpdateBodyBiomes(',
    'private static double ForcedRemainingData(',
    'private static void RecordExperimentData(',
    'private static Drive GetDrive('
)
$experimentMethods = ($experimentSignatures | ForEach-Object {
    Get-ProductionBlock $experimentSource $_
}) -join "`n"
$requirementSignatures = @(
    'public enum Require',
    'public class RequireDef',
    'internal double TestSampleRequirements('
)
$requirementMethods = ($requirementSignatures | ForEach-Object {
    Get-ProductionBlock $requirementSource $_
}) -join "`n"
$intervalSource = [regex]::Replace($intervalSource, '(?m)^using [^\r\n]+;\s*', '')
$fixtures = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures.cs')
$production = "`nnamespace KERBALISM { public partial class Experiment {`n" + $experimentMethods +
    "`n} public partial class ExperimentRequirements {`n" + $requirementMethods + "`n} }"
Add-Type -TypeDefinition ($fixtures + $intervalSource + $production)
[KERBALISM.Experiment]::RunTests()
