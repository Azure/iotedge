"""Daemon-free buildx regression tests; run with python3 -m unittest discover
-s scripts/linux/tests -p test_buildx_scripts.py -v from the repository root.
"""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import textwrap
import unittest


REPO = Path(__file__).resolve().parents[3]
SCRIPTS = ("buildImage.sh", "buildRocksDb.sh")
PLATFORMS = ("linux/amd64", "linux/arm/v7", "linux/arm64")
SUFFIXES = ("linux-amd64", "linux-arm32v7", "linux-arm64v8")
PLATFORM_MAP = [
    {"platform": platform, "tag_suffix": suffix}
    for platform, suffix in zip(
        (PLATFORMS[0], PLATFORMS[2], PLATFORMS[1]),
        (SUFFIXES[0], SUFFIXES[2], SUFFIXES[1]),
    )
]
AUTO_BUILDER = "builder-returned-by-create"
REGISTRY = "registry.invalid"
IMAGE = REGISTRY + "/microsoft/test-image:1.2.3"
FAILURE = 42

# Unknown commands fail rather than accidentally hiding a production-script error.
TOOL_STUB = r'''
import json
import os
from pathlib import Path
import sys

mode, *args = sys.argv[1:]

def record(variable, value):
    with open(os.environ[variable], "a", encoding="utf-8") as log:
        log.write(json.dumps(value) + "\n")

def reject(message):
    sys.exit("unexpected test stub invocation: " + message)

if mode == "docker":
    record("TEST_DOCKER_LOG", args)
    if args in (["version"], ["info"], ["buildx", "version"]):
        operation = "diagnostic"
    elif args[:2] in (["buildx", "create"], ["buildx", "inspect"],
                      ["buildx", "build"], ["buildx", "rm"]):
        operation = args[1]
    elif args[:3] == ["buildx", "imagetools", "create"]:
        operation = "imagetools"
    elif args[:1] == ["run"]:
        operation = "run"
    else:
        reject(repr(args))
    if operation == os.environ.get("TEST_DOCKER_FAIL"):
        print("injected Docker " + operation + " failure", file=sys.stderr)
        sys.exit(42)
    if operation == "create":
        print(os.environ["TEST_CREATED_BUILDER"])
elif mode == "manifests":
    if len(args) != 4:
        reject(repr(args))
    registry, repository, tag, platform_map = args
    record("TEST_MANIFEST_LOG", dict(registry=registry, repository=repository,
                                    tag=tag, platform_map=json.loads(platform_map)))
elif mode == "jq":
    # Only the filter used by buildImage.sh, not a general jq replacement.
    expression = 'map(select(.platform == ($platforms | split(",")[])))'
    if (len(args) != 5 or args[:3] != ["-c", "--arg", "platforms"]
            or "".join(args[4].split()) != "".join(expression.split())):
        reject(repr(args))
    platforms = args[3].split(",")
    print(json.dumps([item for item in json.load(sys.stdin)
                      if item["platform"] in platforms]))
else:
    reject(mode)
'''


