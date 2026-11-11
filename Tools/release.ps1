<#
.SYNOPSIS
    Build a DesktopPet release and publish it on GitHub (the source of the in-app updater).

.DESCRIPTION
    1. Writes the version into src\Properties\AssemblyInfoPortable.cs
    2. Publishes the .NET 10 single-file exe
    3. Packs lang\*.json into lang.zip
    4. Writes SHA256SUMS (the updater refuses any file whose hash does not match)
    5. Creates the GitHub release "v<Version>" with the three files (needs the GitHub CLI, "gh auth login")

    Commit the version change after the release.

.EXAMPLE
    .\Tools\release.ps1 -Version 2.1.0 -Notes "- New pet: Capi"
.EXAMPLE
    .\Tools\release.ps1 -Version 2.1.0 -NoUpload     # build and check only, nothing is sent to GitHub
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Notes = "",
    [string]$Repo = "lfeliipeadv-stack/meu-pet",
    [switch]$NoUpload
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 2.1.0" }

$root = Split-Path $PSScriptRoot -Parent
$src = Join-Path $root 'src'
$out = Join-Path $root "release\$Version"
$utf8 = New-Object System.Text.UTF8Encoding($false)

# 1. version
$info = Join-Path $src 'Properties\AssemblyInfoPortable.cs'
$text = [IO.File]::ReadAllText($info)
$text = $text -replace 'AssemblyVersion\("[^"]*"\)', "AssemblyVersion(""$Version.0"")"
$text = $text -replace 'AssemblyFileVersion\("[^"]*"\)', "AssemblyFileVersion(""$Version.0"")"
[IO.File]::WriteAllText($info, $text, $utf8)
Write-Output "Version $Version written to AssemblyInfoPortable.cs"

# 2. exe
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $src 'DesktopPet.Net.csproj') -c Release -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
$exe = Join-Path $out 'DesktopPet.exe'
$built = (Get-Item $exe).VersionInfo.FileVersion
if ($built -ne "$Version.0") { throw "The exe reports version $built instead of $Version.0" }

# 3. languages
$langZip = Join-Path $out 'lang.zip'
Compress-Archive -Path (Join-Path $src 'lang\*.json') -DestinationPath $langZip -Force

# 4. hashes ("hash  name", LF, like sha256sum)
$sums = foreach ($f in @($exe, $langZip)) {
    (Get-FileHash $f -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + (Split-Path $f -Leaf)
}
$sumsFile = Join-Path $out 'SHA256SUMS'
[IO.File]::WriteAllText($sumsFile, ($sums -join "`n") + "`n", $utf8)
Write-Output ""
Write-Output ($sums -join "`n")
Write-Output ""
Write-Output ("DesktopPet.exe: {0:N0} KB" -f ((Get-Item $exe).Length / 1KB))

if ($NoUpload) { Write-Output "Built in $out (not uploaded)."; return }

# 5. GitHub release
if (-not $Notes) { $Notes = "DesktopPet $Version" }
$Notes += "`n`nRequires the .NET 10 Desktop Runtime: https://dotnet.microsoft.com/download/dotnet/10.0"
gh release create "v$Version" $exe $langZip $sumsFile --repo $Repo --title "DesktopPet $Version" --notes $Notes
if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
Write-Output "Release v$Version published on https://github.com/$Repo/releases"
