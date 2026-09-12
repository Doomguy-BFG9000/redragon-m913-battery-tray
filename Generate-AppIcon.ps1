$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class IconNativeMethods {
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);
}
'@

$size = 256
$bitmap = [Drawing.Bitmap]::new($size, $size)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$graphics.Clear([Drawing.Color]::Transparent)
$body = [Drawing.RectangleF]::new(15, 15, 215, 226)
$background = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 22, 24, 28))
$outline = [Drawing.Pen]::new([Drawing.Color]::FromArgb(42, 220, 105), 15)
$terminal = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(42, 220, 105))
$graphics.FillRectangle($background, $body)
$graphics.DrawRectangle($outline, $body.X, $body.Y, $body.Width, $body.Height)
$graphics.FillRectangle($terminal, 228, 86, 24, 84)
$font = [Drawing.Font]::new('Segoe UI', 136, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
$format = [Drawing.StringFormat]::new()
$format.Alignment = [Drawing.StringAlignment]::Center
$format.LineAlignment = [Drawing.StringAlignment]::Center
$textBrush = [Drawing.SolidBrush]::new([Drawing.Color]::White)
$graphics.DrawString('M', $font, $textBrush, $body, $format)

$handle = $bitmap.GetHicon()
$icon = [Drawing.Icon]::FromHandle($handle)
$stream = [IO.File]::Create((Join-Path $PSScriptRoot 'app.ico'))
try {
    $icon.Save($stream)
}
finally {
    $stream.Dispose()
    $icon.Dispose()
    [void][IconNativeMethods]::DestroyIcon($handle)
    $font.Dispose(); $format.Dispose(); $textBrush.Dispose()
    $terminal.Dispose(); $outline.Dispose(); $background.Dispose()
    $graphics.Dispose(); $bitmap.Dispose()
}

Write-Host 'Generated app.ico.'
