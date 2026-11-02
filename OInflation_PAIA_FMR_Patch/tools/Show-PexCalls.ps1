<#
.SYNOPSIS
    Minimal Skyrim .pex disassembler: prints every function that calls a given method.
    Used to read how Fertility Mode Reloaded sets ImmersiveEffectsFaction (it ships no sources).
#>
param([Parameter(Mandatory)][string]$Pex, [string]$Method = 'SetFactionRank')

$b = [IO.File]::ReadAllBytes($Pex); $p = 0
# .pex is big-endian
function U8 { $script:p++; $b[$script:p - 1] }
function U16 { $v = ([int]$b[$script:p] -shl 8) -bor [int]$b[$script:p + 1]; $script:p += 2; $v }
function U32 { $v = ([int64]$b[$script:p] -shl 24) -bor ([int64]$b[$script:p + 1] -shl 16) -bor ([int64]$b[$script:p + 2] -shl 8) -bor [int64]$b[$script:p + 3]; $script:p += 4; $v }
function WStr { $n = U16; $s = [Text.Encoding]::UTF8.GetString($b, $script:p, $n); $script:p += $n; $s }
function F32 { $a = [byte[]]$b[($script:p + 3)..$script:p]; $script:p += 4; [BitConverter]::ToSingle($a, 0) }

$null = U32; $null = U8; $null = U8; $null = U16; $script:p += 8
$null = WStr; $null = WStr; $null = WStr
$strings = @(); $n = U16; for ($i = 0; $i -lt $n; $i++) { $strings += WStr }
function S { $strings[(U16)] }
if (U8) { $script:p += 8; $n = U16; for ($i = 0; $i -lt $n; $i++) { $script:p += 7; $c = U16; $script:p += 2 * $c } }
$n = U16; $script:p += 3 * $n

function Val {
    switch (U8) {
        0 { 'none' } 1 { S } 2 { '"' + (S) + '"' } 3 { $u = U32; if ($u -ge 2147483648L) { $u - 4294967296L } else { $u } } 4 { F32 } 5 { [bool](U8) }
    }
}
$ops = @{ 0='nop',0;1='iadd',3;2='fadd',3;3='isub',3;4='fsub',3;5='imul',3;6='fmul',3;7='idiv',3;8='fdiv',3;9='imod',3
    10='not',2;11='ineg',2;12='fneg',2;13='assign',2;14='cast',2;15='cmp_eq',3;16='cmp_lt',3;17='cmp_lte',3;18='cmp_gt',3;19='cmp_gte',3
    20='jmp',1;21='jmpt',2;22='jmpf',2;23='callmethod',3;24='callparent',2;25='callstatic',3;26='return',1;27='strcat',3;28='propget',3
    29='propset',3;30='array_create',2;31='array_length',2;32='array_get',3;33='array_set',3;34='array_find',4;35='array_rfind',4 }

function Func([string]$name) {
    $null = S; $null = S; $null = U32; $null = U8
    $np = U16; $params = for ($i = 0; $i -lt $np; $i++) { "$(S) $(S)" }
    $nl = U16; for ($i = 0; $i -lt $nl; $i++) { $null = S; $null = S }
    $ni = U16
    $lines = for ($i = 0; $i -lt $ni; $i++) {
        $op = U8; $def = $ops[[int]$op]
        $operands = @(for ($k = 0; $k -lt $def[1]; $k++) { Val })
        if ($op -in 23, 24, 25) { $extra = Val; $operands += @(for ($k = 0; $k -lt $extra; $k++) { Val }) }
        '{0,4}  {1,-11} {2}' -f $i, $def[0], ($operands -join ', ')
    }
    if ($lines -match "\b$Method\b") { "=== $name($($params -join ', '))"; $lines; '' }
}

$no = U16
for ($o = 0; $o -lt $no; $o++) {
    $null = S; $null = U32; $null = S; $null = S; $null = U32; $null = S
    $nv = U16; for ($i = 0; $i -lt $nv; $i++) { $null = S; $null = S; $null = U32; $null = Val }
    $npr = U16
    for ($i = 0; $i -lt $npr; $i++) {
        $pn = S; $null = S; $null = S; $null = U32; $fl = U8
        if ($fl -band 4) { $null = S } else { if ($fl -band 1) { Func "$pn.get" }; if ($fl -band 2) { Func "$pn.set" } }
    }
    $ns = U16
    for ($i = 0; $i -lt $ns; $i++) { $null = S; $nf = U16; for ($j = 0; $j -lt $nf; $j++) { Func (S) } }
}
