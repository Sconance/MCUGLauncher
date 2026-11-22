#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."

setup_linux() {
  sudo apt update
  sudo apt install -y build-essential pkg-config libwebkit2gtk-4.1-dev \
    curl wget file libxdo-dev libssl-dev libayatana-appindicator3-dev librsvg2-dev nodejs npm

  if command -v rustup >/dev/null 2>&1; then
    rustup update stable
    rustup default stable
  else
    curl --proto '=https' --tlsv1.2 -fsSL https://sh.rustup.rs |
      sh -s -- -y --profile minimal --default-toolchain stable
    export PATH="${CARGO_HOME:-$HOME/.cargo}/bin:$PATH"
  fi

  pkg-config --modversion webkit2gtk-4.1
  printf '%s\n' 'If Rust was just installed, reopen your terminal before building.'
}

setup_macos() {
  if ! xcode-select -p >/dev/null 2>&1; then
    xcode-select --install
    printf '%s\n' 'Complete the Xcode Command Line Tools installation to continue.'
    until xcode-select -p >/dev/null 2>&1; do sleep 5; done
  fi

  if ! command -v brew >/dev/null 2>&1; then
    if [[ ! -x /opt/homebrew/bin/brew && ! -x /usr/local/bin/brew ]]; then
      local installer
      installer=$(mktemp)
      if ! curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh -o "$installer" ||
         ! /bin/bash "$installer"; then
        rm -f -- "$installer"
        return 1
      fi
      rm -f -- "$installer"
    fi
    if [[ -x /opt/homebrew/bin/brew ]]; then
      eval "$(/opt/homebrew/bin/brew shellenv)"
    else
      eval "$(/usr/local/bin/brew shellenv)"
    fi
  fi

  brew install node@24 rustup
  local toolchain_path
  toolchain_path="$(brew --prefix node@24)/bin:$(brew --prefix rustup)/bin"
  export PATH="$toolchain_path:$PATH"
  rustup update stable
  rustup default stable
  printf 'Before building in your terminal, run:\nexport PATH="%s:$PATH"\n' "$toolchain_path"
}

case "$(uname -s)" in
  Linux) setup_linux ;;
  Darwin) setup_macos ;;
  *) printf '%s\n' 'On Windows, use scripts/setup-env.ps1.' >&2; exit 1 ;;
esac

node --version
rustc --version
npm ci --prefer-offline
