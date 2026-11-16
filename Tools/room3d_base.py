"""簡單 3D 房間底圖產生器：用方塊拼出房間，輸出「平塗底色＋黑線」的透視圖，給你截圖／描繪背景用。
用法：python3 Tools/room3d_base.py <輸出資料夾>   （需要 Pillow）
要改家具位置／大小／顏色，改下面 bathroom() 和 player_room() 裡的方塊就好（單位：公尺，x 右、y 上、z 往房間深處）。"""
import math, sys, os
from PIL import Image, ImageDraw

W, H, SS = 1920, 1080, 2          # 輸出大小、超取樣倍率（先畫 2 倍再縮小，線條比較平滑）

class Cam:
    def __init__(s, pos, look, fov=62):
        s.pos = pos
        dx, dy, dz = look[0]-pos[0], look[1]-pos[1], look[2]-pos[2]
        s.yaw = math.atan2(dx, dz); s.pitch = math.atan2(dy, math.hypot(dx, dz))
        s.f = (W*SS/2) / math.tan(math.radians(fov)/2)
    def cam_space(s, p):
        x, y, z = p[0]-s.pos[0], p[1]-s.pos[1], p[2]-s.pos[2]
        cy, sy = math.cos(-s.yaw), math.sin(-s.yaw)
        x, z = x*cy + z*sy, -x*sy + z*cy
        cp, sp = math.cos(s.pitch), math.sin(s.pitch)
        y, z = y*cp - z*sp, y*sp + z*cp
        return (x, y, z)
    def proj(s, c):
        return (W*SS/2 + c[0]/c[2]*s.f, H*SS/2 - c[1]/c[2]*s.f)

NEAR = 0.05
def clip_poly(poly):
    out = []
    for i in range(len(poly)):
        a, b = poly[i], poly[(i+1) % len(poly)]
        ina, inb = a[2] > NEAR, b[2] > NEAR
        if ina: out.append(a)
        if ina != inb:
            t = (NEAR - a[2]) / (b[2] - a[2])
            out.append(tuple(a[k] + (b[k]-a[k])*t for k in range(3)))
    return out

