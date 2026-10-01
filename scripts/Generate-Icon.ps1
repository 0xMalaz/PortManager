param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\src\PortManager.App\Assets\PortManager.ico")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Rectangle,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($Rectangle.X, $Rectangle.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rectangle.X, $Rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-PortManagerPng {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $stream = [System.IO.MemoryStream]::new()

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $inset = [Math]::Max(1.0, $Size * 0.04)
        $bounds = [System.Drawing.RectangleF]::new(
            [single]$inset,
            [single]$inset,
            [single]($Size - (2 * $inset)),
            [single]($Size - (2 * $inset)))
        $path = New-RoundedRectanglePath -Rectangle $bounds -Radius ([single]($Size * 0.22))
        $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 99, 91, 255))

        try {
            $graphics.FillPath($background, $path)
        }
        finally {
            $background.Dispose()
            $path.Dispose()
        }

        $lineWidth = [single][Math]::Max(1.4, $Size * 0.085)
        $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, $lineWidth)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        try {
            $trunkX = [single]($Size * 0.34)
            $startX = [single]($Size * 0.36)
            $endX = [single]($Size * 0.69)
            $topY = [single]($Size * 0.31)
            $middleY = [single]($Size * 0.50)
            $bottomY = [single]($Size * 0.69)

            $graphics.DrawLine($pen, $trunkX, $topY, $trunkX, $bottomY)
            $graphics.DrawLine($pen, $startX, $topY, $endX, $topY)
            $graphics.DrawLine($pen, $startX, $middleY, $endX, $middleY)
            $graphics.DrawLine($pen, $startX, $bottomY, $endX, $bottomY)
        }
        finally {
            $pen.Dispose()
        }

        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sizes = @(16, 20, 24, 32, 48, 64, 256)
$images = foreach ($size in $sizes) {
    [PSCustomObject]@{
        Size = $size
        Bytes = New-PortManagerPng -Size $size
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
