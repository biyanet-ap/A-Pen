# Generates HighPen.ico (PNG-compressed, 16/24/32/48/64/128/256) from vector drawing code.
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-RoundedPath($x, $y, $w, $h, $r) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90); $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$n) {
    $bmp = New-Object Drawing.Bitmap $n, $n, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $u = $n / 256.0

    # green board: rounded tile, lighter at the top
    $tile = New-RoundedPath 0 0 ($n - 1) ($n - 1) (52 * $u)
    $grad = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.PointF]::new(0, 0), [Drawing.PointF]::new(0, $n), [Drawing.Color]::FromArgb(255, 0x3A, 0x75, 0x50), [Drawing.Color]::FromArgb(255, 0x24, 0x4A, 0x33))
    $g.FillPath($grad, $tile)

    # wooden frame
    $frame = New-RoundedPath (19.5 * $u) (19.5 * $u) (217 * $u) (217 * $u) (36 * $u)
    $g.DrawPath([Drawing.Pen]::new([Drawing.Color]::FromArgb(255, 0xC8, 0xA2, 0x6B), 11 * $u), $frame)

    # chalk "A"
    $chalk = [Drawing.Color]::FromArgb(255, 0xF5, 0xF5, 0xF0)
    $legs = [Drawing.Pen]::new($chalk, 23 * $u)
    $legs.StartCap = 'Round'; $legs.EndCap = 'Round'; $legs.LineJoin = 'Round'
    $g.DrawLines($legs, [Drawing.PointF[]]@([Drawing.PointF]::new(62.5*$u, 197.5*$u), [Drawing.PointF]::new(112.5*$u, 62.5*$u), [Drawing.PointF]::new(162.5*$u, 197.5*$u)))
    $bar = [Drawing.Pen]::new($chalk, 20.5 * $u)
    $bar.StartCap = 'Round'; $bar.EndCap = 'Round'
    $g.DrawLine($bar, [Drawing.PointF]::new(82.5*$u, 150*$u), [Drawing.PointF]::new(142.5*$u, 150*$u))

    # red pen writing: tip at the foot of the right leg, body going up to the right
    $g.TranslateTransform(165*$u, 200*$u)
    $g.RotateTransform(-50)
    $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 0xFF, 0x3B, 0x30))), 18*$u, -13*$u, 64.5*$u, 26*$u)
    $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::White)), 71*$u, -13*$u, 11.5*$u, 26*$u)
    $tip = [Drawing.PointF[]]@([Drawing.PointF]::new(0, 0), [Drawing.PointF]::new(18*$u, 13*$u), [Drawing.PointF]::new(18*$u, -13*$u))
    $g.FillPolygon((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 0xF2, 0xD3, 0xA8))), $tip)
    $nib = [Drawing.PointF[]]@([Drawing.PointF]::new(0, 0), [Drawing.PointF]::new(6.6*$u, 4.7*$u), [Drawing.PointF]::new(6.6*$u, -4.7*$u))
    $g.FillPolygon((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 0x28, 0x28, 0x28))), $nib)
    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($n in $sizes) {
    $b = New-IconBitmap $n
    $ms = New-Object IO.MemoryStream
    $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    , $ms.ToArray()
}

$out = Join-Path $PSScriptRoot 'HighPen.ico'
$fs = [IO.File]::Create($out)
$w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $n = $sizes[$i]; $dim = if ($n -ge 256) { 0 } else { $n }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()

# preview for a quick look
(New-IconBitmap 256).Save((Join-Path $PSScriptRoot 'HighPen-icon-preview.png'), [Drawing.Imaging.ImageFormat]::Png)
Write-Host "Wrote $out"

# MSIX logos (packaging\Assets): the icon on a transparent canvas, scale-100/200 and taskbar target sizes
function Save-Logo([string]$name, [int]$w, [int]$h, [int]$icon) {
    $bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::Transparent)
    $i = New-IconBitmap $icon
    $g.DrawImage($i, [int](($w - $icon) / 2), [int](($h - $icon) / 2), $icon, $icon)
    $g.Dispose(); $i.Dispose()
    $bmp.Save((Join-Path $assets "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
$assets = Join-Path $PSScriptRoot '..\packaging\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null
foreach ($s in 1, 2) {
    Save-Logo "Square44x44Logo.scale-$($s * 100)" (44 * $s) (44 * $s) (36 * $s)
    Save-Logo "Square150x150Logo.scale-$($s * 100)" (150 * $s) (150 * $s) (100 * $s)
    Save-Logo "Wide310x150Logo.scale-$($s * 100)" (310 * $s) (150 * $s) (100 * $s)
    Save-Logo "StoreLogo.scale-$($s * 100)" (50 * $s) (50 * $s) (50 * $s)
}
foreach ($n in 16, 24, 32, 48, 256) {
    Save-Logo "Square44x44Logo.targetsize-$n" $n $n $n
    Save-Logo "Square44x44Logo.targetsize-${n}_altform-unplated" $n $n $n
}
Write-Host "Wrote MSIX logos to $assets"

# Store listing app logos for Partner Center (packaging\StoreListing)
$listing = Join-Path $PSScriptRoot '..\packaging\StoreListing'
New-Item -ItemType Directory -Force $listing | Out-Null
foreach ($n in 71, 150, 300) {
    $b = New-IconBitmap $n
    $b.Save((Join-Path $listing "HighPen-$n.png"), [Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
}
Write-Host "Wrote Store listing logos to $listing"
