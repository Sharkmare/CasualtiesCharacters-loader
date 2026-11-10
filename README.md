# CasualtiesCharacters

A loader for character packs in Casualties: Unknown. Packs define playable characters in JSON, with their own skin, skills, traits, stats, perks and behaviours, and the loader adds them to the pre-run character screen and to multiplayer. This download contains the loader only and adds no characters by itself. A first pack, Avali, is released separately as `CasualtiesCharacters-Avali`.

## Requirements

- Casualties: Unknown with BepInEx 5.4.23.5
- For multiplayer: a Together co-op build with extra character support, built from the source at https://github.com/Sharkmare/casualties-together (main branch, commit 23d8cd2 or later; the repository's version number reads 5.4.1, so go by the commit; its README has a Building section), installed by every player. No prebuilt download is offered.
- Singleplayer works with the loader alone. On any other Together build the characters are not available in multiplayer and everything else stays vanilla: the loader sends no messages and changes nothing there.
- Every player in a multiplayer game needs the loader and the same packs. A player without them sees the character as an Experiment and nothing breaks.

## Install

1. Close the game.
2. Unzip the release into the game folder (the one that contains `CasualtiesUnknown.exe`), so that `BepInEx/plugins/CasualtiesCharacters/CasualtiesCharacters.dll` exists.
3. Install character packs the same way.
4. Start the game. Pack characters appear after the game's own characters on the pre-run screen.

To remove it, delete the folder `BepInEx/plugins/CasualtiesCharacters`.

## Making a pack

A pack is a folder inside `BepInEx/plugins`:

```
BepInEx/plugins/MyPack/
  pack.json                  id, name, version, minLoader
  characters/hero.json       one file per character
  skins/<skin name>/Body/    optional PNG parts named like the game's sprites
```

`pack.json`:

```json
{ "id": "mypack", "name": "My pack", "version": "1.0.0", "minLoader": "0.2.0" }
```

The id is lower case letters, digits, dot, dash and underscore. Without a `pack.json` the folder name is used. A pack needs no code: traits, stats, perks and behaviours are described in the character file. Fields:

| Field | Meaning |
|---|---|
| `key` | Name unique inside the pack. The character is `<pack id>/<key>`. |
| `base` | The game character the body is cloned from. The accepted names are listed in `docs/dev/DESIGN.md`. |
| `name`, `family`, `quote`, `quirks` | Text, a string or a map of language code to string (`en` is the fallback). |
| `skills`, `multipliers` | Starting STR, RES, INT and their gain multipliers. |
| `numbers`, `flags` | Trait values such as `insulationMult`, `hungerRateMult`, `thirstRateMult`, `moveForceMult`, `staminaGainMult`, `carryMult`, `painGainMult`, `bleedGainMult`, `hearingLossGainMult`, `heatThirstMult`, `heatThreshold`, `canSweat`. |
| `stats` | Named values with `min`, `max`, `start`, `perSecond`. |
| `perks` | Named bundles of trait numbers, optionally timed (`duration` seconds). |
| `behaviours` | Rules: an event (`spawn`, `tick`, `eat`, `wake`, `hurt`, `perk`), conditions on `body.`, `stat.` or `perk.` values, and actions (`set`, `add`, `mul`, `grant`, `revoke`). |
| `skin` | Name of a folder under the pack's own `skins`. Optional: a character may only clone a base and change numbers. |
| `id` | Optional request for a slot between 3 and 254. |

A skin may be a downloaded portal zip's `Body` folder. Parts that are missing keep the base character's look.

### Slots and several packs

Each character's slot is worked out from its pack id and key alone, so two machines with the same packs agree on every slot, in any install or load order, and removing a pack leaves the others where they were unless they had shared a slot with it. If two characters ask for the same slot, the one whose `<pack id>/<key>` sorts first keeps it and the other gets the slot its key points to, or the next free one. The log lists every character with its slot and a table fingerprint built from pack ids, pack versions, keys and slots; equal packs give equal fingerprints. If a pack is installed twice the newer copy is used, a pack that needs a newer loader is skipped, and a pack that fails to load is skipped without affecting the others.

## Example characters

- **Avary (Avali)**, pack `CasualtiesCharacters-Avali`: a species character with its own skin and traits, for singleplayer and for multiplayer on a Together build with extra character support (https://github.com/Sharkmare/CU-Character-Avali).

## Known limitations

- The new menu buttons are placed by copying the last game character's button, so their position and portrait may look rough.
- Custom stats and perks are not shown on screen.
- Traits act on a player's own machine only; other players see the skin and the base body.
- A skin is only seen on machines that have its files.
- In multiplayer the characters work only with a Together co-op build that has extra character support.
- Players with different pack sets can disagree about a slot; the character then shows as an Experiment on the machine that lacks it.

## Credits

See `CREDITS.md`.

## Development

Build, test and design notes are in `docs/dev/`.
