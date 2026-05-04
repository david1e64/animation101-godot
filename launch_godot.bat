@echo off
set GODOT=C:\Users\David\Desktop\Godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe
set PROJECT=%~dp0project.godot

echo Launching Godot 4 Mono...
echo Project: %PROJECT%
echo.

if not exist "%GODOT%" (
    echo ERROR: Godot exe not found at %GODOT%
    pause
    exit /b 1
)

"%GODOT%" --editor "%PROJECT%"
