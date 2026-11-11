# Live checklist (not run)

Everything here needs a running game and has not been executed. Install the loader and at least one pack. Log: `BepInEx/LogOutput.log`. Set `Debug Enabled = true` in the loader's cfg for sprite names. The Avali pack has its own checklist for its character; this one covers the loader.

## Loading and slots

1. The log lists `character <slot> = <pack>/<key> (base ..., pack version ...)` for every character and one `character table fingerprint` line.
2. Install two packs; swap the order of their folder names (rename one) and restart: slots and fingerprint stay the same.
3. Two packs asking for the same slot: one keeps it, the other is logged at a derived slot.
4. Install a pack with a broken character file: one error line, the rest still load. Install a pack whose `minLoader` is newer than the loader: one warning, pack skipped.
5. Remove one of three packs: the other packs' slots do not change (unless they had shared a slot with it).

## Menu and singleplayer (launch with --mp-disable if Together is installed)

1. The pre-run screen has one extra button per character; unused slot numbers below the highest are hidden. Position and portrait are copies of the Dune button and may need adjusting.
2. Selecting a button shows the name, the family text with the quote, the quirks and the starting skills.
3. In a run the body is the base character with the pack's skin. Parts that stay unchanged have sprite names missing from the skin.
4. Traits: compare against the base character for the numbers the pack sets.
5. Save and load: the character comes back as itself; with its pack removed it does not crash.
6. Size mod: a size command still scales the character and keeps its traits.

## Multiplayer on the fork build (Together built from the `characters` branch), registration

1. Both logs show `Together registration of <slot> ok` for every character and equal fingerprints.
2. The join screen Character button cycles game characters then pack characters in slot order.
3. Each player sees the others with their skin. A machine without the pack sees an Experiment and no errors. A machine without the skin files sees the base look.
4. Different pack sets on host and client: Together logs a table warning at join; nothing crashes.
5. Traits act on the owner's machine only.
6. Respawn with the Together rule RespawnKeepSkills off: the custom starting skills come back (a postfix on `Skills.Setup` re-applies them).

## Multiplayer on a Together build without character support

Nothing here has run. The loader must be inert.

1. Log: `Together without character support found, characters are available in singleplayer only`. No `Together registration` lines.
2. Join and host a game with a custom slot selected on the pre-run screen: the player spawns as an Experiment, with no skin, no custom traits, no custom skill values and vanilla texts; nothing in the log about attaching a character.
3. Watch the network: no custom message from the loader in either direction (the host log shows no unknown message ids).
4. The three vanilla characters play exactly as without the loader.
5. Leave to the menu and start a singleplayer run (`--mp-disable`): the pack characters work again.
