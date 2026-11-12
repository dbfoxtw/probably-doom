@echo off
rem Build Probably Doom and install it into the game's Mods\dbfoxtw.probably-doom folder (dev build: debug mode on). Close the game first.
rem Needs .NET SDK 8+ and Python 3.10+. MelonLoader is not needed (official mod format).
rem First install: enable the mod in the game's mod manager, then restart the game.
rem Put a WAD (DOOM1.WAD, DOOM.WAD, DOOM2.WAD, freedoom1.wad or freedoom2.wad) in the save folder's ModData\dbfoxtw_probably-doom.
rem This file is ASCII-only on purpose: cmd.exe parses .bat files byte by byte.
setlocal
cd /d "%~dp0"
chcp 65001 >nul
set PYTHONUTF8=1
set PYTHONIOENCODING=utf-8
where py >nul 2>nul
if not errorlevel 1 (
    py -3 tools\install.py install --debug %*
) else (
    python tools\install.py install --debug %*
)
echo.
pause
