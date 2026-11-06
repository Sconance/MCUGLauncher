#!/usr/bin/env bash
# Publish only the TUI entry point and its App/Core dependencies for Windows x64.
set -euo pipefail

if [[ $# -gt 1 || ${1:-} == --help || ${1:-} == -h ]]; then
    echo "Usage: bash scripts/build-tui.sh [output-directory]"
    echo "Default: artifacts/tui/win-x64; self-contained Windows x64 console application."
    echo "Set DOTNET_BIN to select a .NET SDK executable. Relative output paths use the repository root."
    [[ $# -le 1 ]] && exit 0
    exit 2
fi

tui_repository="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
tui_dotnet="${DOTNET_BIN:-dotnet}"
tui_output="${1:-artifacts/tui/win-x64}"
if ! command -v "$tui_dotnet" >/dev/null 2>&1; then
    echo "Missing .NET SDK. Install the version specified in global.json, or set DOTNET_BIN." >&2
    exit 1
fi

cd -- "$tui_repository"
"$tui_dotnet" publish src/MCUGLauncher.Tui/MCUGLauncher.Tui.csproj \
    --configuration Release --runtime win-x64 --self-contained true \
    -p:EnableWindowsTargeting=true -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false \
    -p:DebugType=embedded --output "$tui_output"
echo "TUI published: $tui_output/MCUGLauncher.Tui.exe"
