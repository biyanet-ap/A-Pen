# Builds dist\A-Pen.exe and dist\A-Pen-Setup.exe from src\ (no SDK needed; uses .NET Framework's csc).
#   .\build.ps1              both
#   .\build.ps1 -Target app    only A-Pen.exe
#   .\build.ps1 -Target setup  only the installer, embedding the existing dist\A-Pen.exe
param([ValidateSet('all', 'app', 'setup')] [string]$Target = 'all')
$ErrorActionPreference = 'Stop'
$fw   = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf  = "$fw\WPF"
$src  = Join-Path $PSScriptRoot 'src'
$dist = Join-Path $PSScriptRoot 'dist'
$ico  = Join-Path $src 'HighPen.ico'
$out  = Join-Path $dist 'A-Pen.exe'
$setup = Join-Path $dist 'A-Pen-Setup.exe'

New-Item -ItemType Directory -Force $dist | Out-Null
if (-not (Test-Path $ico)) { & (Join-Path $src 'make-icon.ps1') }

# 1) the app
if ($Target -ne 'setup') {
    Get-Process A-Pen -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $out } | Stop-Process -Force
    & "$fw\csc.exe" /nologo /target:winexe /optimize+ "/win32icon:$ico" "/out:$out" `
        "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" `
        "/r:$wpf\WindowsBase.dll" "/r:$fw\System.Xaml.dll" "/r:$fw\System.Windows.Forms.dll" "/r:$fw\System.Drawing.dll" `
        (Join-Path $src 'HighPen.cs')
    if ($LASTEXITCODE -ne 0) { exit 1 }
    Write-Host "Built $out"
}

# 2) the installer (A-Pen.exe embedded as a resource; the name must match AppName + ".exe" in Setup.cs)
if ($Target -ne 'app') {
    if (-not (Test-Path $out)) { throw "$out not found; build the app first" }
    & "$fw\csc.exe" /nologo /target:winexe /optimize+ "/win32icon:$ico" "/out:$setup" "/resource:$out,A-Pen.exe" `
        "/r:$fw\System.Windows.Forms.dll" (Join-Path $src 'Setup.cs')
    if ($LASTEXITCODE -ne 0) { exit 1 }
    Write-Host "Built $setup"
}
