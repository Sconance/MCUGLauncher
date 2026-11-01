param([switch]$Publish)
$ErrorActionPreference = 'Stop'

function New-Zip([string]$Source, [string]$Destination) {
    $sourcePath = (Resolve-Path $Source).Path
    $zip = [System.IO.Compression.ZipFile]::Open($Destination, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem $sourcePath -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($sourcePath.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative) | Out-Null
        }
    } finally {
        $zip.Dispose()
    }
}

Push-Location (Split-Path $PSScriptRoot)
try {
    dotnet build src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    if ($Publish) {
        [xml]$props = Get-Content Directory.Build.props
        $version = ($props.Project.PropertyGroup | Where-Object McugVersion | Select-Object -First 1).McugVersion
        if (-not $version) { throw 'Directory.Build.props is missing McugVersion' }

        # 文件夹发布避免单文件 apphost 解压到 %TEMP%\.net；该缓存不可写时 EXE 会以 error 160 退出。
        Remove-Item artifacts/full, artifacts/lite -Recurse -Force -ErrorAction SilentlyContinue
        dotnet publish src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/full
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        dotnet publish src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o artifacts/lite
        if ($LASTEXITCODE -ne 0) { throw 'Lite publish failed' }

        New-Item artifacts/release -ItemType Directory -Force | Out-Null
        Get-ChildItem artifacts/release -File | Remove-Item -Force -ErrorAction SilentlyContinue
        Add-Type -AssemblyName System.IO.Compression
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        New-Zip artifacts/full "artifacts/release/MCUGLauncher-$version-Windows-x64.zip"
        New-Zip artifacts/lite "artifacts/release/MCUGLauncher-$version-Windows-x64-Lite.zip"
        Write-Host "Release artifacts: MCUGLauncher-$version-Windows-x64.zip, MCUGLauncher-$version-Windows-x64-Lite.zip"
    }
} finally { Pop-Location }
