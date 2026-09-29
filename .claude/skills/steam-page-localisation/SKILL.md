---
name: steam-page-localisation
description: Use when localising Bladehold's Steam store page or its marketing art — translated heading banners in Inkscape (copy pages per language, swap fonts, fit text), the Steamworks localisation JSON export/import, per-language image uploads, syncing translated "About this game" layouts to English, or using the Inkscape MCP server.
---

# Steam page localisation

Scripts are in `scripts/` next to this file and use only the standard library. Run them from the repo root with `PYTHONIOENCODING=utf-8`, because the Windows console can't print CJK. All paths are resolved from the repo root, so they work from any cwd.

| File | Role |
|---|---|
| `Marketing Media/Steam Page Localisation - Sheet1.csv` | Translations. Row `About this game` holds each language's headings and body text (heading, body, heading, body…) |
| `Marketing Media/Inkscape/Steam page headings.svg` | English heading banners, 9 pages. **Source: never write to it** |
| `Marketing Media/Inkscape/Steam page headings - localised.svg` | Generated: 99 pages, one column per language |
| `Marketing Media/Inkscape/SteamPageHeadings/` | Lance's Inkscape batch export (filenames come from page labels, e.g. `CombatYourWay_FR.png`) |
| `Steam Store/storepage_1330478_all.json` | Steamworks localisation export (Store Page → Localization → export JSON, all languages) |

## Pipeline

1. **Banners.** Run `python ".claude/skills/steam-page-localisation/scripts/localise_headings.py"` to regenerate the localised SVG and print each heading's width.
   - It fails loudly if a German heading is missing from `GERMAN_CASE`, or a language doesn't yield 9 headings.
   - Render a preview, check it visually, then hand off. Lance opens the file and runs Export → Batch export → Pages.
2. **Headings → images in the Steam export.** Run `python ".../steam_heading_json.py" "Steam Store/storepage_1330478_all.json"`. It writes:
   - `heading_uploads/`: 90 PNGs to upload.
   - `<export>_headings.json`: the file to import.

   Run this on an export whose translations still have **text** headings.
3. **Layout sync.** After Lance rearranges English or adds media, he re-exports. Run `python ".../steam_sync_layout.py" "<export>"` to write `<export>_synced.json`.
   - It rebuilds every translation on English's section order and media, keeping each language's own body text.
   - Afterwards verify that no paragraph was lost, and that only `about` fields changed.
4. **Steamworks.** Lance uploads images and imports the JSON himself. Never drive the live Steamworks page unless explicitly asked.

## How Steam handles localised images (verified by Lance)

- An uploaded image is referenced as `{STEAM_APP_IMAGE}/extras/<filename lowercased, no extension>`.
- Upload `<base>_<web API code>.png` and Steam **groups** it under `<base>`. Embed only `<base>` in every language, and each viewer gets their language's version. Embedding `<base>_fr` gives "Missing image".
- Web API codes: `fr de it es ja ko ru pt pt-BR zh-CN` (`pt` = European Portuguese). Underscore variants are *not* recognised: `_pt_br` and `_zh_cn` stay separate images, and `_pt_pt` becomes the wrong base `…_pt`.
- `pt-BR` and `zh-CN` grouping was untested when this skill was written. If they don't group, try Steam's long language names (`_brazilian`, `_schinese`).
- English's heading images have legacy names (`page_1_heading_eng`, `steampageheading_eng__2`…) mapped in `steam_sync_layout.ENGLISH_HEADING_IMAGES`. If Lance replaces one, update that map; the script exits rather than guess.
- The export uses `&quot;` inside BBCode. Keep that encoding when rewriting, and write compact JSON (`ensure_ascii=False`).

## Translation-data quirks (handled in code, keep in mind when the CSV changes)

- **Spanish** sections run in a different order from English: see `SECTION_ORDER`. Always match headings by meaning or text, never by position.
- **Korean** body text is restructured, so its headings are hard-coded in `HEADING_OVERRIDES`. Line 1 of Lance's list was the translator's name (김민재), not a heading. `방어 전략 구성` ("setting up your defence strategy") is used for Game Defining Decisions; flagged to Lance.
- **Korean in the Steam export:**
  - Five headings are plain `[p]` paragraphs.
  - Some `[h2 align="center"] [/h2]` tags are empty spacers, which get removed.
  - One Brazilian heading has no `[b]`.
