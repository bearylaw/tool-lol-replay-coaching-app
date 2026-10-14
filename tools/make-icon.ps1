# Draws the app icon (the gold Hextech diamond of the window header on a dark tile) at every
# size Windows asks for and writes src/RiftReview.App/Assets/RiftReview.ico with PNG entries.
# The .ico is generated: change this script and run it, never edit the icon by hand.
#   pwsh tools/make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\RiftReview.App\Assets\RiftReview.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256

function Color([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

function RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function Diamond([float]$cx, [float]$cy, [float]$r) {
    # The leading comma keeps PowerShell from unrolling the array into loose points.
    , [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($cx, $cy - $r),
        [System.Drawing.PointF]::new($cx + $r, $cy),
        [System.Drawing.PointF]::new($cx, $cy + $r),
        [System.Drawing.PointF]::new($cx - $r, $cy))
}

function Draw([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Dark tile with a gold rim; the rim is left out where it would be a blur of one pixel.
    $inset = [Math]::Max(0.5, $size / 64)
    $tile = RoundedRect $inset $inset ($size - 2 * $inset) ($size - 2 * $inset) ($size * 0.2)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new(0, $size)), (Color '#17202D'), (Color '#0A0E14')
    $g.FillPath($bg, $tile)
    if ($size -ge 32) {
        $rim = New-Object System.Drawing.Pen (Color '#785A28'), ([Math]::Max(1, $size / 40))
        $g.DrawPath($rim, $tile)
    }

    # The header's diamond: an outer diamond with a diamond-shaped hole, gold gradient top to bottom.
    $c = $size / 2
    $outer = if ($size -le 24) { $size * 0.42 } else { $size * 0.34 }
    $hole = $outer * 0.5
    $shape = New-Object System.Drawing.Drawing2D.GraphicsPath ([System.Drawing.Drawing2D.FillMode]::Alternate)
    $shape.AddPolygon((Diamond $c $c $outer))
    $shape.AddPolygon((Diamond $c $c $hole))
    $gold = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, $c - $outer)), ([System.Drawing.PointF]::new(0, $c + $outer)), (Color '#F0D9A4'), (Color '#A07F45')
    $g.FillPath($gold, $shape)

    # A small bright core in the hole, from 48 px up, like a gem in the setting.
    if ($size -ge 48) {
        $core = New-Object System.Drawing.SolidBrush (Color '#C8AA6E')
        $g.FillPolygon($core, (Diamond $c $c ($hole * 0.45)))
    }

    $g.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $stream.ToArray()
}

$images = foreach ($size in $sizes) { , (Draw $size) }

# ICO: 6-byte header, a 16-byte entry per image, then the PNG data.
$file = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $file
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $edge = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$edge); $w.Write([byte]$edge); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $w.Write($image) }
$w.Flush()
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
[System.IO.File]::WriteAllBytes($out, $file.ToArray())
"wrote $out ($($file.Length) bytes, $($sizes.Count) sizes)"
