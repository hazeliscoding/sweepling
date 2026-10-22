"""Build the docs/brand SVGs (mark and lockups) for Sweepling.

Usage: python -I scripts/make_brand.py <Fredoka-SemiBold.ttf> <out_dir>

Fredoka is a variable font; make the SemiBold instance first:
  python -m fontTools.varLib.instancer "Fredoka[wdth,wght].ttf" wght=600 wdth=100 -o Fredoka-SemiBold.ttf
Needs fonttools and uharfbuzz.
"""
import math
import sys
from pathlib import Path

import uharfbuzz as hb
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

font_path = Path(sys.argv[1])
out = Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)

INK = "#17312E"
STRAW = "#E9B949"
STRAW_SHADE = "#C99A2E"
PISTACHIO = "#DCECC4"
GREEN = "#244838"
WORD = INK
WORD_DARK = "#E6F2EE"

# Poses in the 64-unit box: (position, turn, scale). Upright there is work to do; lying down, nothing to
# sweep. Upright, the bristles lean left, so the count badge in the top right stays clear of them.
UPRIGHT = ((29, 38), -28, 1.15)
LYING = ((32, 45), -80, 1.08)
FAN_CENTRE, FAN_OUT, FAN_IN, FAN_FROM, FAN_SPAN, FAN_TEETH = (0, 8), 31, 27.5, -118, 56, 12


def n(v: float) -> str:
    t = f"{v:.3f}".rstrip("0").rstrip(".")
    return "0" if t in ("-0", "") else t


def fan_points() -> list[tuple[float, float]]:
    """The bristles' outline, from the band up to a zigzag-cut top and back."""
    cx, cy = FAN_CENTRE
    tips = []
    for i in range(FAN_TEETH + 1):
        a = math.radians(FAN_FROM + i * FAN_SPAN / FAN_TEETH)
        r = FAN_OUT if i % 2 == 0 else FAN_IN
        tips.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return [(-5.5, -0.5), *tips, (5.5, -0.5)]


def brush(transform: str = "", asleep: bool = False) -> str:
    """Option 2B2, Primrose's straw hand brush in her colors, drawn in a 64-unit box."""
    (x, y), turn, scale = LYING if asleep else UPRIGHT
    attr = f' transform="{transform}"' if transform else ""
    fan = "M" + "L".join(f"{n(px)} {n(py)}" for px, py in fan_points()) + "Z"
    cx, cy = FAN_CENTRE

    def along(angle: float, r: float) -> str:
        a = math.radians(angle)
        return f"{n(cx + r * math.cos(a))} {n(cy + r * math.sin(a))}"

    bristles = "".join(
        f'<path d="M{along(a, 8)}L{along(a, 23)}" stroke="{STRAW_SHADE}" stroke-width="1.6" stroke-linecap="round"/>' for a in (-106, -90, -74)
    )
    return (
        f"<g{attr}>"
        f'<g transform="translate({n(x)} {n(y)}) rotate({n(turn)}) scale({n(scale)})">'
        f'<rect x="-3.8" y="4" width="7.6" height="19" rx="3.8" fill="{PISTACHIO}" stroke="{INK}" stroke-width="2.5"/>'
        f'<path d="{fan}" fill="{STRAW}" stroke="{INK}" stroke-width="2.8" stroke-linejoin="round"/>'
        + bristles
        + f'<rect x="-6.5" y="-1.5" width="13" height="6.5" rx="2" fill="{GREEN}" stroke="{INK}" stroke-width="2.5"/>'
        f'<path d="M-5.2 1.75H5.2" stroke="{STRAW}" stroke-width="1.8"/>'
        "</g></g>"
    )


