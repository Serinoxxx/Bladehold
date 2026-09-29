"""Build localised copies of the Steam page heading banners.

Reads the headings out of the "About this game" row of the Steam localisation
CSV, copies each English heading page once per language (one column of pages
per language), swaps the text and font, centres it on the English heading and
shrinks it until it fits the page width minus padding. Pages are labelled
<Heading>_<LANG> so Inkscape's batch export names the files from them.

    python localise_headings.py            # writes "Steam page headings - localised.svg"

The source SVG is never modified. Re-run whenever the CSV changes.
"""

import copy
import csv
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]  # scripts -> skill -> skills -> .claude -> repo
MEDIA = REPO / "Marketing Media"
SRC_SVG = MEDIA / "Inkscape" / "Steam page headings.svg"
OUT_SVG = MEDIA / "Inkscape" / "Steam page headings - localised.svg"
PNG_DIR = MEDIA / "Inkscape" / "SteamPageHeadings"  # Inkscape batch-export target
CSV_PATH = MEDIA / "Steam Page Localisation - Sheet1.csv"
INKSCAPE = r"C:\Program Files\Inkscape\bin\inkscape.exe"

PADDING_MM = 10.0     # clear space either side of the text inside the page
COLUMN_GAP_MM = 20.0  # gap between language columns on the canvas
PX_PER_MM = 96 / 25.4

NS = {
    "svg": "http://www.w3.org/2000/svg",
    "inkscape": "http://www.inkscape.org/namespaces/inkscape",
    "sodipodi": "http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd",
    "xlink": "http://www.w3.org/1999/xlink",
}
ET.register_namespace("", NS["svg"])
for _p in ("inkscape", "sodipodi", "xlink"):
    ET.register_namespace(_p, NS[_p])
INK_LABEL = "{%s}label" % NS["inkscape"]

# English headings in CSV order; these are the keys every language maps onto.
ENGLISH_ORDER = [
    "Combat Your Way", "Elemental Buildcrafting", "Game Defining Decisions",
    "Rest and Recover", "Endless Progression", "Unique Weapon Abilities",
    "Automated Defenses & Traps", "The Fortress Under Siege", "Challenging Bosses",
]

# CSV column -> (page suffix, font key). Font keys index FONTS below.
LANGUAGES = {
    "English": ("ENG", "cinzel"),
    "Simplified Chinese": ("ZH-CN", "zh"),
    "German": ("DE", "cinzel"),
    "French": ("FR", "cinzel"),
    "Italian": ("IT", "cinzel"),
    "Spanish - Spain": ("ES", "cinzel"),
    "Korean": ("KO", "ko"),
    "Japanese": ("JA", "ja"),
    "European Portugese": ("PT-PT", "cinzel"),
    "Brazilian Portuguese": ("PT-BR", "cinzel"),
    "Russian": ("RU", "ru"),
}

# style overrides per font; None = keep the English style untouched.
FONTS = {
    "cinzel": None,
    "ru": {"font-family": "'Cormorant SC'", "font-weight": "bold",
           "-inkscape-font-specification": "'Cormorant SC Bold'", "font-variation-settings": None},
    "zh": {"font-family": "'Noto Serif SC'", "font-weight": "bold",
           "-inkscape-font-specification": "'Noto Serif SC, @wght=700'", "font-variation-settings": "'wght' 700"},
    "ja": {"font-family": "'Noto Serif JP'", "font-weight": "bold",
           "-inkscape-font-specification": "'Noto Serif JP, @wght=700'", "font-variation-settings": "'wght' 700"},
    "ko": {"font-family": "'Noto Serif KR'", "font-weight": "bold",
           "-inkscape-font-specification": "'Noto Serif KR, @wght=700'", "font-variation-settings": "'wght' 700"},
}

# Non-Cinzel text is sized so its ink height is this multiple of the median English
# heading height (CJK glyphs read smaller than Cinzel capitals at equal height).
HEIGHT_SCALE = {"ru": 1.0, "zh": 1.15, "ja": 1.15, "ko": 1.15}

# Where a language's "About this game" sections run in a different order from
# English: index i here is the English heading that language's i-th heading maps to.
SECTION_ORDER = {
    "Spanish - Spain": [0, 1, 2, 3, 5, 6, 8, 7, 4],
}

# Headings that can't be pulled from the CSV cell (Korean's body text is restructured).
HEADING_OVERRIDES = {
    "Korean": ["당신만의 전투 방식", "원소 빌드", "방어 전략 구성", "휴식과 재정비", "끝없는 진행",
               "고유 무기의 능력", "오토디펜스 & 함정", "포위당한 성채", "보스전"],
}

