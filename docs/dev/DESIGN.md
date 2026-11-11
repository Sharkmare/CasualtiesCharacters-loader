# CasualtiesCharacters design

A BepInEx 5 plugin (`CasualtiesCharacters`, GUID `hive.casualties.characters`) that is a generic character loader. It adds playable characters with ids 3 and up, and knows nothing about any one character. Character mods and packs register with it, either by dropping JSON into a folder or by calling its API. The first pack is Avali (`CasualtiesAvali`), shipped separately from the loader.

Everything below names the file it was read from. Items that only a running game can settle are marked **Unverified**; the game was running when this was written, so nothing here has been executed in game.

## Split

| Part | Contents | Depends on |
|---|---|---|
| `CasualtiesCharacters` (loader) | JSON loading, registry, public API, menu entries, spawn, locale, skin applier, trait library, Together bridge | game, BepInEx, Together (soft) |
| Character pack (Avali) | `characters/*.json`, `skins/<name>/Body/*.png` or a download note, optional DLL that registers custom traits | the loader |

A pack is found in two ways, both without the loader knowing the pack's name:

1. Folder discovery: every `BepInEx/plugins/*/characters/*.json` and `BepInEx/plugins/CasualtiesCharacters/characters/*.json` is read at startup. Skin folders are looked up in the pack's own `skins/` first, then in `CasualtiesCharacters/skins/`.
2. Code: a pack plugin with `[BepInDependency("hive.casualties.characters")]` calls `CharacterApi.Register(CharacterDefinition)` and `CharacterApi.RegisterTrait(name, TraitHandler)`. The API lives in a small `CasualtiesCharacters.Api.dll` surface (pure types, no game references) so packs compile against it.

Ids: a JSON may say `"id": 3` as a request. The registry resolves clashes by ordering on the character `key` (a string such as `avali.avary`), so all machines holding the same packs give the same id. The wire never carries the key, only the id, so two clients must hold the same packs. A mismatch is detected at join by exchanging a hash of the sorted key list (Together named message, see MP); on mismatch custom ids are shown as their base character.

## Hook points in the game

| Fact | Source |
|---|---|
| Spawn: `PlayerCamera.Awake` calls `SpawnCharacter(PreRunScript.characterSelected)`; `SpawnCharacter` is a switch with 0 Experiment, 1 Milky and `_` (every other number) Dune. Id 3 would spawn Dune. | `PlayerCamera.cs:1107-1124` |
| Console `spawnchar`-style use of the same method | `ConsoleScript.cs:1182` |
| Menu: `PreRunScript.charImages`, `charInnerImages`, `charPodiums` are serialized scene lists of UI `Image`; the three buttons exist in the scene. `SelectCharacter(chr)` stores the index; `UpdateCharacterText` reads `Locale.GetOther("char{N}name")`, `char{N}desc`, `char{N}quirks` and `Skills.BaseSkills(N)`. | `PreRunScript.cs:97-101`, `341-356` |
| Lock: slot i (i not 0) is greyed and non-interactable unless PlayerPref `wonchar(i-1)` is nonzero. `Awake` disables the button, `Start` adds the unlock tooltip, `HandleCharacterSelect` greys it every frame. `characterSelected` is reset to 0 in `Awake`. | `PreRunScript.cs:143-150`, `239-247`, `316-336` |
| Win marks `wonchar<body.charType>` and the Steam achievement `CHARWIN<charType>`. | `WorldGeneration.cs:1408` |
| Skills: `Body.Awake` runs `skills = new Skills(); skills.Setup(charType)`; `Setup` reads `BaseSkills(chr)` and `BaseMultipliers(chr)`; both return zeros for any id above 2. | `Body.cs:1494-1496`, `Skills.cs:43-52`, `168-190` |
| `Body.charType` is a serialized public int. Branches on exactly 2 (Dune): `totalHappiness` ×0.66, mind wipe check, `Eat`, water contact, forced `sicknessAmount = 0`, happiness range ×1.5152, weight loss, `GetTotalInsulation` base 0.5, plus `Item.cs` (5702, 5733-5767, 6195), `CustomItemBehaviour.cs:176`. Branches on exactly 1 (Milky): `Eat`, `WakeUp` sleep penalties, `Item.cs:2965`. | `Body.cs:646, 771, 2796-2801, 3300, 3330, 3550, 3577-3607, 4410` |
| Wearables index their sprite and scale lists by `charType`: `component.sprites[charType]`, `customScale[charType]`; `Wearable.cs:20` does the same for `swap.customScale`. A `charType` of 3 or more indexes past the end (the code guards with `Count > charType`, so it silently shows the default sprite). | `Body.cs:1956-1962`, `Wearable.cs:20` |
| Voice lines: `Locale.GetCharacter(key)` indexes `currentLang.character[charType]`, an array of 3 dictionaries. A `charType` of 3 throws `ArgumentOutOfRange`. | `Locale.cs:114-129` |
| `WoundView` sets the species text and head sprites by `charType` 0, 1, 2. | `WoundView.cs:284-310` |
| Saves store `body.charType` as `character`; load sets `characterSelected = savedCharacter` and spawns through `SpawnCharacter`. | `SaveSystem.cs:209`, `PreRunScript.cs:489` |
| Insulation, hunger and thirst, encumbrance, stamina, move force. | `Body.cs:4407-4425` (`GetTotalInsulation`), `701` (`BaseHungerRate`), `789-791` (`BaseThirstRate`), `4282` (`maxEncumberance`), `552` (`staminaGainMultiplier`), `90`, `612` (`moveForce`, `actualMoveForce`) |

