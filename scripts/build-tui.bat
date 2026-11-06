@echo off
setlocal DisableDelayedExpansion
if "%~1"=="--help" goto help
if "%~1"=="-h" goto help
if not "%~2"=="" goto usage_error

set "TUI_DOTNET=%DOTNET_BIN%"
if not defined TUI_DOTNET set "TUI_DOTNET=dotnet"
set "TUI_OUTPUT=%~1"
if not defined TUI_OUTPUT set "TUI_OUTPUT=artifacts\tui\win-x64"

pushd "%~dp0.." || exit /b 1
"%TUI_DOTNET%" publish src\MCUGLauncher.Tui\MCUGLauncher.Tui.csproj --configuration Release --runtime win-x64 --self-contained true -p:EnableWindowsTargeting=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=embedded --output "%TUI_OUTPUT%"
set "TUI_EXIT=%ERRORLEVEL%"
if "%TUI_EXIT%"=="0" echo TUI published: %TUI_OUTPUT%\MCUGLauncher.Tui.exe
popd
exit /b %TUI_EXIT%

:usage_error
echo Expected at most one output directory. 1>&2
exit /b 2

:help
echo Usage: scripts\build-tui.bat [output-directory]
echo Default: artifacts\tui\win-x64; self-contained Windows x64 console application.
echo Set DOTNET_BIN to select a .NET SDK executable. Relative output paths use the repository root.
exit /b 0
