#!/usr/bin/env bash
# Compatibility entry point; shared script selects only the TUI project.
set -euo pipefail
build_script="$(dirname -- "${BASH_SOURCE[0]}")/build.sh"
if [[ $# -eq 1 && ( $1 == --help || $1 == -h ) ]]; then
    exec bash "$build_script" --help
fi
exec bash "$build_script" tui "$@"
