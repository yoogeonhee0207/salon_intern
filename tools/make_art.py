#!/usr/bin/env python3
"""막내의 가위: 도형 대신 쓸 그림을 코드로 그려서 Assets/Resources/Art 에 저장한다.

실행: python3 tools/make_art.py
- 큰 크기로 그린 뒤 줄여서(4배 슈퍼샘플링) 가장자리를 부드럽게 만든다.
- UI 틀(버튼, 말풍선, 패널)과 바닥은 거의 흰색으로 그려서 게임 안에서 색을 곱해 쓴다.
- 같이 만드는 .png.meta 는 Unity가 바로 읽도록 간단한 설정(압축 없음, 밉맵 없음)만 적는다.
"""
import hashlib
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "game", "SalonIntern", "Assets", "Resources", "Art")
SS = 4  # 슈퍼샘플링 배율

INK = (74, 56, 56)          # 윤곽선(따뜻한 짙은 갈색)
SKINS = [(255, 222, 196), (246, 205, 176), (235, 190, 155), (255, 214, 186)]


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    img.save(path)
    guid = hashlib.md5(("salonintern-art-" + name).encode()).hexdigest()
    meta = (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "TextureImporter:\n"
        "  serializedVersion: 13\n"
        "  mipmaps:\n"
        "    mipMapMode: 0\n"
        "    enableMipMap: 0\n"
        "  sRGBTexture: 1\n"
        "  alphaUsage: 1\n"
        "  alphaIsTransparency: 1\n"
        "  filterMode: 1\n"
        "  wrapMode: 1\n"
        "  maxTextureSize: 2048\n"
        "  textureCompression: 0\n"
        "  textureType: 0\n"
        "  textureShape: 1\n"
        "  isReadable: 0\n"
        "userData:\n"
        "assetBundleName:\n"
        "assetBundleVariant:\n"
    ) % guid
    with open(path + ".meta", "w", encoding="utf-8") as f:
        f.write(meta)
    print("saved", name, img.size)


def canvas(w, h, color=(0, 0, 0, 0)):
    img = Image.new("RGBA", (w * SS, h * SS), color)
    return img, ImageDraw.Draw(img)


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def rr(d, box, r, fill=None, outline=None, width=0):
    x0, y0, x1, y1 = [v * SS for v in box]
    d.rounded_rectangle([x0, y0, x1, y1], radius=r * SS, fill=fill, outline=outline, width=int(width * SS))


def ell(d, box, fill=None, outline=None, width=0):
    d.ellipse([v * SS for v in box], fill=fill, outline=outline, width=int(width * SS))


def poly(d, pts, fill=None, outline=None, width=0):
    d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)
    if outline:
        d.line([(x * SS, y * SS) for x, y in pts + [pts[0]]], fill=outline, width=int(width * SS), joint="curve")


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + ((c[3],) if len(c) > 3 else ())


# ---------------------------------------------------------------- UI 틀

def round_mask(img, w, h, r):
    """둥근 모서리 밖으로 삐져나온 선을 지운다."""
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, w * SS, h * SS], radius=r * SS, fill=255)
    a = img.getchannel("A")
    from PIL import ImageChops
    img.putalpha(ImageChops.multiply(a, mask))
    return img


def ui_button():
    w = h = 96
    img, d = canvas(w, h)
    rr(d, (2, 6, 94, 94), 26, fill=(150, 150, 150, 255))              # 아래 그림자 띠
    rr(d, (2, 2, 94, 86), 26, fill=(255, 255, 255, 255))
    rr(d, (2, 2, 94, 86), 26, outline=(205, 205, 205, 255), width=2)
    rr(d, (8, 7, 88, 40), 18, fill=(255, 255, 255, 255))              # 윗부분 하이라이트
    save(finish(img, w, h), "ui_button")


def ui_bubble():
    w = h = 96
    img, d = canvas(w, h)
    rr(d, (3, 6, 93, 93), 26, fill=(120, 110, 100, 90))               # 그림자
    rr(d, (3, 3, 93, 90), 26, fill=(255, 255, 255, 255))
    rr(d, (3, 3, 93, 90), 26, outline=(185, 175, 165, 255), width=3)
    save(finish(img, w, h), "ui_bubble")


def ui_panel():
    w = h = 96
    img, d = canvas(w, h)
    rr(d, (2, 2, 94, 94), 24, fill=(255, 255, 255, 255))
    rr(d, (2, 2, 94, 94), 24, outline=(196, 184, 170, 255), width=4)
    rr(d, (9, 9, 87, 87), 18, outline=(228, 220, 210, 255), width=2)
    save(finish(img, w, h), "ui_panel")


# ---------------------------------------------------------------- 바닥

def drop_shadow(img, box, r=12, blur=6, alpha=90, off=(4, 6)):
    """상자 모양 부드러운 그림자."""
    x0, y0, x1, y1 = box
    soft_layer(img, lambda dd: dd.rounded_rectangle([(x0 + off[0]) * SS, (y0 + off[1]) * SS, (x1 + off[0]) * SS, (y1 + off[1]) * SS],
                                                    radius=r * SS, fill=(60, 50, 50, 255)), blur, alpha)


def drop_shadow_ell(img, box, blur=6, alpha=90, off=(4, 6)):
    x0, y0, x1, y1 = box
    soft_layer(img, lambda dd: dd.ellipse([(x0 + off[0]) * SS, (y0 + off[1]) * SS, (x1 + off[0]) * SS, (y1 + off[1]) * SS], fill=(60, 50, 50, 255)), blur, alpha)


def floor_tile():
    w = h = 128
    img, d = canvas(w, h, (255, 255, 255, 255))
    rnd = random.Random(7)
    planks = 4
    ph = h // planks
    for i in range(planks):
        shift = rnd.choice([0, 32, 64, 96])
        base = rnd.randint(244, 255)
        d.rectangle([0, i * ph * SS, w * SS, (i + 1) * ph * SS], fill=(base, base, base, 255))
        # 판자 위쪽에 살짝 밝은 면, 아래쪽 그림자 줄
        d.rectangle([0, i * ph * SS, w * SS, (i * ph + 3) * SS], fill=(255, 255, 255, 255))
        d.line([(0, (i + 1) * ph * SS), (w * SS, (i + 1) * ph * SS)], fill=(212, 212, 212, 255), width=3 * SS)
        x = shift
        while x < w + 128:
            d.line([(x * SS, i * ph * SS), (x * SS, (i + 1) * ph * SS)], fill=(216, 216, 216, 255), width=3 * SS)
            x += 128
        for _ in range(7):  # 나뭇결
            y = (i * ph + rnd.randint(5, ph - 5))
            x0 = rnd.randint(-10, w - 24)
            pts = [((x0 + k * 6) * SS, (y + 1.2 * math.sin(k * 0.9 + rnd.random())) * SS) for k in range(rnd.randint(4, 9))]
            d.line(pts, fill=(228, 228, 228, 255), width=SS, joint="curve")
        if rnd.random() < 0.6:  # 옹이
            kx, ky = rnd.randint(14, w - 14), i * ph + ph // 2
            ell(d, (kx - 4, ky - 2, kx + 4, ky + 2), outline=(214, 214, 214, 255), width=1)
    save(finish(img, w, h), "floor_tile")


def corridor_tile():
    w = h = 96
    img, d = canvas(w, h, (255, 255, 255, 255))
    half = w // 2
    for ix in range(2):
        for iy in range(2):
            x0, y0 = ix * half, iy * half
            tone = 238 if (ix + iy) % 2 == 0 else 252
            d.rectangle([x0 * SS, y0 * SS, (x0 + half) * SS, (y0 + half) * SS], fill=(tone, tone, tone, 255))
            # 타일 모서리 하이라이트와 반짝임
            d.line([((x0 + 3) * SS, (y0 + 3) * SS), ((x0 + half - 4) * SS, (y0 + 3) * SS)], fill=(255, 255, 255, 255), width=2 * SS)
            d.line([((x0 + 6) * SS, (y0 + 10) * SS), ((x0 + 16) * SS, (y0 + 6) * SS)], fill=(255, 255, 255, 255), width=2 * SS)
    d.line([(0, 0), (w * SS, 0)], fill=(214, 214, 214, 255), width=2 * SS)
    d.line([(0, 0), (0, h * SS)], fill=(214, 214, 214, 255), width=2 * SS)
    d.line([(half * SS, 0), (half * SS, h * SS)], fill=(214, 214, 214, 255), width=2 * SS)
    d.line([(0, half * SS), (w * SS, half * SS)], fill=(214, 214, 214, 255), width=2 * SS)
    save(finish(img, w, h), "corridor_tile")


def plank_floor(d, w, h, base, line, step=48, seed=1):
    rnd = random.Random(seed)
    for k, gy in enumerate(range(0, h, step)):
        d.line([(0, gy * SS), (w * SS, gy * SS)], fill=line + (255,), width=3 * SS)
        off = rnd.choice([0, 60, 120, 180])
        for gx in range(off, w + 240, 240):
            d.line([(gx * SS, gy * SS), (gx * SS, (gy + step) * SS)], fill=line + (255,), width=3 * SS)
        for _ in range(3):
            y = gy + rnd.randint(8, step - 8)
            x0 = rnd.randint(0, w - 60)
            d.line([(x0 * SS, y * SS), ((x0 + rnd.randint(20, 60)) * SS, y * SS)], fill=shade(base, 0.93) + (255,), width=SS)


