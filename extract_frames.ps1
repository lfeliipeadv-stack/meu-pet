param(
    # Folder with the reference videos (*.mp4)
    [string]$VideoDir = (Join-Path $env:USERPROFILE "Downloads\Video"),
    # Where the extracted frames are written
    [string]$OutDir = (Join-Path $PSScriptRoot "shots\mascot-frames"),
    # Frames per second to extract
    [int]$Fps = 5
)
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$ffmpeg = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links\ffmpeg.exe"
if (-not (Test-Path $ffmpeg)) { $ffmpeg = "ffmpeg" }

$videos = Get-ChildItem (Join-Path $VideoDir "*.mp4")
$i = 0
foreach ($v in $videos) {
    $i++
    $name = "video$i"
    # ${name} with braces: "$name_" would be read as a variable called "name_" (empty).
    & $ffmpeg -i $v.FullName -vf "fps=$Fps,scale=480:-1" -q:v 2 (Join-Path $OutDir "${name}_%03d.jpg") -y 2>$null
    $count = (Get-ChildItem (Join-Path $OutDir "${name}_*.jpg")).Count
    Write-Output "$name ($($v.Name)): $count frames"
}
Write-Output "Done. Output: $OutDir"
