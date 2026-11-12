$ErrorActionPreference = 'Stop'
$plug = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\CeifadoraObsidiana'
$ver  = (Select-String -Path "$PSScriptRoot\src\CeifadoraPlugin.cs" -Pattern 'PluginVersion = "([0-9.]+)"').Matches[0].Groups[1].Value
$tmp  = Join-Path $env:TEMP 'ceifadora_pacote'
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
$dest = Join-Path $tmp 'BepInEx\plugins\CeifadoraObsidiana'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item "$plug\CeifadoraObsidiana.dll", "$plug\ceifadora_golpes" $dest
foreach ($d in 'modelo', 'modelo_ferro', 'modelo_metalnegro', 'modelo_flametal') {
  New-Item -ItemType Directory -Force (Join-Path $dest $d) | Out-Null
  Get-ChildItem "$plug\$d" -File | Where-Object { $_.Extension -in '.png', '.malha', '.txt' } | Copy-Item -Destination (Join-Path $dest $d)
}
Copy-Item "$PSScriptRoot\LEIA-ME.txt" $tmp
$desk = [Environment]::GetFolderPath('Desktop')
$zip  = Join-Path $desk "CeifadoraObsidiana_v$ver.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$tmp\*" -DestinationPath $zip
Remove-Item $tmp -Recurse -Force
Write-Host ""
Write-Host "Pronto: $zip"
