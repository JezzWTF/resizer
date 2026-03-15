param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function Read-PositiveInt {
    param(
        [Parameter(Mandatory = $true)][string]$Prompt,
        [int]$Default = 1
    )

    while ($true) {
        $raw = Read-Host "$Prompt [$Default]"
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return $Default
        }

        $value = 0
        if ([int]::TryParse($raw, [ref]$value) -and $value -gt 0) {
            return $value
        }

        Write-Host 'Please enter a positive whole number.' -ForegroundColor Yellow
    }
}

function Read-PositiveDecimal {
    param(
        [Parameter(Mandatory = $true)][string]$Prompt,
        [double]$Default = 1.0
    )

    while ($true) {
        $raw = Read-Host "$Prompt [$Default]"
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return $Default
        }

        $value = 0.0
        if ([double]::TryParse($raw, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$value) -and $value -gt 0) {
            return $value
        }

        Write-Host 'Please enter a positive number.' -ForegroundColor Yellow
    }
}

function Read-Resolution {
    param(
        [string]$Default = '4096x4096'
    )

    while ($true) {
        $raw = Read-Host "Image resolution WIDTHxHEIGHT [$Default]"
        if ([string]::IsNullOrWhiteSpace($raw)) {
            $raw = $Default
        }

        $value = $raw.Trim().ToLowerInvariant().Replace(' ', '')
        if ($value -match '^(\d+)x(\d+)$') {
            $width = [int]$Matches[1]
            $height = [int]$Matches[2]
            if ($width -gt 0 -and $height -gt 0) {
                return [pscustomobject]@{
                    Width = $width
                    Height = $height
                }
            }
        }

        Write-Host 'Use format like 4096x4096.' -ForegroundColor Yellow
    }
}

function Read-ResolutionPreset {
    while ($true) {
        Write-Host ''
        Write-Host 'Resolution preset:' -ForegroundColor Cyan
        Write-Host '  1) Mixed (1080p + 4K)'
        Write-Host '  2) 1080p only (1920x1080)'
        Write-Host '  3) 4K only (3840x2160)'
        Write-Host '  4) Custom resolution'

        $raw = Read-Host 'Choose preset [1]'
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return 'mixed'
        }

        switch ($raw.Trim()) {
            '1' { return 'mixed' }
            '2' { return '1080p' }
            '3' { return '4k' }
            '4' { return 'custom' }
            default {
                Write-Host 'Choose 1, 2, 3, or 4.' -ForegroundColor Yellow
            }
        }
    }
}

function Read-ImageFormat {
    param(
        [string]$Default = 'jpg'
    )

    while ($true) {
        $raw = Read-Host "Image format (jpg/png) [$Default]"
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return $Default
        }

        $fmt = $raw.Trim().ToLowerInvariant()
        if ($fmt -eq 'jpeg') {
            $fmt = 'jpg'
        }

        if ($fmt -in @('jpg', 'png')) {
            return $fmt
        }

        Write-Host 'Please enter jpg or png.' -ForegroundColor Yellow
    }
}

function Select-OutputFolder {
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Select destination folder'
    $dialog.ShowNewFolderButton = $true

    $result = $dialog.ShowDialog()
    if ($result -ne [System.Windows.Forms.DialogResult]::OK) {
        return $null
    }

    return $dialog.SelectedPath
}

