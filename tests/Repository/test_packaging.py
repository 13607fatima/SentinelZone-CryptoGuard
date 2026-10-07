"""Source-only regression for a verified Debian docs case mismatch.
Set CRYPTOGUARD_REPOSITORY_ROOT to check a retained baseline or clean extraction.
"""
import os
from pathlib import Path
import unittest

ROOT = Path(os.environ.get('CRYPTOGUARD_REPOSITORY_ROOT', Path(__file__).resolve().parents[2]))

class DebianPackagingRegression(unittest.TestCase):
    def test_all_declared_package_documents_exist_with_exact_case(self):
        for value in (ROOT / 'debian/cryptoguard-agent.docs').read_text().splitlines():
            if not value.strip() or value.startswith('#'):
                continue
            with self.subTest(document=value):
                current = ROOT
                for part in Path(value).parts:
                    self.assertIn(part, {p.name for p in current.iterdir()}, 'Missing or wrong-case Debian documentation input')
                    current /= part
                self.assertTrue(current.is_file())

if __name__ == '__main__':
    unittest.main()