## The decision: base body plus a separate custom id

The traps are unsafe to meet head on: any `charType` of 3 or more breaks the voice lookup, the wound view, the wearable sprite lists and every Dune/Milky branch. So a custom character never sets `Body.charType` to its own id.

- A character has a `base` field: `experiment`, `milky` or `dune`. The body is the base prefab; `Body.charType` stays 0, 1 or 2. Voice lines, wound view, wearables and the base's vanilla quirks come with it. Choosing `base: "experiment"` gives the neutral body with no vanilla quirks; that is the default for new characters.
- The custom id lives in a `CustomCharacter` component added to the Body GameObject (holds `Definition`, `Id`, the applied skin and the trait state). Every custom trait reads that component, never `charType`. Vanilla characters have no component, so a vanilla body is untouched.
- Patches (Harmony, all prefixed by a check that the loader has a custom id in play):
  - `Skills.BaseSkills(int)` and `BaseMultipliers(int)`: prefix, if the id is a registered custom id, return that character's numbers and skip the original. This fixes the menu stat block (`PreRunScript.cs:351`) too, because the menu calls them with the custom id.
  - `PlayerCamera.SpawnCharacter(int)`: prefix. For a registered id: spawn the base prefab, attach `CustomCharacter`, re-run `body.skills.Setup` with the custom id (so skills use the custom numbers; the same re-setup Together does in `DuneSupport.ApplyToLiveBody`), apply the skin, return. Other ids fall through unchanged.
  - `Locale.GetString(str, 3)`: postfix. For keys `char<N>name`, `char<N>desc`, `char<N>quirks` with N a registered id, return the JSON text (the lookup returns the key itself when missing, `Locale.cs:98-110`, so the postfix triggers on that). Strings in JSON are per-language maps with an `en` fallback.
  - `SaveSystem` save (`SaveSystem.cs:209`): postfix writing the custom id into `saveInfo.character`, so a load goes `characterSelected = id` then our `SpawnCharacter` prefix. Without this, a saved custom character reloads as its base. **Unverified:** whether anything else reads `saveInfo.character` and chokes on 3 (grep shows only `PreRunScript.LoadRun`).
  - `WorldGeneration` win marking (`WorldGeneration.cs:1408`): postfix, if the body has `CustomCharacter` also set PlayerPref `wonchar_custom_<key>` (the vanilla `wonchar<base>` is set by the game and stays).
  - Winning with the vanilla write `wonchar<charType>` would unlock vanilla characters from a custom run. That is the same unlock the base character gives, so it is accepted and not patched.
