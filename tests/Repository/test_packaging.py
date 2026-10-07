"""Source-only regression for a verified Debian docs case mismatch.
Set CRYPTOGUARD_REPOSITORY_ROOT to check a retained baseline or clean extraction.
"""
import os
import json
import xml.etree.ElementTree as ET
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

    def test_linux_portable_lock_contains_current_project_graph(self):
        project = ROOT / 'src/CryptoGuard.Agent.Linux/CryptoGuard.Agent.Linux.csproj'
        locked = json.loads((project.parent / 'packages.portable.lock.json').read_text())['dependencies']['net10.0']
        locked = {name.lower(): value for name, value in locked.items()}
        visited = set()
        def visit(path):
            if path in visited:
                return
            visited.add(path)
            refs = [(path.parent / node.attrib['Include'].replace('\\', '/')).resolve()
                    for node in ET.parse(path).iter('ProjectReference')]
            if path != project:
                key = path.stem.lower()
                self.assertIn(key, locked, 'Referenced project absent from locked dependency graph')
                actual = {name.lower() for name in locked[key].get('dependencies', {})}
                expected = {ref.stem.lower() for ref in refs}
                self.assertEqual(actual, expected, 'Stale project edges in portable lock')
            for ref in refs:
                visit(ref)
        visit(project)

if __name__ == '__main__':
    unittest.main()
