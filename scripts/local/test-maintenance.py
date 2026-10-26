#!/usr/bin/env python3
"""Safe workflow tests: fake Docker by default; --docker uses a disposable,
unpublished PostgreSQL fixture, never the app service/volume or private .env."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
DOCKER_TESTS = "--docker" in sys.argv
if DOCKER_TESTS:
    sys.argv.remove("--docker")

FAKE_DOCKER = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
args = sys.argv[1:]
entry = {'args': args}
if 'psql' in args: entry['sql'] = sys.stdin.read()
with open(os.environ['DOCKER_CALL_LOG'], 'a') as file: file.write(json.dumps(entry) + '\n')
if os.environ.get('FAIL_ON') in args: sys.exit(2)
if args[:2] == ['context', 'inspect']:
    host = os.environ.get('FAKE_ENDPOINT', 'unix:///var/run/docker.sock')
    print(host if '--format' in args else json.dumps([{'Endpoints': {'docker': {'Host': host}}}]))
elif 'cp' in args:
    target = pathlib.Path(args[-1]); target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(b'PGDMP-fixture-only')
elif 'ps' in args: print(os.environ.get('FAKE_RUNNING', ''))
'''


@unittest.skipUnless(os.name == "posix", "The fake Docker executable harness runs on Linux/WSL/CI")
class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="fantasoccer-maintenance-")
        self.root = Path(self.temp.name)
        local = self.root / "scripts/local"
        local.mkdir(parents=True)
        for name in ["local.ps1", "local.sh", "reset-postgres.ps1", "reset-postgres.sh", "reset-postgres.sql", "sync-storage.ps1", "sync-storage.sh"]:
            shutil.copyfile(ROOT / "scripts/local" / name, local / name)
        self.env_file = self.root / "private fixture.env"
        self.env_file.write_text("# Synthetic fixture only\n")
        (self.root / "docker").write_text(FAKE_DOCKER)
        (self.root / "docker").chmod(0o700)
        self.log = self.root / "docker-calls.jsonl"
        self.env = {key: value for key, value in os.environ.items() if key not in ["DOCKER_HOST", "DOCKER_CONTEXT", "FAIL_ON", "APPLY", "WHAT_IF", "USE_EXISTING_DUMP", "RESUME", "SOURCE_QUIESCED", "CONFIRM_DATABASE", "INCLUDE_TABLES"]}
        self.env.update(PATH=str(self.root) + os.pathsep + os.environ["PATH"], DOCKER_CALL_LOG=str(self.log))

    def tearDown(self):
        self.temp.cleanup()

    def run_script(self, shell, script, args=(), **environment):
        command = ["pwsh", "-NoProfile", "-File"] if shell == "ps1" else ["bash"]
        command += [str(self.root / "scripts/local" / f"{script}.{shell}")]
        command += ["-EnvFile", str(self.env_file)] if shell == "ps1" else [str(self.env_file)]
        result = subprocess.run(command + list(args), env={**self.env, **environment}, capture_output=True, text=True, timeout=30)
        return result

    def shells(self):
        return ["sh"] + (["ps1"] if shutil.which("pwsh") else [])

    def calls(self):
        return [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []

    def clear(self):
        if self.log.exists(): self.log.unlink()

    def test_confirmation_and_whatif_never_connect(self):
        for shell in self.shells():
            with self.subTest(shell=shell):
                self.clear()
                self.assertNotEqual(0, self.run_script(shell, "reset-postgres").returncode)
                self.assertEqual([], self.calls())
                args = ["-ConfirmDatabase", "fantasoccer_local", "-WhatIf"] if shell == "ps1" else []
                result = self.run_script(shell, "reset-postgres", args, CONFIRM_DATABASE="fantasoccer_local", WHAT_IF="true")
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual([], self.calls())

    def test_remote_context_and_failed_backup_refuse_sql(self):
        for shell in self.shells():
            args = ["-ConfirmDatabase", "fantasoccer_local"] if shell == "ps1" else []
            for extra in [{"FAKE_ENDPOINT": "ssh://production.example"}, {"FAIL_ON": "pg_dump"}, {"FAIL_ON": "cp"}, {"FAIL_ON": "pg_restore"}, {"FAKE_RUNNING": "import-core"}]:
                with self.subTest(shell=shell, extra=extra):
                    self.clear()
                    result = self.run_script(shell, "reset-postgres", args, CONFIRM_DATABASE="fantasoccer_local", **extra)
                    self.assertNotEqual(0, result.returncode)
                    self.assertFalse(any("psql" in row["args"] for row in self.calls()))

    def test_reset_backs_up_before_sql_and_never_deletes_volumes_or_restarts_api(self):
        for shell in self.shells():
            with self.subTest(shell=shell):
                self.clear()
                args = ["-ConfirmDatabase", "fantasoccer_local"] if shell == "ps1" else []
                result = self.run_script(shell, "reset-postgres", args, CONFIRM_DATABASE="fantasoccer_local")
                self.assertEqual(0, result.returncode, result.stderr)
                calls = self.calls()
                dump = next(index for index, row in enumerate(calls) if "pg_dump" in row["args"])
                copy = next(index for index, row in enumerate(calls) if "cp" in row["args"])
                sql = next(index for index, row in enumerate(calls) if "psql" in row["args"])
                self.assertLess(dump, copy); self.assertLess(copy, sql)
                self.assertIn("__EFMigrationsHistory", calls[sql]["sql"])
                self.assertNotIn("down", [arg for row in calls for arg in row["args"]])
                self.assertNotIn("--volumes", [arg for row in calls for arg in row["args"]])
                self.assertTrue(all("api" not in row["args"] for row in calls if "up" in row["args"]))

    def test_sync_default_is_analysis_only_and_apply_orders_sql_before_file_restore(self):
        for shell in self.shells():
            with self.subTest(shell=shell):
                self.clear()
                args = ["-DumpName", "fixture"] if shell == "ps1" else []
                result = self.run_script(shell, "sync-storage", args, DUMP_NAME="fixture")
                self.assertEqual(0, result.returncode, result.stderr)
                args_list = [row["args"] for row in self.calls()]
                self.assertTrue(any("storage-export" in args for args in args_list))
                self.assertTrue(any("analyze-core" in args for args in args_list))
                self.assertFalse(any("storage-restore" in args for args in args_list))
                self.assertFalse(any("--include-tables" in args for args in args_list))
                self.assertFalse(any("psql" in args for args in args_list))
                self.assertFalse(any(args[-3:] == ["import-core", "--directory", "/dumps/fixture"] for args in args_list))
                self.clear()
                apply = args + (["-UseExistingDump", "-Apply"] if shell == "ps1" else [])
                result = self.run_script(shell, "sync-storage", apply, DUMP_NAME="fixture", USE_EXISTING_DUMP="true", APPLY="true")
                self.assertEqual(0, result.returncode, result.stderr)
                calls = self.calls()
                self.assertFalse(any("storage-export" in row["args"] for row in calls))
                imp = next(index for index, row in enumerate(calls) if row["args"][-3:] == ["import-core", "--directory", "/dumps/fixture"])
                restore = next(index for index, row in enumerate(calls) if "storage-restore" in row["args"])
                self.assertLess(imp, restore)
                self.assertFalse(any("psql" in row["args"] or "up" in row["args"] for row in calls))

    def test_table_download_and_restore_require_the_explicit_switch(self):
        for shell in self.shells():
            with self.subTest(shell=shell):
                self.clear()
                args = ["-DumpName", "fixture", "-IncludeTables", "-Resume"] if shell == "ps1" else []
                result = self.run_script(shell, "sync-storage", args, DUMP_NAME="fixture", INCLUDE_TABLES="true", RESUME="true")
                self.assertEqual(0, result.returncode, result.stderr)
                export = next(row["args"] for row in self.calls() if "storage-export" in row["args"])
                self.assertIn("--include-tables", export); self.assertIn("--resume", export)
                self.clear()
                args = ["-DumpName", "fixture", "-UseExistingDump", "-Apply", "-IncludeTables"] if shell == "ps1" else []
                result = self.run_script(shell, "sync-storage", args, DUMP_NAME="fixture", INCLUDE_TABLES="true", USE_EXISTING_DUMP="true", APPLY="true")
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertFalse(any("storage-export" in row["args"] for row in self.calls()))
                restore = next(row["args"] for row in self.calls() if "storage-restore" in row["args"])
                self.assertIn("--include-tables", restore)

    def test_export_failure_stops_pipeline_and_whatif_does_nothing(self):
        for shell in self.shells():
            with self.subTest(shell=shell):
                self.clear()
                args = ["-DumpName", "fixture"] if shell == "ps1" else []
                result = self.run_script(shell, "sync-storage", args, DUMP_NAME="fixture", FAIL_ON="storage-export")
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(1, len([row for row in self.calls() if row["args"][0] == "compose"]))
                self.clear()
                result = self.run_script(shell, "sync-storage", args + (["-WhatIf"] if shell == "ps1" else []), DUMP_NAME="fixture", WHAT_IF="true")
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual([], self.calls())

    def test_sync_refuses_remote_context_and_cannot_relabel_an_existing_dump_as_quiesced(self):
        for shell in self.shells():
            with self.subTest(shell=shell):
                self.clear()
                args = ["-DumpName", "fixture"] if shell == "ps1" else []
                result = self.run_script(shell, "sync-storage", args, DUMP_NAME="fixture", FAKE_ENDPOINT="ssh://production.example")
                self.assertNotEqual(0, result.returncode)
                self.assertFalse(any(row["args"][0] == "compose" for row in self.calls()))
                self.clear()
                invalid = args + (["-UseExistingDump", "-SourceQuiesced"] if shell == "ps1" else [])
                result = self.run_script(shell, "sync-storage", invalid, DUMP_NAME="fixture", USE_EXISTING_DUMP="true", SOURCE_QUIESCED="true")
                self.assertNotEqual(0, result.returncode)
                self.assertEqual([], self.calls())


@unittest.skipUnless(DOCKER_TESTS, "Pass --docker for an isolated, unpublished PostgreSQL fixture")
class ResetSqlIntegrationTests(unittest.TestCase):
    def test_actual_sql_preserves_schema_and_history_and_refuses_other_databases(self):
        host = subprocess.check_output(["docker", "context", "inspect", "--format", "{{.Endpoints.docker.Host}}"], text=True).strip()
        self.assertTrue(host.startswith(("unix://", "npipe://")), "Only local Docker is supported")
        self.assertTrue(not os.getenv("DOCKER_HOST") or os.environ["DOCKER_HOST"].startswith(("unix://", "npipe://")))
        with tempfile.TemporaryDirectory(prefix="fantasoccer-reset-fixture-") as folder:
            compose = Path(folder) / "compose.yml"
            compose.write_text('''services:
  postgres:
    image: postgres:17.6-bookworm
    environment:
      POSTGRES_USER: fantasoccer_local
      POSTGRES_DB: fantasoccer_local
      POSTGRES_PASSWORD: fixture-only-not-a-real-password
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U fantasoccer_local -d fantasoccer_local"]
      interval: 1s
      retries: 30
''')
            cmd = ["docker", "compose", "-p", "fantasoccer-reset-fixture-" + uuid.uuid4().hex, "-f", str(compose)]
            def sql(text, database="fantasoccer_local", check=True):
                return subprocess.run(cmd + ["exec", "-T", "postgres", "psql", "-X", "-t", "-A", "-v", "ON_ERROR_STOP=1", "-U", "fantasoccer_local", "-d", database],
                    input=text, capture_output=True, text=True, check=check)
            try:
                subprocess.run(cmd + ["up", "-d", "--wait", "postgres"], check=True, capture_output=True)
                sql('CREATE TABLE "__EFMigrationsHistory" (id int); INSERT INTO "__EFMigrationsHistory" VALUES (1); CREATE TABLE "Users" (id int PRIMARY KEY); CREATE TABLE "Groups" (id int REFERENCES "Users"); INSERT INTO "Users" VALUES (1); INSERT INTO "Groups" VALUES (1);')
                backup = subprocess.check_output(cmd + ["exec", "-T", "postgres", "pg_dump", "-U", "fantasoccer_local", "-d", "fantasoccer_local", "-Fc"])
                self.assertTrue(backup.startswith(b"PGDMP"))
                reset = (ROOT / "scripts/local/reset-postgres.sql").read_text()
                sql(reset)
                self.assertEqual("0|0|1", sql('SELECT (SELECT count(*) FROM "Users"), (SELECT count(*) FROM "Groups"), (SELECT count(*) FROM "__EFMigrationsHistory");').stdout.strip())
                sql(reset)  # Idempotent while the schema remains present.
                refused = sql(reset, database="postgres", check=False)
                self.assertNotEqual(0, refused.returncode)
                self.assertIn("dedicated local", refused.stderr)
            finally:
                # This unique fixture has no host ports or application volumes.
                subprocess.run(cmd + ["down", "--volumes"], check=True, capture_output=True)


if __name__ == "__main__":
    unittest.main(verbosity=2)