- Menu entries: the three vanilla slots are a scene list, so new entries are cloned at runtime in a `PreRunScript.Start` postfix: for each custom character clone `charImages[2]`'s button parent, rewire its `onClick` to `SelectCharacter(id)` (a public method), append to `charImages`, `charInnerImages`, `charPodiums` so the game's per-frame colour loop covers it, and set the inner image to the character's portrait sprite (from the skin's `experimentHead.png` if present, else the base's). The lock at `PreRunScript.cs:143-150, 239-247, 316-336` reads `wonchar(i-1)`, which for index 3 is `wonchar2`; the loader's rule is `unlock` in JSON: `always` (default), `afterBase` (the base's win, `wonchar<base>`), or `like` vanilla chain. A postfix on `Start` and on `HandleCharacterSelect` re-enables the button and recolours it for `always`. **Unverified:** the layout of the cloned element (positions come from the scene's layout group or fixed anchors; needs a screenshot through the link).

## Custom stats, perks and behaviours

A character is not limited to the trait library. JSON can declare its own state and rules, and a pack's code can register handlers for anything JSON cannot say.

**Stats** are named floats that live on the `CustomCharacter` component, saved with the run (a `customStats` map next to `character` in the save JSON, written by the same `SaveSystem` postfix) and replicated by the owning machine only when the character asks (`"sync": true`). Each declares `min`, `max`, `start`, an optional `perSecond` drift, and a label. Example: Avali `molt` rises while well fed and resets on a sleep.

```
"stats": { "molt": { "min": 0, "max": 100, "start": 0, "perSecond": 0.05, "label": { "en": "Molt" } } }
```

**Perks** are named bundles of trait numbers and flags with a text, optionally gated by a condition and switched on and off at runtime (a perk can be granted by a behaviour, which is how a character earns one mid-run). A perk's `effects` use the same names as the trait library.

```
"perks": [ { "id": "coldblood", "name": { "en": "Coldblood" }, "desc": { "en": "..." },
             "active": true, "effects": { "insulationMult": 1.6 } } ]
```

**Behaviours** are rules: an event, optional conditions, and actions.

```
"behaviours": [ { "on": "tick", "when": [ { "path": "body.temperature", "op": ">", "value": 37.5 } ],
                  "do": [ { "add": "stat.molt", "value": 0.5, "perSecond": true } ] } ]
```

- Events: `spawn`, `tick` (once per second of game time, `dt` supplied), `eat`, `wake`, `hurt`, `perk` (a perk was toggled). Each maps to a Harmony postfix on the game method (`Body.Eat` `Body.cs:2787`, `Body.WakeUp` `Body.cs:3590`, `Body.Update` for tick and hurt detection by watching limb `pain` and `skinHealth`).
- Paths: `body.<field>` for any public numeric field on `Body` (read and write through a field accessor that allows only an allowlist in the config, default: hunger, thirst, stamina, energy, temperature, happiness, sicknessAmount, wetness, shock, adrenaline), `stat.<name>`, `perk.<id>` (0 or 1), `time.dt`.
- Comparisons: `>`, `>=`, `<`, `<=`, `==`, `!=`. Several `when` entries are ANDed.
- Actions: `set`, `add`, `mul` on a stat or an allowlisted body path, with an optional `perSecond` (multiplies the value by `dt`), `grant` or `revoke` a perk, and `say` (a voice line key spoken through `Talker`, if the base has it).
- A rule that names an unknown path or perk is rejected at load with the file and rule index in the log; the rest of the character still loads.
- Code behaviours: `CharacterApi.RegisterBehaviour(name, handler)` lets a rule or a perk call `{ "call": "myHandler" }`, and a pack DLL may attach any component to the body in `CharacterApplied`. This is the escape hatch for anything JSON cannot express (new items, new minigames, custom UI).

The rule engine is pure (`src/Core/RuleEngine.cs`): it works against an `IValueSource` that the plugin implements over `Body`, and tests implement over a dictionary. Authoritative effects follow the multiplayer rule above: rules run only on the machine that owns the body (`IsBodyLocal()`), and fields the fork already syncs (hunger, temperature) carry their results to others.

## Traits

JSON holds `skills`, `multipliers`, `numbers` (named floats) and `flags` (named bools). The loader ships a trait library; each trait is a Harmony patch or a per-body component that reads its numbers from the definition, guarded by `body.GetComponent<CustomCharacter>()`.

