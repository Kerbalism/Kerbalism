# Rename BranchLoc keys: remove 6-char hex suffixes and fix truncated stems.
# Kerbalism convention: BranchLoc_Semantic_name_with_underscores (no hash suffix).

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$EnUs = Join-Path $Root 'GameData\KerbalismConfig\Localization\en-us.cfg'
$HexSuffix = [regex]'_[0-9a-f]{6}$'

function Get-Disambiguator([string]$Value) {
    $tags = [regex]::Matches($Value, '<b>([^<]+)</b>') | ForEach-Object { $_.Groups[1].Value }
    if ($tags.Count -gt 0) {
        $t = ($tags | Select-Object -First 2) -join '_and_'
        $t = $t -replace '[^A-Za-z0-9]+', '_'
        $t = $t.Trim('_')
        if ($t.Length -gt 0) { return $t }
    }
    return 'alt'
}

function Normalize-BranchLocStem([string]$Stem) {
    $replacements = [ordered]@{
        '_Processes_requiri$' = ''
        '_Processes_requi$' = ''
        '_Enriched$' = '_Enriched_Uranium_production'
        '_Enriched_Ur$' = '_Enriched_Uranium_production'
        '_Nu$' = '_Nuclear_reactors'
        '_Nucl$' = '_Nuclear_reactors'
        '_producing_W$' = '_producing_Water'
        '_out_o$' = '_out_of_ore'
        '_the_FFT_Orion$' = '_the_FFT_Orion_engine'
        '_together_under_garg$' = '_together_under_high_pressure'
        '_for_Lithium_Aluminium_and_Silica$' = '_for_Lithium_Aluminium_and_Silicate'
        '_LF_priority$' = '_LF_priority'
        '_Easy$' = '_Easy'
        '_Hard$' = '_Hard'
    }
    foreach ($pattern in $replacements.Keys) {
        if ($Stem -match $pattern) {
            $Stem = [regex]::Replace($Stem, $pattern, $replacements[$pattern])
        }
    }
    return $Stem
}

$entries = @()
foreach ($line in Get-Content $EnUs -Encoding UTF8) {
    if ($line -match '^\s*#KERBALISM_(BranchLoc_\S+)\s*=\s*(.+)$') {
        $entries += [pscustomobject]@{
            Old = $matches[1]
            Value = $matches[2].Trim()
        }
    }
}

$mapping = @{}
$usedNew = @{}

foreach ($e in $entries) {
    if (-not $HexSuffix.IsMatch($e.Old)) {
        $mapping[$e.Old] = $e.Old
        $usedNew[$e.Old] = $true
        continue
    }

    $stem = Normalize-BranchLocStem ($HexSuffix.Replace($e.Old, ''))
    $new = $stem
    $suffix = 0
    while ($usedNew.ContainsKey($new)) {
        $suffix++
        $tag = Get-Disambiguator $e.Value
        if ($suffix -eq 1) { $new = "${stem}_${tag}" }
        else { $new = "${stem}_${tag}_${suffix}" }
    }
    $mapping[$e.Old] = $new
    $usedNew[$new] = $true
}

