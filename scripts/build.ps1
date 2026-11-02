param([switch]$Publish, [switch]$SingleFile)
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    dotnet build src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    if($Publish){
        [xml]$props=Get-Content Directory.Build.props
        $version=($props.Project.PropertyGroup | Where-Object McugVersion | Select-Object -First 1).McugVersion
        $channel=($props.Project.PropertyGroup | Where-Object McugChannel | Select-Object -First 1).McugChannel
        $name=if($channel){"$version-$channel"}else{$version}
        # Unique staging paths avoid stale build content and recursive cleanup of user paths.
        $stage=Join-Path 'artifacts' ('publish-'+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $stage,'artifacts/release' | Out-Null
        foreach($flavor in @('full','lite')){
            $selfContained=if($flavor -eq 'full'){'true'}else{'false'}
            $output=Join-Path $stage $flavor
            dotnet publish src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release -r win-x64 --self-contained $selfContained "-p:PublishSingleFile=$($SingleFile.IsPresent.ToString().ToLowerInvariant())" -p:EnableCompressionInSingleFile=false -o $output
            if($LASTEXITCODE -ne 0){throw "$flavor publish failed"}
            $suffix=if($flavor -eq 'lite'){'-Lite'}else{''}
            $baseName="MCUGLauncher-$name-Windows-x64$suffix"
            if($SingleFile){Copy-Item -LiteralPath (Join-Path $output 'MCUGLauncher.exe') -Destination "artifacts/release/$baseName.exe" -Force}
            else{Compress-Archive -Path "$output/*" -DestinationPath "artifacts/release/$baseName.zip" -Force}
        }
    }
} finally {Pop-Location}