def ink_bounds(asleep: bool = False) -> tuple[float, float, float, float]:
    """Bounds of the drawn ink, strokes included, in the 64-unit box."""
    (tx, ty), turn, scale = LYING if asleep else UPRIGHT
    a = math.radians(turn)
    # Points with the half-width of the stroke, or the round end, drawn around them.
    marks = [(px, py, 1.4) for px, py in fan_points()]
    marks += [(0, 7.8, 3.8 + 1.25), (0, 19.2, 3.8 + 1.25)]  # the handle's round ends
    marks += [(sx, sy, 1.25) for sx in (-6.5, 6.5) for sy in (-1.5, 5)]  # the band's corners
    boxes = [
        (tx + scale * (px * math.cos(a) - py * math.sin(a)), ty + scale * (px * math.sin(a) + py * math.cos(a)), half * scale)
        for px, py, half in marks
    ]
    return (
        min(cx - r for cx, _, r in boxes),
        min(cy - r for _, cy, r in boxes),
        max(cx + r for cx, _, r in boxes),
        max(cy + r for _, cy, r in boxes),
    )


def svg(w: float, h: float, body: str, title: str, vb=None, px_scale=1.0) -> str:
    vb = vb or (0, 0, w, h)
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{n(w * px_scale)}" height="{n(h * px_scale)}" '
        f'viewBox="{" ".join(n(v) for v in vb)}" role="img" aria-label="{title}">'
        f"<title>{title}</title>{body}</svg>\n"
    )


bx0, by0, bx1, by1 = ink_bounds()
bw, bh = bx1 - bx0, by1 - by0
print(f"mark ink x {bx0:.2f}..{bx1:.2f} y {by0:.2f}..{by1:.2f} ({bw:.2f} x {bh:.2f})")

# --- mark.svg: tight viewBox with a little padding. The pistachio handle and straw bristles read on a
# dark background as well, so the mark has no dark variant. ---------------------------------------
pad = 1.0
vb = (bx0 - pad, by0 - pad, bw + 2 * pad, bh + 2 * pad)
(out / "mark.svg").write_text(svg(vb[2], vb[3], brush(), "Sweepling", vb=vb, px_scale=4), encoding="utf-8", newline="\n")

# --- tray.svg: square and tight, so the brush fills a 16 px icon (source of sweepling.ico) ---------
side = max(bw, bh) + 2 * pad
vb = (bx0 + bw / 2 - side / 2, by0 + bh / 2 - side / 2, side, side)
(out / "tray.svg").write_text(svg(side, side, brush(), "Sweepling", vb=vb, px_scale=4), encoding="utf-8", newline="\n")

# --- lockups: the mark plus "sweepling" in Fredoka SemiBold, outlined ----------------
font = TTFont(font_path)
upm = font["head"].unitsPerEm
gs = font.getGlyphSet()

hbfont = hb.Font(hb.Face(hb.Blob.from_file_path(str(font_path))))
buf = hb.Buffer()
buf.add_str("sweepling")
buf.guess_segment_properties()
hb.shape(hbfont, buf, {"kern": True, "liga": True})
order = font.getGlyphOrder()
glyphs = [(order[i.codepoint], p.x_advance, p.x_offset, p.y_offset) for i, p in zip(buf.glyph_infos, buf.glyph_positions)]
print("shaped glyphs:", [g[0] for g in glyphs])

# Proportions match the approved canvas: 60 px type beside a 72 px mark box.
EM = 100.0
fs = EM / upm
mark_h = 0.887 * EM
gap = 0.33 * EM
mark_cy = -0.30 * EM  # mark's ink centre, above the baseline at y=0
tracking = -0.01 * upm

m = mark_h / bh
mark_w = bw * m

first = BoundsPen(gs)
gs[glyphs[0][0]].draw(first)
text_x0 = mark_w + gap - first.bounds[0] * fs  # gap measured ink to ink

parts = []
wb = BoundsPen(gs)
x = 0.0
for idx, (gname, adv, xo, yo) in enumerate(glyphs):
    t = (fs, 0, 0, -fs, text_x0 + (x + xo) * fs, -yo * fs)
    sp = SVGPathPen(gs, ntos=n)
    gs[gname].draw(TransformPen(sp, t))
    parts.append(sp.getCommands())
    gs[gname].draw(TransformPen(wb, t))
    x += adv + (tracking if idx < len(glyphs) - 1 else 0)
