param([string]$Plugin, [string[]]$Types)
# Lists EDID -> FormID for records of the given types in a TES5 (SSE) plugin.
$b = [IO.File]::ReadAllBytes($Plugin)
function U32($a, $o) { [BitConverter]::ToUInt32($a, $o) }
function U16($a, $o) { [BitConverter]::ToUInt16($a, $o) }
function Inflate($a, $o, $n) {
    $ms = New-Object IO.MemoryStream($a, ($o + 6), ($n - 6))   # 4-byte size + 2-byte zlib header
    $ds = New-Object IO.Compression.DeflateStream($ms, [IO.Compression.CompressionMode]::Decompress)
    $out = New-Object IO.MemoryStream; $ds.CopyTo($out); , $out.ToArray()
}
function Subs($d) {
    $r = @{}; $p = 0
    while ($p + 6 -le $d.Length) {
        $t = [Text.Encoding]::ASCII.GetString($d, $p, 4); $n = U16 $d ($p + 4)
        if (-not $r.ContainsKey($t)) { $r[$t] = [byte[]]$d[($p + 6)..($p + 5 + $n)] }
        $p += 6 + $n
    }
    $r
}
$pos = 24 + (U32 $b 4)   # skip TES4 header record
while ($pos -lt $b.Length) {
    $t = [Text.Encoding]::ASCII.GetString($b, $pos, 4)
    if ($t -eq 'GRUP') { $pos += 24; continue }
    $size = U32 $b ($pos + 4); $flags = U32 $b ($pos + 8); $fid = U32 $b ($pos + 12)
    if ($Types -contains $t) {
        $data = if ($flags -band 0x40000) { Inflate $b ($pos + 24) $size } else { [byte[]]$b[($pos + 24)..($pos + 23 + $size)] }
        $s = Subs $data
        $ed = if ($s['EDID']) { [Text.Encoding]::ASCII.GetString($s['EDID']).TrimEnd([char]0) } else { '' }
        $full = if ($s['FULL'] -and $s['FULL'].Length -gt 4) { [Text.Encoding]::UTF8.GetString($s['FULL']).TrimEnd([char]0) } else { '' }
        '{0}  {1:X8}  {2}  {3}' -f $t, $fid, $ed, $full
    }
    $pos += 24 + $size
}
