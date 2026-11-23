#!/usr/bin/env python3
"""全年齡版打包：複製專案 → 排除 R18 Spine 與備份 → 把全年齡用不到的圖換成替身（全透明，或已用衣服遮色片挖空的身體／胸／奶頭）
→ 在「複本」裡把 R18 開關鎖死為關（打開設定裡的 R18 也還是全年齡）。
原專案完全不動。用法（在專案根目錄）：
    python Tools/build_allages_project.py [輸出資料夾]
預設輸出到專案旁邊的 <專案名>_allages_build。
之後用 Godot 開那份複本一次（重建 .godot 快取），再匯出到 Steam。
替身清單 = _AllAgesStubs/ 內的檔案（同相對路徑覆蓋）；要增減就增減那個資料夾。
"""
import os, shutil, sys, fnmatch

SRC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DST = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else SRC.rstrip("\\/") + "_allages_build"
STUBS = os.path.join(SRC, "_AllAgesStubs")

SKIP_TOP = {".git", ".godot", "_AllAgesStubs", "Backups", "ArtDrafts", "Marketing", "_參考圖"}
SKIP_TOP_GLOB = ["_backup*", "backup_*"]
# R18 Spine：SpineChar 找不到時會自動改讀 linai_allages
SKIP_SPINE_GLOB = ["linai", "linai_newhair", "linai_backup*", "linai_newhair_backup*", "linai_allages_backup*"]

def skip(rel):
    parts = rel.replace("\\", "/").split("/")
    if parts[0] in SKIP_TOP or any(fnmatch.fnmatch(parts[0], g) for g in SKIP_TOP_GLOB):
        return True
    if len(parts) >= 3 and parts[0] == "Resources" and parts[1] == "Spine" and any(fnmatch.fnmatch(parts[2], g) for g in SKIP_SPINE_GLOB):
        return True
    return False

if os.path.exists(DST):
    sys.exit("輸出資料夾已存在，請先刪掉或換名字：" + DST)
n = 0
for root, dirs, files in os.walk(SRC):
    rel_root = os.path.relpath(root, SRC)
    if rel_root != "." and skip(rel_root):
        dirs[:] = []
        continue
    dirs[:] = [d for d in dirs if not skip(os.path.normpath(os.path.join(rel_root, d)))]
    for f in files:
        rel = os.path.normpath(os.path.join(rel_root, f))
        out = os.path.join(DST, rel)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        shutil.copy2(os.path.join(root, f), out); n += 1
s = 0
for root, _, files in os.walk(STUBS):
    for f in files:
        if f == ".gdignore": continue
        rel = os.path.relpath(os.path.join(root, f), STUBS)
        out = os.path.join(DST, rel)
        if not os.path.exists(out):
            print("略過（目的地沒有這個檔）：", rel); continue
        shutil.copy2(os.path.join(root, f), out); s += 1
# 在複本裡鎖死 R18：PlayerDataManager.R18ContentEnabled 永遠 false（原專案不動）
pdm = os.path.join(DST, "Scripts", "Systems", "PlayerDataManager.cs")
PATCHES = [
    ("R18ContentEnabled { get; set; } = DemoMode.Enabled;", "R18ContentEnabled { get; set; } = false;"),
    ('R18ContentEnabled = (bool)_cfg.GetValue("game", "r18", DemoMode.Enabled);', "R18ContentEnabled = false;"),
    ("R18ContentEnabled = enabled;", "R18ContentEnabled = false; enabled = false;"),
]
if os.path.exists(pdm):
    with open(pdm, "rb") as fh: data = fh.read().decode("utf-8")
    for old, new in PATCHES:
        if old in data: data = data.replace(old, new)
        else: print("警告：PlayerDataManager.cs 找不到要鎖 R18 的這一段，請手動檢查：", old)
    with open(pdm, "wb") as fh: fh.write(data.encode("utf-8"))
else:
    print("警告：找不到 PlayerDataManager.cs，R18 沒有鎖")
print(f"完成：複製 {n} 檔，替換 {s} 張替身圖 → {DST}")