| Trait name | Kind | What it changes (game field) | Source |
|---|---|---|---|
| `insulationMult` | number | `Body.GetTotalInsulation()` result times n (floor 0.5 stays) | `Body.cs:4407-4425` |
| `hungerRateMult` | number | `Body.BaseHungerRate` getter times n | `Body.cs:701` |
| `thirstRateMult` | number | `Body.BaseThirstRate(float)` result times n | `Body.cs:789` |
| `moveForceMult` | number | `Body.moveForce` set once at spawn from the base value | `Body.cs:90, 612` |
| `staminaGainMult` | number | `Body.staminaGainMultiplier` | `Body.cs:552, 3553` |
| `carryMult` | number | `Body.maxEncumberance` scaled in a `Body.Update` postfix, after the game's write at `4282` | `Body.cs:307, 4282` |
| `painGainMult`, `bleedGainMult` | number | per-limb `Limb.pain` and `Limb.bleedAmount` gains damped or amplified frame to frame (the loader's own method, `TraitMath.DampenGain`) | `Limb.cs:73, 78` |
| `hearingLossGainMult` | number | `Body.hearingLoss` gain scaled the same way | `Body.cs:317` |
| `heatThirstMult` | number | extra thirst drain while `Body.temperature` is above `heatThreshold` (default 37.5, the sweat threshold at `Body.cs:3987`) | `Body.cs:193, 3987` |
| `canSweat` | flag | `Body.canSweat` | `Body.cs:538` |
| `limbInfectionMult`, `limbHealMult` | number | `Limb.infectionSpeedMult` set at spawn; heal via skill-neutral `Limb.starvationHealthLossMult` | `Limb.cs:106, 116` |
| `lightBones` | flag | placeholder name for the pack's own handler, see below | |

Packs add traits with `CharacterApi.RegisterTrait(name, handler)`; a handler gets the `Body`, the `CustomCharacter` and its numbers/flags and may attach components. Unknown trait names in JSON are logged once and ignored (a typo never crashes).

Everything pure (parsing, clamping, gain damping, the id allocation, skin name matching) lives in `src/Core` with no Unity references so it runs in an offline console test.

## Skins

Source format (ChangeSkin, read from `05126619z/ChangeSkin`: `SkinLoader.cs`, `Utils.cs`, `ChangeBody.cs`): a folder `Body/` with 33 PNGs named `experimentTail`, `experimentFoot`, `experimentUpTorso`, `experimentUpArm`, `experimentThigh`, `experimentDownTorso`, `experimentDownArm`, `experimentCrus`, `experimentHandB`, `experimentHandF`, `experimentNosebleed`, `experimentHead`, `experimentHeadBack`, `experimentHeadBackMouth`, `experimentHeadBackMouthMini`, `experimentHeadDisfigured1/2/3` and their `Healed` variants, and 12 eye states (`experimentEyeOpen`, `Closed`, `HalfClosed`, `HalfClosedBack`, `Sad`, `SadBack`, `Scared`, `ScaredBack`, `Panic`, `LookBack`, `Gone`, `GoneHealed`). Each becomes a sprite at 8 pixels per unit, pivot centre, point filter, named like the file; a renderer is swapped when its current sprite name equals the file name. No dimensions are stated, so a skin must use the original part sizes. The repo states no licence.

Our applier is written fresh and reads the same folder layout so a portal zip can be dropped into `skins/<name>/Body/`:

- Parts are loaded individually, never all-or-nothing (the source fails the whole skin on one missing file; ours does not).
- For every `SpriteRenderer` under the body, if its current sprite name is a skin part name and that part exists, swap. The result is logged once per body as `matched n of 33, missing: ...`.
- Missing parts keep the base character's sprite.
- The original sprite is stored per renderer so a body can be restored.
- Re-applied when the game replaces a renderer's sprite (the eye and mouth sprites change with expression, `FacialExpression` writes them), so the applier checks in `LateUpdate` for a renderer whose sprite is not ours and whose original name is in the skin, instead of the source's coroutine per frame.
- The portal zip has an extra top folder in some uploads. The loader looks for `Body/` at depth 0 or 1.
- **Unverified:** that the game's sprite names are exactly `experiment*` on the Milky and Dune prefabs (the skin names say Experiment). The loader logs the sprite names it sees on the base prefab so a mismatch is visible; Experiment is the default base for that reason.

Placeholder skin idea: a recolour of the Experiment parts, read from a local install into an ignored folder, never committed. Not built: the real Avary skin replaced it.

## Multiplayer through Together

What the fork does today (`C:\Hive\casualties-together`, commits `f2369ec`, `5f20394`, `48260f7`):

- `SyncBody.characterSelected` is a `SyncVar<byte>`, encoded `type + 1` (`CharacterChoice.Encode`, `Net/ScavLiteEntitySystem.cs:138`); 0 is unset.
- `CharacterChoice.TryDecode` rejected any value above a constant highest character id of 2, and the client report `SERVER_ReportCharacter` was rejected above that too (before the registration hook existed).
- A remote body is built from `CharacterPrefabs.For(type)`, which indexes a three-entry table and otherwise falls back to `ServerMain.CHARACTER_PREFAB` (`Extras/CharacterPrefabs.cs`, `Content/NetBody.cs:1138-1150`). `ApplyBeforeAwake` and `ApplyToLiveBody` write the id straight to `Body.charType` (`DuneSupport.cs:7-26`).
- A client with an id it cannot decode uses the default prefab and does not throw. That is the safe fallback for a client without the loader.

The gap, listed exactly (requested from the Together maintainers; nothing is patched from this plugin):

1. `Together.CharacterRegistry.Register(int id, int baseCharType)` for ids 3 to 254, and the `HighestCharacter` checks use the highest registered id.
2. The three apply points use the base type for `charType`, `skills.Setup` and the prefab.
3. A `CharacterApplied(Body, int id)` event after an id lands on a body (local spawn, remote creation, replicated change).
4. Unknown id on a client without the loader: keep today's behaviour (reject, default prefab).
5. Optional label hook for the join-screen button.

Loader side, once that exists: soft dependency via `Chainloader.PluginInfos.ContainsKey("CasualtiesMP")` and a separate bridge class (the same shape as CasualtiesSize, `size-mod/DESIGN.md`), so singleplayer loads with no Together DLL. The bridge registers every character at startup and, on `CharacterApplied`, attaches `CustomCharacter` and the skin. A remote body therefore gets the same base prefab, skin (if that client has the skin files) and traits on every machine. Trait effects that must be authoritative (damage taken) run on the machine that owns the body; the fork already gates the same pattern with `IsBodyLocal()` (`DuneSupport.cs`).

CasualtiesSize multiplies on top: it works on `Body` limbs, `heightMultiplier` and colliders (`size-mod/DESIGN.md`), none of which the loader touches, and the loader's `moveForceMult` and `carryMult` act on different fields than the size rules. **Unverified:** the pair works in game; test in the pair rig.

## Avali pack (first character)

Source of the lore: the Avali are the fan species from Starbound (Avali Empire and the Avali Chronicles): a small, feathered, bird-like people, about a third the mass of a human, from a cold world; digitigrade; very fast metabolism; fragile, light bones; sensitive hearing; strong in tool use and engineering, weak in raw strength; poorly adapted to heat. "Avary" by Paili-16 is a Scav Prototype skin of that kind (the source links were garbled and are unconfirmed; the skin was later taken from portal id 200).

Game balance reference: Experiment is 9/8/7 with x1 gain; Milky is 4/6/12 with 0.5/0.5/1; Dune is 17/18/0 with 1/1/0.5 (`Skills.cs:168-190`). Avali sits between Milky and Experiment on skills and gets a body-level profile that costs it in a fight and pays it in cold and speed.

`characters/avali_avary.json` values (tunable):

| Field | Value | Reason |
|---|---|---|
| base | `experiment` | neutral body, no Dune or Milky vanilla branch |
| STR / RES / INT | 5 / 6 / 11 | light frame, good with machines; above Milky on STR/RES |
| multipliers | 0.8 / 0.8 / 1.25 | learns tools fast, grows in strength slowly |
| `insulationMult` | 1.6 | cold-world feathers; cold is the Avali home; helps the deep frozen layers |
| `heatThirstMult` | 1.8 (above 37.5) | poor heat tolerance |
| `canSweat` | false | feathers, no sweat; heat is a real hazard |
| `hungerRateMult` | 1.3 | fast metabolism |
| `thirstRateMult` | 1.15 | same |
| `moveForceMult` | 1.10 | light, quick |
| `staminaGainMult` | 1.15 | small frame recovers breath quickly |
| `carryMult` | 0.75 | small body carries less (this is where the size plugin's `size` command stays separate) |
| `painGainMult` | 1.25 | fragile |
| `bleedGainMult` | 1.2 | small blood volume; **Unverified** whether `Body.bloodVolume` would be a better field |
| `hearingLossGainMult` | 1.5 | sensitive ears; explosions and guns hurt more |

Net play: fast, good at cold depths, learns INT quickly; poor in fights, hungry, thirsty, drops its pack sooner, and bad in the geothermal layers. It is a different character, not a better one.

## Debug and defaults

Debug logging and any overlay are off by default (`Debug.Enabled=false` in the BepInEx config). The skin match report is one line per body at `Info`; the sprite-name dump is `Debug` only. The loader opens no socket and makes no network call. The portal is never contacted by the plugin.

## Proof plan

| Claim | How it is checked | Where |
|---|---|---|
| JSON parses, defaults fill, bad fields reject, ids are stable | offline unit tests | `tests/CoreTests` |
| Skin name matching and the matched/missing report | offline unit tests over name lists | `tests/CoreTests` |
| Trait maths (damp gain, clamps, multipliers) | offline unit tests | `tests/CoreTests` |
| A custom character spawns, has the custom skills, skin and traits | rig test through the link plugin (`get`/`set`), singleplayer, `--mp-disable` | needs a free game |
| The menu shows the entry and its text | link screenshot and read of `charSelectName` | same |
| Replication through Together | pair rig, host plus client | same, after the Together hook |
| The release package contains the plugin, one character file, credits and only the Avary skin (full variant) | package test | `tests/package` |

## As built (loader 0.2.0)

This section is the current state; where it differs from the text above, this wins.

- Slot 3 is requested by the Avary character in the Avali pack (`"id": 3`); the slot rules are in the next section.
- Perk `effects` are added to the character's base number (`Trait(name, neutral)` in `CustomCharacter.cs`); a perk with `duration` switches itself off after that many seconds.
- Together: the registration hook exists as `Together.TogetherCharacters` (package 5.4.1-dune.3): `Register(byte id, string displayName, byte baseCharacter)`, events `BodyBuilt(Body, byte, ScavPlayer)` and `CharacterChanged(ScavPlayer, byte, byte)`, `TableFingerprint`. The bridge in `ForkBridge.cs` uses it and is only touched when the `CasualtiesMP` plugin is loaded. The five-item gap list above is therefore closed; Together builds the body from the base prefab, and this plugin's `PlayerCamera.SpawnCharacter` patch does the same in singleplayer.
- A postfix on `Skills.Setup` re-applies a custom character's skills when Together resets skills on respawn (`Util.Body_ResetSkills` calls `Setup(charType)` with the base id).
- Only `unlock: always` is enforced. `afterBase` is parsed and ignored.
- Only the local player's body runs rules, stat drift, gain damping and the carry limit (`CustomCharacter.IsLocal`); insulation, hunger and thirst patches act on any body that has the component, which is harmless on remote bodies because those fields are not simulated there.
- Custom stats and perks have no on-screen display. The menu portrait is the cloned Dune slot's.
- The release package holds the loader, its install text and credits only; third party skins ship with the packs that use them.
- Checked offline: core tests (`tests/CoreTests`, 37), the slot and catalog rules with a mutation check (`tools/mutation_check.py`), every Harmony target and the save site (`tests/PatchTargets`), the package contents (`tests/package`). Not run: anything in a live game (`docs/dev/LIVE_CHECKLIST.md`).

### Packs, slots and conflicts (loader 0.2.0)

The Avali pack no longer lives in this repository: it is the repository `CU Character Avali` (pack id `avali`, character `avali/avary`), released as `CasualtiesCharacters-Avali`. The Avali design section above describes that pack's character. This repository ships the loader and holds only a sample pack (`tests/fixtures/samplepack`) for the tests.

- Pack discovery: every folder directly under `BepInEx/plugins` that has a `characters` folder is a pack; `pack.json` gives `id`, `name`, `version`, `minLoader` (`PackManifest`). Without it the sanitised folder name is the id. The loader's own folder is the pack `local`.
- Identity: a character is `<pack id>/<key>` (`CharacterDefinition.FullKey`). Two packs may use the same key; texts, skins and slots stay separate. A key declared twice inside one pack keeps the first file by name.
- Skins are namespaced by pack: a skin is only looked up in the owning pack's `skins/` folder (`CharacterDefinition.SkinPath`), so two packs can use the same skin folder name. The old shared `CasualtiesCharacters/skins` fallback is gone.
- Locale: `char<N>name/desc/quirks` resolve through the slot, and each slot holds the text of exactly one character, so packs cannot overwrite each other's text.
- Slots (`IdAllocator`): decision is to derive slots purely from keys, with no id map file. A persisted map would be per machine and would let a host and a client disagree after different install histories, which is the failure the fingerprint exists to catch. Rule: sort all characters by `FullKey` (ordinal); first, requested ids (`id` field) are granted in that order, a clash leaves the later key without one; then each remaining key takes slot `3 + FNV1a(FullKey) mod 252`, probing forward past taken slots. The result does not depend on folder order, plugin load order or file order. Removing a pack moves another character only if it had probed past a slot the removed pack held; adding a pack can only displace characters that sort after it and collide with it.
- Saves store the slot number. A save made with a pack character loads as that slot, so removing the pack, or a collision change from installing another pack, can make an old save show a different character. This is not mitigated; the save would have to store the `FullKey` too.
- Fingerprint (`IdAllocator.Fingerprint`): SHA-256 over lines `packId@packVersion|key|slot|base` ordered by slot, first 6 bytes. It is logged at startup. The loader does not exchange it; Together compares its own table (slot, name, base) at join and warns on a difference. Players with different pack sets get a Together warning and the character falls back to Experiment on the machine that lacks the slot.
- Failure isolation (`CharacterCatalog`): a pack with a bad manifest, a duplicate id (newer version wins, older logged), a `minLoader` above the loader version, or an exception while reading is skipped as a whole; a bad character file is skipped alone. Each problem is logged once.
- A character may have no skin and no stats: `base` plus `numbers` is enough.
- `tools/PackCheck` runs the same catalog over pack folders and fails on any warning or missing skin part; pack repositories call it from their tests.
- `tools/mutation_check.py` breaks six rules in the catalog code one at a time and requires the core tests to fail each time.

### Together builds and multiplayer (loader 0.2.0)

Decision: custom characters never meet a Together build without character support. In multiplayer the loader is active only when `Together.TogetherCharacters` is present (checked once at startup by `TogetherLink`, one reflection probe, no traffic). On any other Together build, with a host that does not run the fork, or whenever the character API is absent while a network game is running, the loader is inert: it registers nothing, sends nothing (not even a hello), applies no skin, traits, `charType` or locale, and a selected custom slot spawns as Experiment. Singleplayer, and any time the network is not running, is fully on.

- `MultiplayerPolicy` (pure, tested): `CharactersActive(multiplayerRunning, characterApiPresent)` and `SpawnSlot(requested, active)`. `CharacterGate` in the plugin feeds it the API probe and a reflected `Together.Net.IsRunning` getter; if Together is present but the running flag cannot be read, the gate fails closed (treats the game as multiplayer).
- Every patch that changes behaviour checks `CharacterGate.Active`: starting skills and multipliers, locale text, spawn, insulation, hunger, thirst, the per frame update, wake and eat events, the save hook and `Skills.Setup` re-application, plus `CustomCharacter.Attach`.
- Host flavour: the loader does not probe the host. It relies on the fork rejecting hosts of another Together build at connection time (stated by the Together maintainers, not checked here); with a host that is not the fork there is no fork client, so no characters. If that assumption is wrong, a fork client on a non-fork host would still have its own character locally and nothing is sent.
- The message layer written earlier for the standard build (hello, welcome, roster, apply table, gated channel) was removed; its history is in git. `tests/inert` fails if any source file or the built plugin contains a message-sending member again (`CreateNamedWriter`, receiver registration, `Client_Send`, `Server_SendTo`, `NetDataWriter`), and the probe test fails if `TogetherLink` probes anything besides the character API.
- Multiplayer with the fork uses `ForkBridge`: registration of every slot with `TogetherCharacters.Register`, `BodyBuilt` and `CharacterChanged` to attach characters.
