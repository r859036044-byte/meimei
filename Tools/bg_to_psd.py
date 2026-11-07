#!/usr/bin/env python3
"""把背景圖拆成 PSD：LineArt_multiply（線稿，正片疊底）＋ Color_01..N（色塊，每個顏色一個圖層，數字小的在下）＋ Original_ref_HIDDEN（原圖參考，預設隱藏）。
圖層名稱用英文，因為 PSD 的圖層名稱寫中文會變亂碼。
用法：python3 Tools/bg_to_psd.py <輸入資料夾> <輸出資料夾> [色塊數量=14]
需要：pip install psd-tools opencv-python-headless numpy pillow
"""
import sys, os, glob
import numpy as np, cv2
from PIL import Image
from psd_tools import PSDImage
from psd_tools.api.layers import PixelLayer
from psd_tools.constants import BlendMode


def split(path, out_dir, k):
    im = Image.open(path)
    has_alpha = im.mode == 'RGBA'
    rgba = np.array(im.convert('RGBA'))
    rgb = rgba[:, :, :3]
    alpha = rgba[:, :, 3]
    h, w = alpha.shape

    # ── 線稿：局部對比找出暗線（含細線），輸出成黑線＋透明底 ──
    gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
    blur = cv2.GaussianBlur(gray, (0, 0), 0.8)
    # blackhat：只留「細的暗結構」（線條），大片暗色區塊（櫃子、夜景）不會被當成線稿
    bh = cv2.morphologyEx(blur, cv2.MORPH_BLACKHAT, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (13, 13)))
    line_a = np.clip(bh.astype(np.float32) * 5.0, 0, 255).astype(np.uint8)
    line_a = np.where(line_a < 40, 0, line_a).astype(np.uint8)
    line_a = (line_a.astype(np.float32) * (alpha.astype(np.float32) / 255.0)).astype(np.uint8)
    line = np.zeros((h, w, 4), np.uint8)
    line[:, :, 3] = line_a                                                                # 黑色（0,0,0）
    # ── 色塊：先壓平再分群 ──
    flat = cv2.pyrMeanShiftFiltering(rgb, sp=12, sr=28)
    Z = flat.reshape(-1, 3).astype(np.float32)
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 30, 1.0)
    _, lab, cen = cv2.kmeans(Z, k, None, crit, 3, cv2.KMEANS_PP_CENTERS)
    lab = lab.reshape(h, w).astype(np.uint8)
    lab = cv2.medianBlur(lab, 5)                                                          # 清掉零碎雜點
    cen = cen.astype(np.uint8)
    order = np.argsort([-(lab == i).sum() for i in range(k)])                             # 面積大的在下面

    psd = PSDImage.new('RGB', (w, h))
    ref = PixelLayer.frompil(im.convert('RGBA'), psd, 'Original_ref_HIDDEN', 0, 0)
    ref.visible = False
    psd.append(ref)
    for n, i in enumerate(order):
        mask = (lab == i)
        layer = np.zeros((h, w, 4), np.uint8)
        layer[mask, :3] = cen[i]
        layer[:, :, 3] = np.where(mask, alpha, 0)
        # 讓上層色塊略微往外擴 1px，避免疊起來出現縫
        if n > 0:
            layer[:, :, 3] = cv2.dilate(layer[:, :, 3], np.ones((3, 3), np.uint8))
            solid = layer[:, :, 3] > 0
            layer[solid & ~mask, :3] = cen[i]
        name = 'Color_%02d_%02x%02x%02x' % (n + 1, cen[i][0], cen[i][1], cen[i][2])
        psd.append(PixelLayer.frompil(Image.fromarray(layer, 'RGBA'), psd, name, 0, 0))
    ll = PixelLayer.frompil(Image.fromarray(line, 'RGBA'), psd, 'LineArt_multiply', 0, 0)
    ll.blend_mode = BlendMode.MULTIPLY
    psd.append(ll)
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, os.path.splitext(os.path.basename(path))[0] + '.psd')
    psd.save(out)
    return out


if __name__ == '__main__':
    src, dst = sys.argv[1], sys.argv[2]
    k = int(sys.argv[3]) if len(sys.argv) > 3 else 14
    files = sorted(sum([glob.glob(os.path.join(src, e)) for e in ('*.png', '*.jpg', '*.jpeg', '*.webp')], []))
    for f in files:
        print('處理', os.path.basename(f), flush=True)
        print('  →', split(f, dst, k), flush=True)
