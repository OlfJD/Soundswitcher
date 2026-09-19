@echo off
if exist "%~dp0SoundSwitcher.exe" (
    start "" "%~dp0SoundSwitcher.exe"
) else (
    start "" "%~dp0SoundSwitcher\bin\Release\net10.0-windows\win-x64\publish\SoundSwitcher.exe"
)
exit