function Get-JpegCodec {
    return [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' } | Select-Object -First 1
}

function Save-BitmapAsJpeg {
    param(
        [Parameter(Mandatory = $true)][System.Drawing.Bitmap]$Bitmap,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$Quality,
        [Parameter(Mandatory = $true)][System.Drawing.Imaging.ImageCodecInfo]$Codec
    )

    $encoder = [System.Drawing.Imaging.Encoder]::Quality
    $encoderParam = New-Object System.Drawing.Imaging.EncoderParameter($encoder, [int64]$Quality)
    $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters(1)
    $encoderParams.Param[0] = $encoderParam

    try {
        $Bitmap.Save($Path, $Codec, $encoderParams)
    }
    finally {
        $encoderParam.Dispose()
        $encoderParams.Dispose()
    }
}

function Save-JpegWithTargetSize {
    param(
        [Parameter(Mandatory = $true)][System.Drawing.Bitmap]$Bitmap,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][double]$TargetSizeMb
    )

    $codec = Get-JpegCodec
    $targetBytes = [int64]($TargetSizeMb * 1MB)
    $minBytes = [int64]([Math]::Max(1MB, ($TargetSizeMb - 0.5) * 1MB))
    $maxBytes = [int64](($TargetSizeMb + 0.5) * 1MB)

    $trialPath = Join-Path $env:TEMP ("placeholder_trial_{0}.jpg" -f [Guid]::NewGuid().ToString('N'))
    $bestQuality = 75
    $bestDistance = [int64]::MaxValue
    $bestSize = [int64]0

    $low = 1
    $high = 100

    try {
        while ($low -le $high) {
            $quality = [int][Math]::Floor(($low + $high) / 2)
            Save-BitmapAsJpeg -Bitmap $Bitmap -Path $trialPath -Quality $quality -Codec $codec
            $size = (Get-Item -Path $trialPath).Length

            $distance = [int64][Math]::Abs($size - $targetBytes)
            if ($distance -lt $bestDistance) {
                $bestDistance = $distance
                $bestQuality = $quality
                $bestSize = $size
            }

            if ($size -gt $maxBytes) {
                $high = $quality - 1
            }
            elseif ($size -lt $minBytes) {
                $low = $quality + 1
            }
            else {
                $bestQuality = $quality
                $bestSize = $size
                break
            }
        }

        Save-BitmapAsJpeg -Bitmap $Bitmap -Path $Path -Quality $bestQuality -Codec $codec
        $bestSize = (Get-Item -Path $Path).Length

        return [pscustomobject]@{
            Quality = $bestQuality
            SizeBytes = $bestSize
        }
    }
    finally {
        if (Test-Path -Path $trialPath) {
            Remove-Item -Path $trialPath -Force
        }
    }
}

