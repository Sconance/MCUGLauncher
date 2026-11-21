$ErrorActionPreference = 'Stop'

$options = @('--exact', '--source', 'winget', '--accept-package-agreements', '--accept-source-agreements', '--no-upgrade')
$packages = @('OpenJS.NodeJS.LTS', 'Microsoft.EdgeWebView2Runtime', 'Rustlang.Rustup', 'Microsoft.VisualStudio.2022.BuildTools')
foreach ($id in $packages) {
    $installerOptions = @()
    if ($id -eq 'Microsoft.VisualStudio.2022.BuildTools') {
        $installerOptions = @('--override', '--passive --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended')
    }
    winget install --id $id @options @installerOptions
    # WinGet returns 0x8A15002B when no applicable upgrade is available.
    if ($LASTEXITCODE -notin @(0, -1978335189)) {
        throw "WinGet installation failed for $id (exit code $LASTEXITCODE)."
    }
}

$machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$cargoHome = if ($env:CARGO_HOME) { $env:CARGO_HOME } else { Join-Path $env:USERPROFILE '.cargo' }
$env:Path = "$(Join-Path $cargoHome 'bin');$machinePath;$userPath;$env:Path"

rustup update stable-msvc
if ($LASTEXITCODE -ne 0) { throw 'Failed to update the Rust MSVC toolchain.' }
rustup default stable-msvc
if ($LASTEXITCODE -ne 0) { throw 'Failed to select the Rust MSVC toolchain.' }

Push-Location (Join-Path $PSScriptRoot '..')
try {
    npm.cmd ci --prefer-offline
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
} finally {
    Pop-Location
}

Write-Host 'Open a new PowerShell session before building.'