@unittest.skipUnless(shutil.which("bash"), "These Linux scripts require Bash")
class BuildxScriptsTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="iotedge-buildx-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.scripts = self.root / "scripts" / "linux"
        self.scripts.mkdir(parents=True)
        for name in SCRIPTS:
            shutil.copyfile(REPO / "scripts" / "linux" / name, self.scripts / name)
        self.tools = self.root / "tools"
        self.tools.mkdir()
        self.helper = self.tools / "stub.py"
        self.helper.write_text(textwrap.dedent(TOOL_STUB), encoding="utf-8")
        self.install_tool("docker", "docker")
        self.install_tool("record-manifests", "manifests")
        if not shutil.which("jq"):
            self.install_tool("jq", "jq")
        (self.scripts / "manifest-tools.sh").write_text(
            "DEFAULT_PLATFORM_MAP='" + json.dumps(PLATFORM_MAP) + "'\n"
            'copy_manifests() {\n'
            '    record-manifests "$REGISTRY" "$REPOSITORY" "$TAG" "$PLATFORM_MAP" "$@"\n'
            '}\n', encoding="utf-8"
        )
        self.binaries = self.root / "binaries"
        for app, directories in (
            ("Test.App", ("docker/linux",)),
            ("api-proxy-module", ("docker/linux/amd64", "docker/linux/arm32v7",
                                  "docker/linux/arm64v8")),
        ):
            for directory in directories:
                folder = self.binaries / "publish" / app / directory
                folder.mkdir(parents=True)
                (folder / "Dockerfile").write_text("FROM scratch\n", encoding="utf-8")
        (self.root / "edge-util/docker/linux").mkdir(parents=True)
        self.output = self.root / "artifacts"
        self.output.mkdir()
        self.config = self.root / "docker-config"
        (self.config / "buildx").mkdir(parents=True)
        (self.config / "config.json").write_text(
            '{"currentContext":"existing-context"}\n', encoding="utf-8"
        )
        (self.config / "buildx/current").write_text(
            '{"Name":"existing-global-builder","Global":true}\n', encoding="utf-8"
        )
        self.docker_log = self.root / "docker.jsonl"
        self.manifest_log = self.root / "manifests.jsonl"

    def install_tool(self, name, mode):
        path = self.tools / name
        path.write_text(
            '#!/bin/sh\nexec "$TEST_PYTHON" "$TEST_HELPER" ' + mode + ' "$@"\n',
            encoding="utf-8",
        )
        path.chmod(0o755)

    @staticmethod
    def snapshot(directory):
        return {str(path.relative_to(directory)): path.read_bytes()
                for path in directory.rglob("*") if path.is_file()}

    @staticmethod
    def read_log(path):
        if not path.exists():
            return []
        return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]

    def run_script(self, target, extra=(), builder_env=None, fail=None,
                   platforms=PLATFORMS, arch="amd64"):
        for log in (self.docker_log, self.manifest_log):
            log.unlink(missing_ok=True)
        env = {key: value for key, value in os.environ.items()
               if not key.startswith(("DOCKER_", "BUILDX_", "BUILDKIT_", "BUILD_", "TEST_"))
               and key not in ("BASH_ENV", "ENV")}
        env.update(
            PATH=str(self.tools) + os.pathsep + os.environ.get("PATH", ""),
            HOME=str(self.root), DOCKER_CONFIG=str(self.config),
            BUILD_REPOSITORY_LOCALPATH=str(self.root),
            # This internal implementation variable is not a public selection input.
            DOCKER_BUILDX_BUILDER="do-not-inherit-this-builder",
            TEST_PYTHON=sys.executable, TEST_HELPER=str(self.helper),
            TEST_DOCKER_LOG=str(self.docker_log), TEST_MANIFEST_LOG=str(self.manifest_log),
            TEST_CREATED_BUILDER=AUTO_BUILDER, TEST_DOCKER_FAIL=fail or "",
        )
        if builder_env is not None:
            env["BUILDX_BUILDER"] = builder_env
        if target == "rocksdb":
            script = "buildRocksDb.sh"
            args = ["--output-dir", str(self.output), "--build-number", "1.2.3",
                    "--arch", arch]
        else:
            script = "buildImage.sh"
            app = "api-proxy-module" if target == "proxy" else "Test.App"
            args = ["--app", app, "--bin", str(self.binaries), "--name", "test-image",
                    "--registry", REGISTRY, "--version", "1.2.3",
                    "--platforms", ",".join(platforms)]
        scripts_before, config_before = self.snapshot(self.scripts), self.snapshot(self.config)
        result = subprocess.run(
            [shutil.which("bash"), str(self.scripts / script), *args, *extra],
            cwd=self.root, env=env, capture_output=True, text=True, timeout=20,
        )
        self.assertEqual(self.snapshot(self.scripts), scripts_before, "Scripts were mutated")
        self.assertEqual(self.snapshot(self.config), config_before, "Docker selection was mutated")
        return result, self.read_log(self.docker_log), self.read_log(self.manifest_log)

    @staticmethod
    def commands(calls, *prefix):
        return [call for call in calls if call[:len(prefix)] == list(prefix)]

    def option(self, call, flag):
        values = [call[index + 1] for index, value in enumerate(call[:-1]) if value == flag]
        values += [value[len(flag) + 1:] for value in call if value.startswith(flag + "=")]
        self.assertEqual(len(values), 1, (flag, call))
        return values[0]

    def assert_lifecycle(self, calls, builder):
        automatic = builder == AUTO_BUILDER
        self.assertEqual(self.commands(calls, "buildx", "create"),
                         [["buildx", "create", "--driver", "docker-container"]]
                         if automatic else [])
        inspect = ["buildx", "inspect", builder, "--bootstrap"]
        self.assertEqual(self.commands(calls, "buildx", "inspect"), [inspect])
        removed = ["buildx", "rm", builder]
        self.assertEqual(self.commands(calls, "buildx", "rm"), [removed] if automatic else [])
        if automatic:
            self.assertLess(calls.index(self.commands(calls, "buildx", "create")[0]),
                            calls.index(inspect))
            self.assertEqual(calls[-1], removed)
        for call in self.commands(calls, "buildx", "build"):
            self.assertEqual(self.option(call, "--builder"), builder)
            self.assertLess(calls.index(inspect), calls.index(call))
        self.assertFalse(self.commands(calls, "buildx", "use"))
        for call in calls:
            self.assertNotIn("--use", call, "Do not change the globally selected builder")

    def assert_outputs(self, target, result, calls, manifests, builder,
                       platforms=PLATFORMS, arch="amd64"):
        builds = self.commands(calls, "buildx", "build")
        if target == "rocksdb":
            platform = dict(amd64="linux/amd64", arm32v7="linux/arm/v7",
                            arm64v8="linux/arm64")[arch]
            self.assertEqual(len(builds), 1)
            build = builds[0]
            self.assertIn("--load", build)
            self.assertEqual(self.option(build, "--platform"), platform)
            tag = "rocksdb-build:main-" + arch + "-1.2.3"
            self.assertEqual(self.option(build, "--tag"), tag)
            self.assertEqual(build[-1], ".")
            self.assertEqual(self.commands(calls, "run"), [[
                "run", "--rm", "--platform", platform, "-v",
                str(self.output / "librocksdb" / platform) + ":/artifacts/" + platform,
                tag, "cp", "/publish/" + platform + "/librocksdb.so",
                "/artifacts/" + platform + "/",
            ]])
            self.assertLess(calls.index(build), calls.index(self.commands(calls, "run")[0]))
            self.assertEqual(manifests, [])
            self.assertFalse(self.commands(calls, "buildx", "imagetools"))
            return

        for command in (["version"], ["buildx", "version"], ["info"]):
            self.assertEqual(calls.count(command), 1)
        self.assertTrue(any("docker buildx inspect" in line and builder in line
                            for line in result.stdout.splitlines()), result.stdout)
        app = "api-proxy-module" if target == "proxy" else "Test.App"
        context = self.binaries / "publish" / app
        expected_platforms = platforms if target == "proxy" else (",".join(platforms),)
        self.assertEqual(len(builds), len(expected_platforms))
        tags = []
        for build, platform in zip(builds, expected_platforms):
            self.assertEqual(self.option(build, "--platform"), platform)
            self.assertEqual(self.option(build, "--provenance"), "mode=min,version=v1")
            self.assertIn("--pull", build)
            self.assertIn("--no-cache", build)
            self.assertNotIn("--load", build)
            suffix = dict(zip(PLATFORMS, SUFFIXES)).get(platform)
            tag = IMAGE + "-" + suffix if target == "proxy" else IMAGE
            tags.append(tag)
            self.assertEqual(self.option(build, "--output"), "type=registry,name=" + tag)
            dockerfile = (context / "docker" / suffix.replace("-", "/", 1) / "Dockerfile"
                          if target == "proxy" else context / "docker/linux/Dockerfile")
            self.assertEqual(self.option(build, "--file"), str(dockerfile))
            self.assertEqual(self.option(build, "--build-arg"), "EXE_DIR=.")
            self.assertEqual(build[-1], str(context))
        self.assertEqual(self.commands(calls, "buildx", "imagetools"),
                         [["buildx", "imagetools", "create", "--builder", builder,
                           "--tag", IMAGE, *tags]]
                         if target == "proxy" else [])
        self.assertEqual(manifests, [dict(
            registry=REGISTRY, repository="microsoft/test-image", tag="1.2.3",
            platform_map=[entry for entry in PLATFORM_MAP if entry["platform"] in platforms],
        )])
        self.assertFalse(self.commands(calls, "run"))

    def check_selections(self, target):
        for label, env, extra, builder in (
            ("unset", None, (), AUTO_BUILDER),
            ("empty", "", (), AUTO_BUILDER),
            ("cli", None, ("--builder", "cli-builder"), "cli-builder"),
            ("environment", "env-builder", (), "env-builder"),
            ("cli-wins", "env-builder", ("--builder", "cli-builder"), "cli-builder"),
            ("default-cli", "env-builder", ("--builder", "default"), "default"),
            ("default-env", "default", (), "default"),
        ):
            with self.subTest(target=target, selection=label):
                result, calls, manifests = self.run_script(target, extra, env)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assert_lifecycle(calls, builder)
                self.assert_outputs(target, result, calls, manifests, builder)

    def test_application_selection_and_outputs(self):
        self.check_selections("app")

    def test_proxy_selection_and_all_platform_outputs(self):
        self.check_selections("proxy")

    def test_rocksdb_selection_and_extraction(self):
        self.check_selections("rocksdb")

    def test_rocksdb_other_architectures(self):
        for arch in ("arm32v7", "arm64v8"):
            with self.subTest(arch=arch):
                result, calls, manifests = self.run_script("rocksdb", builder_env="default", arch=arch)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assert_lifecycle(calls, "default")
                self.assert_outputs("rocksdb", result, calls, manifests, "default", arch=arch)

    def test_bootstrap_and_build_failures_do_not_fall_back(self):
        for target in ("app", "proxy", "rocksdb"):
            for env, extra, builder in (
                (None, (), AUTO_BUILDER),
                (None, ("--builder", "cli-builder"), "cli-builder"),
                ("env-builder", (), "env-builder"),
                ("default", (), "default"),
            ):
                for failure in ("inspect", "build"):
                    with self.subTest(target=target, builder=builder, failure=failure):
                        result, calls, manifests = self.run_script(target, extra, env, failure)
                        self.assertEqual(result.returncode, FAILURE, result.stdout + result.stderr)
                        self.assertIn("injected Docker " + failure + " failure", result.stderr)
                        self.assert_lifecycle(calls, builder)
                        self.assertEqual(len(self.commands(calls, "buildx", "build")),
                                         1 if failure == "build" else 0)
                        self.assertFalse(self.commands(calls, "buildx", "imagetools"))
                        self.assertFalse(self.commands(calls, "run"))
                        self.assertEqual(manifests, [])

    def test_create_failure_does_not_fall_back_or_remove_unknown_builder(self):
        for target in ("app", "proxy", "rocksdb"):
            with self.subTest(target=target):
                result, calls, manifests = self.run_script(target, fail="create")
                self.assertEqual(result.returncode, FAILURE, result.stdout + result.stderr)
                self.assertIn("injected Docker create failure", result.stderr)
                self.assertEqual(self.commands(calls, "buildx", "create"),
                                 [["buildx", "create", "--driver", "docker-container"]])
                for prefix in (("buildx", "inspect"), ("buildx", "build"),
                               ("buildx", "rm"), ("buildx", "imagetools"), ("run",)):
                    self.assertFalse(self.commands(calls, *prefix), calls)
                self.assertEqual(manifests, [])

    def test_malformed_builder_rejected_before_docker(self):
        for target in ("app", "proxy", "rocksdb"):
            for env in (None, "env-builder"):
                next_option = "--arch" if target == "rocksdb" else "--platforms"
                for extra in (("--builder",), ("--builder", ""), ("--builder", next_option)):
                    with self.subTest(target=target, env=env, arguments=extra):
                        result, calls, manifests = self.run_script(target, extra, env)
                        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
                        self.assertIn("--builder", result.stdout + result.stderr)
                        self.assertEqual(calls, [], result.stdout + result.stderr)
                        self.assertEqual(manifests, [])

    def test_manifest_filtering_with_narrow_jq_fallback(self):
        self.install_tool("jq", "jq")
        platforms = ("linux/arm64", "linux/amd64")
        for target in ("app", "proxy"):
            with self.subTest(target=target):
                result, calls, manifests = self.run_script(
                    target, builder_env="env-builder", platforms=platforms
                )
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assert_lifecycle(calls, "env-builder")
                self.assert_outputs(target, result, calls, manifests, "env-builder", platforms)

    def test_application_binaries_argument_preserves_spaces(self):
        binaries = self.root / "binaries with spaces"
        self.binaries.rename(binaries)
        self.binaries = binaries
        result, calls, manifests = self.run_script("app", builder_env="env-builder")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assert_lifecycle(calls, "env-builder")
        self.assert_outputs("app", result, calls, manifests, "env-builder")


if __name__ == "__main__":
    unittest.main()
