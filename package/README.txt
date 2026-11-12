Probably DOOM v{version}
========================

A mod for Probably Stolen: play DOOM on the store's Tablet.
The DOOM engine is ManagedDoom (C#, by Nobuaki Tanaka); music is synthesized with MeltySynth and the TimGM6mb sound font.
This mod does NOT include any DOOM game data (WAD). Bring your own.

[Install]
This is an official-format mod for the game and needs no permissions.
1. Close the game. Unzip the dbfoxtw.probably-doom folder in this zip into the game's Mods folder
   (you should end up with Mods\dbfoxtw.probably-doom\manifest.xml).
   To find the game folder: Steam Library > right-click the game > Manage > Browse local files.
2. Start the game, enable "Probably DOOM" on the main menu's Mods screen, then restart the game.

[Where to put the WAD]
Put one of these files into the mod's data folder, inside the game's save folder:
  %USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModData\dbfoxtw_probably-doom\
- DOOM1.WAD (shareware), DOOM.WAD (The Ultimate DOOM), DOOM2.WAD (DOOM II)
- freedoom1.wad / freedoom2.wad (Freedoom, free at https://freedoom.github.io/)
If there is no WAD yet, the DOOM screen in the game shows the full path of this folder.
DOOM's own saves (saves\<wad name>\) and settings (doom.cfg) are kept in the same folder.

[License]
Probably DOOM is free software under the GNU General Public License, version 2 or (at your option) any later version
(LICENSE.txt). Source code: https://github.com/dbfoxtw/probably-doom
Third-party parts: ManagedDoom (GPL v2 or later, LICENSE_ManagedDoom.txt), MeltySynth (MIT, LICENSE_MeltySynth.txt),
TimGM6mb.sf2 by Tim Brechbill (GPL v2, LICENSE_TimGM6mb.txt and TimGM6mb.txt).