def room_rest():
    w, h = 380, 400
    img, d = canvas(w, h)
    rr(d, (0, 0, w, h), 28, fill=(198, 214, 232, 255))
    plank_floor(d, w, h, (198, 214, 232), (184, 202, 224), 44, 3)
    rr(d, (0, 0, w, h), 28, outline=(120, 142, 170, 255), width=6)
    # 창문 빛(위쪽)
    soft_layer(img, lambda dd: dd.polygon([(60 * SS, 0), (200 * SS, 0), (270 * SS, 200 * SS), (120 * SS, 200 * SS)], fill=(255, 250, 220, 255)), 14, 90)
    d = ImageDraw.Draw(img)
    # 러그 (줄무늬 + 술)
    drop_shadow_ell(img, (70, 150, 310, 330), 5, 60, (2, 4))
    d = ImageDraw.Draw(img)
    ell(d, (70, 150, 310, 330), fill=(228, 238, 250, 255), outline=(150, 172, 204, 255), width=4)
    ell(d, (92, 168, 288, 312), outline=(176, 196, 226, 255), width=4)
    ell(d, (116, 188, 264, 292), outline=(204, 218, 240, 255), width=3)
    for ang in range(0, 360, 14):
        x = 190 + 120 * math.cos(math.radians(ang)); y = 240 + 90 * math.sin(math.radians(ang))
        d.line([(x * SS, y * SS), ((x + 8 * math.cos(math.radians(ang))) * SS, (y + 8 * math.sin(math.radians(ang))) * SS)], fill=(150, 172, 204, 255), width=2 * SS)
    # 소파
    drop_shadow(img, (24, 252, 150, 376), 24, 6, 100)
    d = ImageDraw.Draw(img)
    rr(d, (24, 252, 150, 376), 24, fill=(232, 132, 124, 255), outline=INK, width=4)
    rr(d, (38, 266, 138, 322), 14, fill=(247, 164, 152, 255), outline=(200, 110, 104, 255), width=2)     # 등쿠션
    rr(d, (38, 326, 138, 366), 12, fill=(244, 156, 146, 255), outline=(200, 110, 104, 255), width=2)     # 좌석쿠션
    d.line([(88 * SS, 270 * SS), (88 * SS, 318 * SS)], fill=(210, 120, 112, 255), width=2 * SS)         # 박음질
    for k in range(5):
        d.line([((46 + k * 20) * SS, 346 * SS), ((52 + k * 20) * SS, 346 * SS)], fill=(210, 120, 112, 255), width=2 * SS)
    rr(d, (14, 280, 40, 370), 12, fill=(222, 120, 112, 255), outline=INK, width=3)                         # 팔걸이
    rr(d, (44, 270, 74, 298), 8, fill=(255, 226, 150, 255), outline=INK, width=3)                          # 쿠션
    d.line([(52 * SS, 278 * SS), (66 * SS, 290 * SS)], fill=(236, 190, 110, 255), width=2 * SS)
    # 탁자 위 컵·잡지
    drop_shadow(img, (210, 260, 340, 330), 18, 5, 90)
    d = ImageDraw.Draw(img)
    rr(d, (210, 260, 340, 330), 18, fill=(214, 170, 120, 255), outline=INK, width=4)
    rr(d, (218, 268, 332, 322), 12, outline=(190, 146, 100, 255), width=2)
    rr(d, (222, 284, 252, 316), 3, fill=(250, 250, 255, 255), outline=INK, width=2)                        # 잡지
    d.line([(228 * SS, 292 * SS), (246 * SS, 292 * SS)], fill=(220, 90, 110, 255), width=3 * SS)
    ell(d, (270, 272, 308, 304), fill=(255, 255, 255, 255), outline=INK, width=3)
    ell(d, (277, 279, 301, 297), fill=(142, 98, 70, 255))
    ell(d, (296, 282, 312, 296), outline=INK, width=3)                                                      # 손잡이
    for k in range(2):
        d.arc([(282 + k * 8) * SS, 258 * SS, (292 + k * 8) * SS, 272 * SS], 200, 340, fill=(255, 255, 255, 220), width=2 * SS)
    # 화분
    drop_shadow(img, (300, 340, 350, 384), 8, 4, 90)
    d = ImageDraw.Draw(img)
    rr(d, (300, 340, 350, 384), 8, fill=(206, 130, 96, 255), outline=INK, width=3)
    rr(d, (296, 336, 354, 350), 5, fill=(220, 146, 110, 255), outline=INK, width=3)
    for ang in (-50, -25, 0, 25, 50):
        x = 325 + ang * 0.7
        pts = [(325, 346), (x - 14, 304), (x + 6, 296), (x + 14, 316)]
        poly(d, pts, fill=(96 + abs(ang) // 4, 176 - abs(ang) // 4, 110, 255), outline=INK, width=2)
        d.line([(325 * SS, 344 * SS), ((x + 2) * SS, 304 * SS)], fill=(70, 140, 86, 255), width=2 * SS)
    round_mask(img, w, h, 28)
    save(finish(img, w, h), "room_rest")


def room_supply():
    w, h = 380, 400
    img, d = canvas(w, h)
    rr(d, (0, 0, w, h), 28, fill=(206, 222, 196, 255))
    for gy in range(0, h, 40):
        d.line([(0, gy * SS), (w * SS, gy * SS)], fill=(196, 214, 186, 255), width=2 * SS)
    for gx in range(0, w, 40):
        d.line([(gx * SS, 0), (gx * SS, h * SS)], fill=(196, 214, 186, 255), width=2 * SS)
    rr(d, (0, 0, w, h), 28, outline=(126, 150, 116, 255), width=6)
    rr(d, (80, 90, 300, 330), 22, fill=(238, 244, 232, 255))
    rr(d, (86, 96, 294, 324), 18, outline=(214, 226, 206, 255), width=2)
    for side, x0 in (("L", 14), ("R", 314)):
        drop_shadow(img, (x0, 60, x0 + 52, 380), 10, 5, 90, (4, 4))
        d = ImageDraw.Draw(img)
        rr(d, (x0, 60, x0 + 52, 380), 10, fill=(190, 150, 104, 255), outline=INK, width=4)
        rr(d, (x0 + 4, 64, x0 + 48, 376), 8, outline=(214, 176, 130, 255), width=2)
        for k in range(4):
            y = 74 + k * 76
            rr(d, (x0, y + 60, x0 + 52, y + 68), 2, fill=(160, 122, 80, 255), outline=INK, width=2)   # 선반 판
            if side == "L":
                col = [(255, 255, 255), (255, 214, 224), (205, 232, 255), (255, 238, 176)][k]
                for t in range(3):                                                                       # 접어 쌓은 수건
                    rr(d, (x0 + 6, y + 40 - t * 14, x0 + 46, y + 54 - t * 14), 5, fill=shade(col, 1 - t * 0.04) + (255,), outline=INK, width=2)
                    d.line([((x0 + 10) * SS, (y + 46 - t * 14) * SS), ((x0 + 42) * SS, (y + 46 - t * 14) * SS)], fill=shade(col, 0.88) + (255,), width=2 * SS)
            else:
                col = [(255, 168, 120), (136, 200, 255), (176, 230, 150), (240, 190, 240)][k]
                rr(d, (x0 + 8, y + 10, x0 + 28, y + 60), 8, fill=col + (255,), outline=INK, width=3)
                rr(d, (x0 + 12, y + 28, x0 + 24, y + 46), 3, fill=(255, 255, 255, 255), outline=INK, width=2)
                rr(d, (x0 + 12, y + 2, x0 + 24, y + 12), 3, fill=(140, 140, 150, 255), outline=INK, width=2)
                rr(d, (x0 + 30, y + 24, x0 + 46, y + 60), 6, fill=shade(col, 0.9) + (255,), outline=INK, width=3)
                rr(d, (x0 + 33, y + 18, x0 + 43, y + 26), 2, fill=(140, 140, 150, 255), outline=INK, width=2)
    round_mask(img, w, h, 28)
    save(finish(img, w, h), "room_supply")


def zone_wash():
    w, h = 480, 380
    img, d = canvas(w, h)
    rr(d, (0, 0, w, h), 28, fill=(206, 228, 250, 255))
    for gx in range(0, w, 60):
        d.line([(gx * SS, 0), (gx * SS, h * SS)], fill=(186, 212, 238, 255), width=3 * SS)
    for gy in range(0, h, 60):
        d.line([(0, gy * SS), (w * SS, gy * SS)], fill=(186, 212, 238, 255), width=3 * SS)
    for gx in range(0, w, 60):                       # 타일 반짝임
        for gy in range(0, h, 60):
            d.line([((gx + 8) * SS, (gy + 10) * SS), ((gx + 22) * SS, (gy + 6) * SS)], fill=(240, 248, 255, 255), width=3 * SS)
    rr(d, (0, 0, w, h), 28, outline=(120, 160, 205, 255), width=6)
    for cx in (120, 360):
        drop_shadow(img, (cx - 80, 218, cx + 80, 350), 30, 7, 100)
        d = ImageDraw.Draw(img)
        rr(d, (cx - 80, 218, cx + 80, 350), 30, fill=(250, 250, 252, 255), outline=INK, width=4)
        rr(d, (cx - 72, 226, cx + 72, 342), 24, outline=(228, 232, 240, 255), width=3)
        ell(d, (cx - 56, 236, cx + 56, 330), fill=(186, 220, 246, 255), outline=(110, 150, 190, 255), width=4)
        ell(d, (cx - 40, 248, cx + 20, 280), fill=(214, 238, 255, 255))
        ell(d, (cx - 6, 276, cx + 6, 288), fill=(120, 150, 180, 255), outline=INK, width=2)           # 배수구
        rr(d, (cx - 8, 190, cx + 8, 238), 6, fill=(170, 176, 190, 255), outline=INK, width=3)
        rr(d, (cx - 30, 180, cx + 8, 198), 8, fill=(170, 176, 190, 255), outline=INK, width=3)
        ell(d, (cx - 40, 168, cx - 20, 188), fill=(70, 130, 220, 255), outline=INK, width=3)          # 수도 손잡이
        ell(d, (cx - 32, 196, cx - 24, 208), fill=(110, 180, 245, 255))                                  # 물방울
        rr(d, (cx + 40, 210, cx + 66, 244), 6, fill=(240, 150, 200, 255), outline=INK, width=3)         # 샴푸병
        rr(d, (cx + 48, 202, cx + 58, 212), 3, fill=(250, 250, 250, 255), outline=INK, width=2)
    round_mask(img, w, h, 28)
    save(finish(img, w, h), "zone_wash")


def zone_seat():
    w, h = 480, 600
    img, d = canvas(w, h)
    rr(d, (0, 0, w, h), 28, fill=(252, 232, 208, 255))
    plank_floor(d, w, h, (252, 232, 208), (238, 212, 182), 50, 5)
    rr(d, (0, 0, w, h), 28, outline=(212, 168, 126, 255), width=6)
    for k in range(3):
        y = 96 + k * 150
        drop_shadow(img, (410, y, 468, y + 122), 12, 5, 90, (-3, 4))
        d = ImageDraw.Draw(img)
        rr(d, (410, y, 468, y + 122), 12, fill=(150, 112, 80, 255), outline=INK, width=3)             # 거울 프레임
        rr(d, (416, y + 6, 462, y + 116), 8, fill=(206, 230, 246, 255), outline=(120, 90, 66, 255), width=2)
        poly(d, [(420, y + 14), (440, y + 12), (424, y + 60)], fill=(240, 250, 255, 255))               # 반사광
        d.line([(432 * SS, (y + 70) * SS), (452 * SS, (y + 40) * SS)], fill=(255, 255, 255, 255), width=3 * SS)
        # 거울 앞 작업대: 빗 통과 드라이기
        rr(d, (370, y + 30, 408, y + 96), 8, fill=(226, 196, 160, 255), outline=INK, width=3)
        rr(d, (376, y + 38, 388, y + 56), 3, fill=(236, 120, 150, 255), outline=INK, width=2)
        rr(d, (392, y + 44, 402, y + 78), 4, fill=(120, 170, 230, 255), outline=INK, width=2)
    round_mask(img, w, h, 28)
    save(finish(img, w, h), "zone_seat")


def seat_mirror():
    w, h = 72, 200
    img, d = canvas(w, h)
    soft_layer(img, lambda dd: dd.ellipse([4 * SS, 6 * SS, 62 * SS, 176 * SS], fill=(255, 250, 225, 255)), 7, 170)   # 뒤에서 비추는 빛
    d = ImageDraw.Draw(img)
    ell(d, (10, 12, 56, 170), fill=(214, 234, 246, 255), outline=(236, 236, 240, 255), width=4)
    ell(d, (10, 12, 56, 170), outline=(150, 160, 172, 255), width=2)
    poly(d, [(20, 40), (34, 28), (22, 100)], fill=(244, 252, 255, 255))
    d.line([(36 * SS, 130 * SS), (46 * SS, 74 * SS)], fill=(255, 255, 255, 255), width=3 * SS)
    rr(d, (14, 178, 66, 192), 3, fill=(206, 170, 130, 255), outline=INK, width=3)                      # 나무 선반
    save(finish(img, w, h), "seat_mirror")


def seat_chair():
    w = h = 200
    img, d = canvas(w, h)
    drop_shadow_ell(img, (12, 12, 184, 184), 8, 110, (6, 8))
    d = ImageDraw.Draw(img)
    # 발받침 대(위에서 본 아래쪽)과 팔걸이
    rr(d, (64, 162, 136, 192), 12, fill=(168, 142, 120, 255), outline=INK, width=4)
    rr(d, (16, 62, 44, 150), 12, fill=(150, 150, 156, 255), outline=INK, width=4)                  # 왼쪽 팔걸이
    rr(d, (156, 62, 184, 150), 12, fill=(150, 150, 156, 255), outline=INK, width=4)                # 오른쪽 팔걸이
    # 좌석 방석
    ell(d, (28, 36, 172, 180), fill=(172, 146, 124, 255), outline=INK, width=4)
    ell(d, (42, 50, 158, 166), fill=(204, 178, 154, 255), outline=(160, 134, 112, 255), width=3)
    ell(d, (58, 64, 142, 150), outline=(184, 158, 136, 255), width=2)
    for k in range(8):                                                                               # 박음질 점
        ang = math.radians(k * 45 + 22)
        d.line([((100 + 56 * math.cos(ang)) * SS, (107 + 56 * math.sin(ang)) * SS), ((100 + 52 * math.cos(ang)) * SS, (107 + 52 * math.sin(ang)) * SS)], fill=(150, 124, 102, 255), width=2 * SS)
    # 등받이와 머리받침
    rr(d, (36, 6, 164, 54), 24, fill=(168, 142, 120, 255), outline=INK, width=4)
    rr(d, (46, 12, 154, 44), 18, fill=(196, 170, 148, 255), outline=(120, 100, 86, 255), width=2)
    rr(d, (70, 0, 130, 22), 10, fill=(120, 100, 86, 255), outline=INK, width=4)                  # 머리받침
    save(finish(img, w, h), "seat_chair")


# ---------------------------------------------------------------- 캐릭터

def draw_hair_back(d, style, color, ink):
    c = color + (255,)
    if style == "long":
        rr(d, (122, 90, 390, 430), 90, fill=c, outline=ink, width=5)
    elif style == "bob":
        rr(d, (112, 90, 400, 330), 100, fill=c, outline=ink, width=5)
    elif style == "bun":
        ell(d, (206, 22, 306, 112), fill=c, outline=ink, width=5)
    elif style == "twin":
        ell(d, (60, 190, 170, 330), fill=c, outline=ink, width=5)
        ell(d, (342, 190, 452, 330), fill=c, outline=ink, width=5)


def draw_hair_front(d, style, color, ink):
    c = color + (255,)
    hi = shade(color, 1.18) + (255,)
    if style == "curly":
        for (cx, cy, r) in [(150, 150, 70), (210, 110, 74), (290, 104, 76), (362, 150, 70), (256, 150, 90), (120, 215, 56), (392, 215, 56)]:
            ell(d, (cx - r, cy - r, cx + r, cy + r), fill=c, outline=ink, width=5)
    elif style == "spiky":
        pts = [(116, 250), (122, 186), (146, 150), (140, 92), (184, 134), (206, 66), (246, 126), (288, 58), (318, 128), (366, 92), (366, 150), (390, 188), (396, 250), (372, 226), (336, 204), (292, 214), (256, 198), (216, 214), (176, 204), (140, 226)]
        poly(d, pts, fill=c, outline=ink, width=5)
    else:
        # 기본 앞머리: 머리 윗부분을 덮는다
        ell(d, (112, 74, 400, 330), fill=c, outline=ink, width=5)
        # 이마 쪽은 얼굴색으로 다시 파낸다(앞머리 모양)
    # 하이라이트
    if style not in ("spiky", "curly"):
        ell(d, (170, 90, 260, 126), fill=hi)


def soft_layer(img, draw_fn, blur, alpha=255, clip=None):
    """흐릿하게 번지는 그림자·볼터치 층을 만들어 img 위에 합친다. clip(Image L 마스크)로 모양 안쪽만 남긴다."""
    from PIL import ImageChops
    lay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(lay))
    lay = lay.filter(ImageFilter.GaussianBlur(blur * SS))
    a = lay.getchannel("A").point(lambda v: v * alpha // 255)
    if clip is not None:
        a = ImageChops.multiply(a, clip)
    lay.putalpha(a)
    img.alpha_composite(lay)


def lighting(img, dark=0.80, bright=1.06):
    """왼쪽 위에서 빛이 오는 느낌: 오른쪽 아래로 갈수록 어둡게, 왼쪽 위는 살짝 밝게 (색에만 곱함)."""
    import numpy as np
    arr = np.asarray(img).astype("float32")
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    t = (xx / w * 0.45 + yy / h * 0.55)            # 0(왼쪽 위) ~ 1(오른쪽 아래)
    m = bright + (dark - bright) * np.clip((t - 0.15) / 0.8, 0, 1)
    arr[..., :3] = np.clip(arr[..., :3] * m[..., None], 0, 255)
    return Image.fromarray(arr.astype("uint8"), "RGBA")


# ---------------------------------------------------------------- 남자 헤어스타일 (참고: 남자 헤어스타일 종류 가이드)

def smooth(pts, n=10):
    """점들을 지나는 부드러운 곡선(Catmull-Rom)."""
    out = []
    P = [pts[0]] + list(pts) + [pts[-1]]
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            out.append((x, y))
    out.append(pts[-1])
    return out


# 키: (앞머리 종류, 윗머리 볼륨, 옆머리 짧게(투블럭), 웨이브 정도, 이름)
MALE_STYLES = [
    ("part",   30, False, 1, "가르마 펌"),
    ("up",     54, False, 1, "리젠트 펌"),
    ("comma",  42, False, 1, "볼륨 쉼표펌"),
    ("swept",  30, True,  0, "투블럭 댄디컷"),
    ("see",    26, False, 1, "쉐도우 펌"),
    ("center", 32, False, 1, "애즈 펌"),
    ("swept",  20, False, 0, "댄디컷"),
    ("part",   24, False, 0, "내추럴 가르마"),
    ("part",   12, False, 0, "가르마 다운펌"),
    ("swept",  18, False, 0, "내추럴 댄디컷"),
    ("crop",   14, False, 0, "크롭컷"),
    ("bangs",  22, False, 0, "앞머리 내린 스타일"),
    ("swept",  32, False, 1, "댄디 펌"),
    ("center", 26, False, 0, "애즈 가르마"),
    ("swept",  22, True,  0, "소프트 투블럭"),
    ("comma",  28, False, 0, "내추럴 쉼표펌"),
    ("crop",   10, True,  0, "아이비 컷"),
    ("swept",  46, False, 1, "볼륨 댄디펌"),
    ("part",   30, True,  0, "투블럭 가르마"),
    ("part",   20, False, 0, "내추럴 가르마 2"),
    ("swept",  20, True,  0, "댄디 투블럭"),
    ("layer",  28, False, 0, "내추럴 레이어"),
    ("part",   36, False, 1, "가르마 펌 2"),
    ("see",    18, False, 0, "시스루 댄디컷"),
]


def male_hair(img, key, color, ink):
    """둥근 얼굴(512 좌표) 위에 올리는 짧은 남자 머리. 앞머리 종류마다 이마 선과 결을 다르게 그린다."""
    from PIL import ImageChops
    kind, vol, under, wave, _ = MALE_STYLES[key]
    d = ImageDraw.Draw(img)
    hc = color + (255,)
    dark = shade(color, 0.72) + (255,)
    light = shade(color, 1.3) + (255,)
    hl = {"up": 168, "part": 194, "swept": 196, "comma": 200, "bangs": 234, "crop": 206, "see": 214, "center": 196, "layer": 200}[kind]
    tx = 118 if not under else 142       # 관자놀이 위치(투블럭은 윗머리가 좁다)
    ty = 262 if not under else 214
    L_, R_ = tx, 512 - tx
    vol = int(vol * 1.4)
    top = 112 - vol
    # 바깥 윤곽 (왼쪽 관자놀이 -> 정수리 -> 오른쪽 관자놀이)
    outer = smooth([(L_, ty), (L_ + 2, 190), (166, 132 - vol * 0.5), (256, top), (346, 132 - vol * 0.5), (R_ - 2, 190), (R_, ty)], 10)
    # 이마 선 (오른쪽 -> 왼쪽)
    if kind == "part":
        inner = smooth([(R_, ty), (370, 226), (326, hl + 8), (262, hl - 4), (212, hl - 34), (186, hl + 2), (150, hl + 14), (L_, ty)], 8)
    elif kind == "swept":
        inner = smooth([(R_, ty), (372, 228), (330, hl + 8), (260, hl - 6), (190, hl - 26), (150, hl + 10), (L_, ty)], 8)
    elif kind == "up":
        inner = smooth([(R_, ty), (368, 216), (326, hl - 2), (256, hl - 16), (186, hl - 2), (144, 216), (L_, ty)], 8)
    elif kind == "comma":
        inner = smooth([(R_, ty), (372, 228), (330, hl + 6), (262, hl - 4), (196, hl - 20), (150, hl + 8), (L_, ty)], 8)
    elif kind == "bangs":
        inner = smooth([(R_, ty), (376, 250), (344, hl - 4), (300, hl + 8), (256, hl - 2), (212, hl + 8), (168, hl - 4), (138, 250), (L_, ty)], 8)
    elif kind == "crop":
        inner = smooth([(R_, ty), (374, 232), (336, hl), (296, hl + 6), (256, hl - 2), (216, hl + 6), (176, hl), (138, 232), (L_, ty)], 8)
    elif kind == "see":
        inner = smooth([(R_, ty), (372, 226), (326, hl - 30), (256, hl - 40), (186, hl - 30), (140, 226), (L_, ty)], 8)
    elif kind == "center":
        inner = smooth([(R_, ty), (372, 232), (332, hl + 18), (288, hl - 2), (256, hl - 36), (224, hl - 2), (180, hl + 18), (140, 232), (L_, ty)], 8)
    else:  # layer
        inner = smooth([(R_, ty), (372, 232), (330, hl + 14), (270, hl - 4), (206, hl - 22), (160, hl + 12), (L_, ty)], 8)
    cap = outer + inner

    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)

    def P(pts):
        return [(x * SS, y * SS) for x, y in pts]

    def ribbon(center, w0, taper=0.7, tip_hook=0):
        """중심선을 따라 끝으로 갈수록 가늘어지는 머리 한 뭉치(리본) 모양."""
        c = smooth(center, 8)
        n = len(c)
        left, right = [], []
        for i, (x, y) in enumerate(c):
            j0, j1 = max(0, i - 1), min(n - 1, i + 1)
            dx, dy = c[j1][0] - c[j0][0], c[j1][1] - c[j0][1]
            ln = math.hypot(dx, dy) or 1
            nx, ny = -dy / ln, dx / ln
            t = i / (n - 1)
            w = w0 * (1 - t) ** taper + 0.6
            left.append((x + nx * w, y + ny * w))
            right.append((x - nx * w, y - ny * w))
        return left + right[::-1], c

    stub_done = False
    if under:
        # 옆머리를 짧게 민 부분(투블럭): 귀 위쪽 어두운 면 (얼굴 윤곽 안쪽만)
        stub = Image.new("RGBA", img.size, (0, 0, 0, 0))
        sd = ImageDraw.Draw(stub)
        for side in (-1, 1):
            cx = 256 + side * 140
            sd.polygon(P([(cx - side * 30, 170), (cx + side * 30, 190), (cx + side * 30, 262), (cx - side * 18, 250)]), fill=shade(color, 0.9)[:3] + (150,))
            for k in range(6):
                xx = cx - side * 22 + side * k * 8
                sd.line(P([(xx, 196), (xx, 246)]), fill=dark, width=2 * SS)
        hm = Image.new("L", img.size, 0)
        ImageDraw.Draw(hm).ellipse([112 * SS, 120 * SS, 400 * SS, 380 * SS], fill=255)
        stub.putalpha(ImageChops.multiply(stub.getchannel("A"), hm))
        layer.alpha_composite(stub)
        ld = ImageDraw.Draw(layer)

    # 1) 바탕: 가장 어두운 톤으로 전체 모양
    ld.polygon(P(cap), fill=shade(color, 0.78) + (255,))

    # 2) 머리 뭉치(락)들의 흐름을 스타일마다 다르게 정한다
    locks = []   # (중심선, 굵기, 톤배율)
    rnd = random.Random(key * 7 + 3)
    def tone():
        return rnd.choice([0.92, 1.0, 1.08, 1.16])
    px = 214
    if kind == "part":
        for k in range(10):                      # 큰 쪽(오른쪽) 뭉치
            u = k / 9
            locks.append(([(px + 4 + u * 30, top + 10 + u * 18), (px + 40 + u * 110, 150 - vol * 0.2 + u * 22), (px + 60 + u * 120, hl + 6 + u * 36)], 15, tone()))
        for k in range(5):                       # 작은 쪽(왼쪽)
            u = k / 4
            locks.append(([(px - 4 - u * 30, top + 14 + u * 10), (px - 40 - u * 60, 156 + u * 10), (px - 56 - u * 74, hl + 10 + u * 34)], 13, tone()))
    elif kind == "swept":
        for k in range(12):
            u = k / 11
            locks.append(([(176 + u * 60, top + 8 + u * 10), (190 + u * 120, 146 + u * 20), (176 + u * 190, hl + 4 + u * 34)], 14, tone()))
    elif kind == "up":
        for k in range(11):
            u = k / 10
            xr = 150 + u * 212
            locks.append(([(xr, hl + 2), (256 + (xr - 256) * 0.86, (hl + top) / 2 + 8), (256 + (xr - 256) * 0.55, top + 4 + abs(u - 0.5) * 22)], 17, tone()))
    elif kind == "comma":
        for k in range(10):
            u = k / 9
            locks.append(([(168 + u * 70, top + 8 + u * 12), (200 + u * 120, 146 + u * 18), (184 + u * 180, hl + 4 + u * 24)], 14, tone()))
    elif kind == "center":
        for side in (-1, 1):
            for k in range(7):
                u = k / 6
                locks.append(([(256 + side * (6 + u * 26), top + 8 + u * 10), (256 + side * (50 + u * 80), 150 + u * 10), (256 + side * (70 + u * 100), hl + 8 + u * 36)], 14, tone()))
    elif kind == "bangs":
        for k in range(12):
            u = k / 11
            xr = 160 + u * 192
            locks.append(([(xr + (u - 0.5) * 20, top + 12), (xr + (u - 0.5) * 10, 160), (xr + (u - 0.5) * 8, hl + 4 + abs(u - 0.5) * 6)], 14, tone()))
    elif kind == "crop":
        for k in range(14):
            u = k / 13
            xr = 156 + u * 200
            locks.append(([(xr, top + 14), (xr + (u - 0.5) * 6, 170), (xr + (u - 0.5) * 4, hl + 2)], 12, tone()))
    elif kind == "see":
        for k in range(8):
            u = k / 7
            xr = 170 + u * 172
            locks.append(([(xr, top + 14), (xr + (u - 0.5) * 14, 150), (xr + (u - 0.5) * 20, hl + 14 + (k % 2) * 16)], 11, tone()))
    else:  # layer
        for k in range(10):
            u = k / 9
            locks.append(([(176 + u * 80, top + 10), (194 + u * 110, 150), (170 + u * 190, hl + 8 + u * 28)], 14, tone()))
        for side in (-1, 1):
            for k in range(3):
                locks.append(([(256 + side * (110 + k * 8), 180), (256 + side * (124 + k * 8), 214), (256 + side * (126 + k * 10), 250 + k * 6)], 10, tone()))

    # 3) 뭉치를 겹쳐서 그린다: 색을 번갈아 쓰고, 가장자리와 가운데 결선을 그린다
    clip = Image.new("L", img.size, 0)
    ImageDraw.Draw(clip).polygon(P(cap), fill=255)
    lockl = Image.new("RGBA", img.size, (0, 0, 0, 0))
    lk = ImageDraw.Draw(lockl)
    shine = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sh = ImageDraw.Draw(shine)
    for (cl, w0, tm) in locks:
        poly_, c = ribbon(cl, w0, 0.75)
        lk.polygon(P(poly_), fill=shade(color, tm) + (255,))
        lk.line(P(poly_ + [poly_[0]]), fill=shade(color, 0.6) + (255,), width=2 * SS, joint="curve")
        # 가운데 결선
        lk.line(P(c[: int(len(c) * 0.8)]), fill=shade(color, 0.7) + (255,), width=2 * SS, joint="curve")
        # 윤기: 위쪽 1/3 구간에 밝은 선
        seg = c[int(len(c) * 0.12): int(len(c) * 0.5)]
        if len(seg) > 2:
            sh.line(P(seg), fill=shade(color, 2.0) + (255,), width=4 * SS, joint="curve")
    # 바탕 모양 밖으로 나간 부분은 잘라낸다
    lockl.putalpha(ImageChops.multiply(lockl.getchannel("A"), clip))
    layer.alpha_composite(lockl)
    shine = shine.filter(ImageFilter.GaussianBlur(1.5 * SS))
    shine.putalpha(ImageChops.multiply(shine.getchannel("A").point(lambda v: v * 3 // 5), clip))
    layer.alpha_composite(shine)
    ld = ImageDraw.Draw(layer)

    # 가르마 선
    if kind == "part":
        ld.line(P(smooth([(px, hl - 34), (px + 2, 150), (px + 8, top + 6)], 8)), fill=ink, width=4 * SS)
    if kind == "center":
        ld.line(P(smooth([(256, hl - 36), (256, 150), (256, top + 4)], 8)), fill=ink, width=4 * SS)
    # 윤곽선
    ld.line(P(outer), fill=ink, width=5 * SS, joint="curve")
    ld.line(P(inner), fill=ink, width=4 * SS, joint="curve")
    img.alpha_composite(layer)


# ---------------------------------------------------------------- 여자 헤어스타일 (참고: 여자 얼굴형별 어울리는 헤어스타일)

def ribbon_poly(center, w0, taper=0.7):
    c = smooth(center, 8)
    n = len(c)
    left, right = [], []
    for i, (x, y) in enumerate(c):
        j0, j1 = max(0, i - 1), min(n - 1, i + 1)
        dx, dy = c[j1][0] - c[j0][0], c[j1][1] - c[j0][1]
        ln = math.hypot(dx, dy) or 1
        nx, ny = -dy / ln, dx / ln
        t = i / (n - 1)
        w = w0 * (1 - t) ** taper + 0.8
        left.append((x + nx * w, y + ny * w))
        right.append((x - nx * w, y - ny * w))
    return left + right[::-1], c


# (이름, 길이, 앞머리, 웨이브(0 직모/1 C컬/2 S웨이브), 볼륨)
FEMALE_STYLES = [
    ("레이어드컷",     "long", "curtain", 1, 0),
    ("긴 웨이브",      "long", "see",     2, 0),
    ("롱 허쉬컷",      "long", "curtain", 1, 6),
    ("단발",           "bob",  "full",    0, 0),
    ("긴머리",         "long", "none",    0, 0),
    ("C컬 중단발",     "mid",  "curtain", 1, 0),
    ("S컬 중단발",     "mid",  "see",     2, 0),
    ("턱선 단발",      "bob",  "see",     1, 0),
    ("레이어드 단발",  "bob",  "curtain", 1, 6),
    ("빌드펌",         "mid",  "full",    2, 10),
    ("굵은 웨이브",    "long", "curtain", 2, 8),
    ("볼륨펌",         "mid",  "side",    2, 14),
    ("앞머리 중단발",  "mid",  "full",    0, 0),
    ("C컬 중단발 2",   "mid",  "see",     1, 0),
    ("레이어드 롱",    "long", "side",    1, 4),
    ("볼륨 보브",      "bob",  "none",    1, 14),
    ("히피펌",         "long", "curtain", 2, 12),
    ("사이드 단발",    "bob",  "side",    0, 0),
    ("레이어드 스타일","mid",  "curtain", 1, 6),
    ("시스루 롱",      "long", "see",     0, 0),
]
F_LEN = {"bob": 342, "mid": 410, "long": 492}


def female_back(img, key, color, ink):
    """머리 뒤쪽 덩어리(몸 뒤에 깔림)."""
    name, length, bangs, wave, vol = FEMALE_STYLES[key]
    d = ImageDraw.Draw(img)
    Lb = F_LEN[length]
    flare = {"bob": 8, "mid": 20, "long": 30}[length] + vol
    amp = [0, 8, 16][wave]
    pts_l = [(112 - vol * 0.4, 200), (98 - vol * 0.7, 270), (92 - flare, (270 + Lb) / 2 + amp), (96 - flare + amp, Lb - 6)]
    pts_r = [(512 - x, y) for x, y in pts_l]
    left = smooth(pts_l, 8)
    right = smooth(pts_r, 8)
    bottom = [(left[-1][0] + (right[-1][0] - left[-1][0]) * t / 10, Lb + (6 if wave and (t % 3 == 0) else 0) + (amp * 0.5 if t % 2 else 0)) for t in range(0, 11)]
    poly_pts = left + bottom + right[::-1]
    sc = (color + (255,))
    d.polygon([(x * SS, y * SS) for x, y in poly_pts], fill=shade(color, 0.82) + (255,))
    # 결
    from PIL import ImageChops
    lay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(lay)
    rnd = random.Random(key * 11 + 5)
    for k in range(16):
        x0 = 120 + k * 17
        wob = rnd.uniform(-6, 6)
        ld.line([(x0 * SS, 200 * SS), ((x0 + wob + (x0 - 256) * 0.1) * SS, ((200 + Lb) / 2) * SS), ((x0 + wob + (x0 - 256) * 0.16) * SS, (Lb - 8) * SS)],
                fill=shade(color, rnd.choice([0.6, 0.7, 1.15])) + (200,), width=3 * SS, joint="curve")
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in poly_pts], fill=255)
    lay.putalpha(ImageChops.multiply(lay.getchannel("A"), m))
    img.alpha_composite(lay)
    d = ImageDraw.Draw(img)
    d.line([(x * SS, y * SS) for x, y in left + bottom + right[::-1]], fill=ink, width=5 * SS, joint="curve")


def female_front(img, key, color, ink):
    """정수리·앞머리와 얼굴 옆으로 내려오는 머리(몸 앞에 그림)."""
    from PIL import ImageChops
    name, length, bangs, wave, vol = FEMALE_STYLES[key]
    d = ImageDraw.Draw(img)
    rnd = random.Random(key * 13 + 1)
    top = 108 - vol
    ty = 262
    L_, R_ = 112, 400
    outer = smooth([(L_, ty), (L_ + 2, 190), (166, 128 - vol * 0.4), (256, top), (346, 128 - vol * 0.4), (R_ - 2, 190), (R_, ty)], 10)
    hl = {"full": 236, "see": 222, "curtain": 176, "side": 196, "none": 172}[bangs]
    if bangs == "full":
        inner = smooth([(R_, ty), (376, 250), (344, hl - 2), (300, hl + 8), (256, hl), (212, hl + 8), (168, hl - 2), (136, 250), (L_, ty)], 8)
    elif bangs == "see":
        inner = smooth([(R_, ty), (374, 232), (330, hl - 26), (256, hl - 34), (182, hl - 26), (138, 232), (L_, ty)], 8)
    elif bangs == "curtain":
        inner = smooth([(R_, ty), (376, 240), (338, 214), (296, 188), (256, hl - 4), (216, 188), (174, 214), (136, 240), (L_, ty)], 8)
    elif bangs == "side":
        inner = smooth([(R_, ty), (376, 238), (336, 214), (262, 196), (206, hl - 22), (160, 214), (L_, ty)], 8)
    else:
        inner = smooth([(R_, ty), (376, 232), (336, 190), (276, hl - 6), (240, hl - 4), (180, 190), (138, 232), (L_, ty)], 8)
    cap = outer + inner
    P = lambda pts: [(x * SS, y * SS) for x, y in pts]
    lay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(lay)
    ld.polygon(P(cap), fill=shade(color, 0.78) + (255,))
    locks = []
    tone = lambda: rnd.choice([0.92, 1.0, 1.08, 1.18])
    # 정수리에서 앞쪽으로 흐르는 결
    if bangs == "curtain":
        for side in (-1, 1):
            for k in range(8):
                u = k / 7
                locks.append(([(256 + side * (4 + u * 24), top + 10 + u * 8), (256 + side * (60 + u * 70), 150 + u * 20), (256 + side * (86 + u * 56), 262 + u * 26)], 16, tone()))
    elif bangs == "see":
        for k in range(10):
            u = k / 9
            xr = 164 + u * 184
            locks.append(([(xr, top + 14), (xr + (u - 0.5) * 12, 150), (xr + (u - 0.5) * 18, hl + 4 + (k % 2) * 14)], 12, tone()))
    elif bangs == "full":
        for k in range(12):
            u = k / 11
            xr = 150 + u * 212
            locks.append(([(xr, top + 12), (xr + (u - 0.5) * 10, 160), (xr + (u - 0.5) * 8, hl + 4 + abs(u - 0.5) * 6)], 14, tone()))
    elif bangs == "side":
        for k in range(12):
            u = k / 11
            locks.append(([(196 + u * 40, top + 10 + u * 8), (210 + u * 110, 150 + u * 20), (200 + u * 160, hl + 10 + u * 50)], 15, tone()))
    else:
        for side in (-1, 1):
            for k in range(8):
                u = k / 7
                locks.append(([(250 + side * (4 + u * 20), top + 10), (256 + side * (60 + u * 70), 140 + u * 14), (256 + side * (110 + u * 36), 190 + u * 60)], 15, tone()))
    clip = Image.new("L", img.size, 0)
    ImageDraw.Draw(clip).polygon(P(cap), fill=255)
    if bangs in ("curtain", "side", "none"):
        # 앞머리가 이마·볼 위로 내려오므로 얼굴 윤곽 안쪽까지 허용
        ImageDraw.Draw(clip).ellipse([112 * SS, 120 * SS, 400 * SS, 380 * SS], fill=255)
    lockl = Image.new("RGBA", img.size, (0, 0, 0, 0))
    lk = ImageDraw.Draw(lockl)
    shine = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sh = ImageDraw.Draw(shine)
    for (cl, w0, tm) in locks:
        pl, c = ribbon_poly(cl, w0, 0.75)
        lk.polygon(P(pl), fill=shade(color, tm) + (255,))
        lk.line(P(pl + [pl[0]]), fill=shade(color, 0.6) + (255,), width=2 * SS, joint="curve")
        lk.line(P(c[: int(len(c) * 0.8)]), fill=shade(color, 0.7) + (255,), width=2 * SS, joint="curve")
        seg = c[int(len(c) * 0.12): int(len(c) * 0.5)]
        if len(seg) > 2:
            sh.line(P(seg), fill=shade(color, 2.0) + (255,), width=4 * SS, joint="curve")
    lockl.putalpha(ImageChops.multiply(lockl.getchannel("A"), clip))
    lay.alpha_composite(lockl)
    shine = shine.filter(ImageFilter.GaussianBlur(1.5 * SS))
    shine.putalpha(ImageChops.multiply(shine.getchannel("A").point(lambda v: v * 3 // 5), clip))
    lay.alpha_composite(shine)
    ld = ImageDraw.Draw(lay)
    ld.line(P(outer), fill=ink, width=5 * SS, joint="curve")
    ld.line(P(inner), fill=ink, width=4 * SS, joint="curve")
    img.alpha_composite(lay)

    # 얼굴 옆으로 내려오는 앞쪽 머리 (어깨 위로)
    Lb = F_LEN[length]
    amp = [0, 10, 20][wave]
    end_y = {"bob": 336, "mid": 396, "long": 470}[length]
    side_lay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sl = ImageDraw.Draw(side_lay)
    for side in (-1, 1):
        for k in range(5):
            x_top = 256 + side * (146 - k * 4)
            x_mid = 256 + side * (160 + k * 7 + amp * (0.5 if k % 2 else -0.5))
            if wave == 1:
                x_end = 256 + side * (150 + k * 6 - 26 - (k % 2) * 8)    # C컬: 끝이 안쪽으로 말림
            elif wave == 2:
                x_end = 256 + side * (172 + k * 6 + (14 if k % 2 else -8))
            else:
                x_end = 256 + side * (160 + k * 8)
            cl = [(x_top, 226 + k * 4), (x_mid, (226 + end_y) / 2), (x_end, end_y - 4 + k * 3)]
            if wave == 2:
                cl = [(x_top, 226 + k * 4), (x_mid + 12 * side, 226 + (end_y - 226) * 0.35), (x_mid - 10 * side, 226 + (end_y - 226) * 0.68), (x_end, end_y - 4 + k * 3)]
            pl, c = ribbon_poly(cl, 16 - k, 0.45)
            sl.polygon(P(pl), fill=shade(color, [0.95, 1.05, 0.9, 1.12, 1.0][k]) + (255,))
            sl.line(P(pl + [pl[0]]), fill=shade(color, 0.6) + (255,), width=2 * SS, joint="curve")
            sl.line(P(c[: int(len(c) * 0.85)]), fill=shade(color, 0.7) + (255,), width=2 * SS, joint="curve")
    img.alpha_composite(side_lay)
    d = ImageDraw.Draw(img)


def character(name, skin, hair_color, style, outfit, apron=None, glasses=False, band=None,
              blush=True, mouth="smile", ink=INK, uniform_overlay=False, brow="normal", tool=None,
              tag=None, towel=False, lashes=False, ribbon=None, freckles=False):
    from PIL import ImageChops
    img = Image.new("RGBA", (512 * SS, 512 * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def R(box, r, **kw):
        x0, y0, x1, y1 = [v * SS for v in box]
        d.rounded_rectangle([x0, y0, x1, y1], radius=r * SS, fill=kw.get("fill"), outline=kw.get("outline"),
                            width=int(kw.get("width", 0) * SS))

    def E(box, **kw):
        d.ellipse([v * SS for v in box], fill=kw.get("fill"), outline=kw.get("outline"), width=int(kw.get("width", 0) * SS))

    def L(pts, color, width):
        d.line([(x * SS, y * SS) for x, y in pts], fill=color, width=int(width * SS), joint="curve")

    skin_c = skin + (255,)
    skin_dark = shade(skin, 0.88) + (255,)

    if uniform_overlay:
        # 유니폼 색을 입히는 층: 흰색으로 어깨·팔·앞치마만 그린다 (게임에서 색을 곱함)
        for box in ((84, 366, 142, 480), (370, 366, 428, 480)):
            R(box, 28, fill=(255, 255, 255, 255), outline=ink, width=5)
        R((120, 350, 392, 506), 70, fill=(255, 255, 255, 255), outline=ink, width=5)
        R((176, 392, 336, 506), 34, fill=(236, 236, 236, 255), outline=ink, width=5)
        R((222, 360, 290, 404), 14, fill=(236, 236, 236, 255), outline=ink, width=4)
        R((200, 456, 312, 500), 12, fill=(220, 220, 220, 255), outline=ink, width=4)
        save(lighting(img.resize((512, 512), Image.LANCZOS)), name)
        return

    # 그림자
    E((100, 456, 412, 508), fill=(90, 80, 80, 70))
    # 뒷머리
    if isinstance(style, str) and style.startswith("f:"):
        female_back(img, int(style[2:]), hair_color, ink)
        d = ImageDraw.Draw(img)
    else:
        draw_hair_back(d, style, hair_color, ink)
    # 팔 (소매)과 손
    out_c = outfit + (255,)
    for (x0, x1) in ((84, 142), (370, 428)):
        R((x0, 366, x1, 480), 28, fill=out_c, outline=ink, width=5)
        E((x0 + 2, 456, x1 - 2, 508), fill=skin_c, outline=ink, width=5)
    # 몸통
    R((120, 350, 392, 506), 70, fill=out_c, outline=ink, width=5)
    # 옷 주름
    L([(150, 430), (168, 470)], shade(outfit, 0.86) + (255,), 5)
    L([(362, 430), (344, 470)], shade(outfit, 0.86) + (255,), 5)
    # 목과 목 그림자
    R((226, 318, 286, 372), 20, fill=skin_c, outline=ink, width=5)
    R((228, 352, 284, 372), 10, fill=skin_dark)
    if apron:
        R((176, 392, 336, 506), 34, fill=apron + (255,), outline=ink, width=5)
        R((222, 360, 290, 404), 14, fill=apron + (255,), outline=ink, width=4)
        # 앞치마 끈
        L([(190, 400), (130, 372)], ink, 5)
        L([(322, 400), (382, 372)], ink, 5)
        # 주머니와 빗·가위가 삐죽 나온 모습
        R((200, 456, 312, 500), 12, fill=shade(apron, 0.93) + (255,), outline=ink, width=4)
        R((222, 436, 236, 464), 5, fill=(236, 120, 150, 255), outline=ink, width=3)       # 빗
        R((254, 430, 266, 464), 5, fill=(214, 220, 232, 255), outline=ink, width=3)       # 가위 손잡이
        R((276, 440, 288, 464), 5, fill=(120, 170, 230, 255), outline=ink, width=3)       # 분무기 끝
        if tag:
            R((330, 396, 372, 424), 8, fill=tag + (255,), outline=ink, width=3)
            E((344, 404, 358, 416), fill=(255, 255, 255, 200))
    else:
        # 옷깃

        d.polygon([(x * SS, y * SS) for x, y in [(222, 352), (256, 398), (290, 352), (270, 350), (256, 372), (242, 350)]], fill=shade(outfit, 1.1) + (255,))
        L([(222, 352), (256, 398), (290, 352)], ink, 4)
    if towel:
        R((124, 336, 270, 392), 24, fill=(250, 250, 252, 255), outline=ink, width=5)
        for xx in (160, 196, 232):
            L([(xx, 348), (xx, 382)], (200, 214, 232, 255), 5)
    if tool == "scissors":
        # 오른손에 든 가위
        L([(398, 470), (404, 400)], (214, 220, 232, 255), 14)
        L([(398, 470), (404, 400)], ink, 3)
        E((386, 486, 416, 516), outline=(232, 100, 128, 255), width=8)

    # 머리
    E((112, 120, 400, 380), fill=skin_c, outline=ink, width=5)
    head_mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(head_mask).ellipse([112 * SS, 120 * SS, 400 * SS, 380 * SS], fill=255)
    # 머리카락 덮개
    if isinstance(style, str) and style.startswith("f:"):
        female_front(img, int(style[2:]), hair_color, ink)
        d = ImageDraw.Draw(img)
    elif isinstance(style, str) and style.startswith("m:"):
        male_hair(img, int(style[2:]), hair_color, ink)
        d = ImageDraw.Draw(img)
    elif style in ("short", "long", "bob", "bun", "twin"):
        d.pieslice([108 * SS, 84 * SS, 404 * SS, 380 * SS], 180, 360, fill=hair_color + (255,), outline=ink, width=5 * SS)
        # 머리결 줄 (덮개 모양으로 잘라서 붙임)
        strands = Image.new("RGBA", img.size, (0, 0, 0, 0))
        sd = ImageDraw.Draw(strands)
        dark = shade(hair_color, 0.78) + (255,)
        for k in range(7):
            x0 = 200 + k * 18
            sd.line([(x0 * SS, 96 * SS), ((120 + k * 44) * SS, 236 * SS)], fill=dark, width=4 * SS)
        mask = Image.new("L", img.size, 0)
        ImageDraw.Draw(mask).pieslice([112 * SS, 88 * SS, 400 * SS, 376 * SS], 180, 360, fill=255)
        strands.putalpha(ImageChops.multiply(strands.getchannel("A"), mask))
        img.alpha_composite(strands)
        d = ImageDraw.Draw(img)
        pts = [(116, 232), (150, 168), (206, 224), (252, 160), (316, 226), (366, 168), (396, 232)]
        d.polygon([(x * SS, y * SS) for x, y in pts] + [(396 * SS, 232 * SS), (116 * SS, 232 * SS)], fill=hair_color + (255,))
        d.line([(x * SS, y * SS) for x, y in pts], fill=ink, width=4 * SS, joint="curve")
        # 윤기
        soft_layer(img, lambda dd: dd.arc([160 * SS, 92 * SS, 352 * SS, 170 * SS], 200, 340, fill=shade(hair_color, 1.9) + (255,), width=14 * SS), 4, 200)
        d = ImageDraw.Draw(img)
    elif style == "curly":
        draw_hair_front(d, style, hair_color, ink)
        E((148, 190, 364, 380), fill=skin_c)
        E((148, 190, 364, 380), outline=ink, width=5)
        for (cx, cy, r) in [(190, 200, 36), (256, 188, 40), (322, 200, 36)]:
            E((cx - r, cy - r, cx + r, cy + r), fill=hair_color + (255,), outline=ink, width=4)
        soft_layer(img, lambda dd: dd.arc([200 * SS, 96 * SS, 320 * SS, 170 * SS], 200, 340, fill=shade(hair_color, 1.9) + (255,), width=12 * SS), 4, 180)
        d = ImageDraw.Draw(img)
    elif style == "spiky":
        draw_hair_front(d, style, hair_color, ink)
        L([(205, 72), (214, 140)], shade(hair_color, 1.3) + (255,), 6)
        L([(300, 64), (292, 140)], shade(hair_color, 1.3) + (255,), 6)
    if band:
        R((114, 196, 398, 226), 12, fill=band + (255,), outline=ink, width=4)
        E((226, 190, 262, 232), fill=shade(band, 1.1) + (255,), outline=ink, width=4)
    if ribbon:
        for (x0, x1) in ((96, 156), (356, 416)):
            E((x0, 176, x1, 226), fill=ribbon + (255,), outline=ink, width=4)
            E((x0 + 18, 190, x1 - 18, 212), fill=shade(ribbon, 0.8) + (255,), outline=ink, width=3)
    # 귀
    for (x0, x1) in ((98, 134), (378, 414)):
        E((x0, 236, x1, 294), fill=skin_c, outline=ink, width=4)
    # 눈: 동글동글한 검은 눈 + 하이라이트
    for cx in (200, 312):
        E((cx - 20, 262, cx + 20, 318), fill=(54, 40, 44, 255))
        E((cx - 8, 270, cx + 8, 286), fill=(255, 255, 255, 255))
    if False and lashes:
        for side in (-1, 1):
            cx = 256 + side * 56
            L([(cx + side * 22, 264), (cx + side * 36, 254)], ink, 6)
            L([(cx + side * 22, 274), (cx + side * 38, 270)], ink, 5)
    # 볼과 주근깨
    if blush:
        E((146, 306, 200, 340), fill=(255, 150, 150, 120))
        E((312, 306, 366, 340), fill=(255, 150, 150, 120))
    if False and freckles:
        for (fx, fy) in ((160, 322), (178, 330), (196, 320), (316, 320), (334, 330), (352, 322)):
            E((fx - 3, fy - 3, fx + 3, fy + 3), fill=(190, 120, 90, 200))
    # 입
    if mouth == "smile":
        d.arc([226 * SS, 312 * SS, 286 * SS, 350 * SS], 20, 160, fill=ink, width=5 * SS)
    elif mouth == "flat":
        L([(232, 338), (280, 338)], ink, 5)
    elif mouth == "open":
        E((234, 324, 278, 356), fill=(190, 80, 80, 255), outline=ink, width=4)
        E((244, 340, 268, 354), fill=(235, 130, 130, 255))
    # 안경
    if glasses:
        for cx in (200, 312):
            E((cx - 38, 250, cx + 38, 328), outline=ink, width=5)
        L([(238, 288), (274, 288)], ink, 5)
        L([(162, 280), (112, 270)], ink, 4)
        L([(350, 280), (400, 270)], ink, 4)
    out = lighting(img.resize((512, 512), Image.LANCZOS))
    save(out, name)


def hsv(h, s, v):
    import colorsys
    r, g, b = colorsys.hsv_to_rgb(h % 1.0, s, v)
    return (int(r * 255), int(g * 255), int(b * 255))


DESIGNER_LOOKS = [
    # (머리색, 스타일, 안경, 띠, 피부)
    ((60, 44, 40), "short", False, None, 0),       # 박태양: 남성 단발
    ((118, 70, 48), "long", False, None, 1),       # 디자이너2
    ((236, 190, 90), "bob", False, (236, 120, 130), 2),
    ((40, 36, 52), "spiky", True, None, 0),
    ((196, 80, 90), "bun", False, None, 3),
    ((84, 60, 110), "twin", False, None, 1),
    ((150, 100, 60), "curly", False, None, 2),
    ((52, 52, 60), "bob", True, None, 3),
    ((220, 120, 70), "long", False, (250, 240, 180), 0),
    ((36, 60, 56), "short", False, None, 1),
    ((240, 224, 200), "bun", True, None, 2),
    ((110, 150, 190), "twin", False, None, 3),
]


def designers():
    for i, (hair, style, glasses, band, sk) in enumerate(DESIGNER_LOOKS):
        outfit = hsv(i * 0.083, 0.40, 0.96)
        apron = (250, 250, 252)
        tagc = hsv(i * 0.083 + 0.5, 0.5, 0.95)
        character("designer_%d" % i, SKINS[sk], hair, style, outfit, apron=apron, glasses=glasses, band=band,
                  tag=tagc, tool="scissors" if i % 2 == 0 else None, lashes=style in ("long", "bob", "twin", "bun") and not glasses,
                  brow="happy" if i % 3 == 1 else "normal", freckles=(i % 5 == 2))


def intern():
    character("intern", SKINS[3], (60, 44, 40), "twin", (250, 250, 252), apron=(250, 250, 252), tag=(120, 190, 240), towel=True, lashes=True, brow="happy", ribbon=(240, 130, 150))
    character("intern_uniform", SKINS[3], (60, 44, 40), "twin", (250, 250, 252), uniform_overlay=True)


def customers():
    looks = [
        ((90, 70, 60), "short", SKINS[0], (150, 190, 230), "smile", False),     # 평범
        ((50, 40, 40), "bob", SKINS[1], (230, 170, 190), "flat", True),         # 까다로운
        ((200, 140, 80), "curly", SKINS[2], (170, 215, 170), "open", False),    # 수다쟁이
        ((236, 120, 150), "twin", SKINS[0], (255, 214, 120), "open", False),    # 아이
    ]
    for i, (hair, style, skin, outfit, mouth, glasses) in enumerate(looks):
        brow = ["normal", "angry", "happy", "happy"][i]
        character("customer_%d" % i, skin, hair, style, outfit, mouth=mouth, glasses=glasses, brow=brow,
                  lashes=False, ribbon=((240, 90, 130) if i == 3 else None), freckles=(i == 2))


# ---------------------------------------------------------------- 미니게임 그림

def layered(size, draw_fn, names, out=None):
    """색을 입히는 층(main, accent)과 윤곽선·고정색 층(line)을 따로 그려서 저장한다.
    draw_fn(L) 에서 L.fill('main'|'accent', kind, box/pts, ...)로 모양을 그린다."""
    layers = {k: Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0)) for k in ("main", "accent", "line")}
    draws = {k: ImageDraw.Draw(v) for k, v in layers.items()}

    class L:
        @staticmethod
        def shape(kind, which, geom, color=None, width=5, outline=True):
            fill = (255, 255, 255, 255) if kind != "fixed" else color + (255,)
            tgt = draws["line"] if kind == "fixed" else draws[kind]
            if which == "rr":
                x0, y0, x1, y1, r = geom
                tgt.rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], radius=r * SS, fill=fill)
                if outline:
                    draws["line"].rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], radius=r * SS, outline=INK + (255,), width=width * SS)
            elif which == "ell":
                x0, y0, x1, y1 = geom
                tgt.ellipse([x0 * SS, y0 * SS, x1 * SS, y1 * SS], fill=fill)
                if outline:
                    draws["line"].ellipse([x0 * SS, y0 * SS, x1 * SS, y1 * SS], outline=INK + (255,), width=width * SS)
            elif which == "poly":
                pts = [(x * SS, y * SS) for x, y in geom]
                tgt.polygon(pts, fill=fill)
                if outline:
                    draws["line"].line(pts + [pts[0]], fill=INK + (255,), width=width * SS, joint="curve")

        @staticmethod
        def line(pts, width=4):
            draws["line"].line([(x * SS, y * SS) for x, y in pts], fill=INK + (255,), width=width * SS, joint="curve")

        @staticmethod
        def hole(which, geom, width=5):
            """구멍: 색 층과 윤곽선 층을 모두 비운 뒤 안쪽 윤곽선을 그린다."""
            for k in ("main", "accent", "line"):
                if which == "ell":
                    x0, y0, x1, y1 = geom
                    draws[k].ellipse([x0 * SS, y0 * SS, x1 * SS, y1 * SS], fill=(0, 0, 0, 0))
            x0, y0, x1, y1 = geom
            draws["line"].ellipse([x0 * SS, y0 * SS, x1 * SS, y1 * SS], outline=INK + (255,), width=width * SS)

    draw_fn(L)
    for k, nm in zip(("main", "accent", "line"), names):
        o = out or size
        save(layers[k].resize((o, o), Image.LANCZOS), nm)


def scissors_draw(L):
    # 오른쪽 날 (위로 갈수록 가늘어져 뾰족해짐) 과 왼쪽 날
    A = [(176, 6), (166, 60), (152, 112), (142, 152), (124, 148)]
    B = [(256 - x, y) for x, y in A]
    L.shape("main", "poly", B, width=4)
    L.shape("main", "poly", A, width=4)
    # 날에서 반대쪽 손잡이로 이어지는 목
    L.shape("accent", "poly", [(124, 148), (142, 154), (112, 188), (90, 177)], outline=False)
    L.shape("accent", "poly", [(132, 148), (114, 154), (144, 186), (166, 175)], outline=False)
    L.line([(124, 148), (90, 177)], 4); L.line([(142, 154), (112, 188)], 4)
    L.line([(132, 148), (166, 175)], 4); L.line([(114, 154), (144, 186)], 4)
    # 손잡이 고리 (왼쪽은 크고 오른쪽은 작게, 어긋나게) + 손가락 받침
    L.shape("accent", "poly", [(196, 214), (212, 218), (240, 242), (230, 252), (204, 236)], outline=False)
    L.line([(209, 220), (240, 242), (230, 252), (203, 234)], 4)
    L.shape("accent", "ell", (38, 172, 116, 248))
    L.shape("accent", "ell", (142, 168, 214, 232))
    L.hole("ell", (58, 190, 96, 230))
    L.hole("ell", (160, 184, 196, 216))
    # 가운데 나사
    L.shape("fixed", "ell", (116, 138, 140, 162), color=(200, 204, 216), width=3)


def compose_title_scissors():
    """시작 화면용 가위: 도구 가위의 세 층을 기본 색으로 입혀 합친다."""
    def tint(im, c):
        r, g, b, al = im.split()
        return Image.merge("RGBA", (r.point(lambda v: v * c[0] // 255), g.point(lambda v: v * c[1] // 255), b.point(lambda v: v * c[2] // 255), al))
    base = os.path.join(OUT, "")
    m = Image.open(base + "tool_scissors_m.png").convert("RGBA")
    a = Image.open(base + "tool_scissors_a.png").convert("RGBA")
    l = Image.open(base + "tool_scissors_l.png").convert("RGBA")
    comp = Image.alpha_composite(Image.new("RGBA", m.size, (0, 0, 0, 0)), tint(m, (226, 230, 242)))
    comp = Image.alpha_composite(comp, tint(a, (232, 100, 128)))
    comp = Image.alpha_composite(comp, l)
    save(comp, "title_scissors")


def tools():
    # 샤워 호스
    def shower(L):
        L.shape("main", "rr", (112, 6, 144, 120, 14))
        L.shape("main", "ell", (60, 100, 196, 196))
        L.shape("accent", "ell", (80, 140, 176, 200))
        for i in range(-1, 2):
            L.shape("fixed", "ell", (122 + i * 24, 164, 134 + i * 24, 176), color=(80, 80, 96), width=1, outline=False)
        for i, y in enumerate((218, 232, 218)):
            L.shape("fixed", "ell", (96 + i * 32, y, 118 + i * 32, y + 26), color=(110, 180, 245), width=3)
    layered(256, shower, ["tool_shower_m", "tool_shower_a", "tool_shower_l"])

    # 거품
    def foam(L):
        L.shape("main", "ell", (24, 110, 134, 220))
        L.shape("accent", "ell", (122, 110, 232, 220))
        L.shape("main", "ell", (70, 40, 190, 160))
        L.shape("accent", "ell", (28, 60, 92, 124))
        L.shape("fixed", "ell", (96, 66, 122, 90), color=(255, 255, 255), width=1, outline=False)
        L.shape("fixed", "ell", (150, 138, 172, 158), color=(255, 255, 255), width=1, outline=False)
    layered(256, foam, ["tool_foam_m", "tool_foam_a", "tool_foam_l"])

    # 빗
    def comb(L):
        L.shape("main", "rr", (24, 40, 232, 92, 20))
        for i in range(8):
            x = 36 + i * 26
            L.shape("accent", "rr", (x, 88, x + 14, 220, 6), width=4)
    layered(256, comb, ["tool_comb_m", "tool_comb_a", "tool_comb_l"])

    # 드라이기
    def dryer(L):
        L.shape("main", "rr", (30, 50, 190, 140, 34))
        L.shape("accent", "rr", (180, 62, 236, 128, 14))
        L.shape("main", "rr", (62, 128, 118, 238, 22))
        L.shape("fixed", "ell", (84, 178, 98, 192), color=(250, 250, 250), width=3)
        for k in range(3):
            L.shape("fixed", "rr", (194, 74 + k * 18, 224, 82 + k * 18, 3), color=(255, 255, 255), width=1, outline=False)
    layered(256, dryer, ["tool_dryer_m", "tool_dryer_a", "tool_dryer_l"])

    # 가위 (미용 가위: 가늘고 뾰족한 날, 어긋난 손잡이, 손가락 받침). 시작 화면 가위도 같은 그림을 쓴다.
    layered(256, scissors_draw, ["tool_scissors_m", "tool_scissors_a", "tool_scissors_l"], out=512)
    compose_title_scissors()

    # 염색 붓
    def brush(L):
        L.shape("main", "rr", (108, 6, 148, 150, 18))
        L.shape("fixed", "rr", (100, 140, 156, 178, 8), color=(190, 196, 206), width=5)
        L.shape("accent", "rr", (92, 170, 164, 246, 28))
    layered(256, brush, ["tool_brush_m", "tool_brush_a", "tool_brush_l"])


def hair_textures():
    # 문지르는 머리카락 한 칸 (동그란 덩어리). 색을 곱해서 젖음·거품·염색을 보여준다.
    w = h = 128
    img, d = canvas(w, h)
    rnd = random.Random(3)
    ell(d, (4, 4, 124, 124), fill=(255, 255, 255, 255))
    for k in range(9):
        x = 18 + k * 11 + rnd.randint(-2, 2)
        d.line([(x * SS, 14 * SS), ((x + rnd.randint(-10, 10)) * SS, 114 * SS)], fill=(214, 214, 214, 255), width=3 * SS)
    ell(d, (4, 4, 124, 124), outline=(190, 190, 190, 255), width=3)
    ell(d, (30, 16, 64, 36), fill=(255, 255, 255, 255))
    round_mask_ell(img, w, h)
    save(finish(img, w, h), "hair_blob")

    # 큰 머리 덩어리(머리 위에서 본 모습)
    w = h = 512
    img, d = canvas(w, h)
    ell(d, (6, 6, 506, 506), fill=(255, 255, 255, 255))
    cx = cy = 256
    for k in range(48):
        a = k / 48 * 2 * math.pi
        x0, y0 = cx + math.cos(a) * 70, cy + math.sin(a) * 70
        x1, y1 = cx + math.cos(a + 0.28) * 240, cy + math.sin(a + 0.28) * 240
        d.line([(x0 * SS, y0 * SS), (x1 * SS, y1 * SS)], fill=(208, 208, 208, 255), width=4 * SS)
    ell(d, (6, 6, 506, 506), outline=(176, 176, 176, 255), width=6)
    ell(d, (220, 220, 292, 292), fill=(224, 224, 224, 255))
    round_mask_ell(img, w, h)
    save(finish(img, w, h), "hair_head")

    # 염색·드라이용 머리카락 판
    w, h = 220, 120
    img, d = canvas(w, h)
    rr(d, (3, 3, 217, 117), 26, fill=(255, 255, 255, 255))
    for k in range(14):
        x = 16 + k * 14
        d.line([(x * SS, 14 * SS), ((x + (3 if k % 2 else -3)) * SS, 106 * SS)], fill=(216, 216, 216, 255), width=3 * SS)
    rr(d, (3, 3, 217, 117), 26, outline=(180, 180, 180, 255), width=4)
    save(finish(img, w, h), "hair_block")

    # 파마 머리 가닥
    w, h = 64, 256
    img, d = canvas(w, h)
    rr(d, (6, 2, 58, 254), 22, fill=(255, 255, 255, 255))
    for k in range(4):
        x = 16 + k * 11
        d.line([(x * SS, 12 * SS), ((x + 2) * SS, 244 * SS)], fill=(214, 214, 214, 255), width=3 * SS)
    rr(d, (6, 2, 58, 254), 22, outline=(176, 176, 176, 255), width=4)
    save(finish(img, w, h), "hair_strand")

    # 구불거리는 점(빗질 전 컬)
    w = h = 64
    img, d = canvas(w, h)
    ell(d, (4, 4, 60, 60), fill=(255, 255, 255, 255), outline=(170, 170, 170, 255), width=3)
    d.arc([14 * SS, 14 * SS, 50 * SS, 50 * SS], 40, 330, fill=(170, 170, 170, 255), width=3 * SS)
    ell(d, (26, 26, 38, 38), outline=(170, 170, 170, 255), width=3)
    save(finish(img, w, h), "curl_dot")


def round_mask_ell(img, w, h):
    from PIL import ImageChops
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).ellipse([0, 0, w * SS, h * SS], fill=255)
    img.putalpha(ImageChops.multiply(img.getchannel("A"), mask))


def perm_props():
    # 파지(흰 종이)
    w, h = 128, 72
    img, d = canvas(w, h)
    rr(d, (3, 3, 125, 69), 10, fill=(255, 255, 255, 255), outline=(170, 170, 170, 255), width=3)
    for k in range(3):
        d.line([((20 + k * 8) * SS, 14 * SS), ((20 + k * 8) * SS, 58 * SS)], fill=(222, 222, 222, 255), width=2 * SS)
    d.line([(64 * SS, 10 * SS), (64 * SS, 62 * SS)], fill=(220, 220, 220, 255), width=2 * SS)
    save(finish(img, w, h), "perm_paper")

    # 롯드(위에서 본 원통)
    w = h = 128
    img, d = canvas(w, h)
    ell(d, (4, 4, 124, 124), fill=(255, 255, 255, 255), outline=INK, width=5)
    ell(d, (26, 26, 102, 102), fill=(236, 236, 240, 255), outline=(150, 150, 160, 255), width=4)
    ell(d, (50, 50, 78, 78), fill=(190, 190, 200, 255), outline=(130, 130, 140, 255), width=3)
    for k in range(8):
        a = k / 8 * 2 * math.pi
        d.line([((64 + math.cos(a) * 30) * SS, (64 + math.sin(a) * 30) * SS), ((64 + math.cos(a) * 54) * SS, (64 + math.sin(a) * 54) * SS)],
               fill=(210, 210, 216, 255), width=3 * SS)
    save(finish(img, w, h), "perm_rod")

    # 놓을 자리 표시 링
    w = h = 128
    img, d = canvas(w, h)
    for k in range(16):
        d.arc([8 * SS, 8 * SS, 120 * SS, 120 * SS], k * 22.5, k * 22.5 + 13, fill=(255, 255, 255, 255), width=7 * SS)
    save(finish(img, w, h), "ring_dash")

    # 고무줄
    w = h = 96
    img, d = canvas(w, h)
    ell(d, (6, 6, 90, 90), fill=(255, 255, 255, 255), outline=INK, width=5)
    ell(d, (26, 26, 70, 70), fill=(0, 0, 0, 0), outline=INK, width=5)
    for k in range(3):
        d.arc([14 * SS, 14 * SS, 82 * SS, 82 * SS], 20 + k * 120, 70 + k * 120, fill=(255, 255, 255, 190), width=4 * SS)
    save(finish(img, w, h), "rubber_band")

    # 반짝이는 구슬(안내 점)
    w = h = 64
    img, d = canvas(w, h)
    ell(d, (4, 4, 60, 60), fill=(255, 255, 255, 255), outline=(140, 140, 140, 255), width=3)
    ell(d, (14, 12, 32, 28), fill=(255, 255, 255, 255))
    ell(d, (34, 36, 54, 54), fill=(225, 225, 225, 255))
    save(finish(img, w, h), "bead")

    # 부드러운 빛(드라이기 바람 범위, 빗자루 범위)
    w = h = 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y in range(h):
        for x in range(w):
            r = math.hypot(x - w / 2, y - h / 2) / (w / 2)
            if r < 1:
                a = int(255 * (1 - r) ** 0.6)
                px[x, y] = (255, 255, 255, a)
    save(img, "glow")


def props():
    # 쓰레기통
    w, h = 300, 220
    img, d = canvas(w, h)
    rr(d, (30, 40, 270, 214), 26, fill=(86, 94, 112, 255), outline=INK, width=6)
    for x in (80, 140, 200):
        d.line([(x * SS, 62 * SS), ((x - 6) * SS, 196 * SS)], fill=(112, 120, 138, 255), width=6 * SS)
    rr(d, (14, 20, 286, 54, ), 14, fill=(120, 130, 150, 255), outline=INK, width=6)
    rr(d, (118, 6, 182, 28), 10, fill=(120, 130, 150, 255), outline=INK, width=5)
    save(finish(img, w, h), "trash_bin")

    # 바닥에 떨어진 머리카락
    w, h = 140, 40
    img, d = canvas(w, h)
    pts = []
    for k in range(40):
        x = 6 + k * 3.3
        pts.append((x * SS, (20 + 9 * math.sin(k * 0.55)) * SS))
    d.line(pts, fill=(255, 255, 255, 255), width=6 * SS, joint="curve")
    d.line([(x, y + 7 * SS) for x, y in pts], fill=(235, 235, 235, 255), width=3 * SS, joint="curve")
    save(finish(img, w, h), "hair_clump")

    # 수도꼭지
    w = h = 256
    img, d = canvas(w, h)
    rr(d, (96, 150, 150, 246), 14, fill=(160, 166, 180, 255), outline=INK, width=5)           # 아래 몸통
    rr(d, (96, 80, 160, 170), 18, fill=(176, 182, 196, 255), outline=INK, width=5)
    rr(d, (96, 52, 232, 100), 22, fill=(176, 182, 196, 255), outline=INK, width=5)             # 윗가지
    rr(d, (200, 80, 236, 140), 12, fill=(176, 182, 196, 255), outline=INK, width=5)            # 꺾인 주둥이
    ell(d, (96, 8, 156, 68), fill=(70, 130, 220, 255), outline=INK, width=5)                   # 손잡이
    ell(d, (110, 18, 126, 34), fill=(150, 195, 250, 255))
    ell(d, (208, 150, 228, 180), fill=(110, 180, 245, 255), outline=INK, width=3)             # 물방울
    save(finish(img, w, h), "prop_faucet")

    # 샴푸통
    w = h = 256
    img, d = canvas(w, h)
    rr(d, (60, 90, 196, 246), 28, fill=(222, 112, 178, 255), outline=INK, width=6)             # 통
    rr(d, (80, 128, 176, 214), 14, fill=(255, 238, 246, 255), outline=INK, width=4)           # 라벨
    ell(d, (110, 150, 146, 186), fill=(240, 150, 200, 255))
    rr(d, (112, 56, 144, 96), 8, fill=(250, 250, 252, 255), outline=INK, width=5)             # 펌프 기둥
    rr(d, (100, 30, 190, 60), 14, fill=(250, 250, 252, 255), outline=INK, width=5)            # 펌프 머리
    save(finish(img, w, h), "prop_bottle")

    # 샴푸대에 누운 손님(위에서 본 모습): 760x440, 월드 중심 (0,-25)
    w, h = 760, 440
    img, d = canvas(w, h)
    rr(d, (0, 395, 760, 440), 22, fill=(246, 246, 252, 255), outline=(170, 176, 196, 255), width=5)  # 샴푸대
    skin = SKINS[0] + (255,)
    ell(d, (170 - 35 + 2, 215 - 35 - 5 + 2, 170 + 35 + 2, 215 + 35 - 5 + 2), fill=skin, outline=INK, width=4)  # 왼쪽 귀(임시 위치)
    # 월드→캔버스: cx = x + 380, cy = 245 - y
    def W(x, y):
        return (x + 380, 245 - y)
    d.rectangle([0, 0, 0, 0])
    img2, d2 = canvas(w, h)
    img = img2; d = d2
    rr(d, (0, 395, 760, 440), 22, fill=(246, 246, 252, 255), outline=(170, 176, 196, 255), width=5)
    for ex in (-175, 175):
        cx, cy = W(ex, 5)
        ell(d, (cx - 35, cy - 35, cx + 35, cy + 35), fill=skin, outline=INK, width=4)
    cx, cy = W(0, -115)
    ell(d, (cx - 125, cy - 125, cx + 125, cy + 125), fill=skin, outline=INK, width=5)
    ell(d, (cx - 100, cy + 12, cx - 56, cy + 40), fill=(255, 150, 150, 110))
    ell(d, (cx + 56, cy + 12, cx + 100, cy + 40), fill=(255, 150, 150, 110))
    cx, cy = W(0, 20)
    ell(d, (cx - 170, cy - 170, cx + 170, cy + 170), fill=(64, 44, 34, 255), outline=INK, width=6)
    for ex in (-48, 48):
        cx, cy = W(ex, -190)
        d.arc([(cx - 22) * SS, (cy - 10) * SS, (cx + 22) * SS, (cy + 14) * SS], 20, 160, fill=INK, width=5 * SS)
    cx, cy = W(0, -222)
    d.arc([(cx - 18) * SS, (cy - 12) * SS, (cx + 18) * SS, (cy + 8) * SS], 20, 160, fill=INK, width=4 * SS)
    save(finish(img, w, h), "shampoo_scene")



def male_styles():
    """남자 헤어스타일 24종을 같은 얼굴에 입혀서 male_00 ~ male_23 으로 저장한다."""
    colors = [(52, 40, 38), (70, 52, 44), (40, 34, 36), (96, 66, 48)]
    for i, st in enumerate(MALE_STYLES):
        character("male_%02d" % i, SKINS[i % 3], colors[i % 4], "m:%d" % i, (86 + (i * 23) % 120, 110 + (i * 37) % 100, 150 + (i * 11) % 90),
                  brow="normal", mouth="smile", blush=False)


def female_styles():
    colors = [(52, 38, 34), (96, 62, 44), (40, 30, 34), (150, 98, 60), (88, 52, 60), (30, 28, 36)]
    for i, st in enumerate(FEMALE_STYLES):
        character("female_%02d" % i, SKINS[i % 4], colors[i % 6], "f:%d" % i,
                  (230 - (i * 17) % 90, 150 + (i * 29) % 90, 170 + (i * 13) % 80), mouth="smile", blush=True)


def female_sheet(path):
    cols, rows, cell = 4, 5, 340
    sheet = Image.new("RGB", (cols * cell, rows * (cell + 24)), (244, 240, 232))
    dr = ImageDraw.Draw(sheet)
    from PIL import ImageFont
    try:
        font = ImageFont.truetype("/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc", 18)
    except Exception:
        font = None
    for i, st in enumerate(FEMALE_STYLES):
        im = Image.open(os.path.join(OUT, "female_%02d.png" % i)).convert("RGBA").resize((cell, cell), Image.LANCZOS)
        x, y = (i % cols) * cell, (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im)
        dr.text((x + 8, y + cell - 2), "%02d %s" % (i, st[0]), fill=(60, 50, 50), font=font)
    sheet.save(path)


def male_sheet(path):
    cols, rows, cell = 4, 6, 340
    sheet = Image.new("RGB", (cols * cell, rows * (cell + 24)), (244, 240, 232))
    dr = ImageDraw.Draw(sheet)
    from PIL import ImageFont
    try:
        font = ImageFont.truetype("/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc", 18)
    except Exception:
        font = None
    for i, st in enumerate(MALE_STYLES):
        im = Image.open(os.path.join(OUT, "male_%02d.png" % i)).convert("RGBA").resize((cell, cell), Image.LANCZOS)
        x, y = (i % cols) * cell, (i // cols) * (cell + 24)
        sheet.paste(im, (x, y), im)
        dr.text((x + 8, y + cell - 2), "%02d %s" % (i, st[4]), fill=(60, 50, 50), font=font)
    sheet.save(path)


def main():
    ui_button(); ui_bubble(); ui_panel()
    floor_tile(); corridor_tile()
    room_rest(); room_supply(); zone_wash(); zone_seat(); seat_chair(); seat_mirror()
    designers(); intern(); customers()
    tools(); hair_textures(); perm_props(); props()
    male_styles()
    female_styles()


if __name__ == "__main__":
    main()
