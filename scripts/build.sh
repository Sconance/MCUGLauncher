#!/usr/bin/env bash
# Select exactly one frontend; publish its project rather than the solution.
set -euo pipefail

build_usage() {
    echo "Usage: bash scripts/build.sh <ui|tui> [output-directory]"
    echo "ui:  Avalonia GUI only -> artifacts/ui/win-x64/MCUGLauncher.exe"
    echo "tui: console only      -> artifacts/tui/win-x64/MCUGLauncher.Tui.exe"
    echo "Both modes target self-contained Windows x64. Set DOTNET_BIN to select an SDK."
    echo "Relative output paths use the repository root. Never reuse a folder containing the other frontend."
}

if [[ $# -eq 1 && ( $1 == --help || $1 == -h ) ]]; then
    build_usage
    exit 0
fi
if [[ $# -lt 1 || $# -gt 2 ]]; then
    build_usage >&2
    exit 2
fi

build_frontend="$1"
case "$build_frontend" in
    ui)
        build_project="src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj"
        build_executable="MCUGLauncher.exe"
        ;;
    tui)
        build_project="src/MCUGLauncher.Tui/MCUGLauncher.Tui.csproj"
        build_executable="MCUGLauncher.Tui.exe"
        ;;
    *)
        build_usage >&2
        exit 2
        ;;
esac

build_repository="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
build_dotnet="${DOTNET_BIN:-dotnet}"
build_output="${2:-artifacts/$build_frontend/win-x64}"
if ! command -v "$build_dotnet" >/dev/null 2>&1; then
    echo "Missing .NET SDK. Install the version specified in global.json, or set DOTNET_BIN." >&2
    exit 1
fi

cd -- "$build_repository"
build_check_output() {
    local build_file build_name
    for build_file in "$build_output"/*; do
        [[ -e "$build_file" ]] || continue
        build_name="${build_file##*/}"
        case "$build_frontend:$build_name" in
            ui:MCUGLauncher.Tui.*|tui:MCUGLauncher.exe|tui:MCUGLauncher.dll|tui:MCUGLauncher.deps.json|tui:MCUGLauncher.runtimeconfig.json|tui:Avalonia*.dll)
                echo "Output contains the other frontend: $build_file. Choose a separate output directory." >&2
                return 1
                ;;
        esac
    done
}

build_check_output
"$build_dotnet" publish "$build_project" \
    --configuration Release --runtime win-x64 --self-contained true \
    -p:EnableWindowsTargeting=true -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false \
    -p:DebugType=embedded --output "$build_output"
build_check_output
if [[ ! -f "$build_output/$build_executable" ]]; then
    echo "Expected executable was not generated: $build_output/$build_executable" >&2
    exit 1
fi
echo "$build_frontend published: $build_output/$build_executable"
