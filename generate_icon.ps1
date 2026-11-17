# Generates SoundSwitcher/Assets/app.ico and app.png (and App.ico in the repo root for the README).
#
# The mark: a white Stormtrooper tile with a black outline and a big toggle switch, flipped on.
# Same family as MacroMaster and SmartNotes (flat tile + dark glyph, no gradients or glow).
# Every size is rendered natively from vector shapes, and outlines snap to whole pixels at
# small sizes so the 16/20/24/32 px tray and taskbar icons stay sharp.
#
# Usage (Windows PowerShell):  powershell -ExecutionPolicy Bypass -File generate_icon.ps1

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$TileColor = [System.Windows.Media.Color]::FromRgb(0xF8, 0xF9, 0xFA)
$InkColor  = [System.Windows.Media.Color]::FromRgb(0x20, 0x21, 0x24)

# Design grid is 64x64 units
function Render-Mark([int]$s) {
    $u     = $s / 64.0
    $small = ($s -le 64)
    $snap  = { param($v) if ($small) { [math]::Round($v) } else { $v } }

    $ink  = New-Object System.Windows.Media.SolidColorBrush($InkColor)
    $tile = New-Object System.Windows.Media.SolidColorBrush($TileColor)

    # Tile with an outline (3 units, never under 1 px)
    $ring = [math]::Max(1.0, (& $snap (3 * $u)))
    $tileRect = New-Object System.Windows.Rect(($ring / 2), ($ring / 2), ($s - $ring), ($s - $ring))
    $tilePen = New-Object System.Windows.Media.Pen($ink, $ring)

    # Toggle track: 40 x 24 units, 4.5-unit outline
    $sw = [math]::Max(1.0, (& $snap (4.5 * $u)))
    $x0 = & $snap (12 * $u); $x1 = $s - $x0
    $y0 = & $snap (20 * $u); $y1 = $s - $y0
    $trackRect = New-Object System.Windows.Rect(($x0 + $sw / 2), ($y0 + $sw / 2), ($x1 - $x0 - $sw), ($y1 - $y0 - $sw))
    $trackR = $trackRect.Height / 2
    $trackPen = New-Object System.Windows.Media.Pen($ink, $sw)

    # Knob on the right ("on"), with a small gap inside the track
    $inner = ($y1 - $y0) - 2 * $sw
    $gap   = [math]::Max(1.0, (& $snap ($inner * 0.16)))
    $kd    = $inner - 2 * $gap
    $kc    = New-Object System.Windows.Point(($x1 - $sw - $gap - $kd / 2), ($s / 2.0))

    $dv = New-Object System.Windows.Media.DrawingVisual
    $dc = $dv.RenderOpen()
    $dc.DrawRoundedRectangle($tile, $tilePen, $tileRect, (14 * $u), (14 * $u))
    $dc.DrawRoundedRectangle($null, $trackPen, $trackRect, $trackR, $trackR)
    $dc.DrawEllipse($ink, $null, $kc, ($kd / 2), ($kd / 2))
    $dc.Close()

    $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($s, $s, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($dv)
    return $bmp
}

function To-Png($bmp) {
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = New-Object System.IO.MemoryStream
    $enc.Save($ms)
    return ,$ms.ToArray()
}

# Classic 32-bit DIB icon frame (BITMAPINFOHEADER + bottom-up BGRA rows + empty AND mask).
function To-Dib($bmp) {
    $s = $bmp.PixelWidth
    $straight = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($bmp, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $stride = $s * 4
    $px = New-Object byte[] ($stride * $s)
    $straight.CopyPixels($px, $stride, 0)

    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($ms)
    $w.Write([uint32]40); $w.Write([int32]$s); $w.Write([int32]($s * 2))
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]0)
    $w.Write([uint32]($stride * $s)); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($px, $y * $stride, $stride) }
    $maskStride = [math]::Ceiling($s / 32.0) * 4
    $w.Write((New-Object byte[] ($maskStride * $s)))
    $w.Flush()
    return ,$ms.ToArray()
}

$assets = Join-Path $PSScriptRoot 'SoundSwitcher\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

[System.IO.File]::WriteAllBytes((Join-Path $assets 'app.png'), (To-Png (Render-Mark 1024)))

# Multi-size ICO: bitmap frames up to 128 px, PNG for 256 px (the standard Windows layout)
$sizes  = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @($sizes | ForEach-Object {
    $bmp = Render-Mark $_
    if ($_ -ge 256) { ,(To-Png $bmp) } else { ,(To-Dib $bmp) }
})

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
$ico = $out.ToArray()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'app.ico'), $ico)
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'App.ico'), $ico)

# Preview strip for checking small sizes by eye
$strip = New-Object System.Windows.Media.DrawingVisual
$dc = $strip.RenderOpen()
$dc.DrawRectangle((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x20,0x20,0x20))), $null, (New-Object System.Windows.Rect(0, 0, 360, 140)))
$x = 10
foreach ($s in 128, 64, 48, 32, 24, 16) {
    $dc.DrawImage((Render-Mark $s), (New-Object System.Windows.Rect($x, 6, $s, $s)))
    $x += $s + 10
}
$dc.Close()
$sb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(360, 140, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$sb.Render($strip)
if ($args.Count -gt 0) { [System.IO.File]::WriteAllBytes($args[0], (To-Png $sb)) }

"Wrote SoundSwitcher/Assets/app.png (1024), SoundSwitcher/Assets/app.ico and App.ico ($($sizes -join ', '))"
