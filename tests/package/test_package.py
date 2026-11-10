import hashlib
import os
import pathlib
import re
import subprocess
import sys
import sys
import tempfile
import unittest
import zipfile

TOOLS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools")
sys.path.insert(0, TOOLS)

import build_package as package  # noqa: E402

BANNED_NAME_PARTS = (".pdb", ".log", ".cs", ".py", "test", "debug")
TEXT_SUFFIXES = (".txt", ".md", ".json")
THIRD_PARTY_ART = (".png", ".jpg", ".jpeg", ".gif", ".bmp")
EXPECTED_ENTRIES = {package.INSTALL_ENTRY, package.PLUGIN_ENTRY, package.CREDITS_ENTRY}
VERSION = "9.9.9"


ALLOWED_LINK = "https://github.com/Sharkmare/casualties-together"
LICENCE_WORDING = r"licen[cs]e|reuse"


def foreign_repository_text(text):
    without_allowed = re.sub(re.escape(ALLOWED_LINK) + r"(?![\w/.-])", "", text)
    return re.findall(r"github[^\s]*", without_allowed, flags=re.IGNORECASE)


def unexpected_urls(text):
    found = [url.rstrip(".,;)") for url in re.findall(r"https?://[^\s`\"']+", text)]
    return [url for url in found if url != ALLOWED_LINK]


def licence_wording(text):
    return re.findall(LICENCE_WORDING, text, flags=re.IGNORECASE)


def build_suffixes(text):
    return re.findall(r"\d\+\d+", text)


def private_script():
    override = os.environ.get("CU_RELEASE_RULES")
    candidates = [pathlib.Path(override)] if override else []
    candidates += [sibling / "release_rules" / "banned_words.txt" for sibling in pathlib.Path(__file__).resolve().parents[3].iterdir()]
    for candidate in candidates:
        rules = candidate / "banned_words.txt" if candidate.is_dir() else candidate
        script = rules.parent.parent / "tools" / "check_release_text.py"
        if rules.is_file() and script.is_file():
            return script
    return None


def private_wording_problems(target):
    script = private_script()
    if script is None:
        print("internal wording check skipped (private rules not found)")
        return []
    result = subprocess.run([sys.executable, "-I", str(script), str(target)], capture_output=True, text=True)
    return [] if result.returncode == 0 else result.stdout.splitlines()


def sha256(data):
    return hashlib.sha256(data).hexdigest()