# The CSV is mostly ALL CAPS. English keeps its title case; other Latin/Cyrillic
# languages use sentence case (Cinzel / Cormorant SC then draw one full capital
# followed by small caps). German capitalises nouns, so it gets explicit casing.
GERMAN_CASE = {
    "DEIN KAMPFSTIL, DEINE REGELN": "Dein Kampfstil, deine Regeln",
    "ELEMENTARE BUILDS": "Elementare Builds",
    "JEDE ENTSCHEIDUNG ZÄHLT": "Jede Entscheidung zählt",
    "KURZ DURCHATMEN": "Kurz durchatmen",
    "MIT JEDEM RUN STÄRKER": "Mit jedem Run stärker",
    "EINZIGARTIGE WAFFEN": "Einzigartige Waffen",
    "FALLEN & AUTOMATISCHE ABWEHR": "Fallen & automatische Abwehr",
    "DIE TORE DÜRFEN NICHT FALLEN": "Die Tore dürfen nicht fallen",
    "KNALLHARTE BOSSE": "Knallharte Bosse",
}


# ---------------------------------------------------------------------------- CSV

def clean(s):
    return s.replace("\u200e", "").replace("\u200f", "").strip()


def load_headings():
    with open(CSV_PATH, encoding="utf-8") as f:
        rows = list(csv.reader(f))
    header = [clean(h) for h in rows[0]]
    about = next(r for r in rows if clean(r[0]) == "About this game")
    out = {}
    for col, name in enumerate(header):
        if name not in LANGUAGES:
            continue
        if name in HEADING_OVERRIDES:
            heads = HEADING_OVERRIDES[name]
        else:
            paras = [clean(p) for p in about[col].split("\n")]
            paras = [p for p in paras if p]
            heads = paras[0::2][:9]
        heads = [h.rstrip(".") for h in heads]
        if len(heads) != 9:
            sys.exit(f"{name}: expected 9 headings, got {len(heads)}: {heads}")
        order = SECTION_ORDER.get(name, range(9))
        mapped = [None] * 9
        for src_i, eng_i in enumerate(order):
            mapped[eng_i] = heads[src_i]
        out[name] = mapped
    return out


def display_case(lang, text):
    if re.search(r"[　-鿿가-힯＀-￯]", text):
        return text  # CJK: leave as-is
    if lang == "German":
        if text not in GERMAN_CASE:
            sys.exit(f"German heading missing from GERMAN_CASE: {text!r}")
        return GERMAN_CASE[text]
    low = text.lower()
    return low[:1].upper() + low[1:]


# ---------------------------------------------------------------------------- SVG helpers

def parse_style(s):
    d = {}
    for part in (s or "").split(";"):
        if ":" in part:
            k, v = part.split(":", 1)
            d[k.strip()] = v.strip()
    return d


def style_str(d):
    return ";".join(f"{k}:{v}" for k, v in d.items())


def mat_mul(a, b):
    return (a[0] * b[0] + a[2] * b[1], a[1] * b[0] + a[3] * b[1],
            a[0] * b[2] + a[2] * b[3], a[1] * b[2] + a[3] * b[3],
            a[0] * b[4] + a[2] * b[5] + a[4], a[1] * b[4] + a[3] * b[5] + a[5])


def parse_transform(t):
    m = (1, 0, 0, 1, 0, 0)
    for fn, args in re.findall(r"(\w+)\(([^)]*)\)", t or ""):
        v = [float(x) for x in re.split(r"[ ,]+", args.strip())]
        if fn == "translate":
            m = mat_mul(m, (1, 0, 0, 1, v[0], v[1] if len(v) > 1 else 0))
        elif fn == "matrix":
            m = mat_mul(m, tuple(v))
        elif fn == "scale":
            m = mat_mul(m, (v[0], 0, 0, v[1] if len(v) > 1 else v[0], 0, 0))
        else:
            raise ValueError(f"unsupported transform on text chain: {fn}")
    return m


def invert_point(m, x, y):
    a, b, c, d, e, f = m
    det = a * d - b * c
    x, y = x - e, y - f
    return ((d * x - c * y) / det, (-b * x + a * y) / det)


def query_all(svg_path):
    out = subprocess.run([INKSCAPE, str(svg_path), "--query-all"],
                         capture_output=True, text=True, encoding="utf-8").stdout
    boxes = {}
    for line in out.splitlines():
        parts = line.split(",")
        if len(parts) == 5:
            try:
                boxes[parts[0]] = tuple(float(p) / PX_PER_MM for p in parts[1:])  # mm, doc coords
            except ValueError:
                pass
    return boxes


