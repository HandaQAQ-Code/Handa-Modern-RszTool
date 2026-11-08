@echo off
setlocal
set "RSZ_FEATURE_DIR=%~dp0publish\Drop-Flags-Montage-Editor"
if not exist "%RSZ_FEATURE_DIR%\RszTool.App.exe" (
    echo Editor not found: "%RSZ_FEATURE_DIR%\RszTool.App.exe"
    pause
    exit /b 1
)
pushd "%RSZ_FEATURE_DIR%"
if "%~1"=="" (
    start "" "%RSZ_FEATURE_DIR%\RszTool.App.exe"
) else (
    start "" "%RSZ_FEATURE_DIR%\RszTool.App.exe" "%~f1"
)
popd
endlocal
