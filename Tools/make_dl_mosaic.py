#!/usr/bin/env python3
"""
DL 版馬賽克圖產生工具。

把「雞雞、開1、開上、插入、洞、M 腿小穴」六張圖做成馬賽克版，存到 Resources/Sprites/DL/（檔名跟原圖相同）。
原圖完全不動；遊戲在 DL 模式（見 Scripts/Systems/DlMode.cs）才會改載入 DL 資料夾裡的版本。

用法（在專案根目錄）：
    python3 Tools/make_dl_mosaic.py            # 預設 12px
    python3 Tools/make_dl_mosaic.py --block 16 # 想換格子大小時

做法：以畫布左上角為原點切成 block×block 的格子，每格的顏色＝格子裡所有像素「依不透明度加權」的平均色，
每格的不透明度＝格子裡不透明度的平均（所以原圖很淡的地方，馬賽克後還是一樣淡，不會變明顯）。
格子大小的單位是「原圖像素」（這些圖都是 2000x3200 的整張畫布）。
改完要重新產生後，在 Godot 開一次專案讓它重新匯入。
"""
import argparse, os, sys
import numpy as np
from PIL import Image

# 檔名（不含 .png）：對應遊戲圖層  雞雞→雞雞、xiaoxuekai→開1、xiaoxuekaishang→開上、xiaoxuekai2→插入、
#                                  xiaoxuekaidi→洞、小穴_M→m腿/小穴（M 腿的小穴）
SOURCES = ["雞雞", "xiaoxuekai", "xiaoxuekaishang", "xiaoxuekai2", "xiaoxuekaidi", "小穴_M"]

def mosaic(im: Image.Image, block: int) -> Image.Image:
    a = np.asarray(im.convert("RGBA")).astype(np.float32)
    h, w = a.shape[:2]
    ph, pw = (-h) % block, (-w) % block
    a = np.pad(a, ((0, ph), (0, pw), (0, 0)))                       # 補到格子的整數倍（補的部分透明）
    H, W = a.shape[:2]
    blocks = a.reshape(H // block, block, W // block, block, 4)     # (by, y, bx, x, c)
    alpha = blocks[..., 3] / 255.0
    mean_a = alpha.mean(axis=(1, 3))                                # 每格的平均不透明度
    wsum = alpha.sum(axis=(1, 3))
    rgb = (blocks[..., :3] * alpha[..., None]).sum(axis=(1, 3)) / np.maximum(wsum, 1e-6)[..., None]   # 以不透明度加權的平均色
    out = np.zeros((H // block, W // block, 4), np.float32)
    out[..., :3] = rgb
    out[..., 3] = mean_a * 255.0
    out = np.repeat(np.repeat(out, block, axis=0), block, axis=1)   # 每格放大回 block×block
    out = out[:h, :w]
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--block", type=int, default=12, help="馬賽克格子大小（原圖像素）")
    ap.add_argument("--src", default="Resources/Sprites")
    ap.add_argument("--dst", default="Resources/Sprites/DL")
    args = ap.parse_args()
    os.makedirs(args.dst, exist_ok=True)
    for name in SOURCES:
        p = os.path.join(args.src, name + ".png")
        if not os.path.exists(p):
            print(f"找不到 {p}，略過", file=sys.stderr); continue
        out = mosaic(Image.open(p), args.block)
        out.save(os.path.join(args.dst, name + ".png"), optimize=True)
        print(f"{name}.png → {args.dst}/{name}.png（{args.block}px）")

if __name__ == "__main__":
    main()
