import argparse
import hashlib
import os
import re
import shutil
import subprocess
import sys
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PLUGIN_PROJECT = os.path.join(ROOT, "src", "Plugin")
DEFAULT_DLL = os.path.join(PLUGIN_PROJECT, "bin", "CasualtiesCharacters.dll")
DEFAULT_OUTPUT_DIRECTORY = os.path.join(ROOT, "dist")
PLUGIN_SOURCE = os.path.join(PLUGIN_PROJECT, "Plugin.cs")
CREDITS_SOURCE = os.path.join(ROOT, "CREDITS.md")
PLUGIN_FOLDER = "BepInEx/plugins/CasualtiesCharacters"
PLUGIN_ENTRY = PLUGIN_FOLDER + "/CasualtiesCharacters.dll"
CREDITS_ENTRY = PLUGIN_FOLDER + "/CREDITS.md"
INSTALL_ENTRY = "INSTALL.txt"
ZIP_TIMESTAMP = (2026, 1, 1, 0, 0, 0)

PRODUCT_NAME = "CasualtiesCharacters"
BEPINEX_VERSION = "5.4.23.5"
TOGETHER_NAME = "Together co-op build with extra character support"
TOGETHER_BASE_VERSION = "5.4.1"
TOGETHER_MIN_COMMIT = "23d8cd2"
TOGETHER_URL = "https://github.com/Sharkmare/casualties-together"
AUTHOR = ""
EXAMPLE_CHARACTERS = [
    ("Avary (Avali)", "CasualtiesCharacters-Avali", "https://github.com/Sharkmare/CU-Character-Avali",
     "a species character with its own skin and traits, for singleplayer and for multiplayer on a Together build with extra character support"),
]
LICENSE_NAME = ""

INSTALL_TEMPLATE = """{product} {version}

A loader for character packs in Casualties: Unknown. It adds the packs' characters to the pre-run
character screen and in multiplayer, with their skins, skills, traits, stats, perks and behaviours.
This download contains the loader only; it adds no characters by itself.

Requirements
- Casualties: Unknown with BepInEx {bepinex}
{together_requirement}

Install
1. Close the game.
2. Unzip this file into the game folder (the one that contains CasualtiesUnknown.exe),
   so that BepInEx\\plugins\\CasualtiesCharacters\\CasualtiesCharacters.dll exists.
3. Install one or more character packs the same way. Each pack is a folder in BepInEx\\plugins
   with a pack.json and a characters folder.
4. Start the game. Every pack's characters appear after the game's own characters on the pre-run screen.
To remove it, delete the folder BepInEx\\plugins\\CasualtiesCharacters. Do not load a save made
with a pack character while the loader or that pack is missing.

Use
Pick a pack character on the pre-run screen. Singleplayer works with the loader alone. For multiplayer
every player needs the loader, the same packs (the same versions) and the Together build that
supports extra characters; the join screen Character button then lists the pack characters. On any
other Together build the characters are not available in multiplayer and everything else stays
vanilla; the loader sends no messages and changes nothing there. A player without the loader sees
the character as an Experiment and nothing breaks.
Several packs can be installed together. A character is identified by its pack id and its key,
and its slot number is worked out from that, so every machine with the same packs agrees.
The loader log lists each character with its slot and a table fingerprint; equal packs give equal
fingerprints. A pack that fails to load is skipped and the others still load.
To make a pack, see README.md. Logging is quiet by default; the plugin's .cfg file in
BepInEx\\config has a Debug option. The plugin makes no network connections.

Example characters
{examples}

Known limitations
- The new menu buttons are placed by copying the last game character's button, so their position and portrait may look rough.
- Custom stats and perks are not shown on screen.
- Traits act on a player's own machine only; other players see the skin and the base body.
- A skin is only seen on machines that have its files.
- In multiplayer the characters work only with the Together build that supports extra characters.
{credits}"""


