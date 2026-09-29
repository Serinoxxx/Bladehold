"""Swap the text headings in a Steam store-page localisation export for heading images.

    python steam_heading_json.py "<path to storepage_<appid>_all.json>"

Writes, next to the export:
  * heading_uploads/       the exported heading PNGs renamed <heading>_<web API code>
                           (e.g. combatyourway_fr, combatyourway_pt-BR) so Steam
                           groups them by language - upload all of these first
  * <export>_headings.json the export with every translated "About this game"
                           heading replaced by its language's image - import this

English is left untouched (it already uses heading images). Italian's description is
empty in the export, so it is built from the localisation CSV using the same layout
as the other languages.
"""

import html
import json
import re
import shutil
import sys
from pathlib import Path

from localise_headings import ENGLISH_ORDER, LANGUAGES, PNG_DIR, load_headings, slug

# Steam language key -> CSV column name
STEAM_LANG = {
    "schinese": "Simplified Chinese", "german": "German", "french": "French",
    "italian": "Italian", "spanish": "Spanish - Spain", "koreana": "Korean",
    "japanese": "Japanese", "portuguese": "European Portugese",
    "brazilian": "Brazilian Portuguese", "russian": "Russian",
}

# gameplay videos that follow the first four headings on every language's page
SECTION_VIDEOS = [
    "bladehold_ultimates_steampagedescription", "bladehold_elements_steampagedescription",
    "bladehold_bannersnav_steampagedescription", "bladehold_restmeta_steampagedescription",
]

Q = "&quot;"
HEADING_RE = re.compile(r"\[h2 align=&quot;center&quot;\](?:\[b\])?([^\[]*?)(?:\[/b\])?\[/h2\]")


# Steam groups uploads named <base>_<web API language code> under <base> and serves
# each viewer their language's version, so descriptions embed just the base name.
WEB_API_CODE = {
    "Simplified Chinese": "zh-CN", "German": "de", "French": "fr", "Italian": "it",
    "Spanish - Spain": "es", "Korean": "ko", "Japanese": "ja",
    "European Portugese": "pt", "Brazilian Portuguese": "pt-BR", "Russian": "ru",
}


def steam_name(heading_idx):
    return slug(ENGLISH_ORDER[heading_idx]).lower()


def upload_name(heading_idx, csv_lang):
    return f"{steam_name(heading_idx)}_{WEB_API_CODE[csv_lang]}.png"


def img(name):
    return f"[img src={Q}{{STEAM_APP_IMAGE}}/extras/{name}{Q}][/img]"


def heading_block(name):
    return f"[h2 align={Q}center{Q}]{img(name)}[/h2]"


def norm(s):
    s = html.unescape(s).replace("‎", "").replace("‏", "").replace(" ", " ")
    return s.strip().rstrip(".").strip().casefold()


def build_from_csv(csv_lang, csv_about):
    paras = [p.replace("‎", "").strip() for p in csv_about.split("\n")]
    paras = [p for p in paras if p]
    out = []
    for i in range(9):
        out.append(heading_block(steam_name(i)))
        if i < len(SECTION_VIDEOS):
            out.append(f"[p]{img(SECTION_VIDEOS[i])}[/p]")
        out.append(f"[p]{html.escape(paras[2 * i + 1], quote=True)}[/p][p][/p]")
    return "".join(out)


def main(export_path):
    export_path = Path(export_path)
    data = json.loads(export_path.read_text(encoding="utf-8-sig"))
    headings = load_headings()

    import csv
    from localise_headings import CSV_PATH
    with open(CSV_PATH, encoding="utf-8") as f:
        rows = list(csv.reader(f))
    header = [h.strip() for h in rows[0]]
    csv_about = dict(zip(header, next(r for r in rows if r[0].strip() == "About this game")))

    # 1. Steam-safe copies of the PNGs
    up = export_path.parent / "heading_uploads"
    shutil.rmtree(up, ignore_errors=True)
    up.mkdir()
    n = 0
    for csv_lang in LANGUAGES:
        if csv_lang == "English":
            continue  # English already uses its own uploaded heading images
        for i in range(9):
            src = PNG_DIR / f"{slug(ENGLISH_ORDER[i])}_{LANGUAGES[csv_lang][0]}.png"
            if not src.exists():
                sys.exit(f"missing export: {src}")
            shutil.copyfile(src, up / upload_name(i, csv_lang))
            n += 1

    # 2. rewrite descriptions
    report = []
    for steam_lang, csv_lang in STEAM_LANG.items():
        entry = data["languages"][steam_lang]
        about = entry["app[content][about]"]
        if not about.strip():
            entry["app[content][about]"] = build_from_csv(csv_lang, csv_about[csv_lang])
            report.append(f"{steam_lang:11} built from CSV (description was empty)")
            continue
        lookup = {norm(h): i for i, h in enumerate(headings[csv_lang])}
        found = []

        def swap(m):
            key = norm(m[1])
            if not key:
                return m[0]  # empty spacer heading; removed below
            if key not in lookup:
                sys.exit(f"{steam_lang}: heading {m[1]!r} not in CSV headings {headings[csv_lang]}")
            found.append(lookup[key])
            return heading_block(steam_name(lookup[key]))

        about = HEADING_RE.sub(swap, about)
        # some translations (Korean) set later headings as plain paragraphs
        about = re.sub(r"\[p\]([^\[]*?)\[/p\]",
                       lambda m: swap(m) if norm(m[1]) in lookup and lookup[norm(m[1])] not in found else m[0],
                       about)
        # drop empty centred headings left between sections
        about = re.sub(r"\[h2 align=&quot;center&quot;\]\s*\[/h2\]", "", about)
        if sorted(found) != list(range(9)):
            sys.exit(f"{steam_lang}: matched headings {found}, expected all 9 once")

        # each of the first four sections should carry its own gameplay video
        parts = re.split(r"(\[h2 align=&quot;center&quot;\].*?\[/h2\])", about)
        fixes = []
        for s in range(1, len(parts), 2):
            idx = found[s // 2]
            if idx < len(SECTION_VIDEOS):
                body = parts[s + 1]
                vids = re.findall(r"extras/(bladehold_\w+_steampagedescription)", body)
                if vids and vids[0] != SECTION_VIDEOS[idx]:
                    parts[s + 1] = body.replace(vids[0], SECTION_VIDEOS[idx], 1)
                    fixes.append(f"{ENGLISH_ORDER[idx]}: {vids[0]} -> {SECTION_VIDEOS[idx]}")
        entry["app[content][about]"] = "".join(parts)
        order = "" if found == list(range(9)) else f"  (section order {found})"
        report.append(f"{steam_lang:11} 9 headings -> images{order}" + "".join(f"\n{'':13}fixed video {f}" for f in fixes))

    out = export_path.with_name(export_path.stem + "_headings.json")
    out.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    sys.stdout.buffer.write(("\n".join(report) + f"\n\n{n} PNGs -> {up}\nimport file -> {out}\n").encode("utf-8"))


if __name__ == "__main__":
    main(sys.argv[1])
