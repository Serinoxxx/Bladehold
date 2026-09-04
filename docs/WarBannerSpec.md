# Specification & Implementation Prompt: War Banner Selection System

## Overview & Objective
Replace the legacy random end-of-wave reward drop table with an interactive, in-world **War Banner Selection System**.

Between combat waves, the game will spawn 3 physical War Banners along the perimeter of the fortress. Approaching a banner displays a contextual HUD element revealing the clan's sigil, the enemy wave modifier (buff), and the bounty (reward) granted upon completing the wave. Pressing **[E]** tears down the targeted banner, immediately despawns the other two, triggers an angry enemy voice bark, and spawns the incoming wave carrying that specific modifier.

## 1. System Refactor & Removals
* **Remove:** Legacy end-of-wave random reward loot rolling.
* **Remove:** Automatic / instant draft prompts popping up immediately after standard waves.
* **Retain:** Fixed Rest Areas occurring strictly every 3 waves (Waves 3, 6, 9, etc.) containing the Well, Shop, and baseline Rest Area drafts. Banners do not spawn preceding a Rest Area; they spawn only before standard combat waves.

## 2. In-World Banner Entity Specification

### Visuals & Interaction
* **Entity Name:** `BP_WarBanner` / `WarBannerController`
* **Spawn Locations:** 3 designated anchor points outside the fortress gate / perimeter.
* **Interaction Trigger:** Proximity trigger sphere (`Radius: 2.5m`).
* **Input Action:** `Interact` (Default key: **E**).

### HUD / Tooltip Component
When the player enters the banner's trigger zone, display an in-world UI billboard:
* **Header:** `[E] Tear Down Banner`
* **Clan Name & Sigil:** Clan Name (e.g., *Ironfang Clan*)
* **Wave Modifier (Risk):** Description of the buff applied to all wave enemies (e.g., *Enemies gain +25% Max HP Shields*).
* **Bounty (Reward):** Guaranteed reward upon clearing the wave (e.g., *Weapon Upgrade Draft*).

### Selection Execution Flow
1. Player approaches Banner **A** and presses **E**.
2. Play banner teardown / destruction SFX & VFX.
3. Play enemy audio bark (e.g., *"Them's tearin down ours bannah! Get 'em!"*).
4. Instantly despawn or play a collapse animation on Banners **B** and **C**.
5. Lock the selected wave buff and wave bounty into the `WaveManager`.
6. Start a 3-second grace period timer before the next wave enemies begin spawning.

## 3. Banner Data Schema & Tables
Define a data table or scriptable object pool containing combinations of **Buff Types** and **Reward Types**.

### Wave Modifiers (Buff Pool)
| Buff ID | Clan Name | In-Game Description | Gameplay Effect |
| --- | --- | --- | --- |
| `BUFF_SHIELD` | Ironfang Clan | **Shielded Vanguard** | All enemies spawn with a shield equal to 25% of their max HP. |
| `BUFF_HASTE` | Wind-Strider Clan | **Frenzied Advance** | All enemies gain +35% movement and attack speed. |
| `BUFF_BERSERK` | Blood-Drinker Clan | **Glass Cannons** | All enemies deal +50% damage, but have -30% max HP. |
| `BUFF_REGEN` | Swarm-Blight Clan | **Flesh Mending** | Enemies passively regenerate 3% max HP per second. |
| `BUFF_ARMOR` | Stone-Hide Clan | **Thick Hide** | Enemies take 25% reduced damage from physical strikes. |

### Wave Bounties (Reward Pool)
| Reward ID | In-Game Display | Logic Executed Upon Wave Clear |
| --- | --- | --- |
| `REWARD_WEAPON_DRAFT` | **Weapon Upgrade Draft** | Triggers a 3-card draft restricted to equipped melee/ranged weapons. |
| `REWARD_FORTRESS_DRAFT` | **Fortress Upgrade Draft** | Triggers a 3-card draft for Arrow Slits, Oil, or Barricades. |
| `REWARD_ELEMENT_DRAFT` | **Elemental Upgrade Draft** | Triggers a 3-card draft for the active element (or initial element selection). |
| `REWARD_GOLD_CACHE` | **Gold Cache (75–125 Gold)** | Adds randomized Gold directly into the player's run balance. |
| `REWARD_ORCISH_METAL` | **Plundered Metal (2–3 Orcish Metal)** | Grants permanent Orcish Metal to player meta-inventory. |
| `REWARD_GOBLIN_BLOOD` | **Blood Vial (4–6 Goblin Blood)** | Grants permanent Goblin Blood to player meta-inventory. |
| `REWARD_TROLL_HEART` | **Troll Heart (+25 Max HP)** | Permanently grants +25 Max HP for the duration of the run. |

## 4. State Machine & Event Hooks

### 1. `OnWaveCompleted(int waveIndex)`
* Check if `(waveIndex + 1) % 3 == 0`.
* **True:** Route player to the **Rest Area** (Well + Shop + Fixed Draft). Do not spawn banners.
* **False:** Trigger `SpawnWarBanners()`.

### 2. `SpawnWarBanners()`
* Randomly pick 3 unique `Buffs` and 3 unique `Bounties`.
* Instantiate 3 `BP_WarBanner` actors at predefined spawn markers.
* Initialize banner parameters.
* Enable player interact input.

### 3. `OnBannerInteracted(BP_WarBanner selectedBanner)`
* Disable interact input on all banners.
* Cache `selectedBanner.buff` and `selectedBanner.bounty` to `WaveManager`.
* Despawn non-selected banners.
* Play banner sound effect and voice bark.
* Trigger `StartWaveCountdown(float delaySeconds = 3.0f)`.

### 4. `OnWaveSpawnEnemy(EnemyBase enemyInstance)`
* Query active `WaveManager.currentBuff`.
* Apply the modifier dynamically to the enemy’s stat container upon spawn.

### 5. `OnWaveVictory()`
* Query active `WaveManager.currentBounty`.
* Dispatch the selected reward to the player.
* Clear current wave modifiers.
