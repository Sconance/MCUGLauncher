param([switch]$Publish)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    dotnet build src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    if ($Publish) {
        [xml]$props = Get-Content Directory.Build.props
        $version = ($props.Project.PropertyGroup | Where-Object McugVersion | Select-Object -First 1).McugVersion
        if (-not $version) { throw 'Directory.Build.props is missing McugVersion' }
        dotnet publish src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release -o artifacts/publish
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        dotnet publish src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -o artifacts/publish-lite
        if ($LASTEXITCODE -ne 0) { throw 'Lite publish failed' }
        New-Item artifacts/release -ItemType Directory -Force | Out-Null
        Copy-Item artifacts/publish/MCUGLauncher.exe "artifacts/release/MCUGLauncher-$version-Windows-x64.exe" -Force
        Copy-Item artifacts/publish-lite/MCUGLauncher.exe "artifacts/release/MCUGLauncher-$version-Windows-x64-Lite.exe" -Force
        Write-Host "Release artifacts: MCUGLauncher-$version-Windows-x64.exe, MCUGLauncher-$version-Windows-x64-Lite.exe"
    }
} finally { Pop-Location }
