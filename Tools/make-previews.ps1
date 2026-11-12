# Pets/<folder>/preview.gif for every pet of the gallery: the pet walking and doing one of its tricks, transparent,
# about 120 px tall. The program shows it in the pet panel before downloading. Needs ffmpeg on the PATH.
#
#   powershell -ExecutionPolicy Bypass -File Tools\make-previews.ps1            (all pets)
#   powershell -ExecutionPolicy Bypass -File Tools\make-previews.ps1 -Only rex   (one pet)
param([string]$Only = "")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$pets = (Get-Content (Join-Path $root 'Pets\pets.json') -Raw | ConvertFrom-Json).pets.folder
if ($Only) { $pets = @($Only) }
$tmp = Join-Path $env:TEMP "pet-previews"
$tricks = 'wave', 'dance', 'eat', 'jump', 'sleep', 'spin', 'happy', 'cheer', 'think', 'boing', 'flower'

foreach ($pet in $pets) {
  $xmlPath = Join-Path $root "Pets\$pet\animations.xml"
  if (-not (Test-Path $xmlPath)) { Write-Warning "${pet}: no animations.xml"; continue }
  [xml]$d = Get-Content -Raw -Encoding UTF8 $xmlPath
  $ns = New-Object Xml.XmlNamespaceManager $d.NameTable; $ns.AddNamespace("e", "https://esheep.petrucci.ch/")
  $tilesX = [int]$d.SelectSingleNode("//e:image/e:tilesx", $ns).InnerText
  $tilesY = [int]$d.SelectSingleNode("//e:image/e:tilesy", $ns).InnerText
  $png = [Convert]::FromBase64String($d.SelectSingleNode("//e:image/e:png", $ns).InnerText.Trim())
  $sheet = [Drawing.Bitmap]::new([IO.MemoryStream]::new($png))
  $cw = [int]($sheet.Width / $tilesX); $ch = [int]($sheet.Height / $tilesY)
  $scale = [Math]::Min(1.0, 120.0 / $ch); if ($ch -lt 60) { $scale = [Math]::Floor(120.0 / $ch) }
  $W = [int]($cw * $scale); $H = [int]($ch * $scale)

  # walk (or the first animation) + one trick the pet has
  $anims = @($d.SelectNodes("//e:animations/e:animation", $ns))
  $walk = $anims | Where-Object { $_.SelectSingleNode("e:name", $ns).InnerText -eq 'walk' } | Select-Object -First 1
  if (-not $walk) { $walk = $anims[0] }
  $trick = $null
  foreach ($t in $tricks) { $trick = $anims | Where-Object { $_.SelectSingleNode("e:name", $ns).InnerText -eq $t } | Select-Object -First 1; if ($trick) { break } }
  $play = @($walk); if ($trick -and $trick -ne $walk) { $play += $trick }

  $dir = Join-Path $tmp $pet; Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory $dir | Out-Null
  $list = New-Object Text.StringBuilder; $i = 0; $p = $null
  foreach ($a in $play) {
    $seq = $a.SelectSingleNode("e:sequence", $ns)
    $frames = @($seq.SelectNodes("e:frame", $ns) | ForEach-Object { [int]$_.InnerText })
    if ($frames.Count -eq 0) { continue }
    $from = [int]$seq.GetAttribute("repeatfrom")
    $seqFrames = @($frames)
    $times = if ($a -eq $walk) { 3 } else { 1 }
    if ($seq.GetAttribute("repeat") -ne "0" -and $from -lt $frames.Count) { for ($k = 0; $k -lt $times; $k++) { $seqFrames += $frames[$from..($frames.Count - 1)] } }
    $ms = 150; $iv = $a.SelectSingleNode("e:start/e:interval", $ns); if ($iv -and $iv.InnerText -match '^\d+$') { $ms = [Math]::Max(40, [int]$iv.InnerText) }
    foreach ($f in ($seqFrames | Select-Object -First 80)) {
      $bmp = [Drawing.Bitmap]::new($W, $H, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
      $g = [Drawing.Graphics]::FromImage($bmp); $g.Clear([Drawing.Color]::Transparent)
      $g.InterpolationMode = if ($scale -ge 1) { "NearestNeighbor" } else { "HighQualityBicubic" }; $g.PixelOffsetMode = "Half"
      $src = [Drawing.Rectangle]::new(($f % $tilesX) * $cw, [Math]::Floor($f / $tilesX) * $ch, $cw, $ch)
      $g.DrawImage($sheet, [Drawing.Rectangle]::new(0, 0, $W, $H), $src, "Pixel"); $g.Dispose()
      $p = Join-Path $dir ("{0:D4}.png" -f $i); $bmp.Save($p); $bmp.Dispose()
      [void]$list.AppendLine("file '$($p -replace '\\','/')'"); [void]$list.AppendLine("duration $($ms/1000)"); $i++
    }
  }
  $sheet.Dispose()
  if ($i -eq 0) { Write-Warning "${pet}: no frames"; continue }
  [void]$list.AppendLine("file '$($p -replace '\\','/')'")
  $lf = Join-Path $dir "list.txt"; [IO.File]::WriteAllText($lf, $list.ToString())
  $gif = Join-Path $root "Pets\$pet\preview.gif"
  & ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i $lf -filter_complex "[0:v]split[a][b];[a]palettegen=reserve_transparent=1:stats_mode=full:max_colors=128[p];[b][p]paletteuse=alpha_threshold=128:dither=none" -loop 0 $gif
  "{0,-18} {1,4} frames {2,6:N0} KB" -f $pet, $i, ((Get-Item $gif).Length / 1KB)
}
