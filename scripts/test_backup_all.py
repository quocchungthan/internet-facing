from pathlib import Path
import subprocess
import tempfile
import unittest


class AllServicesBackupTests(unittest.TestCase):
    def test_installed_seafile_and_affine_are_not_backed_up(self):
        script = Path(__file__).with_name("backup-all.sh").read_text()
        service_steps = script.split("# ---- Farm (", 1)[1]
        service_steps = service_steps.split("\n", 1)[1].split("# ---- Metadata", 1)[0]

        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for service, compose in [
                ("mail", "compose.yaml"),
                ("storage", "docker-compose.yml"),
                ("note", "compose.yml"),
            ]:
                deploy = root / service
                deploy.mkdir()
                (deploy / compose).touch()
            (root / "fences" / "data").mkdir(parents=True)
            (root / "fences" / "data" / "identity.db").touch()
            (root / "mail" / "maddy").mkdir()
            (root / "mail" / "maddy" / "maddy.conf").write_text(
                "auth.pass_table {\n dsn credentials.db\n}\n"
                "storage.imapsql {\n dsn imapsql.db\n}\n"
            )
            harness = r"""
set -euo pipefail
root=$1
trace="$root/trace"
FARM_STATIC_ROOT="$root/farm"
FARM_CONFIG_DIR="$root/farm-config"
FARM_CONTAINER=eldervibe-farm
FARM_DB_CONTAINER=eldervibe-farm-postgres
FARM_PG_PORT=4554
FENCES_DEPLOY_DIR="$root/fences"
FENCES_CONFIG_DIR="$FENCES_DEPLOY_DIR"
FENCES_DATA_DIR="$FENCES_DEPLOY_DIR/data"
FENCES_CONTAINER=shuneo-fences
MAILBOX_DEPLOY_DIR="$root/mail"
STORAGE_DEPLOY_DIR="$root/storage"
AFFINE_DEPLOY_DIR="$root/note"
SEAFILE_CONTAINER=seafile-server
SEAFILE_DB_CONTAINER=seafile-mysql
AFFINE_CONTAINER=affine_server
AFFINE_DB_CONTAINER=affine_postgres
trace_call() { printf '%s\t' "$@" >> "$trace"; printf '\n' >> "$trace"; }
container_exists() { trace_call inspect "$1"; return 0; }
docker() { trace_call docker "$@"; }
mount_source() { trace_call mount "$@"; }
copy_path() { trace_call copy "$@"; }
dump_to() { trace_call dump "$@"; }
dump_para_postgres() { trace_call para "$@"; }
sqlite_snapshot() { trace_call sqlite "$@"; }
record() { trace_call record "$@"; }
warn() { trace_call warn "$@"; }
"""
            subprocess.run(
                ["bash", "-c", harness + service_steps, "backup-test", folder],
                check=True, capture_output=True, text=True,
            )
            calls = (root / "trace").read_text()
            for excluded in [
                "seafile", "affine", str(root / "storage"), str(root / "note"),
                "\tstorage\t", "\tnote\t",
            ]:
                self.assertNotIn(excluded, calls)
            for retained in [
                "copy\tfarm-hanging-post\tfarm\t",
                "dump\tfarm-postgres\tfarm\t",
                "copy\tfences-config\tfences\t",
                "sqlite\tfences-db-identity\tfences\t",
                "copy\tmail-secrets\tmail\t",
                "sqlite\tmaddy-credentials\tmail\t",
                "para\tpara-car-postgres\t",
                "para\tpara-actions-postgres\t",
            ]:
                self.assertIn(retained, calls)


if __name__ == "__main__":
    unittest.main()
