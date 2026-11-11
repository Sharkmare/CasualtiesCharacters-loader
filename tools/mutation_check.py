import subprocess
import sys
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parent.parent
CORE_TESTS = REPOSITORY / "tests" / "CoreTests"
PACKAGE_TESTS = "tests/package"

MUTATIONS = [
    ("src/Core/IdAllocator.cs", ".OrderBy(d => d.FullKey, StringComparer.Ordinal).ToList();", ".ToList();", "ids depend on input order"),
    ("src/Core/IdAllocator.cs", 'd.PackId + "@" + d.PackVersion + "|"', 'd.PackId + "|"', "fingerprint ignores pack version"),
    ("src/Core/CharacterCatalog.cs", "if (chosen.Any(c => c.Info.Id == source.Info.Id))", "if (false)", "duplicate packs both load"),
    ("src/Core/CharacterCatalog.cs", "if (VersionCompare.Compare(source.Info.MinLoaderVersion, loaderVersion) > 0)", "if (false)", "loader version ignored"),
    ("src/Core/CharacterCatalog.cs", "if (!seenKeys.Add(definition.Key))", "if (false)", "duplicate keys in a pack both load"),
    ("src/Core/MultiplayerPolicy.cs", "return !multiplayerRunning || characterApiPresent;", "return true;", "characters active on a build without character support"),
    ("src/Core/MultiplayerPolicy.cs", "return charactersActive ? requestedSlot : FallbackVanillaSlot;", "return requestedSlot;", "custom slot kept when inactive"),
    ("src/Core/Definition.cs", 'System.IO.Path.Combine(SourceFolder, "skins", SkinFolder)', 'System.IO.Path.Combine("skins", SkinFolder)', "skin folders not namespaced by pack"),
]

PACKAGE_MUTATIONS = [
    ("tools/build_package.py", '"https://github.com/Sharkmare/CU-Character-Avali"', '"https://github.com/Sharkmare/CU-Character-Avali-old"', "example link points elsewhere"),
    ("tools/build_package.py", 'TOGETHER_URL = "https://github.com/Sharkmare/casualties-together/tree/characters"', 'TOGETHER_URL = "https://github.com/Sharkmare/other"', "Together link points elsewhere"),
    ("tools/build_package.py", 'TOGETHER_URL = "https://github.com/Sharkmare/casualties-together/tree/characters"', 'TOGETHER_URL = "https://github.com/Sharkmare/casualties-together/tree/main"', "Together link names the wrong branch"),
    ("tools/build_package.py", "Example characters\n{examples}\n\n", "", "install text lists no examples"),
]


def run_package_tests():
    result = subprocess.run([sys.executable, "-W", "ignore", "-I", "-m", "unittest", "discover", "-s", PACKAGE_TESTS],
                            cwd=str(REPOSITORY), capture_output=True, text=True)
    return result.returncode, [line for line in result.stderr.splitlines() if line.startswith("FAIL")]


def run_tests():
    result = subprocess.run(["dotnet", "run", "-c", "Release"], cwd=str(CORE_TESTS), capture_output=True, text=True)
    return result.returncode, [line for line in result.stdout.splitlines() if line.startswith("FAIL")]


def main():
    survivors = 0
    work = [(m, run_tests) for m in MUTATIONS] + [(m, run_package_tests) for m in PACKAGE_MUTATIONS]
    for (relative, original, mutant, label), runner in work:
        path = REPOSITORY / relative
        source = path.read_text(encoding="utf-8")
        if source.count(original) != 1:
            print("anchor not found once:", label)
            return 2
        try:
            path.write_text(source.replace(original, mutant), encoding="utf-8", newline="\n")
            code, failures = runner()
        finally:
            path.write_text(source, encoding="utf-8", newline="\n")
        status = "caught by %d test(s)" % len(failures) if code != 0 else "SURVIVED"
        survivors += code == 0
        print("%-45s %s" % (label, status))
    return 1 if survivors else 0


if __name__ == "__main__":
    sys.exit(main())
