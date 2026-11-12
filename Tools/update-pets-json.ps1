# Writes "size", "animations" and "sounds" into every entry of Pets/pets.json, read from
# Pets/<folder>/animations.xml. The program shows them before the pet is downloaded
# (size on the download button, counts in the detail panel). Run it after changing a pet.
#
#   powershell -ExecutionPolicy Bypass -File Tools\update-pets-json.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$jsonPath = Join-Path $root 'Pets\pets.json'
$json = [IO.File]::ReadAllText($jsonPath)

$entry = [regex]'\{[^{}]*?"folder"\s*:\s*"([^"]+)"[^{}]*\}'
$updated = 0
$json = $entry.Replace($json, {
    param($m)
    $folder = $m.Groups[1].Value
    $file = Join-Path $root "Pets\$folder\animations.xml"
    $text = $m.Value
    if (-not (Test-Path $file)) { Write-Warning "missing $file"; return $text }

    # size as GitHub serves it: the repository stores LF line endings (* text=auto)
    $bytes = [IO.File]::ReadAllBytes($file)
    $crlf = 0
    for ($i = 1; $i -lt $bytes.Length; $i++) { if ($bytes[$i] -eq 10 -and $bytes[$i - 1] -eq 13) { $crlf++ } }
    $size = $bytes.Length - $crlf

    $xml = New-Object Xml.XmlDocument
    $xml.Load($file)
    $anims = @($xml.DocumentElement.SelectNodes("*[local-name()='animations']/*")).Count
    $sounds = @($xml.DocumentElement.SelectNodes("*[local-name()='sounds']/*")).Count

    # drop old values, then add the new ones after the last property
    $text = [regex]::Replace($text, ',?\s*"(size|animations|sounds)"\s*:\s*\d+', '')
    $indent = [regex]::Match($text, '\n([ \t]*)"folder"').Groups[1].Value
    $close = [regex]::Match($text, '\s*\}$')
    $body = $text.Substring(0, $close.Index)
    $nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $script:updated++
    return $body + ",$nl$indent""size"" : $size,$nl$indent""animations"" : $anims,$nl$indent""sounds"" : $sounds" + $close.Value
})

$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($jsonPath, $json, $utf8)
Write-Host "pets.json: $updated entries updated"
