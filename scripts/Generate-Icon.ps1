param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot "..\assets\icon"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\src\PortManager.App\Assets\PortManager.ico")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

# Converts a PNG into a 32-bit ICO DIB: BITMAPINFOHEADER, bottom-up BGRA rows, then a 1-bit AND mask.
# Small frames are stored this way because some ICO readers only decode PNG frames at 256 px.
function ConvertTo-IconBitmap {
    param([byte[]]$PngBytes, [int]$Size)

    $pngStream = [System.IO.MemoryStream]::new($PngBytes)
    $bitmap = [System.Drawing.Bitmap]::new($pngStream)
    $output = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($output)

    try {
        $writer.Write([uint32]40)
        $writer.Write([int32]$Size)
        $writer.Write([int32]($Size * 2))
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]0)
        $writer.Write([uint32]0)
        $writer.Write([int32]0)
        $writer.Write([int32]0)
        $writer.Write([uint32]0)
        $writer.Write([uint32]0)

        $bounds = [System.Drawing.Rectangle]::new(0, 0, $Size, $Size)
        $data = $bitmap.LockBits($bounds, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $row = [byte[]]::new($Size * 4)
            for ($y = $Size - 1; $y -ge 0; $y--) {
                [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + ($y * $data.Stride)), $row, 0, $row.Length)
                $writer.Write($row)
            }
        }
        finally {
            $bitmap.UnlockBits($data)
        }

        # Alpha carries transparency, so the AND mask is all zeros; rows are padded to 32 bits.
        $maskStride = [int]([Math]::Ceiling($Size / 32.0) * 4)
        $writer.Write([byte[]]::new($maskStride * $Size))
        $writer.Flush()
        return $output.ToArray()
    }
    finally {
        $writer.Dispose()
        $bitmap.Dispose()
        $pngStream.Dispose()
    }
}

# Packs the hand-tuned PNG renders from assets/icon into one multi-resolution ICO.
# Each size is drawn separately (16/20/24 come from pixel-grid SVGs), so nothing is resampled here.
# Frames below 256 px are stored as bitmaps and the 256 px frame as PNG.
$sizes = @(16, 20, 24, 32, 48, 64, 256)
$images = foreach ($size in $sizes) {
    $path = Join-Path $SourceDirectory "PortManager-$size.png"
    $bytes = [System.IO.File]::ReadAllBytes([System.IO.Path]::GetFullPath($path))

    # PNG IHDR stores big-endian width and height at offsets 16 and 20.
    $width = ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
    $height = ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
    if ($width -ne $size -or $height -ne $size) {
        throw "$path is ${width}x${height}; expected ${size}x${size}."
    }

    if ($size -lt 256) {
        $bytes = ConvertTo-IconBitmap -PngBytes $bytes -Size $size
    }

    [PSCustomObject]@{
        Size = $size
        Bytes = $bytes
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$fileStream = [System.IO.File]::Create([System.IO.Path]::GetFullPath($OutputPath))
$writer = [System.IO.BinaryWriter]::new($fileStream)

try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)

    $dataOffset = 6 + (16 * $images.Count)
    foreach ($image in $images) {
        $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Bytes.Length)
        $writer.Write([uint32]$dataOffset)
        $dataOffset += $image.Bytes.Length
    }

    $writer.Flush()
    foreach ($image in $images) {
        $fileStream.Write([byte[]]$image.Bytes, 0, $image.Bytes.Length)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}

Write-Host "Generated $OutputPath"
