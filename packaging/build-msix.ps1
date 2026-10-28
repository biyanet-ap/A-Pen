# Builds dist\HighPen.msix for the Microsoft Store (the Store signs it; no certificate needed here).
#   .\packaging\build-msix.ps1                       uses makeappx.exe from the newest installed Windows SDK
#   .\packaging\build-msix.ps1 -MakeAppx <path>      or an explicit makeappx.exe (e.g. from the
#                                                    Microsoft.Windows.SDK.BuildTools NuGet package)
param([string]$MakeAppx)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root 'dist'
$msix = Join-Path $dist 'HighPen.msix'

if (-not $MakeAppx) {
    $MakeAppx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $MakeAppx) { throw 'makeappx.exe not found; install the Windows SDK or pass -MakeAppx <path>' }
}
$makePri = Join-Path (Split-Path $MakeAppx) 'makepri.exe'

# the Store needs a four-part version with 0 as the last part
$m = Select-String -Path (Join-Path $root 'src\HighPen.cs') -Pattern 'AppVersion = "([^"]+)"'
$version = $m.Matches[0].Groups[1].Value + '.0'

& (Join-Path $root 'build.ps1') -Target app
if ($LASTEXITCODE) { exit 1 }

# package layout: HighPen.exe, AppxManifest.xml, Assets\
$stage = Join-Path ([IO.Path]::GetTempPath()) 'HighPen-msix'
Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $dist 'HighPen.exe') $stage
Copy-Item -Recurse (Join-Path $PSScriptRoot 'Assets') $stage
(Get-Content -Raw (Join-Path $PSScriptRoot 'AppxManifest.xml')).Replace('$Version$', $version) |
    Set-Content -Encoding UTF8 (Join-Path $stage 'AppxManifest.xml')

# resources.pri maps Assets\Square44x44Logo.png etc. to the scale-* / targetsize-* files
$priConfig = Join-Path ([IO.Path]::GetTempPath()) 'HighPen-priconfig.xml'
& $makePri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
if ($LASTEXITCODE) { exit 1 }
& $makePri new /pr $stage /cf $priConfig /mn (Join-Path $stage 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o | Out-Null
if ($LASTEXITCODE) { exit 1 }

& $MakeAppx pack /o /d $stage /p $msix
if ($LASTEXITCODE) { exit 1 }
Remove-Item -Recurse -Force $stage, $priConfig
Write-Host "Built $msix ($version)"