def together_requirement(singleplayer_note):
    if TOGETHER_URL:
        text = ("- For multiplayer: a %s, built from the source at %s\n"
                "  (main branch, commit %s or later; the repository's version number reads %s, so go by the commit;\n"
                "  see the Building section of its README), installed by every player") % (TOGETHER_NAME, TOGETHER_URL, TOGETHER_MIN_COMMIT, TOGETHER_BASE_VERSION)
    else:
        text = "- For multiplayer: a %s (based on version %s), installed by every player" % (TOGETHER_NAME, TOGETHER_BASE_VERSION)
    return text + "\n  " + singleplayer_note


def examples_text():
    return "\n".join("- %s, pack %s: %s.\n  %s" % (name, pack, description, link) for name, pack, link, description in EXAMPLE_CHARACTERS)


def credits_text():
    lines = []
    if AUTHOR:
        lines.append("Made by " + AUTHOR + ".")
    if LICENSE_NAME:
        lines.append("Licence: " + LICENSE_NAME + ".")
    lines.append("Casualties: Unknown is made by its developers. BepInEx and Harmony are by their authors.")
    return "\nCredits\n" + "\n".join(lines) + "\n"


def install_text(version):
    return INSTALL_TEMPLATE.format(product=PRODUCT_NAME, version=version, bepinex=BEPINEX_VERSION,
                                   examples=examples_text(), together_requirement=together_requirement("Singleplayer works with the loader alone. With any other Together build the characters are not\n  available in multiplayer and everything else stays vanilla."), credits=credits_text())


def read_version(plugin_source=PLUGIN_SOURCE):
    with open(plugin_source, encoding="utf-8") as handle:
        match = re.search(r'public const string Version = "([^"]+)"', handle.read())
    if not match:
        raise ValueError("no Version constant in " + plugin_source)
    return match.group(1)


def package_file_name(version):
    return "%s-%s.zip" % (PRODUCT_NAME, version)


def file_md5(path):
    with open(path, "rb") as handle:
        return hashlib.md5(handle.read()).hexdigest()


def keep_previous(output_path):
    if not os.path.isfile(output_path):
        return None
    stem, extension = os.path.splitext(output_path)
    kept = "%s.previous-%s%s" % (stem, file_md5(output_path)[:8], extension)
    if not os.path.exists(kept):
        shutil.copy2(output_path, kept)
    return kept


def read_bytes(path):
    with open(path, "rb") as handle:
        return handle.read()


def entries(dll_path, version):
    return {
        INSTALL_ENTRY: install_text(version).replace("\n", "\r\n").encode("utf-8"),
        PLUGIN_ENTRY: read_bytes(dll_path),
        CREDITS_ENTRY: read_bytes(CREDITS_SOURCE),
    }


def build(dll_path, output_path, version):
    if not os.path.isfile(dll_path):
        raise FileNotFoundError("build first: dotnet build -c Release src/Plugin (missing %s)" % dll_path)
    content = entries(dll_path, version)
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    keep_previous(output_path)
    with zipfile.ZipFile(output_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for entry in sorted(content):
            info = zipfile.ZipInfo(entry, ZIP_TIMESTAMP)
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, content[entry])
    return output_path


def build_dll():
    subprocess.run(["dotnet", "build", "-c", "Release", PLUGIN_PROJECT], check=True, stdout=subprocess.DEVNULL)
    return DEFAULT_DLL


def main(argv):
    parser = argparse.ArgumentParser(description="Build the CasualtiesCharacters release package.")
    parser.add_argument("--dll", help="use this DLL instead of building one")
    parser.add_argument("--output-directory", default=DEFAULT_OUTPUT_DIRECTORY)
    arguments = parser.parse_args(argv)
    dll = arguments.dll or build_dll()
    version = read_version()
    path = build(dll, os.path.join(arguments.output_directory, package_file_name(version)), version)
    print(path)
    print("md5", file_md5(path))
    print("dll md5", file_md5(dll))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
