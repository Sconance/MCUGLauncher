@echo off
setlocal DisableDelayedExpansion
if "%~1"=="--help" goto help
if "%~1"=="-h" goto help
if not "%~2"=="" goto usage_error

call "%~dp0build.bat" tui "%~1"
exit /b %ERRORLEVEL%

:usage_error
echo Expected at most one output directory. 1>&2
exit /b 2

:help
call "%~dp0build.bat" --help
exit /b 0
