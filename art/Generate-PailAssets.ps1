param(
    [string]$Source = (Join-Path $PSScriptRoot 'Pail.png'),
    [string]$AssetsDir = (Join-Path $PSScriptRoot '..\src\Pail.App\Assets')
)

$ErrorActionPreference = 'Stop'

$Magick = 'magick'
$TemplatesDir = $PSScriptRoot

if (-not (Test-Path $Source)) {
    throw "Source image not found: $Source"
}

if (-not (Test-Path $AssetsDir)) {
    New-Item -ItemType Directory -Path $AssetsDir | Out-Null
}

$scaleFactors = @{
    100 = 1.00
    125 = 1.25
    150 = 1.50
    200 = 2.00
    400 = 4.00
}

function Get-ScaledSize {
    param(
        [int]$BaseSize,
        [int]$Scale
    )

    return [int][Math]::Round($BaseSize * $script:scaleFactors[$Scale], [MidpointRounding]::AwayFromZero)
}

function Get-PaddingPixels {
    param(
        [int]$Width,
        [int]$Height,
        [double]$PaddingRatio
    )

    $minDimension = [Math]::Min($Width, $Height)
    $padding = [int][Math]::Round($minDimension * $PaddingRatio, [MidpointRounding]::AwayFromZero)

    if ($PaddingRatio -gt 0 -and $padding -lt 1) {
        return 1
    }

    return $padding
}

