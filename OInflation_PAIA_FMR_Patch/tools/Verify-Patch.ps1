<#
.SYNOPSIS
    File-level checks for the "OInflation + FMR for P.A.I.A" patch.

.PARAMETER PaiaDir       Extracted P.A.I.A 4.5 OAR folder ("...\OpenAnimationReplacer\PAIA").
.PARAMETER ExpansionDir  Extracted P.A.I.A Expansion OAR 1.2 folder ("...\OpenAnimationReplacer\PAIA Expansion").
.PARAMETER FmrPlugin     Fertility Mode.esm from Fertility Mode Reloaded 1.0.7.
.PARAMETER OInflationPlugin  OInflation.esp from OInflation 1.0.1.
#>
param(
    [Parameter(Mandatory)][string]$PaiaDir,
    [Parameter(Mandatory)][string]$ExpansionDir,
    [Parameter(Mandatory)][string]$FmrPlugin,
    [Parameter(Mandatory)][string]$OInflationPlugin
)

$ErrorActionPreference = 'Stop'
$pkg = Join-Path (Split-Path $PSScriptRoot -Parent) 'FOMOD\OInflation PAIA FMR Patch FOMOD'
$script:pass = 0; $script:fail = 0
function Check([string]$what, [bool]$ok) {
    if ($ok) { $script:pass++ } else { $script:fail++; Write-Host "FAIL: $what" -ForegroundColor Red }
}

# Every form the patch points at, with the record it must be.
$forms = & (Join-Path $PSScriptRoot 'Get-PluginForms.ps1') -Plugin $FmrPlugin -Types FACT, SPEL
$forms += & (Join-Path $PSScriptRoot 'Get-PluginForms.ps1') -Plugin $OInflationPlugin -Types MGEF
$expected = @{
    'Fertility Mode.esm|02666B' = 'FACT  ImmersiveEffectsFaction'
    'Fertility Mode.esm|1B818'  = 'SPEL  _JSW_BB_Trimester2'
    'Fertility Mode.esm|1B81A'  = 'SPEL  _JSW_BB_Trimester3'
    'OInflation.esp|2FA1'       = 'MGEF  OInflationEffect'
}
function Find-Form([string]$id, [string]$typeAndEdid) {
    $type, $edid = $typeAndEdid -split '\s+'
    [bool]($forms | Where-Object { $_ -match "^$type\s+[0-9A-F]{2}0*$($id.TrimStart('0'))\s+$edid(\s|$)" })
}
foreach ($k in $expected.Keys) { Check "form $k is $($expected[$k])" (Find-Form ($k -split '\|')[1] $expected[$k]) }

function Get-Refs($node) {
    if ($node -is [Collections.IEnumerable] -and $node -isnot [string]) { foreach ($n in $node) { Get-Refs $n }; return }
    if ($node -isnot [PSCustomObject]) { return }
    foreach ($p in $node.PSObject.Properties) {
        if ($p.Value -is [PSCustomObject] -and $p.Value.pluginName) { "$($p.Value.pluginName)|$($p.Value.formID)" }
        Get-Refs $p.Value
    }
}
function Get-Leaves($conds) {   # flattens OR/AND
    foreach ($c in $conds) { if ($c.Conditions) { Get-Leaves $c.Conditions } else { $c } }
}

# --- P.A.I.A -------------------------------------------------------------------------------
$paiaPriorities = @(Get-ChildItem $PaiaDir -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'config.json') } |
    ForEach-Object { (Get-Content (Join-Path $_.FullName 'config.json') -Raw | ConvertFrom-Json).priority })
foreach ($sub in Get-ChildItem (Join-Path $pkg 'PAIA') -Directory) {
    $j = Get-Content (Join-Path $sub.FullName 'config.json') -Raw | ConvertFrom-Json
    Check "PAIA\$($sub.Name): animation folder exists" ([bool](Get-ChildItem (Join-Path $PaiaDir $j.overrideAnimationsFolder) -Recurse -Filter *.hkx -ErrorAction SilentlyContinue))
    Check "PAIA\$($sub.Name): priority not used by P.A.I.A" ($paiaPriorities -notcontains $j.priority)
    Check "PAIA\$($sub.Name): player-only toggle is off" ([bool]($j.conditions | Where-Object { $_.Keyword.editorID -eq 'PlayerKeyword' -and $_.disabled }))
    foreach ($r in Get-Refs $j.conditions) { Check "PAIA\$($sub.Name): known form $r" ($expected.ContainsKey($r)) }
}

