# Renders Icon.xaml into a multi-size Tools/ModEditor/Icon.ico. Must run in an STA PowerShell (powershell -STA).
# Pass -PreviewDir to also save a PNG of each size for checking the result.
param(
	[string]$XamlPath = (Join-Path $PSScriptRoot 'Icon.xaml'),
	[string]$IcoPath = (Join-Path $PSScriptRoot '..\Icon.ico'),
	[string]$PreviewDir)

$XamlPath = [IO.Path]::GetFullPath($XamlPath)
$IcoPath = [IO.Path]::GetFullPath($IcoPath)
if ($PreviewDir) { $PreviewDir = [IO.Path]::GetFullPath($PreviewDir) }

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$drawing = [Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText($XamlPath))
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = @()

foreach ($size in $sizes) {
	$visual = New-Object Windows.Media.DrawingVisual
	$context = $visual.RenderOpen()
	$context.PushTransform((New-Object Windows.Media.ScaleTransform ($size / 256.0), ($size / 256.0)))
	$context.DrawDrawing($drawing)
	$context.Pop()
	$context.Close()

	$bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32)
	$bitmap.Render($visual)

	$encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
	$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
	$stream = New-Object IO.MemoryStream
	$encoder.Save($stream)
	$bytes = $stream.ToArray()
	$pngs += , $bytes
	if ($PreviewDir) { [IO.File]::WriteAllBytes((Join-Path $PreviewDir "Icon$size.png"), $bytes) }
}

# ICO container with PNG-compressed entries.
$out = New-Object IO.MemoryStream
$writer = New-Object IO.BinaryWriter $out
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
	$dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
	$writer.Write([byte]$dimension)
	$writer.Write([byte]$dimension)
	$writer.Write([byte]0)
	$writer.Write([byte]0)
	$writer.Write([UInt16]1)
	$writer.Write([UInt16]32)
	$writer.Write([UInt32]$pngs[$i].Length)
	$writer.Write([UInt32]$offset)
	$offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $writer.Write($png) }
$writer.Flush()
[IO.File]::WriteAllBytes($IcoPath, $out.ToArray())
"Wrote $IcoPath ($($out.Length) bytes)"
