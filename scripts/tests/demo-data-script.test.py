"""Verify reset scope and refusal paths without deleting actual Docker storage."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


def find_bash():
    git_bash = Path("C:/Program Files/Git/bin/bash.exe")
    if git_bash.exists():
        return str(git_bash)
    which_bash = shutil.which("bash")
    if which_bash and "system32" not in which_bash.lower():
        return which_bash
    return "bash"


BASH = find_bash()
SCRIPT = Path(__file__).resolve().parent.parent / "demo-data.sh"
VOLUME = "fieldsales-demo-sqlserver-data"


class DemoResetTests(unittest.TestCase):
    def run_reset(self, running="", stopped="", volumes=VOLUME, failure=""):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / "calls"
            docker = Path(directory) / "docker"
            docker.write_text("""#!/usr/bin/env bash
set -e
echo "$*" >> "$MOCK_LOG"
if [[ "$*" == "$MOCK_FAILURE" ]]; then exit 1; fi
case "$*" in
    'ps --filter volume=fieldsales-demo-sqlserver-data --format {{.ID}}') printf '%s' "$MOCK_RUNNING" ;;
    'ps -a --filter volume=fieldsales-demo-sqlserver-data --format {{.ID}}') printf '%s' "$MOCK_STOPPED" ;;
    'volume ls --format {{.Name}}') printf '%s' "$MOCK_VOLUMES" ;;
    'rm demo-stopped'|'volume rm fieldsales-demo-sqlserver-data') ;;
    *) exit 99 ;;
esac
""")
            docker.chmod(0o755)
            env = dict(os.environ, PATH=directory + os.pathsep + os.environ["PATH"],
                       MOCK_LOG=log.as_posix(), MOCK_RUNNING=running, MOCK_STOPPED=stopped,
                       MOCK_VOLUMES=volumes, MOCK_FAILURE=failure)
            result = subprocess.run([BASH, SCRIPT.as_posix(), "reset"], env=env, text=True, capture_output=True)
            return result, log.read_text().splitlines()

    def test_refuses_reset_while_demo_is_running(self):
        result, calls = self.run_reset(running="demo-running")
        self.assertEqual(1, result.returncode)
        self.assertIn("Stop the demo AppHost", result.stderr)
        self.assertEqual(1, len(calls))

    def test_removes_only_demo_storage_and_stopped_demo_container(self):
        result, calls = self.run_reset(stopped="demo-stopped", volumes=VOLUME + "\nfieldsales-identity-sqlserver-data")
        self.assertEqual(0, result.returncode)
        self.assertIn("rm demo-stopped", calls)
        self.assertEqual("volume rm " + VOLUME, calls[-1])
        self.assertFalse(any("fieldsales-identity-sqlserver-data" in call for call in calls))

    def test_repeat_reset_does_nothing_when_volume_is_absent(self):
        result, calls = self.run_reset(volumes="fieldsales-identity-sqlserver-data")
        self.assertEqual(0, result.returncode)
        self.assertIn("already removed", result.stdout)
        self.assertFalse(any(call.startswith("volume rm") for call in calls))

    def test_docker_failure_does_not_report_success(self):
        result, calls = self.run_reset(failure="volume ls --format {{.Name}}")
        self.assertEqual(1, result.returncode)
        self.assertNotIn("already removed", result.stdout)
        self.assertFalse(any(call.startswith("volume rm") for call in calls))

    def test_container_removal_failure_prevents_volume_removal(self):
        result, calls = self.run_reset(stopped="demo-stopped", failure="rm demo-stopped")
        self.assertEqual(1, result.returncode)
        self.assertFalse(any(call.startswith("volume rm") for call in calls))


if __name__ == "__main__":
    unittest.main()