# --- P.A.I.A Expansion ---------------------------------------------------------------------
$upstream = Get-ChildItem -LiteralPath $ExpansionDir -Directory
foreach ($variant in 'PAIA Expansion - Recommended', 'PAIA Expansion - All Tiers') {
    $dirs = Get-ChildItem -LiteralPath (Join-Path $pkg $variant) -Directory
    Check "$variant`: one submod per upstream submod" ($dirs.Count -eq $upstream.Count)
    $prios = @()
    foreach ($u in $upstream) {
        $src = Get-Content -LiteralPath (Join-Path $u.FullName 'config.json') -Raw | ConvertFrom-Json
        $path = Join-Path (Join-Path $pkg $variant) "$($u.Name) - FMR OInflation\config.json"
        if (-not (Test-Path -LiteralPath $path)) { Check "$variant\$($u.Name): exists" $false; continue }
        $j = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $tag = "$variant\$($u.Name)"
        $prios += $j.priority

        Check "$tag`: points at the upstream animations" ($j.overrideAnimationsFolder -eq $src.overrideAnimationsFolder)
        Check "$tag`: priority is upstream + 100000" ($j.priority -eq $src.priority + 100000)

        # All non-pregnancy conditions carried over unchanged, in order.
        $keepSrc = @($src.conditions | Where-Object { $_.condition -ne 'OR' } | ForEach-Object { $_ | ConvertTo-Json -Depth 8 -Compress })
        $keepNew = @($j.conditions | Where-Object { $_.condition -notin 'OR', 'IsChild' } | ForEach-Object { $_ | ConvertTo-Json -Depth 8 -Compress })
        Check "$tag`: same non-pregnancy conditions as upstream" (($keepSrc -join '|') -eq ($keepNew -join '|'))
        Check "$tag`: excludes children" ([bool]($j.conditions | Where-Object { $_.condition -eq 'IsChild' -and $_.negated }))

        $tier = ((Get-Leaves $src.conditions) | Where-Object { $_.Faction.pluginName -eq 'Fertility Mode 3 Fixes and Updates.esp' }).'Numeric value'.value
        $leaves = @(Get-Leaves ($j.conditions | Where-Object { $_.condition -eq 'OR' }))
        $min = $leaves | Where-Object { $_.Faction.formID -eq '02666B' -and $_.Comparison -eq '>=' }
        $max = $leaves | Where-Object { $_.Faction.formID -eq '02666B' -and $_.Comparison -eq '<=' }
        Check "$tag`: FMR rank >= $tier" ($min.'Numeric value'.value -eq $tier)
        Check "$tag`: FMR rank <= 100" ($max.'Numeric value'.value -eq 100)

        $hasOinf = [bool]($leaves | Where-Object { $_.'Magic effect'.formID -eq '2FA1' })
        $wantOinf = ($variant -like '*All Tiers') -or ($tier -le 50)
        Check "$tag`: OInflation included = $wantOinf" ($hasOinf -eq $wantOinf)

        foreach ($r in Get-Refs $j.conditions) { Check "$tag`: known form $r" ($expected.ContainsKey($r)) }
    }
    Check "$variant`: priorities unique" (($prios | Select-Object -Unique).Count -eq $prios.Count)
}

# --- Installer -----------------------------------------------------------------------------
[xml]$mc = Get-Content (Join-Path $pkg 'fomod\ModuleConfig.xml') -Raw
[xml]$null = Get-Content (Join-Path $pkg 'fomod\info.xml') -Raw
foreach ($f in $mc.SelectNodes('//folder')) {
    Check "installer source '$($f.source)' exists" (Test-Path -LiteralPath (Join-Path $pkg $f.source))
    Check "installer destination '$($f.destination)' is under OpenAnimationReplacer" ($f.destination -like 'Meshes\Actors\Character\Animations\OpenAnimationReplacer\PAIA*')
}
$shipped = Get-ChildItem -LiteralPath $pkg -Recurse -File | Where-Object { $_.Extension -notin '.json', '.xml' }
Check "package ships only .json and .xml files" (-not $shipped)

Write-Host "$script:pass passed, $script:fail failed"
if ($script:fail) { exit 1 }
