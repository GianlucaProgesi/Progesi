# tools/Generate-RepoBanner.ps1
[CmdletBinding()]
param(
  [string]$LogoPath        = 'docs/assets/progesi-logo.jpg',
  [string]$OutPath         = 'docs/assets/repo-banner.png',
  [int]$Width              = 1920,
  [int]$Height             = 480,
  [string]$Title           = 'Progesi Engineering Toolchain',
  [string]$Subtitle        = 'Parametric Bridge Engineering • NuGet + CI/CD ready',
  [string]$BgColorLeftHex  = '#F7FBFF',
  [string]$BgColorRightHex = '#E9F6F0',
  [string]$AccentHex       = '#2EA44F',
  [string]$TitleColorHex   = '#0B3D91',
  [string]$SubtitleHex     = '#20435C',
  [int]$LogoMaxHeight      = 300,
  [int]$Padding            = 48,
  [int]$Gap                = 28
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Ensure-Dir([string]$path) {
  $dir = Split-Path -Parent $path
  if ($dir -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
  }
}
function HexToColor([string]$hex) {
  $h = $hex.Trim()
  if ($h.StartsWith('#')) { $h = $h.Substring(1) }
  if ($h.Length -eq 6) {
    $r=[Convert]::ToInt32($h.Substring(0,2),16)
    $g=[Convert]::ToInt32($h.Substring(2,2),16)
    $b=[Convert]::ToInt32($h.Substring(4,2),16)
    return [System.Drawing.Color]::FromArgb(255,$r,$g,$b)
  } elseif ($h.Length -eq 8) {
    $a=[Convert]::ToInt32($h.Substring(0,2),16)
    $r=[Convert]::ToInt32($h.Substring(2,2),16)
    $g=[Convert]::ToInt32($h.Substring(4,2),16)
    $b=[Convert]::ToInt32($h.Substring(6,2),16)
    return [System.Drawing.Color]::FromArgb($a,$r,$g,$b)
  } else { return [System.Drawing.Color]::Black }
}
function Load-Image([string]$path) {
  if (-not (Test-Path -LiteralPath $path)) { throw "Logo non trovato: $path" }
  return [System.Drawing.Image]::FromFile($path)
}

Add-Type -AssemblyName System.Drawing

$bgL    = HexToColor $BgColorLeftHex
$bgR    = HexToColor $BgColorRightHex
$accent = HexToColor $AccentHex
$tCol   = HexToColor $TitleColorHex
$sCol   = HexToColor $SubtitleHex

$bmp = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

# Sfondo gradiente
$rect = New-Object System.Drawing.Rectangle(0,0,$Width,$Height)
$lg   = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $bgL, $bgR, 0.0)
$g.FillRectangle($lg, $rect); $lg.Dispose()

# Barra accent
$accentHeight = [Math]::Max([int]($Height * 0.015), 6)
$g.FillRectangle((New-Object System.Drawing.SolidBrush($accent)), 0, 0, $Width, $accentHeight)

# Logo
$logo = Load-Image -path $LogoPath
try {
  $scale = [Math]::Min($LogoMaxHeight / $logo.Height, 1.0)
  $logoW = [int]([Math]::Round($logo.Width * $scale))
  $logoH = [int]([Math]::Round($logo.Height * $scale))
  $logoX = $Padding
  $logoY = [int](($Height - $logoH)/2.0)
  $g.DrawImage($logo, $logoX, $logoY, $logoW, $logoH)

  # Area testo
  $textX = $logoX + $logoW + $Gap
  $textW = $Width - $textX - $Padding
  $textY = $logoY

  # Font
  $titleFontNames = @('Segoe UI Semibold', 'Segoe UI', 'Arial', 'Helvetica')
  $subFontNames   = @('Segoe UI', 'Arial', 'Helvetica')
  function Pick-Font([string[]]$candidates, [float]$size, [bool]$bold=$false) {
    foreach ($n in $candidates) {
      try {
        if ($bold) { return New-Object System.Drawing.Font($n, $size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel) }
        else       { return New-Object System.Drawing.Font($n, $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel) }
      } catch { continue }
    }
    return New-Object System.Drawing.Font('Arial', $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
  }

  $titleSizePx = [Math]::Min([int]($Height * 0.12), 64)
  $subSizePx   = [Math]::Min([int]($Height * 0.07), 36)

  $titleFont = Pick-Font $titleFontNames $titleSizePx $true
  $subFont   = Pick-Font $subFontNames   $subSizePx   $false

  $titleBrush = New-Object System.Drawing.SolidBrush($tCol)
  $subBrush   = New-Object System.Drawing.SolidBrush($sCol)

  $sf = New-Object System.Drawing.StringFormat
  $sf.Trimming = [System.Drawing.StringTrimming]::EllipsisWord
  $sf.FormatFlags = [System.Drawing.StringFormatFlags]::NoClip

  # Rettangoli (cast a Single per l’overload corretto)
  $titleRect = [System.Drawing.RectangleF]::new(
    [single]$textX, [single]$textY, [single]$textW, [single]($titleSizePx * 1.4)
  )
  $g.DrawString($Title, $titleFont, $titleBrush, $titleRect, $sf)

  $subRectY = [int]($titleRect.Y + $titleRect.Height + ($Height * 0.02))
  $subRect  = [System.Drawing.RectangleF]::new(
    [single]$textX, [single]$subRectY, [single]$textW, [single]($subSizePx * 2.2)
  )
  $g.DrawString($Subtitle, $subFont, $subBrush, $subRect, $sf)

  # Puntinatura estetica
  $dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(28, $tCol))
  $dotR = [Math]::Max([int]($Height * 0.01), 4)
  for ($x = $textX; $x -le ($textX + $textW); $x += ($dotR*6)) {
    $g.FillEllipse($dotBrush, $x, ($Height - $Padding - $dotR*4), $dotR, $dotR)
  }
} finally {
  $logo.Dispose()
}

# Salva PNG
Ensure-Dir $OutPath
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()

Write-Host "✓ Banner generato: $OutPath ($Width x $Height)" -ForegroundColor Green
