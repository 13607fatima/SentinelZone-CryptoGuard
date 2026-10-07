"""Verify screening detects staged bytes and narrow synthetic exceptions."""
import importlib.util
import io
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SCRIPT=Path(__file__).resolve().parents[2]/'scripts/secret-scan.py'
spec=importlib.util.spec_from_file_location('secret_scan', SCRIPT)
scan=importlib.util.module_from_spec(spec)
spec.loader.exec_module(scan)

class PublicationScreeningTests(unittest.TestCase):
    def screen(self,path,content):
        findings,exceptions=[],[]
        scan.inspect(path,content,findings,exceptions)
        return findings,exceptions

    def test_synthetic_exception_is_path_specific(self):
        sample=('Bearer '+'SuperSecret').encode()
        self.assertFalse(self.screen('tests/Contract/Program.cs',sample)[0])
        self.assertTrue(self.screen('src/unreviewed.txt',sample)[0])

    def test_changed_fixture_value_is_not_allowlisted(self):
        sample=('Bearer '+'AnotherUnreviewedValue').encode()
        self.assertTrue(self.screen('tests/Contract/Program.cs',sample)[0])

    def test_private_key_in_nested_archive_is_detected(self):
        buffer=io.BytesIO()
        with zipfile.ZipFile(buffer,'w') as archive:
            archive.writestr('data.txt', '-----BEGIN '+'PRIVATE KEY-----')
        self.assertTrue(self.screen('licenses/example.zip',buffer.getvalue())[0])

    def test_staged_bytes_are_scanned_after_worktree_is_cleaned(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            subprocess.run(['git','init','-q',directory],check=True)
            file=root/'sample.txt'
            file.write_text('Bearer '+'UnreviewedStagedValue')
            subprocess.run(['git','add','sample.txt'],cwd=root,check=True)
            file.write_text('redacted')
            with patch.object(scan,'ROOT',root):
                entries=list(scan.candidates(staged=True))
            self.assertEqual(len(entries),1)
            self.assertTrue(self.screen(*entries[0])[0])

if __name__=='__main__':unittest.main()
