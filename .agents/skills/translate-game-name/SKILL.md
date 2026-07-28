---
name: translate-game-name
description: Use when localizing the game title or core terminology into different languages, ensuring context, gaming terminology, and cultural resonance are handled correctly.
---
# Translate Game Name / Core Lexicon

This skill provides the systematic approach for Antigravity when translating a game title, character classes, or core game mechanics into foreign languages for the Steam Store or in-game UI.

## 1. Context Disambiguation
Before translating any term, explicitly break down the English words to identify their intended meaning:
- **Part of Speech:** Is it a noun or a verb? (e.g., "Hold" = a physical fortress, OR "Hold" = the action of gripping a weapon?).
- **Gaming Context:** "Wave-based" means enemy hordes, not ocean waves. "Hit-stop" is an animation freeze, not a physical stop sign. 
- **Tone:** Is the game dark fantasy, sci-fi, casual, or retro? The vocabulary chosen (e.g., ancient Kanji vs modern Katakana) must match this tone.

## 2. Gaming Lexicon & Cultural Nuance
- **Avoid Literal Machine Translations:** Never do a 1:1 direct dictionary translation. Use established gaming terminology native to that region.
- **Transliteration vs Translation:** In some markets (like Japan), English fantasy names are traditionally transliterated into phonetic alphabets (Katakana) rather than translated into native words (Kanji). Always evaluate if a phonetic approach fits the genre better.

## 3. Options Generation
Always provide the user with 3-4 categorized options to choose from, breaking down the exact meaning of each:
- **The Worldbuilding/Lore Translation:** Translates the exact meaning of the location or object in the game world.
- **The Action/Vibe Translation:** Translates the *feeling* of the gameplay (e.g., surviving, slashing, defending).
- **The Phonetic Transliteration:** (e.g., Katakana in Japanese) spelling out the English name phonetically for brand consistency.

## 4. IP / Collision Check
- **Mandatory Check:** Always perform a web search (`search_web`) to verify the suggested translation isn't already a registered trademark, a well-known existing game, or a slang term with negative connotations in that specific region.