# Manual overrides for remaining ambiguous pairs (Kerbalism-style semantic names)
$overrides = @{
    'BranchLoc_Primary_purpose_of_this_converter_is_to_extract_5d6d5a' = 'BranchLoc_Extract_LqdFluorine_from_Minerals_desc'
    'BranchLoc_Primary_purpose_of_this_converter_is_to_extract_4574f8' = 'BranchLoc_Extract_LqdFluorine_and_Beryllium_desc'
    'BranchLoc_Fissile_Fuel_Easy_852195' = 'BranchLoc_Fissile_Fuel_Easy_title'
    'BranchLoc_Fissile_Fuel_Easy_5de83d' = 'BranchLoc_Fissile_Fuel_Easy_setup'
    'BranchLoc_Fissile_Fuel_Hard_118de4' = 'BranchLoc_Fissile_Fuel_Hard_title'
    'BranchLoc_Fissile_Fuel_Hard_7170f9' = 'BranchLoc_Fissile_Fuel_Hard_setup'
    'BranchLoc_Extract_Oxygen_CarbonDioxide_and_Shielding_out_o_e52b2d' = 'BranchLoc_Extract_Oxygen_from_Ore_MRE_desc'
    'BranchLoc_Extract_Oxygen_CarbonDioxide_and_Shielding_out_o_9618db' = 'BranchLoc_Extract_Oxygen_from_Rock_desc'
    'BranchLoc_Extract_Oxygen_CarbonDioxide_and_Shielding_out_o_5da0fa' = 'BranchLoc_Extract_Oxygen_from_Ore_desc'
    'BranchLoc_Slam_LqdDeuterium_and_LqdHe3_together_under_garg_b7cf8f' = 'BranchLoc_Slam_LqdDeuterium_and_LqdHe3_compress_desc'
    'BranchLoc_Slam_LqdDeuterium_and_LqdHe3_together_under_garg_bb7638' = 'BranchLoc_Slam_LqdDeuterium_and_LqdHe3_fusion_pellets_desc'
    'BranchLoc_Produce_shaped_nuclear_charges_for_the_FFT_Orion_6f6de3' = 'BranchLoc_FFT_Orion_pulse_production_desc'
    'BranchLoc_Squeeze_Water_from_Ore_b35b65' = 'BranchLoc_Squeeze_Water_from_Ore_title'
    'BranchLoc_Squeeze_Water_from_Ore_835070' = 'BranchLoc_Squeeze_Water_from_Ore_desc'
    'BranchLoc_Split_Spodumene_for_Lithium_Aluminium_and_Silica_85f883' = 'BranchLoc_Split_Spodumene_extended_desc'
    'BranchLoc_Split_Spodumene_for_Lithium_Aluminium_and_Silica_a54560' = 'BranchLoc_Split_Spodumene_desc'
    'BranchLoc_Sabatier_Process_LF_priority_0103ec' = 'BranchLoc_Sabatier_Process_LF_priority'
    'BranchLoc_Sabatier_process_LF_priority_6ae7f6' = 'BranchLoc_Sabatier_Process_LF_priority'
    'BranchLoc_LH2_is_almost_depleted_on_VESSEL_Processes_requi_c0d34b' = 'BranchLoc_LH2_is_almost_depleted_on_VESSEL'
    'BranchLoc_There_is_no_more_LH2_on_VESSEL_Processes_requiri_862839' = 'BranchLoc_There_is_no_more_LH2_on_VESSEL'
    'BranchLoc_Uraninite_is_almost_depleted_on_VESSEL_Enriched_746e74' = 'BranchLoc_Uraninite_is_almost_depleted_on_VESSEL'
    'BranchLoc_There_is_no_more_Uraninite_on_VESSEL_Enriched_Ur_521c40' = 'BranchLoc_There_is_no_more_Uraninite_on_VESSEL'
    'BranchLoc_Enriched_Uranium_is_almost_depleted_on_VESSEL_Nu_675523' = 'BranchLoc_Enriched_Uranium_is_almost_depleted_on_VESSEL'
    'BranchLoc_There_is_no_more_Enriched_Uranium_on_VESSEL_Nucl_04de5d' = 'BranchLoc_There_is_no_more_Enriched_Uranium_on_VESSEL'
    'BranchLoc_Burns_Liquid_Hydrogen_and_Oxygen_gas_producing_W_464df2' = 'BranchLoc_Burns_Liquid_Hydrogen_and_Oxygen_gas_producing_Water'
    'BranchLoc_Evaporate_Liquid_Hydrogen_into_Hydrogen_gas_34a6a0' = 'BranchLoc_Evaporate_Liquid_Hydrogen_into_Hydrogen_gas'
}

foreach ($k in $overrides.Keys) {
    if ($mapping.ContainsKey($k)) { $mapping[$k] = $overrides[$k] }
}

# Manual overrides applied above; spot-check for hex suffixes after run.

$targets = Get-ChildItem -Path (Join-Path $Root 'GameData\KerbalismConfig') -Recurse -Include *.cfg
$changed = 0
foreach ($file in $targets) {
    $text = [IO.File]::ReadAllText($file.FullName)
    $orig = $text
    foreach ($old in ($mapping.Keys | Sort-Object { $_.Length } -Descending)) {
        $new = $mapping[$old]
        if ($old -eq $new) { continue }
        $text = $text.Replace($old, $new)
    }
    if ($text -ne $orig) {
        [IO.File]::WriteAllText($file.FullName, $text, [Text.UTF8Encoding]::new($false))
        $changed++
        Write-Host "Updated $($file.FullName.Replace($Root + '\', ''))"
    }
}

Write-Host "Renamed $($mapping.Count) keys across $changed files."

# Remove duplicate BranchLoc key lines introduced by merges (keep first)
$locDir = Join-Path $Root 'GameData\KerbalismConfig\Localization'
foreach ($loc in Get-ChildItem $locDir -Filter *.cfg) {
    $lines = Get-Content $loc.FullName -Encoding UTF8
    $seen = @{}
    $out = foreach ($line in $lines) {
        if ($line -match '#KERBALISM_(BranchLoc_\S+)\s*=') {
            $k = $matches[1]
            if ($seen.ContainsKey($k)) { continue }
            $seen[$k] = $true
        }
        $line
    }
    Set-Content -Path $loc.FullName -Value $out -Encoding UTF8
}
Write-Host "Deduped localization cfg files."
$mapFile = Join-Path $Root 'misc\branchloc-key-map.tsv'
$mapping.GetEnumerator() | Where-Object { $_.Key -ne $_.Value } | Sort-Object Key |
    ForEach-Object { "{0}`t{1}" -f $_.Key, $_.Value } |
    Set-Content -Path $mapFile -Encoding UTF8
Write-Host "Mapping written to misc/branchloc-key-map.tsv"
