r"""建置 Probably Doom 並安裝到遊戲（正式版的官方 mod 格式；只用 Python 標準函式庫，不用另外裝套件）。

事前準備：.NET SDK 8 以上（mod 是 netstandard2.1，離線測試 verify\ 是 net8.0）、Python 3.10 以上。編譯時參考遊戲的
Probably Stolen_Data\Managed（只讀）。

用法：
  python tools/install.py build        編譯 mod（不動遊戲）
  python tools/install.py stage        編譯後把 mod 資料夾組到 build\modscan\dbfoxtw.probably-doom\（不動遊戲；給官方掃描
                                       tools/modscan 檢查用，內容和安裝的一樣）
  python tools/install.py install      編譯後裝到遊戲的 Mods\dbfoxtw.probably-doom\（整個資料夾清空重放）
  python tools/install.py uninstall    移除 Mods\dbfoxtw.probably-doom\（ModData 裡的 WAD、設定、Doom 的存檔保留）
  python tools/install.py package      編譯後打包給玩家的 zip（還沒有 package\README.txt，下一段再補）
都可加 --game "遊戲資料夾"（也可設環境變數 PS_GAME_DIR）。
找遊戲資料夾、編譯、產生 manifest.xml、安裝、打包的做法在 modkit.py（從 Probably Stolen mods 的 monorepo 原樣複製，MIT），
這裡只寫這個 mod 的設定。
"""
from __future__ import annotations

import shutil
import sys
from pathlib import Path
from typing import Callable

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import modkit  # noqa: E402

ROOT = HERE.parent
# 音色庫（GPL v2，Tim Brechbill）：音樂用 MeltySynth 合成時讀，放進 mod 資料夾（RIFF 檔頭，遊戲的掃描當成已知媒體放行），
# 連同它的來源說明與授權全文（純文字，掃描放行）
ASSETS = [ROOT / "mod" / "assets" / n for n in ("TimGM6mb.sf2", "TimGM6mb.txt", "LICENSE_TimGM6mb.txt")]


class DoomMod(modkit.OfficialMod):
    """modkit 的 official_main 安裝、打包時不傳 extra，所以在這裡把音色庫加進 fill（安裝、打包、stage 共用）。"""

    def fill(self, dest: Path, version: str, extra: Callable[[Path], None] | None = None) -> None:
        def add_assets(d: Path) -> None:
            for p in ASSETS:
                shutil.copy2(p, d / p.name)
            if extra:
                extra(d)

        super().fill(dest, version, add_assets)


MOD = DoomMod(
    root=ROOT,
    mod_id="dbfoxtw.probably-doom",  # 和 mod/src/ProbablyDoomMod.cs 的 ModId 一致
    name="Probably Doom",
    # 模組畫面的說明，只有英文（遊戲直接顯示、不翻譯）
    description=("Play Doom on the store's tablet. Bring your own WAD: put DOOM1.WAD, DOOM.WAD, DOOM2.WAD or Freedoom "
                 "in the mod's data folder."),
    assembly="ProbablyDoom",
    zip_name="ProbablyStolen-ProbablyDoom",
    debug_note="這個檔存在時，mod 會把除錯用的經過寫進 Player.log（0.1.0 還沒有除錯輸出）。\n",
)


def stage(game: Path) -> Path:
    """編譯並把 mod 資料夾組到 build\\modscan\\<ID>\\（先清空），給 tools/modscan 掃。"""
    MOD.build(game)
    dest = ROOT / "build" / "modscan" / MOD.mod_id
    if dest.exists():
        shutil.rmtree(dest)
    MOD.fill(dest, MOD.version())
    print(f"已組好 mod 資料夾：{dest}")
    return dest


if __name__ == "__main__":
    if len(sys.argv) >= 2 and sys.argv[1] == "stage":
        game_arg = sys.argv[sys.argv.index("--game") + 1] if "--game" in sys.argv else None
        stage(modkit.find_game_dir(game_arg, demo=False))
    else:
        modkit.official_main(MOD)
