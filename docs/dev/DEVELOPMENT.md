# Development

Design and source citations: `DESIGN.md`. What needs a running game and has not been run: `LIVE_CHECKLIST.md`. Character content lives in pack repositories (for example `CU Character Avali`); this repository is the loader.

## Build

```
dotnet build -c Release src/Plugin
```

Needs the game install (`GameDir`, default the Steam path) for the game, BepInEx and Newtonsoft assemblies, and `.refs/Together/` holding `CasualtiesMP.dll`, `LiteNetLib.dll` and `LiteEntitySystem.dll` from a Together 5.4.1-dune.3 package (`TogetherDir` overrides the path). `.refs` is not in the repository. The build writes no debug symbols so the binary carries no local paths. The loader version is the `Version` constant in `src/Plugin/Plugin.cs`; packs name the oldest loader they accept in `pack.json`.

## Test (offline, no game launch)

```
cd tests/CoreTests && dotnet run -c Release
cd tests/PatchTargets && dotnet run -c Release
python -I -m unittest discover -s tests/package
python -I -m unittest discover -s tests/inert
python -I -m unittest discover -s tests/commitcheck
python tools/mutation_check.py
```

- `CoreTests`: parser, rule engine, trait maths, skin matching, slot allocation and the multi-pack catalog (same slots in any pack order, same requested slot in two packs, same key in two packs, same skin folder name in two packs, removal stability, fingerprint inputs, duplicate packs, minimum loader version, broken files and packs isolated, base-only characters). The fixtures are `tests/fixtures/samplepack`.
- `mutation_check.py`: breaks eight catalog and multiplayer rules one at a time and requires the core tests to fail each time. Run it after changing `IdAllocator`, `CharacterCatalog` or `Definition`.
- `MultiplayerPolicyTests`: singleplayer always on; multiplayer on only with the character API; a custom slot becomes Experiment when inactive.
- `tests/inert` (python): fails if any source file or the built plugin has a member that sends a Together message, if the probe grows beyond the character API, or if the removed stock message layer reappears; the checker has a self-test that proves it can fail.
- `PatchTargets`: every Harmony target exists in the installed game assemblies with matching parameter names, and the save site matches once. Build the plugin first.
- `package`: exact contents of the zip (loader, install text, credits; no character, no art), hashes after unzipping, banned wording in every shipped name and text and in the built binary's strings, previous zips kept.
- `commitcheck`: the commit message rules.

## Check a pack

```
dotnet run -c Release --project tools/PackCheck -- <pack folder> [<pack folder> ...]
```

Prints each character with its slot and the table fingerprint, and exits 1 on any parser warning or error, a missing skin folder or a missing skin part.

## Package

```
python tools/build_package.py
```

The zip goes to `dist/` named `CasualtiesCharacters-<version>.zip`; an existing zip is copied to `*.previous-<md5>.zip` before it is replaced. The constants at the top of `tools/build_package.py` (`TOGETHER_MIN_VERSION`, `TOGETHER_URL`, `AUTHOR`, `LICENSE_NAME`) feed `INSTALL.txt`; the URL, author and licence lines are omitted while empty.

## Commits

`git config core.hooksPath .githooks` turns on the message check: one subject line of at most 72 characters, no ticket keys, none of the internal words.
