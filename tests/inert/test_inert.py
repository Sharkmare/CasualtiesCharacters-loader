import os
import unittest

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SOURCE_FOLDER = os.path.join(ROOT, "src")
BUILT_DLL = os.path.join(ROOT, "src", "Plugin", "bin", "CasualtiesCharacters.dll")
SKIPPED_FOLDERS = ("bin", "obj")

SENDING_NAMES = (
    "CreateNamedWriter",
    "RegisterCustomServerReceiver",
    "RegisterCustomClientReceiver",
    "Client_Send",
    "Server_SendTo",
    "Server_SendToClients",
    "NetDataWriter",
    "PutBytesWithLength",
)


def forbidden_hits(data):
    hits = []
    for name in SENDING_NAMES:
        for encoded in (name.encode("utf-8"), name.encode("utf-16-le")):
            if encoded in data:
                hits.append(name)
                break
    return hits


def source_files():
    for folder, folders, names in os.walk(SOURCE_FOLDER):
        folders[:] = [f for f in folders if f not in SKIPPED_FOLDERS]
        for name in names:
            if name.endswith(".cs"):
                yield os.path.join(folder, name)


class InertTests(unittest.TestCase):

    def test_the_checker_can_fail(self):
        self.assertEqual(["Client_Send"], forbidden_hits(b"call Net.Client_Send(x)"))
        self.assertEqual(["NetDataWriter"], forbidden_hits("NetDataWriter".encode("utf-16-le")))
        self.assertEqual([], forbidden_hits(b"nothing here"))

    def test_no_source_file_can_send_a_message(self):
        for path in source_files():
            with open(path, "rb") as handle:
                self.assertEqual([], forbidden_hits(handle.read()), path)

    def test_the_built_plugin_has_no_message_sending_member(self):
        if not os.path.isfile(BUILT_DLL):
            self.skipTest("plugin not built")
        with open(BUILT_DLL, "rb") as handle:
            self.assertEqual([], forbidden_hits(handle.read()))

    def test_the_only_together_probe_is_the_character_api(self):
        with open(os.path.join(SOURCE_FOLDER, "Plugin", "TogetherLink.cs"), encoding="utf-8") as handle:
            text = handle.read()
        self.assertIn("Together.TogetherCharacters, CasualtiesMP", text)
        self.assertNotIn("Together.Multiplayer", text)
        self.assertNotIn("Hello", text)

    def test_the_stock_message_layer_is_gone(self):
        for name in ("StockBridge.cs", "StockChannel.cs", "StockProtocol.cs", "CharacterWire.cs"):
            for folder in ("Plugin", "Core"):
                self.assertFalse(os.path.exists(os.path.join(SOURCE_FOLDER, folder, name)), name)


if __name__ == "__main__":
    unittest.main()
