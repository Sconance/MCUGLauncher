@echo off
setlocal DisableDelayedExpansion
if "%~1"=="--help" goto help
if "%~1"=="-h" goto help
if not "%~3"=="" goto usage_error
set "BUILD_FRONTEND=%~1"
if "%BUILD_FRONTEND%"=="ui" goto ui
if "%BUILD_FRONTEND%"=="tui" goto tui
goto usage_error

:ui
set "BUILD_PROJECT=src\MCUGLauncher.Avalonia\MCUGLauncher.Avalonia.csproj"
set "BUILD_EXECUTABLE=MCUGLauncher.exe"
goto publish

:tui
set "BUILD_PROJECT=src\MCUGLauncher.Tui\MCUGLauncher.Tui.csproj"
set "BUILD_EXECUTABLE=MCUGLauncher.Tui.exe"

:publish
set "BUILD_DOTNET=%DOTNET_BIN%"
if not defined BUILD_DOTNET set "BUILD_DOTNET=dotnet"
set "BUILD_OUTPUT=%~2"
if not defined BUILD_OUTPUT set "BUILD_OUTPUT=artifacts\%BUILD_FRONTEND%\win-x64"
pushd "%~dp0.." || exit /b 1
call :check_output
if errorlevel 1 goto failed
"%BUILD_DOTNET%" publish "%BUILD_PROJECT%" --configuration Release --runtime win-x64 --self-contained true -p:EnableWindowsTargeting=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=embedded --output "%BUILD_OUTPUT%"
if errorlevel 1 goto failed
call :check_output
if errorlevel 1 goto failed
if not exist "%BUILD_OUTPUT%\%BUILD_EXECUTABLE%" goto missing_executable
echo %BUILD_FRONTEND% published: %BUILD_OUTPUT%\%BUILD_EXECUTABLE%
popd
exit /b 0

:check_output
if "%BUILD_FRONTEND%"=="ui" if exist "%BUILD_OUTPUT%\MCUGLauncher.Tui.*" goto mixed_output
if "%BUILD_FRONTEND%"=="tui" for %%F in (MCUGLauncher.exe MCUGLauncher.dll MCUGLauncher.deps.json MCUGLauncher.runtimeconfig.json Avalonia*.dll) do if exist "%BUILD_OUTPUT%\%%F" goto mixed_output
exit /b 0

:mixed_output
echo Output contains the other frontend. Choose a separate output directory: "%BUILD_OUTPUT%" 1>&2
exit /b 1

:missing_executable
echo Expected executable was not generated: "%BUILD_OUTPUT%\%BUILD_EXECUTABLE%" 1>&2
popd
exit /b 1

:failed
set "BUILD_EXIT=%ERRORLEVEL%"
popd
exit /b %BUILD_EXIT%

:usage_error
echo Usage: scripts\build.bat ^<ui^|tui^> [output-directory] 1>&2
exit /b 2

:help
echo Usage: scripts\build.bat ^<ui^|tui^> [output-directory]
echo ui:  Avalonia GUI only -^> artifacts\ui\win-x64\MCUGLauncher.exe
echo tui: console only      -^> artifacts\tui\win-x64\MCUGLauncher.Tui.exe
echo Both modes target self-contained Windows x64. Set DOTNET_BIN to select an SDK.
echo Relative output paths use the repository root. Never reuse a folder containing the other frontend.
exit /b 0
