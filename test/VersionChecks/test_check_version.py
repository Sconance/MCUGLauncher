"""Regression checks for product-version detection versus repository refs."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import unittest


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    'check_version', ROOT / 'scripts' / 'check-version.py')
CHECK_VERSION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK_VERSION)


class VersionChecks(unittest.TestCase):
    def test_repository_refs_are_not_product_versions(self):
        for url in (
            'https://raw.githubusercontent.com/Sconance/MCUGLauncher/'
            '0.22.3-Avalonia/community/announcements.md',
            'https://github.com/Sconance/MCUGLauncher/tree/0.22.3feat/tui',
            'https://github.com/Sconance/MCUGLauncher/1.2.3/file.cs',
        ):
            with self.subTest(url=url):
                self.assertIsNone(CHECK_VERSION.HARDCODED.search(url))

    def test_literal_product_versions_are_still_rejected(self):
        for source in (
            '"MCUGLauncher/0.22.3"',
            'Console.WriteLine("MCUG Launcher 0.22.3");',
            'MCUG Launcher\t1.2.3',
            '<TextBlock Text="MCUG Launcher 1.2.3" />',
        ):
            with self.subTest(source=source):
                self.assertIsNotNone(CHECK_VERSION.HARDCODED.search(source))

    def test_url_does_not_hide_product_literal_on_same_line(self):
        source = ('"https://raw.githubusercontent.com/Sconance/MCUGLauncher/'
                  '0.22.3-Avalonia/file"; var agent = "MCUGLauncher/1.2.3";')
        match = CHECK_VERSION.HARDCODED.search(source)
        self.assertIsNotNone(match)
        self.assertEqual('MCUGLauncher/1.2.3', match.group())

    def test_current_app_config_is_not_flagged(self):
        source = (ROOT / 'src/MCUGLauncher.App/Models/AppConfig.cs').read_text(
            encoding='utf-8')
        self.assertIsNone(CHECK_VERSION.HARDCODED.search(source))

    def test_repository_version_check_succeeds(self):
        result = subprocess.run(
            [sys.executable, str(ROOT / 'scripts/check-version.py')],
            cwd=ROOT, text=True, capture_output=True, check=False)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()
