<#
.SYNOPSIS
    Builds the OAR condition files for the "OInflation + FMR for P.A.I.A" patch.

.DESCRIPTION
    Writes every config.json under FOMOD\OInflation PAIA FMR Patch FOMOD\ next to this tools folder.
    The fomod\ installer config there is hand-written and left alone.
    - P.A.I.A: two hand-written submods (OInflation, Fertility Mode Reloaded) that reuse "PAIA Idle".
    - P.A.I.A Expansion: one sibling submod per upstream submod. Each keeps the upstream
      non-pregnancy conditions (sex, keyword, shield, combat, random), swaps the pregnancy OR
      block for FMR's ImmersiveEffectsFaction rank (+ OInflation's effect) and points
      overrideAnimationsFolder back at the upstream folder, so no animation is copied.

.PARAMETER ExpansionDir
    The extracted "PAIA Expansion" OAR folder from P.A.I.A Expansion OAR 1.2 (Nexus 124228).
    Only read, never copied.
#>
param(
    [Parameter(Mandatory)][string]$ExpansionDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fomod = Join-Path $root 'FOMOD\OInflation PAIA FMR Patch FOMOD'

# --- Forms (verified against the plugins) -------------------------------------------------
$FMR_PLUGIN = 'Fertility Mode.esm'
$FMR_FACTION = '02666B'          # ImmersiveEffectsFaction, rank = pregnancy progress
$FMR_TRIMESTER2 = '1B818'        # _JSW_BB_Trimester2
$FMR_TRIMESTER3 = '1B81A'        # _JSW_BB_Trimester3
$OINF_PLUGIN = 'OInflation.esp'
$OINF_EFFECT = '2FA1'            # OInflationEffect, present while the belly is inflated

$PRIORITY_OFFSET = 100000        # keeps upstream ordering, sits above every upstream submod
$MODERATE_MAX_TIER = 50          # OInflation's default max belly (60%) ~ these tiers

# --- Minimal JSON writer (OAR style: 4-space indent, floats keep a decimal point) ------------
function ConvertTo-OarJson($v, [int]$depth = 0, [string]$key = '') {
    $pad = ' ' * (4 * ($depth + 1)); $end = ' ' * (4 * $depth)
    if ($null -eq $v) { return 'null' }
    if ($v -is [bool]) { return $v.ToString().ToLower() }
    if ($v -is [string]) { return '"' + ($v -replace '\\', '\\' -replace '"', '\"') + '"' }
    if ($v -is [int] -or $v -is [long] -or $v -is [double] -or $v -is [decimal] -or $v -is [single]) {
        if ($key -eq 'priority') { return ([long]$v).ToString() }
        # ConvertFrom-Json reads upstream numbers as decimal; keep their exact digits.
        $s = if ($v -is [decimal]) { $v.ToString([Globalization.CultureInfo]::InvariantCulture) }
             else { ([double]$v).ToString('R', [Globalization.CultureInfo]::InvariantCulture) }
        if ($s -notmatch '[.eE]') { $s += '.0' }
        return $s
    }
    if ($v -is [Collections.IList]) {
        if ($v.Count -eq 0) { return '[]' }
        $items = foreach ($i in $v) { $pad + (ConvertTo-OarJson $i ($depth + 1)) }
        return "[`n" + ($items -join ",`n") + "`n$end]"
    }
    $props = New-Object 'Collections.Generic.List[object[]]'
    if ($v -is [Collections.IDictionary]) { foreach ($k in $v.Keys) { $props.Add(@($k, $v[$k])) } }
    else { foreach ($p in $v.PSObject.Properties) { $props.Add(@($p.Name, $p.Value)) } }
    $items = foreach ($p in $props) { $pad + '"' + $p[0] + '": ' + (ConvertTo-OarJson $p[1] ($depth + 1) $p[0]) }
    return "{`n" + ($items -join ",`n") + "`n$end}"
}

function Write-Config([string]$dir, $obj) {
    New-Item -ItemType Directory -Force $dir | Out-Null
    $json = ConvertTo-OarJson $obj
    [IO.File]::WriteAllText((Join-Path $dir 'config.json'), $json + "`n", (New-Object Text.UTF8Encoding $false))
}

# --- Condition builders -------------------------------------------------------------------
function Cond-IsFemale { [ordered]@{ condition = 'IsFemale'; requiredVersion = '1.0.0.0' } }
function Cond-NotChild { [ordered]@{ condition = 'IsChild'; requiredVersion = '1.0.0.0'; negated = $true } }
function Cond-PlayerOnlyToggle {
    # Same switch P.A.I.A ships: disabled by default, tick it in OAR's editor for player-only.
    [ordered]@{ condition = 'HasKeyword'; requiredVersion = '1.0.0.0'; disabled = $true; Keyword = [ordered]@{ editorID = 'PlayerKeyword' } }
}
function Cond-FmrRank([string]$cmp, [double]$value) {
    [ordered]@{
        condition = 'FactionRank'; requiredVersion = '1.0.0.0'
        Faction = [ordered]@{ pluginName = $FMR_PLUGIN; formID = $FMR_FACTION }
        Comparison = $cmp; 'Numeric value' = [ordered]@{ value = $value }
    }
}
function Cond-FmrPregnant([double]$minRank) {
    [ordered]@{ condition = 'AND'; requiredVersion = '1.0.0.0'; Conditions = @((Cond-FmrRank '>=' $minRank), (Cond-FmrRank '<=' 100.0)) }
}
function Cond-FmrSpell([string]$formID) {
    [ordered]@{ condition = 'HasSpell'; requiredVersion = '1.0.0.0'; Spell = [ordered]@{ pluginName = $FMR_PLUGIN; formID = $formID } }
}
function Cond-OInflated {
    [ordered]@{
        condition = 'HasMagicEffect'; requiredVersion = '1.0.0.0'
        'Magic effect' = [ordered]@{ pluginName = $OINF_PLUGIN; formID = $OINF_EFFECT }
        'Active effects only' = $false
    }
}
function Cond-Or($list) { [ordered]@{ condition = 'OR'; requiredVersion = '1.0.0.0'; Conditions = @($list) } }

# --- P.A.I.A (base) -----------------------------------------------------------------------
$paia = Join-Path $fomod 'PAIA'
Write-Config (Join-Path $paia 'OInflation') ([ordered]@{
    name = 'OInflation'
    description = 'Plays the P.A.I.A idle while OInflation has inflated the belly.'
    priority = 91044067
    overrideAnimationsFolder = 'PAIA Idle'
    conditions = @((Cond-PlayerOnlyToggle), (Cond-IsFemale), (Cond-NotChild), (Cond-OInflated))
})
Write-Config (Join-Path $paia 'Fertility Mode Reloaded') ([ordered]@{
    name = 'Fertility Mode Reloaded'
    description = 'Plays the P.A.I.A idle in the 2nd and 3rd trimester of a Fertility Mode Reloaded pregnancy.'
    priority = 91044068
    overrideAnimationsFolder = 'PAIA Idle'
    conditions = @((Cond-PlayerOnlyToggle), (Cond-IsFemale), (Cond-NotChild),
        (Cond-Or @((Cond-FmrSpell $FMR_TRIMESTER2), (Cond-FmrSpell $FMR_TRIMESTER3), (Cond-FmrPregnant 33.0))))
})

# --- P.A.I.A Expansion --------------------------------------------------------------------
$upstream = Get-ChildItem -LiteralPath $ExpansionDir -Directory | Sort-Object Name
if (-not $upstream) { throw "No submods found in $ExpansionDir" }

foreach ($variant in @(@{ Dir = 'PAIA Expansion - Recommended'; AllTiers = $false },
                       @{ Dir = 'PAIA Expansion - All Tiers';   AllTiers = $true })) {
    $out = Join-Path $fomod $variant.Dir
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }

    foreach ($sub in $upstream) {
        $src = Get-Content -LiteralPath (Join-Path $sub.FullName 'config.json') -Raw | ConvertFrom-Json
        $or = $src.conditions | Where-Object { $_.condition -eq 'OR' }
        if (@($or).Count -ne 1) { throw "$($sub.Name): expected exactly one OR block" }

        # Upstream tier = its Fertility Mode 3 Fixes threshold; FMR's rank uses the same 0-100 scale.
        $fm3 = $or.Conditions | Where-Object { $_.condition -eq 'FactionRank' -and $_.Faction.pluginName -eq 'Fertility Mode 3 Fixes and Updates.esp' }
        if (-not $fm3) { throw "$($sub.Name): no Fertility Mode threshold to map" }
        $tier = [double]$fm3.'Numeric value'.value

        $pregnant = @($or.Conditions | Where-Object { $_.condition -eq 'HasSpell' -and $_.Spell.pluginName -eq $FMR_PLUGIN })
        $pregnant += Cond-FmrPregnant $tier
        if ($variant.AllTiers -or $tier -le $MODERATE_MAX_TIER) { $pregnant += Cond-OInflated }

        $conditions = @()
        foreach ($c in $src.conditions) {
            if ($c.condition -eq 'OR') { $conditions += Cond-Or $pregnant; continue }
            $conditions += $c
            if ($c.condition -eq 'IsFemale') { $conditions += Cond-NotChild }
        }

        $animFolder = $src.overrideAnimationsFolder
        if (-not (Get-ChildItem -LiteralPath (Join-Path $ExpansionDir $animFolder) -Recurse -Filter *.hkx)) {
            throw "$($sub.Name): override folder '$animFolder' has no animations"
        }

        Write-Config (Join-Path $out "$($sub.Name) - FMR OInflation") ([ordered]@{
            name = "$($src.name) - FMR OInflation"
            description = "$($src.description) (Fertility Mode Reloaded / OInflation)"
            priority = [long]$src.priority + $PRIORITY_OFFSET
            overrideAnimationsFolder = $animFolder
            conditions = $conditions
        })
    }
}

Write-Host "Wrote configs to $fomod"
