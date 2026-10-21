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
MINT = "#BDE7DA"
STRAW = "#E9B949"
BLUSH = "#F2A69B"
HANDLE_DARK = "#E7D7B5"
WORD = INK
WORD_DARK = "#E6F2EE"

BROOM_AT, BROOM_TURN = (50, 36), -15


def n(v: float) -> str:
    t = f"{v:.3f}".rstrip("0").rstrip(".")
    return "0" if t in ("-0", "") else t


def pebble(handle: str, transform: str = "") -> str:
    """Option 1A, drawn in a 64-unit box."""
    attr = f' transform="{transform}"' if transform else ""
    return (
        f"<g{attr}>"
        f'<g transform="translate({BROOM_AT[0]} {BROOM_AT[1]}) rotate({BROOM_TURN})">'
        f'<line x1="0" y1="-22" x2="0" y2="5" stroke="{handle}" stroke-width="3" stroke-linecap="round"/>'
        f'<path d="M-5 5H5L7.5 16H-7.5Z" fill="{STRAW}" stroke="{INK}" stroke-width="2.5" stroke-linejoin="round"/>'
        f'<line x1="-5.5" y1="8.5" x2="5.5" y2="8.5" stroke="{INK}" stroke-width="2"/>'
        "</g>"
        f'<circle cx="27" cy="38" r="19" fill="{MINT}" stroke="{INK}" stroke-width="3"/>'
        f'<circle cx="22" cy="36" r="2.4" fill="{INK}"/>'
        f'<circle cx="32" cy="36" r="2.4" fill="{INK}"/>'
        f'<path d="M25 40.5Q27 42.5 29 40.5" fill="none" stroke="{INK}" stroke-width="1.8" stroke-linecap="round"/>'
        f'<ellipse cx="17.5" cy="42" rx="2.6" ry="1.6" fill="{BLUSH}"/>'
        f'<ellipse cx="36.5" cy="42" rx="2.6" ry="1.6" fill="{BLUSH}"/>'
        f'<circle cx="48.5" cy="39" r="3.6" fill="{MINT}" stroke="{INK}" stroke-width="2.5"/>'
        "</g>"
    )


def ink_bounds() -> tuple[float, float, float, float]:
    """Bounds of the drawn ink, strokes included, in the 64-unit box."""
    boxes = [(27, 38, 19 + 1.5), (48.5, 39, 3.6 + 1.25)]  # body and paw: centre, radius
    a = math.radians(BROOM_TURN)

    def place(x: float, y: float) -> tuple[float, float]:
        return (BROOM_AT[0] + x * math.cos(a) - y * math.sin(a), BROOM_AT[1] + x * math.sin(a) + y * math.cos(a))

    # Broom points with the half-width of the stroke drawn through them.
    for x, y, half in [(0, -22, 1.5), (0, 5, 1.5), (-5, 5, 1.25), (5, 5, 1.25), (7.5, 16, 1.25), (-7.5, 16, 1.25)]:
        px, py = place(x, y)
        boxes.append((px, py, half))
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

# --- mark.svg / mark-dark.svg: tight viewBox with a little padding ------------------
pad = 1.0
vb = (bx0 - pad, by0 - pad, bw + 2 * pad, bh + 2 * pad)
for name, handle in (("mark.svg", INK), ("mark-dark.svg", HANDLE_DARK)):
    (out / name).write_text(svg(vb[2], vb[3], pebble(handle), "Sweepling", vb=vb, px_scale=4), encoding="utf-8", newline="\n")

# --- tray.svg: square and tight, so the mascot fills a 16 px tray icon (source of sweepling.ico) ---
side = max(bw, bh) + 2 * pad
vb = (bx0 + bw / 2 - side / 2, by0 + bh / 2 - side / 2, side, side)
(out / "tray.svg").write_text(svg(side, side, pebble(INK), "Sweepling", vb=vb, px_scale=4), encoding="utf-8", newline="\n")

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
for name, handle, color in (("lockup.svg", INK, WORD), ("lockup-dark.svg", HANDLE_DARK, WORD_DARK)):
    body = (
        f'<g transform="translate({n(-x0)} {n(-y0)})">'
        + pebble(handle, f"translate({n(mx)} {n(my)}) scale({m:.5f})")
        + f'<path d="{word_d}" fill="{color}"/>'
        + "</g>"
    )
    (out / name).write_text(svg(W, H, body, "Sweepling"), encoding="utf-8", newline="\n")

print("wrote", sorted(p.name for p in out.glob("*.svg")))
