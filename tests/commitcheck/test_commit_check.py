import os
import sys
import unittest

TOOLS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools")
sys.path.insert(0, TOOLS)

import check_commit_message as check  # noqa: E402


class CommitCheckTests(unittest.TestCase):
    def test_plain_verb_first_subject_is_accepted(self):
        self.assertEqual([], check.problems("Add the package builder and its tests\n"))

    def test_ticket_keys_are_rejected(self):
        self.assertTrue(check.problems("Add the parser (ABC-11)\n"))
        self.assertTrue(check.problems("Add the parser\n\nCloses ABC-7\n"))

    def test_private_word_list_is_applied_when_it_is_found(self):
        words = check.banned_words()
        if not words:
            print("internal wording check skipped (private rules not found)")
            return
        for word in words:
            self.assertTrue(check.problems("Add the %s notes\n" % word), word)

    def test_existing_rules_still_apply(self):
        self.assertTrue(check.problems("fix\n"))
        self.assertTrue(check.problems("A" + "b" * 80 + "\n"))
        self.assertTrue(check.problems("Add a thing " + chr(0x2014) + " quickly\n"))
        self.assertTrue(check.problems(""))

    def test_words_inside_other_words_do_not_trip_the_check(self):
        self.assertEqual([], check.problems("Read the operatingsystem name\n"))


if __name__ == "__main__":
    unittest.main()
