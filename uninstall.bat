@echo off
rem Remove Probably Doom from the game (deletes Mods\dbfoxtw.probably-doom). Close the game first.
rem Kept: the settings (ModConfigs\dbfoxtw.probably-doom.json in the save folder) and the save folder's
rem ModData\dbfoxtw_probably-doom (your WAD, Doom's saves and doom.cfg).
rem This file is ASCII-only on purpose: cmd.exe parses .bat files byte by byte.
setlocal
cd /d "%~dp0"
chcp 65001 >nul
set PYTHONUTF8=1
set PYTHONIOENCODING=utf-8
where py >nul 2>nul
if not errorlevel 1 (
    py -3 tools\install.py uninstall %*
) else (
    python tools\install.py uninstall %*
)
echo.
pause
