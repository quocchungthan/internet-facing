import importlib.util
import io
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("backup_postgres", Path(__file__).with_name("backup-postgres.py"))
backup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(backup)


def connection_string(overrides=None):
    fields = {
        "Host": "localhost", "Database": "para", "Username": "para",
        "Password": secrets.token_hex(12),
    }
    fields.update(overrides or {})
    return ";".join(f"{key}={value}" for key, value in fields.items())


class PostgresBackupTests(unittest.TestCase):
    def test_npgsql_fields_and_port_are_passed_without_password_in_arguments(self):
        credential = secrets.token_hex(12) + "+"
        connection = connection_string({"Host": "127.0.0.1", "Port": "1442", "Username": "cbt", "Password": credential})
        with tempfile.TemporaryDirectory() as folder, patch.object(backup.subprocess, "run") as run:
            run.return_value = subprocess.CompletedProcess([], 0)
            backup.dump(connection, Path(folder) / "dump.sql")
            args, kwargs = run.call_args
            self.assertIn("postgres:18-alpine", args[0])
            self.assertNotIn(credential, " ".join(args[0]))
            self.assertEqual(kwargs["env"]["PGPORT"], "1442")
            self.assertEqual(kwargs["env"]["PGPASSWORD"], credential)
            self.assertIn("--clean", args[0])

    def test_quoted_password_handles_semicolons_quotes_and_shell_characters(self):
        for value, expected in [
            ('"a; b""c $(touch /not-executed)"', 'a; b"c $(touch /not-executed)'),
            ("'a; b''c'", "a; b'c"),
        ]:
            with self.subTest(value=value):
                env = backup.postgres_environment(connection_string({"Password": value}))
                self.assertEqual(env["PGPASSWORD"], expected)

    def test_defaults_aliases_and_ssl_mode(self):
        env = backup.postgres_environment(
            connection_string() + ";Server=localhost;Initial Catalog=para;User ID=para;SSL Mode=VerifyFull;Timeout=30"
        )
        self.assertEqual(env["PGPORT"], "5432")
        self.assertEqual(env["PGSSLMODE"], "verify-full")
        self.assertEqual(env["PGCONNECT_TIMEOUT"], "30")

    def test_last_alias_wins_like_npgsql(self):
        env = backup.postgres_environment(
            connection_string({"Host": "old"}) + ";Server=other;Host=localhost"
        )
        self.assertEqual(env["PGHOST"], "localhost")

    def test_invalid_or_unsupported_settings_fail_before_starting_dump(self):
        prefix = connection_string() + ";"
        for connection in [
            "", prefix + "Port=0", prefix + "Port=70000", prefix + "Timeout=0",
            prefix + "SSL Mode=unknown", prefix + "Unknown=private",
            connection_string({"Password": '"unterminated'}),
            connection_string({"Password": '"p"suffix'}),
            prefix + "invalid",
        ]:
            with self.subTest(connection=connection), patch.object(backup.subprocess, "run") as run:
                with self.assertRaises(ValueError):
                    backup.dump(connection, "unused.sql")
                run.assert_not_called()

    def test_dump_failure_is_reported_without_provider_error_or_credentials(self):
        credential = secrets.token_hex(12)
        connection = connection_string({"Password": credential})
        with tempfile.TemporaryDirectory() as folder, \
                patch.object(backup.sys, "stdin", io.StringIO(connection)), \
                patch.object(backup.sys, "argv", ["backup-postgres.py", str(Path(folder) / "dump.sql")]), \
                patch.object(backup.sys, "stderr", io.StringIO()) as stderr, \
                patch.object(backup.subprocess, "run") as run:
            run.return_value = subprocess.CompletedProcess([], 1, stderr=f"provider error {credential}".encode())
            self.assertEqual(backup.main(), 1)
            self.assertIn("PostgreSQL dump failed", stderr.getvalue())
            self.assertNotIn(credential, stderr.getvalue())

    def test_host_libpq_credentials_do_not_override_the_requested_database(self):
        credential = secrets.token_hex(12)
        connection = connection_string({"Password": credential})
        with tempfile.TemporaryDirectory() as folder, \
                patch.dict(os.environ, {"PGSERVICE": "other", "PGPASSWORD": "other"}), \
                patch.object(backup.subprocess, "run") as run:
            run.return_value = subprocess.CompletedProcess([], 0)
            backup.dump(connection, Path(folder) / "dump.sql")
            child_env = run.call_args.kwargs["env"]
            self.assertNotIn("PGSERVICE", child_env)
            self.assertEqual(child_env["PGPASSWORD"], credential)


if __name__ == "__main__":
    unittest.main()
