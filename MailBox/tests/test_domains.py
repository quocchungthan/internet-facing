import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('configure_domains', ROOT / 'scripts' / 'configure_domains.py')
domains = importlib.util.module_from_spec(spec)
spec.loader.exec_module(domains)


class MailDomainTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.config = Path(self.directory.name) / 'maddy.conf'
        self.original = (ROOT / 'maddy' / 'maddy.conf').read_text(encoding='utf-8')
        self.config.write_text(self.original, encoding='utf-8')

    def test_multiple_domains_are_separate_configuration_arguments(self):
        domains.configure_domains(self.config, ['example.test', 'other.test', 'xn--bcher-kva.test'])
        line = next(line for line in self.config.read_text().splitlines() if line.startswith(domains.DECLARATION))
        self.assertEqual(line, '$(local_domains) = example.test other.test xn--bcher-kva.test')
        self.assertNotIn('{env:MADDY_DOMAINS}', self.config.read_text())
        unchanged = lambda value: '\n'.join(
            line for line in value.splitlines() if not line.startswith(domains.DECLARATION)
        )
        self.assertEqual(unchanged(self.config.read_text()), unchanged(self.original))

    def test_single_domain_and_repeated_rendering(self):
        domains.configure_domains(self.config, ['example.test', 'other.test'])
        domains.configure_domains(self.config, ['example.test'])
        first = self.config.read_bytes()
        domains.configure_domains(self.config, ['example.test'])
        self.assertEqual(self.config.read_bytes(), first)
        self.assertIn('$(local_domains) = example.test\n', first.decode())
        self.assertNotIn('other.test', first.decode())

    def test_invalid_or_duplicate_domains_leave_configuration_unchanged(self):
        for values in [[], ['example.test', 'example.test'], ['Example.test'],
                       ['example.test other.test'], ['example.test\nsmtp'], ['-bad.test'], ['a' * 64 + '.test']]:
            with self.subTest(domains=values), self.assertRaises(ValueError):
                domains.configure_domains(self.config, values)
            self.assertEqual(self.config.read_text(), self.original)

    def test_missing_or_duplicate_declaration_is_an_error(self):
        for value in ['hostname mail.example.test\n', self.original + domains.DECLARATION + 'duplicate.test\n']:
            self.config.write_text(value)
            with self.assertRaises(ValueError):
                domains.configure_domains(self.config, ['example.test'])
            self.assertEqual(self.config.read_text(), value)


if __name__ == '__main__':
    unittest.main()
