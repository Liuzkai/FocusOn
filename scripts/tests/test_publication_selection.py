import importlib.util
import pathlib
import unittest

path = pathlib.Path(__file__).resolve().parents[1] / 'select_publication_tag.py'
spec = importlib.util.spec_from_file_location('selection', path)
selection = importlib.util.module_from_spec(spec)
spec.loader.exec_module(selection)

class PublicationSelectionTests(unittest.TestCase):
    def test_new_version_comes_from_its_approval_instead_of_a_previous_version(self):
        self.assertEqual('v0.1.5', self.select(['.github/release-publication/v0.1.5.json']))

    def test_manual_selection_requires_a_stable_version(self):
        self.assertEqual('v0.1.1', selection.select_tag('v0.1.1', {}, '', None))
        for value in ['main', '../v0.1.5', 'v0.1.5-beta']:
            with self.assertRaises(AssertionError): selection.select_tag(value, {}, '', None)

    def test_ordinary_changes_or_multiple_approvals_never_publish(self):
        for paths in [[], ['README.md'], ['.github/release-publication/v0.1.1.json', '.github/release-publication/v0.1.5.json']]:
            with self.assertRaises(AssertionError): self.select(paths)

    def test_different_branch_or_checkout_never_publishes(self):
        for ref, head in [('refs/heads/other', 'b' * 40), ('refs/heads/main', 'c' * 40)]:
            with self.assertRaises(AssertionError):
                selection.select_tag('', {'ref': ref, 'before': 'a' * 40, 'after': 'b' * 40}, head, lambda *_: ['.github/release-publication/v0.1.5.json'])

    def select(self, paths):
        return selection.select_tag('', {'ref': 'refs/heads/main', 'before': 'a' * 40, 'after': 'b' * 40}, 'b' * 40, lambda *_: paths)