def rename_ids(root, suffix):
    ids = {e.get("id") for e in root.iter() if e.get("id")}
    for e in root.iter():
        if e.get("id"):
            e.set("id", e.get("id") + suffix)
        for k, v in list(e.attrib.items()):
            if "#" in v:
                v = re.sub(r"url\(#([^)]+)\)", lambda m: f"url(#{m[1]}{suffix})" if m[1] in ids else m[0], v)
                if k.endswith("href") and v.startswith("#") and v[1:] in ids:
                    v = v + suffix
                e.set(k, v)


def set_pos(text, x=None, y=None):
    """Position a single-line text; x/y go on both <text> and its first tspan."""
    for e in (text, text.find("svg:tspan", NS)):
        if x is not None:
            e.set("x", f"{x:.4f}")
        if y is not None:
            e.set("y", f"{y:.4f}")


# ---------------------------------------------------------------------------- build

def slug(heading):
    return "".join(w[:1].upper() + w[1:] for w in re.sub(r"[^A-Za-z ]", " ", heading).split())


def main():
    headings = load_headings()
    tree = ET.parse(SRC_SVG)
    root = tree.getroot()
    namedview = root.find("sodipodi:namedview", NS)
    pages = namedview.findall("inkscape:page", NS)
    layer = root.find("svg:g[@id='layer1']", NS)
    layer_m = parse_transform(layer.get("transform"))

    boxes = query_all(SRC_SVG)

    def page_of(box):
        cx, cy = box[0] + box[2] / 2, box[1] + box[3] / 2
        for i, p in enumerate(pages):
            x, y, w, h = (float(p.get(k)) for k in ("x", "y", "width", "height"))
            if x <= cx <= x + w and y <= cy <= y + h:
                return i
        return None

    # English texts: id -> (page index, English heading index, local centre-x, baseline-y, box)
    texts = {}
    parent_chain = {}

    def walk(e, m):
        for c in e:
            if c.tag not in ("{%s}g" % NS["svg"], "{%s}text" % NS["svg"]):
                continue
            cm = mat_mul(m, parse_transform(c.get("transform")))
            if c.tag == "{%s}text" % NS["svg"]:
                box = boxes[c.get("id")]
                eng = "".join(c.itertext()).strip()
                idx = [h.lower() for h in ENGLISH_ORDER].index(eng.lower())
                tsp = c.find("svg:tspan", NS)
                cx, _ = invert_point(cm, box[0] + box[2] / 2, box[1] + box[3] / 2)
                _, cy = invert_point(cm, 0, box[1] + box[3] / 2)
                texts[c.get("id")] = dict(page=page_of(box), idx=idx, cx=cx, cy=cy,
                                          baseline=float(tsp.get("y")), box=box, m=cm)
            walk(c, cm)

    walk(layer, layer_m)
    page_heading = {t["page"]: t["idx"] for t in texts.values()}
    if sorted(page_heading) != list(range(len(pages))):
        sys.exit(f"could not match every page to a heading: {page_heading}")

    # top-level layer children that belong to a page (drops the loose off-page images)
    members = [c for c in layer if c.get("id") in boxes and page_of(boxes[c.get("id")]) is not None]

    page_w = max(float(p.get("width")) for p in pages)
    col_dx = page_w + COLUMN_GAP_MM
    max_text_w = page_w - 2 * PADDING_MM

    for p in pages:
        p.set(INK_LABEL, f"{slug(ENGLISH_ORDER[page_heading[pages.index(p)]])}_ENG")

    copies = []  # (lang, text element, english text info)
    col = 0
    for lang, (code, font) in LANGUAGES.items():
        if lang == "English":
            continue
        col += 1
        suffix = "_" + code.replace("-", "")
        g = ET.SubElement(layer, "{%s}g" % NS["svg"], {
            "id": "lang" + suffix, INK_LABEL: code, "transform": f"translate({col * col_dx:.4f},0)"})
        for m in members:
            c = copy.deepcopy(m)
            g.append(c)
        # map copied text ids back to English originals before renaming
        for t in g.iter("{%s}text" % NS["svg"]):
            copies.append((lang, code, font, t, texts[t.get("id")], col * col_dx))
        rename_ids(g, suffix)
        for i, p in enumerate(pages):
            ET.SubElement(namedview, "{%s}page" % NS["inkscape"], {
                "x": f"{float(p.get('x')) + col * col_dx:.4f}", "y": p.get("y"),
                "width": p.get("width"), "height": p.get("height"),
                "id": p.get("id") + suffix, "margin": "0", "bleed": "0",
                INK_LABEL: f"{slug(ENGLISH_ORDER[page_heading[i]])}_{code}"})

    def write():
        tree.write(OUT_SVG, encoding="UTF-8", xml_declaration=True)

    # The English texts flow inside a frame (shape-inside), so their stored tspan x/y
    # aren't where they render. Render a plain clone of each at the stored x/y, measure
    # how far it lands from the real English text, and use that to recover the true
    # baseline for the copies.
    calibs = []
    for tid, info in texts.items():
        src = root.find(f".//svg:text[@id='{tid}']", NS)
        parent = next(e for e in root.iter() if src in list(e))
        c = copy.deepcopy(src)
        st = parse_style(c.get("style"))
        st.pop("shape-inside", None)
        c.set("style", style_str(st))
        c.set("id", "calib_" + tid)
        for e in c.iter():
            if e is not c and e.get("id"):
                e.set("id", "calib_" + e.get("id"))
        parent.append(c)
        calibs.append((parent, c, info))
    write()
    got = query_all(OUT_SVG)
    for parent, c, info in calibs:
        cb = got[c.get("id")]
        info["baseline"] += (info["box"][1] - cb[1]) / info["m"][3]
        parent.remove(c)

    # rewrite each copied text as a single centred line
    for lang, code, font, t, info, dx in copies:
        st = parse_style(t.get("style"))
        st.pop("shape-inside", None)
        st["text-align"] = "center"
        st["text-anchor"] = "middle"
        st["white-space"] = "normal"
        for k, v in (FONTS[font] or {}).items():
            if v is None:
                st.pop(k, None)
            else:
                st[k] = v
        t.set("style", style_str(st))
        outer = t.find("svg:tspan", NS)
        inner = outer.find("svg:tspan", NS)
        for extra in list(outer)[1:]:
            outer.remove(extra)
        set_pos(t, x=info["cx"], y=info["baseline"])
        inner.text = display_case(lang, headings[lang][info["idx"]])

    eng_heights = sorted(t["box"][3] for t in texts.values())
    ref_h = eng_heights[len(eng_heights) // 2]

    # Fit pass: non-Cinzel fonts first matched to the English text height, then any
    # text wider than the page (minus padding) is shrunk. Everything is re-centred
    # horizontally on the English text; non-Cinzel text also vertically (Cinzel keeps
    # the English baseline).
    for it in range(6):
        write()
        got = query_all(OUT_SVG)
        changed = False
        for lang, code, font, t, info, dx in copies:
            box = got[t.get("id")]
            outer = t.find("svg:tspan", NS)
            dx_doc = (info["box"][0] + info["box"][2] / 2 + dx) - (box[0] + box[2] / 2)
            if abs(dx_doc) > 0.05:
                set_pos(t, x=float(outer.get("x")) + dx_doc / info["m"][0])
                changed = True
            st = parse_style(t.get("style"))
            size = float(st["font-size"].rstrip("px"))
            new = size
            if it == 0 and font != "cinzel":
                new = size * ref_h * HEIGHT_SCALE[font] / box[3]
            elif box[2] > max_text_w:
                new = size * max_text_w / box[2] * 0.99
            if abs(new - size) > 1e-3:
                st["font-size"] = f"{new:.4f}px"
                t.set("style", style_str(st))
                changed = True
            if font != "cinzel":
                dy_doc = (info["box"][1] + info["box"][3] / 2) - (box[1] + box[3] / 2)
                dy_local = dy_doc / info["m"][3]
                if abs(dy_doc) > 0.05:
                    set_pos(t, y=float(outer.get("y")) + dy_local)
                    changed = True
        if not changed:
            break

    write()
    got = query_all(OUT_SVG)
    print(f"{'page':34} {'width mm':>8}  text", flush=True)
    for lang, code, font, t, info, dx in sorted(copies, key=lambda c: (c[4]["idx"], c[1])):
        box = got[t.get("id")]
        off = abs((box[0] + box[2] / 2) - (info["box"][0] + info["box"][2] / 2 + dx))
        flag = "  <-- OVER" if box[2] > max_text_w + 0.1 else ""
        flag += f"  <-- off-centre {off:.1f}mm" if off > 0.3 else ""
        name = f"{slug(ENGLISH_ORDER[info['idx']])}_{code}"
        sys.stdout.buffer.write(f"{name:34} {box[2]:8.1f}  {''.join(t.itertext())}{flag}\n".encode("utf-8"))
    print(f"\nmax text width {max_text_w:.1f} mm -> {OUT_SVG.name}")


if __name__ == "__main__":
    main()
