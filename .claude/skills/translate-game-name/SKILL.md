---
name: translate-game-name
description: Use when translating the game title, store copy or core Bladehold terminology (currencies, enemy/boss names, mechanic names) into another language, or reviewing such a translation — covers disambiguation, transliteration vs translation, options to offer and a collision check.
---

# Translate the title and core terms

In-game text lives in `Assets/Bladehold/Resources/Localization/Strings.csv` (`key,context,en,fr,it,de,es,ru,zh,ja,ko`, read by `Localization/Loc.cs`). Put translations there, one row per key. Never touch the gameplay CSVs: their English is the source text and the fallback. Steam store copy lives outside the repo, so give it back in chat.

## 1. Disambiguate first

- **Part of speech and sense.** "Bladehold" is a stronghold of blades (a place), not "hold a blade". "Hold" as a keep, not a grip. "Supply" is the build currency. "Banner" is a war banner. "Wave" is an enemy horde, never water.
- **Tone:** dark fantasy, medieval, goblin hordes. Pick vocabulary to match: archaic over modern, and kanji over katakana only when it reads as lore rather than a textbook.
- Read the `context` column. Invented terms (Goblin Blood, Orcish Metal, Diamond Fish Bones) are named currencies: translate their meaning consistently wherever they appear.

## 2. Use gaming lexicon, not the dictionary

Use the terms players in that market already know (roguelite, meta-progression, ultimate, dash). Some markets transliterate fantasy names (Japanese katakana, Korean hangul) rather than translate them, so decide per term and stay consistent.

## 3. Offer options

For titles and headline terms, give Lance 3 or 4 labelled options, each with a back-translation:
- **Lore:** the literal meaning in the world ("fortress of blades").
- **Vibe:** the feel of play (holding the line, cutting down hordes).
- **Phonetic:** a transliteration for brand consistency.

## 4. Check for collisions (mandatory)

Use WebSearch on every shortlisted title or term in the target language: existing games or trademarks, and slang or negative meanings in that region. Report what you found alongside each option.