class Scene:
    def __init__(s, cam, bg=(230, 230, 235)):
        s.cam = cam; s.items = []; s.bg = bg; s.boxes = []; s.shell = None
    def quad(s, pts, fill, order, lines=None, cull_n=None, line_w=3, outline=True):
        s.items.append(dict(pts=pts, fill=fill, order=order, lines=lines or [], cull=cull_n, w=line_w, outline=outline))
    def box(s, x0, y0, z0, x1, y1, z1, fill, order=None, line_w=3):
        s.boxes.append([x0, y0, z0, x1, y1, z1, list(fill)])
        cx, cy, cz = (x0+x1)/2, (y0+y1)/2, (z0+z1)/2
        if order is None:
            order = -math.dist((cx, cy, cz), s.cam.pos)
        def sh(c, k): return tuple(int(v*k) for v in c)
        faces = [
            ([(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0)], (0,0,-1), sh(fill, .88)),
            ([(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)], (0,0,1),  sh(fill, .88)),
            ([(x0,y0,z0),(x0,y0,z1),(x0,y1,z1),(x0,y1,z0)], (-1,0,0), sh(fill, .78)),
            ([(x1,y0,z0),(x1,y0,z1),(x1,y1,z1),(x1,y1,z0)], (1,0,0),  sh(fill, .78)),
            ([(x0,y1,z0),(x1,y1,z0),(x1,y1,z1),(x0,y1,z1)], (0,1,0),  fill),
        ]
        for pts, n, col in faces:
            s.quad(pts, col, order, cull_n=(n, ((pts[0][0]+pts[2][0])/2, (pts[0][1]+pts[2][1])/2, (pts[0][2]+pts[2][2])/2)), line_w=line_w)
    def grid_lines(s, p0, u, v, nu, nv, step_u, step_v):
        """在平面上畫格線（磁磚）。p0 起點，u、v 為兩個方向單位向量，回傳線段清單。"""
        L = []
        for i in range(nu+1):
            a = tuple(p0[k] + u[k]*step_u*i for k in range(3)); b = tuple(a[k] + v[k]*step_v*nv for k in range(3)); L.append((a, b))
        for j in range(nv+1):
            a = tuple(p0[k] + v[k]*step_v*j for k in range(3)); b = tuple(a[k] + u[k]*step_u*nu for k in range(3)); L.append((a, b))
        return L
    def render(s):
        im = Image.new('RGB', (W*SS, H*SS), s.bg); d = ImageDraw.Draw(im)
        for it in sorted(s.items, key=lambda t: t['order']):
            if it['cull']:
                n, c = it['cull']; v = (s.cam.pos[0]-c[0], s.cam.pos[1]-c[1], s.cam.pos[2]-c[2])
                if n[0]*v[0] + n[1]*v[1] + n[2]*v[2] <= 0: continue
            cp = clip_poly([s.cam.cam_space(p) for p in it['pts']])
            if len(cp) >= 3:
                pp = [s.cam.proj(c) for c in cp]
                d.polygon(pp, fill=it['fill'])
                if it['outline']: d.line(pp + [pp[0]], fill=(25, 25, 30), width=it['w']*SS//2+1, joint='curve')
            for a, b in it['lines']:
                seg = clip_poly([s.cam.cam_space(a), s.cam.cam_space(b), s.cam.cam_space(b)])
                if len(seg) >= 2:
                    pa, pb = s.cam.proj(seg[0]), s.cam.proj(seg[1]); d.line([pa, pb], fill=(120, 125, 135), width=SS)
        return im.resize((W, H), Image.LANCZOS)

def room_shell(sc, x0, x1, z0, z1, hgt, floor, wallL, wallB, wallR, tile=None):
    sc.shell = dict(x0=x0, x1=x1, z0=z0, z1=z1, h=hgt, floor=list(floor), wl=list(wallL), wb=list(wallB), wr=list(wallR), tile=tile)
    far = -1e6
    # 地板、左牆、後牆、右牆（內側，不剔除）；order 很小＝最先畫
    fl = []; wl = []; wb = []; wr = []
    if tile:
        nx, nz = int(round((x1-x0)/tile)), int(round((z1-z0)/tile)); ny = int(round(hgt/tile))
        fl = sc.grid_lines((x0, 0, z0), (1,0,0), (0,0,1), nx, nz, tile, tile)
        wl = sc.grid_lines((x0, 0, z0), (0,0,1), (0,1,0), nz, ny, tile, tile)
        wb = sc.grid_lines((x0, 0, z1), (1,0,0), (0,1,0), nx, ny, tile, tile)
        wr = sc.grid_lines((x1, 0, z0), (0,0,1), (0,1,0), nz, ny, tile, tile)
    sc.quad([(x0,0,z0),(x1,0,z0),(x1,0,z1),(x0,0,z1)], floor, far, fl)
    sc.quad([(x0,0,z0),(x0,0,z1),(x0,hgt,z1),(x0,hgt,z0)], wallL, far+1, wl)
    sc.quad([(x0,0,z1),(x1,0,z1),(x1,hgt,z1),(x0,hgt,z1)], wallB, far+1, wb)
    sc.quad([(x1,0,z0),(x1,0,z1),(x1,hgt,z1),(x1,hgt,z0)], wallR, far+1, wr)

def bathroom():
    cam = Cam((1.95, 1.75, -2.3), (0.95, 1.0, 2.0), fov=58)
    sc = Scene(cam)
    X0, X1, Z0, Z1, HG = 0.0, 2.3, 0.0, 2.7, 2.4
    room_shell(sc, X0, X1, Z0, Z1, HG, (225,228,232), (240,243,247), (236,240,245), (238,241,246), tile=0.3)
    # 天花板燈／換氣扇（貼在後牆上緣，只當位置參考）
    sc.box(0.25, 2.25, 1.3, 0.75, 2.4, 1.6, (215,218,222))
    # 浴缸（沿左牆）
    sc.box(0.0, 0.0, 0.55, 0.75, 0.55, 2.1, (245,247,250), line_w=4)
    sc.box(0.08, 0.5, 0.63, 0.67, 0.56, 2.02, (170,210,235))       # 水面
    # 小凳子（前景左）
    sc.box(0.2, 0.0, 0.15, 0.55, 0.3, 0.5, (240,242,246))
    # 淋浴區：玻璃隔板＋蓮蓬頭／水龍頭
    sc.box(1.15, 0.0, 0.55, 1.2, 2.0, 2.0, (235,245,252), line_w=3)  # 玻璃隔板（淡藍）
    sc.box(0.95, 1.0, 2.62, 1.05, 1.15, 2.7, (190,195,205))             # 龍頭
    sc.box(0.84, 1.15, 2.64, 0.9, 2.0, 2.7, (200,205,212))               # 蓮蓬頭支架
    # 洗手台：櫃子＋台面＋鏡子＋兩側吊櫃＋毛巾架（靠後牆右側）
    sc.box(1.3, 0.0, 2.1, 2.25, 0.85, 2.7, (245,246,248), line_w=4)
    sc.box(1.3, 0.85, 2.1, 2.25, 0.9, 2.7, (228,232,238))                 # 台面
    sc.box(1.5, 0.8, 2.2, 1.95, 0.88, 2.6, (250,250,252))                 # 洗臉盆
    sc.box(1.35, 1.0, 2.69, 2.2, 2.0, 2.7, (200,222,238))                 # 鏡子
    sc.box(1.3, 1.45, 2.4, 1.5, 2.0, 2.7, (245,246,248))                  # 左吊櫃
    sc.box(2.05, 1.45, 2.4, 2.25, 2.0, 2.7, (245,246,248))                # 右吊櫃
    sc.box(2.27, 1.1, 2.0, 2.3, 1.2, 2.55, (200,205,212))                 # 右牆毛巾架
    sc.box(2.25, 0.7, 2.25, 2.3, 1.1, 2.4, (250,250,252))                 # 毛巾
    # 左牆置物架
    sc.box(0.0, 1.1, 1.0, 0.18, 1.14, 1.5, (210,215,222))
    return sc

def player_room():
    cam = Cam((2.7, 1.45, -2.4), (1.9, 0.95, 4.0), fov=56)
    sc = Scene(cam)
    X0, X1, Z0, Z1, HG = 0.0, 4.4, 0.0, 4.6, 2.4
    room_shell(sc, X0, X1, Z0, Z1, HG, (232,214,190), (244,232,214), (246,236,220), (242,230,212), tile=0.6)
    # 天花板燈（參考）
    sc.box(2.0, 2.36, 2.0, 2.6, 2.4, 2.6, (255,250,225))
    # 衣櫃（後牆中間）
    sc.box(2.35, 0.0, 4.0, 3.45, 2.05, 4.6, (205,150,95), line_w=4)
    sc.box(2.88, 0.05, 3.99, 2.9, 2.0, 4.0, (150,100,60))                  # 兩扇門的縫
    # 窗戶＋窗簾（後牆右側）＋冷氣
    sc.box(3.5, 0.9, 4.58, 4.3, 2.1, 4.6, (200,225,245))
    sc.box(3.45, 0.0, 4.45, 3.75, 2.2, 4.6, (250,248,244))                 # 窗簾左
    sc.box(4.05, 0.0, 4.45, 4.4, 2.2, 4.6, (250,248,244))                  # 窗簾右
    sc.box(3.7, 1.95, 4.4, 4.3, 2.3, 4.6, (245,245,248))                   # 冷氣
    # 床：床架＋床墊＋枕頭＋棉被（床頭靠左牆，床身往右）
    sc.box(0.55, 0.0, 1.95, 2.65, 0.3, 3.15, (200,140,90), line_w=4)
    sc.box(0.6, 0.3, 2.0, 2.6, 0.55, 3.1, (248,248,250))
    sc.box(0.35, 0.3, 1.95, 0.55, 0.95, 3.15, (210,150,100))               # 床頭板
    sc.box(0.62, 0.55, 2.05, 1.0, 0.68, 2.55, (252,252,255)); sc.box(0.62, 0.55, 2.6, 1.0, 0.68, 3.05, (252,252,255))   # 枕頭
    sc.box(1.05, 0.55, 2.0, 2.62, 0.72, 3.1, (240,243,248))                # 棉被
    # 床頭櫃＋檯燈＋盆栽
    sc.box(0.02, 0.0, 1.35, 0.5, 0.55, 1.9, (205,145,95))
    sc.box(0.17, 0.55, 1.52, 0.35, 0.95, 1.7, (255,240,200))
    sc.box(0.1, 0.55, 3.25, 0.35, 0.8, 3.5, (200,120,90))                  # 另一側小盆栽
    # 書桌＋椅子（右牆）
    sc.box(3.7, 0.0, 1.6, 4.4, 0.78, 2.7, (200,140,90), line_w=4)
    sc.box(3.2, 0.0, 1.9, 3.65, 0.45, 2.35, (190,130,85))                  # 椅子座
    sc.box(3.2, 0.45, 1.9, 3.25, 1.0, 2.35, (190,130,85))                  # 椅背
    sc.box(4.1, 0.78, 2.3, 4.35, 1.0, 2.55, (200,120,90))                  # 桌上盆栽
    return sc

if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(out, exist_ok=True)
    rooms = {'浴室': bathroom(), '男主房間': player_room()}
    for name, sc in rooms.items():
        sc.render().save(os.path.join(out, f'{name}_3D底圖.png'))
    # 可自己移動視角的 HTML 檢視器（three.js 已內嵌，離線也能開）
    import json
    tpl = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'room3d_viewer_template.html')
    threejs = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'three.min.js')
    if os.path.exists(tpl) and os.path.exists(threejs):
        data = {n: dict(shell=sc.shell, boxes=sc.boxes, cam=dict(pos=sc.cam.pos, yaw=math.degrees(sc.cam.yaw), pitch=math.degrees(sc.cam.pitch), fov=round(2*math.degrees(math.atan((W*SS/2)/sc.cam.f)),1))) for n, sc in rooms.items()}
        html = open(tpl, encoding='utf-8').read().replace('/*THREEJS*/', open(threejs, encoding='utf-8').read()).replace('/*ROOMDATA*/', json.dumps(data, ensure_ascii=False))
        open(os.path.join(out, '房間3D檢視.html'), 'w', encoding='utf-8').write(html)
    print('done')