class PackageTests(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.workdir = tempfile.TemporaryDirectory()
        cls.dll = os.path.join(cls.workdir.name, "CasualtiesCharacters.dll")
        with open(cls.dll, "wb") as handle:
            handle.write(os.urandom(2048))
        cls.zip_path = os.path.join(cls.workdir.name, "out", package.package_file_name(VERSION))
        package.build(cls.dll, cls.zip_path, VERSION)
        with zipfile.ZipFile(cls.zip_path) as archive:
            cls.entries = {name: archive.read(name) for name in archive.namelist()}

    @classmethod
    def tearDownClass(cls):
        cls.workdir.cleanup()

    def test_file_name_is_the_product_and_version(self):
        self.assertEqual("CasualtiesCharacters-9.9.9.zip", os.path.basename(self.zip_path))

    def test_contains_exactly_the_loader_install_text_and_credits(self):
        self.assertEqual(EXPECTED_ENTRIES, set(self.entries))

    def test_ships_no_character_and_no_third_party_art(self):
        for name in self.entries:
            self.assertNotIn("/characters/", name)
            self.assertNotIn("/skins/", name)
            self.assertFalse(name.lower().endswith(THIRD_PARTY_ART), name)

    def test_unzipped_files_equal_the_sources(self):
        target = os.path.join(self.workdir.name, "game")
        with zipfile.ZipFile(self.zip_path) as archive:
            archive.extractall(target)
        installed = os.path.join(target, *package.PLUGIN_ENTRY.split("/"))
        self.assertEqual(package.file_md5(self.dll), package.file_md5(installed))
        with open(package.CREDITS_SOURCE, "rb") as source, open(os.path.join(target, *package.CREDITS_ENTRY.split("/")), "rb") as unzipped:
            self.assertEqual(sha256(source.read()), sha256(unzipped.read()))

    def test_no_debug_or_source_names(self):
        for name in list(self.entries) + [os.path.basename(self.zip_path)]:
            lowered = name.lower().replace("casualtiescharacters", "").replace("characters", "")
            for part in BANNED_NAME_PARTS:
                self.assertNotIn(part, lowered, name)

    def test_private_wording_check_passes_on_the_built_package(self):
        self.assertEqual([], private_wording_problems(self.zip_path))

    def test_readme_ends_with_the_development_line(self):
        with open(os.path.join(package.ROOT, "README.md"), encoding="utf-8") as handle:
            text = handle.read()
        self.assertEqual("Build, test and design notes are in `docs/dev/`.", text.strip().splitlines()[-1])

    def test_private_wording_check_passes_on_the_repository(self):
        self.assertEqual([], private_wording_problems(package.ROOT))

    def test_real_plugin_binary_has_no_repository_text_and_passes_the_private_check_when_built(self):
        if not os.path.isfile(package.DEFAULT_DLL):
            self.skipTest("plugin not built")
        with open(package.DEFAULT_DLL, "rb") as handle:
            data = handle.read().lower()
        for encoded in ("github".encode("utf-8"), "github".encode("utf-16-le")):
            self.assertNotIn(encoded, data)
        wrapped = os.path.join(self.workdir.name, "real_dll.zip")
        with zipfile.ZipFile(wrapped, "w") as archive:
            archive.write(package.DEFAULT_DLL, "CasualtiesCharacters.dll")
        self.assertEqual([], private_wording_problems(wrapped))

    def test_install_text_has_requirements_use_limitations_credits(self):
        text = self.entries[package.INSTALL_ENTRY].decode("utf-8").replace("\r\n", "\n")
        for needle in ("CasualtiesCharacters 9.9.9", "BepInEx " + package.BEPINEX_VERSION,
                       "a %s, built from the source at %s" % (package.TOGETHER_NAME, ALLOWED_LINK),
                       "commit %s or later; the repository's version number reads %s, so go by the commit" % (package.TOGETHER_MIN_COMMIT, package.TOGETHER_BASE_VERSION),
                       "Building section", "installed by every player",
                       "loader only", "pack.json", "Install", "Use", "Known limitations", "Credits",
                       "Singleplayer works with the loader alone", "any other Together build the characters are not\n  available in multiplayer and everything else stays vanilla"):
            self.assertIn(needle, text)

    def test_the_one_allowed_link_is_the_constant_and_appears_once_in_install_text_and_readme(self):
        self.assertEqual(ALLOWED_LINK, package.TOGETHER_URL)
        self.assertEqual(1, self.entries[package.INSTALL_ENTRY].decode("utf-8").count(ALLOWED_LINK))
        with open(os.path.join(package.ROOT, "README.md"), encoding="utf-8") as handle:
            self.assertEqual(1, handle.read().count(ALLOWED_LINK))

    def test_no_other_repository_link_or_github_text_ships(self):
        for name, data in self.entries.items():
            self.assertEqual([], foreign_repository_text(name), name)
            if name.lower().endswith(TEXT_SUFFIXES):
                self.assertEqual([], foreign_repository_text(data.decode("utf-8")), name)
        with open(os.path.join(package.ROOT, "README.md"), encoding="utf-8") as handle:
            self.assertEqual([], foreign_repository_text(handle.read()), "README.md")

    def test_install_text_and_readme_offer_no_download(self):
        with open(os.path.join(package.ROOT, "README.md"), encoding="utf-8") as handle:
            readme = handle.read()
        for text in (self.entries[package.INSTALL_ENTRY].decode("utf-8"), readme):
            self.assertNotIn("download:", text.lower())
            self.assertNotIn("release page", text.lower())
        self.assertIn("No prebuilt download is offered", readme)

    def test_no_build_suffix_ships(self):
        with open(os.path.join(package.ROOT, "README.md"), encoding="utf-8") as handle:
            readme = handle.read()
        texts = [data.decode("utf-8") for name, data in self.entries.items() if name.lower().endswith(TEXT_SUFFIXES)] + [readme]
        for text in texts:
            self.assertEqual([], build_suffixes(text))

    def test_the_suffix_checker_can_fail(self):
        self.assertEqual([], build_suffixes("version 5.4.1 and loader 0.2.0 and commit 23d8cd2"))
        self.assertTrue(build_suffixes("version 5.4.1+1"))
        self.assertTrue(build_suffixes("5.4.1+23"))
        self.assertTrue(build_suffixes("build 7+2 or later"))

    def test_only_the_together_source_url_ships(self):
        with open(os.path.join(package.ROOT, "README.md"), encoding="utf-8") as handle:
            readme = handle.read()
        texts = [data.decode("utf-8") for name, data in self.entries.items() if name.lower().endswith(TEXT_SUFFIXES)] + [readme]
        for text in texts:
            self.assertEqual([], unexpected_urls(text))
        self.assertEqual(["https://example.com/x"], unexpected_urls("see https://example.com/x, thanks"))

    def test_no_licence_or_reuse_wording_ships(self):
        with open(os.path.join(package.ROOT, "README.md"), encoding="utf-8") as handle:
            readme = handle.read()
        texts = [data.decode("utf-8") for name, data in self.entries.items() if name.lower().endswith(TEXT_SUFFIXES)] + [readme]
        for text in texts:
            self.assertEqual([], licence_wording(text))

    def test_the_licence_wording_checker_can_fail(self):
        self.assertTrue(licence_wording("the portal states no reuse licence"))
        self.assertTrue(licence_wording("License: MIT"))
        self.assertEqual([], licence_wording("Skin by paili16"))

    def test_the_link_checker_can_fail(self):
        self.assertEqual([], foreign_repository_text("see " + ALLOWED_LINK + " only"))
        self.assertTrue(foreign_repository_text("see https://github.com/someone/else"))
        self.assertTrue(foreign_repository_text("hosted on GitHub"))
        self.assertTrue(foreign_repository_text(ALLOWED_LINK + "-fork and https://github.com/x/y"))
        self.assertTrue(foreign_repository_text(ALLOWED_LINK + "-fork"))
        self.assertTrue(foreign_repository_text(ALLOWED_LINK + "/releases"))

    def test_the_link_line_is_omitted_when_the_constant_is_empty(self):
        original = package.TOGETHER_URL
        package.TOGETHER_URL = ""
        try:
            text = package.install_text("1.0.0")
        finally:
            package.TOGETHER_URL = original
        self.assertNotIn("built from the source", text)
        self.assertNotIn("github", text.lower())
        self.assertIn("based on version %s" % package.TOGETHER_BASE_VERSION, text)
        self.assertNotIn("Download", text)

    def test_author_and_licence_lines_are_omitted_until_set(self):
        self.assertEqual(("", ""), (package.AUTHOR, package.LICENSE_NAME))
        text = package.install_text("1.0.0")
        self.assertNotIn("Made by", text)
        self.assertNotIn("Licence:", text)
        package.AUTHOR, package.LICENSE_NAME = "Someone", "MIT"
        try:
            filled = package.install_text("1.0.0")
        finally:
            package.AUTHOR, package.LICENSE_NAME = "", ""
        self.assertIn("Made by Someone.", filled)
        self.assertIn("Licence: MIT.", filled)

    def test_build_is_reproducible(self):
        second = os.path.join(self.workdir.name, "again.zip")
        package.build(self.dll, second, VERSION)
        self.assertEqual(package.file_md5(self.zip_path), package.file_md5(second))

    def test_rebuilding_over_a_zip_keeps_the_previous_one(self):
        target = os.path.join(self.workdir.name, "keep", "pkg.zip")
        package.build(self.dll, target, "1.0.0")
        first_md5 = package.file_md5(target)
        package.build(self.dll, target, "2.0.0")
        kept = os.path.join(os.path.dirname(target), "pkg.previous-%s.zip" % first_md5[:8])
        self.assertEqual(first_md5, package.file_md5(kept))
        self.assertNotEqual(first_md5, package.file_md5(target))

    def test_missing_dll_is_refused(self):
        with self.assertRaises(FileNotFoundError):
            package.build(os.path.join(self.workdir.name, "none.dll"), os.path.join(self.workdir.name, "x.zip"), "1")

    def test_version_is_read_from_the_plugin_source(self):
        self.assertRegex(package.read_version(), r"^\d+\.\d+\.\d+$")


if __name__ == "__main__":
    unittest.main()
