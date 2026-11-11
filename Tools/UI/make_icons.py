# Trickshot Defense · iconos de la UI (blancos sobre transparente; el juego los tiñe). Uso: python make_icons.py <carpeta>
import sys, os, math
from PIL import Image, ImageDraw
OUT = sys.argv[1]; os.makedirs(OUT, exist_ok=True)
S = 4; N = 128; W = (255, 255, 255, 255); T = (0, 0, 0, 0)

def canvas():
    im = Image.new("RGBA", (N * S, N * S), T)
    return im, ImageDraw.Draw(im)
def p(*xy): return [v * S for v in xy]
def pts(l): return [(x * S, y * S) for x, y in l]
def save(im, name): im.resize((N, N), Image.LANCZOS).save(os.path.join(OUT, name + ".png"))
def star_pts(cx, cy, r1, r2, n=5, rot=-90):
    out = []
    for i in range(n * 2):
        a = math.radians(rot + i * 180 / n); r = r1 if i % 2 == 0 else r2
        out.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return out

im, d = canvas(); d.polygon(pts([(36, 22), (36, 106), (108, 64)]), W); save(im, "play")
im, d = canvas(); d.rounded_rectangle(p(30, 24, 54, 104), 6 * S, W); d.rounded_rectangle(p(74, 24, 98, 104), 6 * S, W); save(im, "pause")
im, d = canvas(); d.polygon(pts([(14, 26), (14, 102), (62, 64)]), W); d.polygon(pts([(62, 26), (62, 102), (114, 64)]), W); save(im, "fast")
im, d = canvas(); d.polygon(pts(star_pts(64, 68, 54, 23)), W); save(im, "star")
im, d = canvas(); d.rounded_rectangle(p(52, 16, 76, 112), 8 * S, W); d.rounded_rectangle(p(16, 52, 112, 76), 8 * S, W); save(im, "plus")
im, d = canvas(); d.polygon(pts([(64, 14), (110, 64), (82, 64), (82, 112), (46, 112), (46, 64), (18, 64)]), W); save(im, "up")
im, d = canvas(); d.line(pts([(22, 66), (52, 96), (106, 32)]), W, 16 * S, joint="curve"); save(im, "check")
im, d = canvas(); d.line(pts([(28, 28), (100, 100)]), W, 16 * S); d.line(pts([(100, 28), (28, 100)]), W, 16 * S); save(im, "close")
im, d = canvas(); d.polygon(pts([(72, 18), (26, 64), (72, 110), (86, 96), (54, 64), (86, 32)]), W); save(im, "back")
im, d = canvas(); d.polygon(pts([(64, 14), (10, 62), (26, 62), (26, 112), (54, 112), (54, 80), (74, 80), (74, 112), (102, 112), (102, 62), (118, 62)]), W); save(im, "home")
# moneda
im, d = canvas(); d.ellipse(p(12, 12, 116, 116), W); d.ellipse(p(26, 26, 102, 102), T); d.ellipse(p(34, 34, 94, 94), W); save(im, "coin")
# trofeo
im, d = canvas()
d.pieslice(p(30, -20, 98, 76), 0, 180, W); d.rectangle(p(30, 14, 98, 30), W)
d.arc(p(8, 16, 44, 56), 90, 270, W, 8 * S); d.arc(p(84, 16, 120, 56), 270, 90, W, 8 * S)
d.rectangle(p(57, 72, 71, 94), W); d.rounded_rectangle(p(36, 92, 92, 112), 5 * S, W); save(im, "trophy")
# corazón (vida de la base)
im, d = canvas(); d.ellipse(p(14, 20, 68, 74), W); d.ellipse(p(60, 20, 114, 74), W); d.polygon(pts([(18, 60), (110, 60), (64, 112)]), W); save(im, "heart")
# candado
im, d = canvas(); d.arc(p(36, 12, 92, 76), 180, 360, W, 12 * S); d.rectangle(p(36, 44, 48, 60), W); d.rectangle(p(80, 44, 92, 60), W)
d.rounded_rectangle(p(24, 56, 104, 114), 10 * S, W); d.ellipse(p(56, 74, 72, 90), T); save(im, "lock")
# camiseta (equipaciones)
im, d = canvas(); d.polygon(pts([(44, 16), (10, 36), (24, 62), (36, 56), (36, 112), (92, 112), (92, 56), (104, 62), (118, 36), (84, 16), (74, 28), (54, 28)]), W); save(im, "shirt")
# engranaje
im, d = canvas()
for i in range(8):
    a = math.radians(i * 45); cx, cy = 64 + 44 * math.cos(a), 64 + 44 * math.sin(a)
    d.ellipse(p(cx - 12, cy - 12, cx + 12, cy + 12), W)
d.ellipse(p(22, 22, 106, 106), W); d.ellipse(p(46, 46, 82, 82), T); save(im, "gear")
# altavoz y nota
im, d = canvas(); d.polygon(pts([(14, 46), (40, 46), (70, 20), (70, 108), (40, 82), (14, 82)]), W)
d.arc(p(56, 34, 104, 94), -55, 55, W, 9 * S); d.arc(p(56, 14, 126, 114), -50, 50, W, 9 * S); save(im, "sound")
im, d = canvas(); d.rectangle(p(50, 18, 60, 92), W); d.polygon(pts([(50, 18), (104, 30), (104, 50), (50, 38)]), W)
d.ellipse(p(22, 76, 62, 110), W); d.rectangle(p(94, 30, 104, 82), W); d.ellipse(p(66, 66, 106, 100), W); save(im, "music")
# anuncio (vídeo)
im, d = canvas(); d.rounded_rectangle(p(10, 26, 118, 102), 14 * S, W); d.polygon(pts([(52, 44), (52, 84), (86, 64)]), T); save(im, "video")
# robot (oleadas)
im, d = canvas(); d.rounded_rectangle(p(22, 34, 106, 104), 14 * S, W); d.rectangle(p(60, 12, 68, 34), W); d.ellipse(p(54, 6, 74, 26), W)
d.rounded_rectangle(p(36, 54, 92, 70), 6 * S, T); d.rectangle(p(8, 56, 22, 84), W); d.rectangle(p(106, 56, 120, 84), W); save(im, "robot")
# vibración
im, d = canvas(); d.rounded_rectangle(p(42, 14, 86, 114), 10 * S, W); d.rectangle(p(50, 26, 78, 94), T)
d.line(pts([(22, 40), (22, 88)]), W, 9 * S); d.line(pts([(106, 40), (106, 88)]), W, 9 * S); save(im, "vibrate")
# mano que señala (tutorial)
im, d = canvas(); d.rounded_rectangle(p(50, 10, 72, 74), 11 * S, W); d.rounded_rectangle(p(36, 52, 104, 118), 18 * S, W); d.rounded_rectangle(p(24, 58, 46, 92), 10 * S, W); save(im, "hand")
print("iconos:", len(os.listdir(OUT)))