- **Russian** has invisible `\u200e` marks and empty lines: strip them before matching.
- **Casing:** the CSV is mostly ALL CAPS.
  - English stays title case.
  - Other Latin and Cyrillic languages use sentence case. Cinzel and Cormorant SC draw lower case as small caps, so this matches the English look.
  - German capitalises nouns, so its headings come from `GERMAN_CASE`.
  - Trailing full stops are dropped.

## Inkscape SVG pitfalls (all hit while building `localise_headings.py`)

- **Frame-flowed text:** the English texts use `shape-inside:url(#rect…)`, so their stored tspan `x`/`y` are **not** where they render. Calibrate by rendering a clone without the frame and measuring the offset against the real bounding box.
- **Line tspans:** never set `sodipodi:role="line"` on a tspan you position yourself. Inkscape re-positions line tspans from the parent `<text>`'s x/y (0,0 if unset), and the text vanishes off-page. Put x/y on both `<text>` and the tspan.
- **Pretty-printing:** never call `ET.indent`. With `xml:space="preserve"`, the added whitespace renders and bloats every text's width.
- **ElementTree namespaces:** register them (`""`, `inkscape`, `sodipodi`, `xlink`) before writing, or the output gets `ns0:` prefixes. `lxml` is not installed.
- **Walking the tree:** only compose transforms through `<g>` and `<text>`. Images carry `rotate()`, which the small matrix parser rejects.
- **Measuring:** `inkscape.exe "<svg>" --query-all` prints `id,x,y,w,h` in px (96 dpi) for every object. That is the fit and centre loop's ground truth. Divide by 96/25.4 to get mm (the document's user units).
- **Previews:** `--export-area=x0:y0:x1:y1` (in px) with `--export-background="#2a2a2a" --export-background-opacity=1`. The text is white, and `--export-area-drawing` includes far-off-page junk images.
- **Fonts:** the document uses Cinzel with `font-variation-settings:'wght' 700`. The added fonts are:
  - Noto Serif SC/JP/KR, variable, `'wght' 700`.
  - Cormorant SC Bold (Russian; Cinzel has no Cyrillic).

  They're installed per-user in `%LOCALAPPDATA%\Microsoft\Windows\Fonts` and registered under `HKCU\...\Fonts`, which Inkscape picks up. Inkscape renders with installed fonts at export time. On another machine, install them or convert the text to outlines first.
- **Fitting:** each heading is at most page width − 2 × `PADDING_MM` (10 mm). Non-Cinzel fonts are sized to the median English ink height × `HEIGHT_SCALE`: 1.15 for CJK, because CJK glyphs read small next to Cinzel capitals.

## Inkscape MCP (`inkscape_mcp`)

- **Registration:** user scope, `uvx inkscape_mcp`, with `INKSCAPE_BIN=C:\Program Files\Inkscape\bin\inkscape.exe`. The installed version is Inkscape 1.4.2; upstream tests against 1.4.4.
- **Re-registering:** use the Bash tool, because PowerShell eats the `--` in `claude mcp add … -- uvx inkscape_mcp`. The first `uvx` run downloads about 85 packages and times out the 30 s connection check; run `uvx inkscape_mcp --help` once to warm the cache.
- **Tools:**
  - `inkscape_file`, `inkscape_vector` (e.g. `text_to_path`), `inkscape_analysis` (reads text content and font properties).
  - `inkscape_gradient`, `inkscape_metadata`, `inkscape_system`, `inkscape_extension`.
  - `inkscape_live`: `edit_xml` with `set_text`, `append` and similar.
- **Headless limits:** `text_on_path`, `lpe_paste` and mesh gradients need the live GUI. Live mode on Windows needs MSYS2 plus `pacman -S mingw-w64-x86_64-dbus`, which is **not installed**.
- **When to use it:** for bulk or repeatable document changes, prefer a script that edits the SVG plus the Inkscape CLI (`--query-all`, `--export-*`), as above. It can be re-run and diffed. Use the MCP for one-off inspection, text-to-path or exports.

## Verify before handing off

- The script output shows no `OVER` or `off-centre` flags.
- Preview renders of every language column have been checked by eye, including CJK size parity with the English banners.
- For Steam JSON:
  - Only `app[content][about]` changed per language.
  - Every heading reference is a base name with a matching upload.
  - No body paragraph was lost (compare paragraph sets before and after).
