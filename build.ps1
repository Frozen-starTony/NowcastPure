# Builds NowcastPure.exe with the native Windows C# compiler (.NET Framework 4.8)
# Zero external dependencies, nothing to download or install.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File build.ps1

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'

if (-not (Test-Path $csc)) {
    throw "csc.exe not found at $csc"
}

$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll',
    'System.Runtime.dll',
    'System.Runtime.WindowsRuntime.dll'
) | ForEach-Object { "-r:$framework\$_" }

$out = Join-Path $here 'NowcastPure.exe'
$src = Join-Path $here 'src\*.cs'

Write-Host "Compiling NowcastPure with native Windows compiler..." -ForegroundColor Cyan

& $csc -nologo -target:winexe -optimize+ -codepage:65001 `
    "-out:$out" `
    @references $src

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Successfully built: $out" -ForegroundColor Green
$fileInfo = Get-Item $out
Write-Host "Size: $([math]::Round($fileInfo.Length / 1KB, 1)) KB" -ForegroundColor Yellow
