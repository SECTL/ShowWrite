@echo off
setlocal

set PLUGIN_DLL=d:\WindowsPrograms\ShowWrite\Plugins\TestPlugin\bin\Debug\net8.0\TestPlugin.dll
set DEST_DIR=%APPDATA%\ShowWrite\PKG

if not exist "%DEST_DIR%" (
    mkdir "%DEST_DIR%"
)

copy /Y "%PLUGIN_DLL%" "%DEST_DIR%\"

echo 插件已复制到 %DEST_DIR%
pause