wx0, wy0, wx1, wy1 = wb.bounds

mx = -bx0 * m
my = mark_cy - (by0 + bh / 2) * m
mark_y0, mark_y1 = mark_cy - mark_h / 2, mark_cy + mark_h / 2

x0, y0 = min(0.0, wx0), min(mark_y0, wy0)
x1, y1 = max(mark_w, wx1), max(mark_y1, wy1)
W, H = x1 - x0, y1 - y0
print(f"lockup: mark {mark_w:.1f}x{mark_h:.1f} gap {gap:.1f} word ink {wx0:.1f}..{wx1:.1f} / {wy0:.1f}..{wy1:.1f}; size {W:.1f}x{H:.1f}")

word_d = " ".join(p for p in parts if p)
for name, color in (("lockup.svg", WORD), ("lockup-dark.svg", WORD_DARK)):
    body = (
        f'<g transform="translate({n(-x0)} {n(-y0)})">'
        + brush(f"translate({n(mx)} {n(my)}) scale({m:.5f})")
        + f'<path d="{word_d}" fill="{color}"/>'
        + "</g>"
    )
    (out / name).write_text(svg(W, H, body, "Sweepling"), encoding="utf-8", newline="\n")

# --- tray states: lying down with nothing to sweep, upright with a count badge, 1 to 9 then 9+ -----
# Every state uses the whole 64-unit box, which holds both poses, so the brush doesn't jump between
# them. The badge sits in its top right, 1.3 units clear of the upright bristles.
BADGE = "#C2410C"
tray = out / "tray"
tray.mkdir(exist_ok=True)
lx0, ly0, lx1, ly1 = ink_bounds(asleep=True)
assert min(bx0, lx0) >= 0 and min(by0, ly0) >= 0 and max(bx1, lx1) <= 64 and max(by1, ly1) <= 64, "a pose leaves the box"
side = 64.0
tray_vb = (0, 0, side, side)
r = 12.5
cx, cy = 50.0, 14.5


def badge(label: str) -> str:
    """The count on a coral disc in the icon's top right, its digits outlined in Fredoka and centred."""
    run = []
    x = 0.0
    for ch in label:
        gname = font.getBestCmap()[ord(ch)]
        run.append((gname, x))
        x += font["hmtx"][gname][0]
    bounds = BoundsPen(gs)
    for gname, gx in run:
        gs[gname].draw(TransformPen(bounds, (1, 0, 0, 1, gx, 0)))
    gx0, gy0, gx1, gy1 = bounds.bounds
    s = (r * 1.05) / (gy1 - gy0)
    if (gx1 - gx0) * s > r * 1.5:
        s = (r * 1.5) / (gx1 - gx0)
    dx = cx - (gx0 + gx1) / 2 * s
    dy = cy + (gy0 + gy1) / 2 * s
    pen = SVGPathPen(gs, ntos=n)
    for gname, gx in run:
        gs[gname].draw(TransformPen(pen, (s, 0, 0, -s, dx + gx * s, dy)))
    return (
        f'<circle cx="{n(cx)}" cy="{n(cy)}" r="{n(r)}" fill="{BADGE}" stroke="#FFFFFF" stroke-width="{n(side * 0.035)}"/>'
        f'<path d="{pen.getCommands()}" fill="#FFFFFF"/>'
    )


(tray / "tray-0.svg").write_text(svg(side, side, brush(asleep=True), "Sweepling", vb=tray_vb, px_scale=4), encoding="utf-8", newline="\n")
for count in range(1, 11):
    label = str(count) if count < 10 else "9+"
    name = f"tray-{count}.svg" if count < 10 else "tray-many.svg"
    (tray / name).write_text(svg(side, side, brush() + badge(label), "Sweepling", vb=tray_vb, px_scale=4), encoding="utf-8", newline="\n")

print("wrote", sorted(p.name for p in out.glob("*.svg")), "and", len(list(tray.glob("*.svg"))), "tray states")