function New-TransparentAsset {
    param(
        [int]$Width,
        [int]$Height,
        [double]$PaddingRatio,
        [string]$OutputPath
    )

    $padding = Get-PaddingPixels -Width $Width -Height $Height -PaddingRatio $PaddingRatio
    $contentWidth = [Math]::Max(1, $Width - (2 * $padding))
    $contentHeight = [Math]::Max(1, $Height - (2 * $padding))

    & $Magick $Source `
        -alpha on `
        -background none `
        -trim +repage `
        -resize "$($contentWidth)x$($contentHeight)" `
        -gravity center `
        -background none `
        -extent "$($Width)x$($Height)" `
        -strip `
        $OutputPath

    if ($LASTEXITCODE -ne 0) {
        throw "ImageMagick failed while writing $OutputPath"
    }
}

function New-ScaleAssetSet {
    param(
        [string]$Name,
        [int]$BaseWidth,
        [int]$BaseHeight,
        [double]$PaddingRatio,
        [string[]]$AltForms = @()
    )

    foreach ($scale in @(100, 125, 150, 200, 400)) {
        $width = Get-ScaledSize -BaseSize $BaseWidth -Scale $scale
        $height = Get-ScaledSize -BaseSize $BaseHeight -Scale $scale
        New-TransparentAsset -Width $width -Height $height -PaddingRatio $PaddingRatio -OutputPath (Join-Path $AssetsDir "$Name.scale-$scale.png")

        foreach ($altForm in $AltForms) {
            New-TransparentAsset -Width $width -Height $height -PaddingRatio $PaddingRatio -OutputPath (Join-Path $AssetsDir "$Name.scale-$($scale)_$altForm.png")
        }
    }
}

function New-TargetSizeAssetSet {
    param(
        [string]$Name,
        [int[]]$Sizes
    )

    foreach ($size in $Sizes) {
        $paddingRatio = if ($size -le 24) { 0.02 } elseif ($size -le 48) { 0.045 } else { 0.07 }
        New-TransparentAsset -Width $size -Height $size -PaddingRatio $paddingRatio -OutputPath (Join-Path $AssetsDir "$Name.targetsize-$size.png")
        New-TransparentAsset -Width $size -Height $size -PaddingRatio $paddingRatio -OutputPath (Join-Path $AssetsDir "$Name.targetsize-$($size)_altform-unplated.png")
        New-TransparentAsset -Width $size -Height $size -PaddingRatio $paddingRatio -OutputPath (Join-Path $AssetsDir "$Name.targetsize-$($size)_altform-lightunplated.png")
    }
}

function New-TemplateAssets {
    $templates = @(
        @{ Name = 'Pail.Template.AppIcon.png'; Width = 256; Height = 256; Padding = 0.07 },
        @{ Name = 'Pail.Template.SmallTile.png'; Width = 284; Height = 284; Padding = 0.11 },
        @{ Name = 'Pail.Template.MediumTile.png'; Width = 600; Height = 600; Padding = 0.11 },
        @{ Name = 'Pail.Template.WideTile.png'; Width = 1240; Height = 600; Padding = 0.15 },
        @{ Name = 'Pail.Template.LargeTile.png'; Width = 1240; Height = 1240; Padding = 0.12 },
        @{ Name = 'Pail.Template.SplashScreen.png'; Width = 2480; Height = 1200; Padding = 0.19 },
        @{ Name = 'Pail.Template.StoreLogo.png'; Width = 200; Height = 200; Padding = 0.08 },
        @{ Name = 'Pail.Template.BadgeLogo.png'; Width = 96; Height = 96; Padding = 0.06 }
    )

    foreach ($template in $templates) {
        New-TransparentAsset -Width $template.Width -Height $template.Height -PaddingRatio $template.Padding -OutputPath (Join-Path $TemplatesDir $template.Name)
    }
}

New-TemplateAssets

New-ScaleAssetSet -Name 'Square44x44Logo' -BaseWidth 44 -BaseHeight 44 -PaddingRatio 0.055 -AltForms @('altform-colorful_theme-light')
New-ScaleAssetSet -Name 'Square71x71Logo' -BaseWidth 71 -BaseHeight 71 -PaddingRatio 0.08
New-ScaleAssetSet -Name 'Square150x150Logo' -BaseWidth 150 -BaseHeight 150 -PaddingRatio 0.10
New-ScaleAssetSet -Name 'Wide310x150Logo' -BaseWidth 310 -BaseHeight 150 -PaddingRatio 0.14
New-ScaleAssetSet -Name 'Square310x310Logo' -BaseWidth 310 -BaseHeight 310 -PaddingRatio 0.11
New-ScaleAssetSet -Name 'SplashScreen' -BaseWidth 620 -BaseHeight 300 -PaddingRatio 0.18 -AltForms @('altform-colorful_theme-dark', 'altform-colorful_theme-light')
New-ScaleAssetSet -Name 'StoreLogo' -BaseWidth 50 -BaseHeight 50 -PaddingRatio 0.06 -AltForms @('altform-colorful_theme-light')
New-ScaleAssetSet -Name 'BadgeLogo' -BaseWidth 24 -BaseHeight 24 -PaddingRatio 0.04
New-ScaleAssetSet -Name 'LockScreenLogo' -BaseWidth 24 -BaseHeight 24 -PaddingRatio 0.04

New-TargetSizeAssetSet -Name 'Square44x44Logo' -Sizes @(16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256)

New-TransparentAsset -Width 50 -Height 50 -PaddingRatio 0.06 -OutputPath (Join-Path $AssetsDir 'StoreLogo.png')

$ico256 = Join-Path $env:TEMP 'Pail.ico.256.png'
$ico128 = Join-Path $env:TEMP 'Pail.ico.128.png'
$ico64 = Join-Path $env:TEMP 'Pail.ico.64.png'
$ico48 = Join-Path $env:TEMP 'Pail.ico.48.png'
$ico32 = Join-Path $env:TEMP 'Pail.ico.32.png'
$ico24 = Join-Path $env:TEMP 'Pail.ico.24.png'
$ico16 = Join-Path $env:TEMP 'Pail.ico.16.png'

New-TransparentAsset -Width 256 -Height 256 -PaddingRatio 0.07 -OutputPath $ico256
New-TransparentAsset -Width 128 -Height 128 -PaddingRatio 0.07 -OutputPath $ico128
New-TransparentAsset -Width 64 -Height 64 -PaddingRatio 0.055 -OutputPath $ico64
New-TransparentAsset -Width 48 -Height 48 -PaddingRatio 0.045 -OutputPath $ico48
New-TransparentAsset -Width 32 -Height 32 -PaddingRatio 0.035 -OutputPath $ico32
New-TransparentAsset -Width 24 -Height 24 -PaddingRatio 0.02 -OutputPath $ico24
New-TransparentAsset -Width 16 -Height 16 -PaddingRatio 0.02 -OutputPath $ico16

& $Magick $ico256 $ico128 $ico64 $ico48 $ico32 $ico24 $ico16 (Join-Path $AssetsDir 'Pail.ico')
if ($LASTEXITCODE -ne 0) {
    throw 'ImageMagick failed while writing Pail.ico'
}

Remove-Item $ico256, $ico128, $ico64, $ico48, $ico32, $ico24, $ico16 -ErrorAction SilentlyContinue

Write-Host "Generated Pail image assets in $AssetsDir"
Write-Host "Generated template images in $TemplatesDir"