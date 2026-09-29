"""Copy the English "About this game" layout onto every translated language.

    python steam_sync_layout.py "<path to storepage_<appid>_all.json>"

English is the template: its section order and the media (videos/images) in each
section are reproduced in every language that already has image headings (the
output of steam_heading_json.py), keeping that language's own body text for each
section. Writes <export>_synced.json next to the export for importing.
"""

import json
import re
import sys
from pathlib import Path

from localise_headings import ENGLISH_ORDER
from steam_heading_json import heading_block, img, steam_name

# English heading image (as uploaded) -> index into ENGLISH_ORDER
ENGLISH_HEADING_IMAGES = {
    "page_1_heading_eng": 0, "steampageheading_eng__2": 1, "steampageheading_eng__3": 2,
    "steampageheading_eng__4": 3, "steampageheading_eng__5": 4, "page_6_heading_eng": 5,
    "page_7_heading_eng": 6, "page_8_heading_eng": 7, "steampageheading_eng__7": 8,
}
BASE_HEADINGS = {steam_name(i): i for i in range(len(ENGLISH_ORDER))}

IMG_RE = re.compile(r"\[img src=&quot;\{STEAM_APP_IMAGE\}/extras/([^&]+)&quot;\]\[/img\]")
PARA_RE = re.compile(r"\[p[^\]]*\].*?\[/p\]|\[h2[^\]]*\].*?\[/h2\]", re.S)


def sections(about, heading_names):
    """Split a description into [(heading index, [media], [text paragraphs])] in page order."""
    out = []
    for block in PARA_RE.findall(about):
        names = IMG_RE.findall(block)
        heads = [n for n in names if n in heading_names]
        if heads:
            out.append((heading_names[heads[0]], [], []))
            names = [n for n in names if n not in heading_names]
            out[-1][1].extend(names)  # e.g. English puts the Elements video inside its h2
            continue
        if not out:
            continue
        out[-1][1].extend(names)
        text = IMG_RE.sub("", block)
        if re.sub(r"\[/?\w+[^\]]*\]", "", text).strip():
            out[-1][2].append(text)
    return out


def main(export_path):
    export_path = Path(export_path)
    data = json.loads(export_path.read_text(encoding="utf-8-sig"))
    langs = data["languages"]

    template = sections(langs["english"]["app[content][about]"], ENGLISH_HEADING_IMAGES)
    if sorted(s[0] for s in template) != list(range(9)):
        sys.exit(f"English: expected 9 heading images, found sections {[s[0] for s in template]}")
    print("English layout:")
    for idx, media, _ in template:
        print(f"  {ENGLISH_ORDER[idx]:28} {', '.join(media) or '-'}")
    print()

    for lang, entry in langs.items():
        about = entry["app[content][about]"]
        if lang == "english" or not about.strip():
            continue
        secs = sections(about, BASE_HEADINGS)
        body = {idx: paras for idx, _, paras in secs}
        if sorted(body) != list(range(9)):
            sys.exit(f"{lang}: expected 9 image headings, found {sorted(body)} - run steam_heading_json.py first")
        empty = [ENGLISH_ORDER[i] for i in range(9) if not body[i]]
        out = []
        for idx, media, _ in template:
            out.append(heading_block(steam_name(idx)))
            out.extend(f"[p align=&quot;center&quot;]{img(m)}[/p]" for m in media)
            out.extend(body[idx])
        entry["app[content][about]"] = "".join(out)
        print(f"{lang:11} rebuilt" + (f"  (no body text: {empty})" if empty else ""))

    out = export_path.with_name(export_path.stem + "_synced.json")
    out.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"\nimport file -> {out}")


if __name__ == "__main__":
    main(sys.argv[1])
