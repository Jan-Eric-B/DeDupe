@echo off
setlocal enabledelayedexpansion

set "root_dir=%~dp0.."
pushd "%root_dir%"
set "root_dir=%CD%"

echo.
echo  Cleaning solution from: %root_dir%
echo.

for /f "delims=" %%d in ('dir /a:d /s /b "%root_dir%"') do (
    set "dir_path=%%d"
    echo !dir_path! | findstr /i "\\.git\\" >nul
    if !errorlevel! equ 0 (
        rem Skipping .git directory
    ) else (
        if /i "%%~nxd"=="bin" (
            echo Deleting bin folder: %%d
            rd /s /q "%%d"
        ) else if /i "%%~nxd"=="obj" (
            echo Deleting obj folder: %%d
            rd /s /q "%%d"
        ) else if /i "%%~nxd"==".vs" (
            echo Deleting .vs folder: %%d
            rd /s /q "%%d"
        ) else if /i "%%~nxd"=="AppPackages" (
            echo Deleting AppPackages folder: %%d
            rd /s /q "%%d"
        )
    )
)

popd
endlocal
pause
