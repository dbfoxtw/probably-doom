@echo off
rem Dev build for checking text length: same as install.bat (debug mode on), plus the "longest" flag file.
rem With the flag, every text of the mod's own UI (DOOM button tooltip, the no-WAD and error messages, the pause text,
rem the copy path button and the settings window) shows the widest of English, German, French, Spanish, Russian and Polish
rem (they share one font), so one run with the game in English covers the longest text of every Latin/Cyrillic language.
rem Player.log gets one line per text saying which language was picked.
rem Do not use it for normal testing: reinstall with install.bat afterwards (a normal install clears the mod folder,
rem which removes the flag).
rem Close the game first. Needs .NET SDK 8+ and Python 3.10+.
rem This file is ASCII-only on purpose: cmd.exe parses .bat files byte by byte.
setlocal
cd /d "%~dp0"
chcp 65001 >nul
set PYTHONUTF8=1
set PYTHONIOENCODING=utf-8
where py >nul 2>nul
if not errorlevel 1 (
    py -3 tools\install.py install --debug --longest %*
) else (
    python tools\install.py install --debug --longest %*
)
echo.
pause