function Save-SinglePlaceholderImageSized {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$Width,
        [Parameter(Mandatory = $true)][int]$Height,
        [Parameter(Mandatory = $true)][ValidateSet('jpg', 'png')][string]$Format,
        [double]$TargetJpegSizeMb = 3.5
    )

    $pixelFormat = [System.Drawing.Imaging.PixelFormat]::Format24bppRgb
    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, $pixelFormat)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $rect = New-Object System.Drawing.Rectangle(0, 0, $Width, $Height)

        $lockData = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $pixelFormat)
        try {
            $byteCount = [Math]::Abs($lockData.Stride) * $Height
            $bytes = New-Object byte[] $byteCount
            $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
            try {
                $rng.GetBytes($bytes)
            }
            finally {
                $rng.Dispose()
            }
            [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $lockData.Scan0, $byteCount)
        }
        finally {
            $bitmap.UnlockBits($lockData)
        }

        $titleFont = New-Object System.Drawing.Font('Segoe UI', [float]([Math]::Max(18, [Math]::Round($Width / 34))), [System.Drawing.FontStyle]::Bold)
        $subFont = New-Object System.Drawing.Font('Segoe UI', [float]([Math]::Max(12, [Math]::Round($Width / 64))), [System.Drawing.FontStyle]::Regular)
        $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(160, 0, 0, 0))
        $labelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, 255, 255, 255))

        $line1 = "Placeholder Image"
        $line2 = "$Width x $Height"
        $line3 = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        $x = [float]([Math]::Round($Width * 0.05))
        $y1 = [float]([Math]::Round($Height * 0.78))
        $y2 = [float]([Math]::Round($Height * 0.86))
        $y3 = [float]([Math]::Round($Height * 0.92))

        $graphics.DrawString($line1, $titleFont, $shadowBrush, $x + 2, $y1 + 2)
        $graphics.DrawString($line1, $titleFont, $labelBrush, $x, $y1)
        $graphics.DrawString($line2, $titleFont, $shadowBrush, $x + 2, $y2 + 2)
        $graphics.DrawString($line2, $titleFont, $labelBrush, $x, $y2)
        $graphics.DrawString($line3, $subFont, $shadowBrush, $x + 2, $y3 + 2)
        $graphics.DrawString($line3, $subFont, $labelBrush, $x, $y3)

        $titleFont.Dispose()
        $subFont.Dispose()
        $shadowBrush.Dispose()
        $labelBrush.Dispose()

        if ($Format -eq 'jpg') {
            $result = Save-JpegWithTargetSize -Bitmap $bitmap -Path $Path -TargetSizeMb $TargetJpegSizeMb
            return $result
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        return [pscustomobject]@{
            Quality = 0
            SizeBytes = (Get-Item -Path $Path).Length
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

Write-Host ''
Write-Host 'Placeholder Image Batch Generator' -ForegroundColor Cyan
Write-Host '---------------------------------' -ForegroundColor Cyan

$outputRoot = Select-OutputFolder
if (-not $outputRoot) {
    Write-Host 'Cancelled: no output folder selected.' -ForegroundColor Yellow
    exit 0
}

$folderCount = Read-PositiveInt -Prompt 'How many folders to create?' -Default 10
$imagesPerFolder = Read-PositiveInt -Prompt 'How many images in each folder?' -Default 25
$preset = Read-ResolutionPreset
$imageFormat = Read-ImageFormat -Default 'jpg'

$resolutionProfiles = @()
switch ($preset) {
    'mixed' {
        $resolutionProfiles += [pscustomobject]@{ Name = '1080p'; Width = 1920; Height = 1080 }
        $resolutionProfiles += [pscustomobject]@{ Name = '4K'; Width = 3840; Height = 2160 }
    }
    '1080p' {
        $resolutionProfiles += [pscustomobject]@{ Name = '1080p'; Width = 1920; Height = 1080 }
    }
    '4k' {
        $resolutionProfiles += [pscustomobject]@{ Name = '4K'; Width = 3840; Height = 2160 }
    }
    'custom' {
        $customRes = Read-Resolution -Default '4096x4096'
        $resolutionProfiles += [pscustomobject]@{ Name = "$($customRes.Width)x$($customRes.Height)"; Width = $customRes.Width; Height = $customRes.Height }
    }
}

$targetJpegSizeMb = 3.5
if ($imageFormat -eq 'jpg') {
    $targetJpegSizeMb = Read-PositiveDecimal -Prompt 'Target JPEG size in MB (approx)?' -Default 3.5
}

$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$baseDir = Join-Path $outputRoot "Generated_Pictures_$timestamp"
[void](New-Item -ItemType Directory -Path $baseDir -Force)

$extension = if ($imageFormat -eq 'jpg') { 'jpg' } else { 'png' }
$masterLookup = @{}
$masterSizes = @{}

foreach ($profile in $resolutionProfiles) {
    $masterImagePath = Join-Path $baseDir ("_master_placeholder_{0}x{1}.$extension" -f $profile.Width, $profile.Height)
    Write-Host "Creating master $($profile.Width)x$($profile.Height) $imageFormat image..." -ForegroundColor Gray
    $masterInfo = Save-SinglePlaceholderImageSized -Path $masterImagePath -Width $profile.Width -Height $profile.Height -Format $imageFormat -TargetJpegSizeMb $targetJpegSizeMb

    $masterLookup[$profile.Name] = $masterImagePath
    $masterSizes[$profile.Name] = [Math]::Round($masterInfo.SizeBytes / 1MB, 2)

    if ($imageFormat -eq 'jpg') {
        Write-Host "Master [$($profile.Name)] size: $($masterSizes[$profile.Name]) MB (JPEG quality $($masterInfo.Quality))" -ForegroundColor Gray
    }
    else {
        Write-Host "Master [$($profile.Name)] size: $($masterSizes[$profile.Name]) MB" -ForegroundColor Gray
    }
}

$rng = [System.Random]::new()
$distribution = @{}
foreach ($profile in $resolutionProfiles) {
    $distribution[$profile.Name] = 0
}

$total = $folderCount * $imagesPerFolder
$created = 0

for ($f = 1; $f -le $folderCount; $f++) {
    $folderPath = Join-Path $baseDir ("Folder_{0:D3}" -f $f)
    [void](New-Item -ItemType Directory -Path $folderPath -Force)

    for ($i = 1; $i -le $imagesPerFolder; $i++) {
        $selectedProfile = $resolutionProfiles[0]
        if ($resolutionProfiles.Count -gt 1) {
            $selectedProfile = if ($rng.NextDouble() -lt 0.7) { $resolutionProfiles[0] } else { $resolutionProfiles[1] }
        }

        $name = "IMG_{0:D3}_{1:D4}.$extension" -f $f, $i
        $dest = Join-Path $folderPath $name
        Copy-Item -Path $masterLookup[$selectedProfile.Name] -Destination $dest
        $distribution[$selectedProfile.Name] = [int]$distribution[$selectedProfile.Name] + 1

        $created++
        if (($created % 100 -eq 0) -or ($created -eq $total)) {
            Write-Host ("Created {0}/{1}" -f $created, $total) -ForegroundColor Green
        }
    }
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Cyan
Write-Host "Root output folder: $baseDir" -ForegroundColor Cyan
foreach ($profile in $resolutionProfiles) {
    Write-Host "Master template [$($profile.Name)] kept at: $($masterLookup[$profile.Name])" -ForegroundColor DarkGray
}

if ($resolutionProfiles.Count -gt 1) {
    Write-Host "Distribution: 1080p=$($distribution['1080p']) files, 4K=$($distribution['4K']) files" -ForegroundColor Cyan
}
