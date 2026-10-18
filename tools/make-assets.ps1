<#
.SYNOPSIS
  Generates the app icon (src/MohiAltSwitch/Assets/app.ico) from code, so the artwork is reproducible.
  Windows PowerShell 5.1 or later (uses System.Drawing).
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Two overlapping windows on a blue tile: the front one is "where you are", the back one is "where Alt+` takes you".
function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $size / 256.0

    $tile = New-RoundedRectPath (8 * $k) (8 * $k) (240 * $k) (240 * $k) (52 * $k)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x00, 0x78, 0xD4))), $tile)

    $back = New-RoundedRectPath (52 * $k) (64 * $k) (124 * $k) (104 * $k) (14 * $k)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(130, 255, 255, 255))), $back)

    $front = New-RoundedRectPath (84 * $k) (100 * $k) (124 * $k) (108 * $k) (14 * $k)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)), $front)

    $g.SetClip($front)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x9C, 0xCB, 0xF2))), (84 * $k), (100 * $k), (124 * $k), (30 * $k))
    $g.ResetClip()

    $g.Dispose()
    return $bmp
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

# Classic ICO bitmap frame (DIB + AND mask): understood by every icon reader, unlike PNG-compressed small frames.
function Get-DibBytes($bmp) {
    $n = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $b = New-Object System.IO.BinaryWriter $ms
    $b.Write([uint32]40); $b.Write([int32]$n); $b.Write([int32]($n * 2))       # BITMAPINFOHEADER; height counts the mask
    $b.Write([uint16]1); $b.Write([uint16]32); $b.Write([uint32]0); $b.Write([uint32]0)
    $b.Write([int32]0); $b.Write([int32]0); $b.Write([uint32]0); $b.Write([uint32]0)
    for ($y = $n - 1; $y -ge 0; $y--) {                                         # pixels are stored bottom-up, BGRA
        for ($x = 0; $x -lt $n; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $b.Write([byte]$c.B); $b.Write([byte]$c.G); $b.Write([byte]$c.R); $b.Write([byte]$c.A)
        }
    }
    $maskRow = [int]([math]::Floor(($n + 31) / 32) * 4)                         # 1-bit AND mask, all zero (alpha does the work)
    $b.Write((New-Object byte[] ($maskRow * $n)))
    $b.Flush()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 256) { , (Get-PngBytes $bmp) } else { , (Get-DibBytes $bmp) }
}

$outDir = Join-Path $RepoRoot 'src\MohiAltSwitch\Assets'
New-Item -ItemType Directory -Force $outDir | Out-Null
$ico = Join-Path $outDir 'app.ico'

$fs = [System.IO.File]::Create($ico)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)    # ICONDIR
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {                                  # ICONDIRENTRY x N
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }                                     # PNG frames
$w.Dispose(); $fs.Dispose()

"wrote $ico ($((Get-Item $ico).Length) bytes, sizes: $($sizes -join ', '))"
