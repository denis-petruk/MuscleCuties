#!/usr/bin/env python3
"""Deploy a verified backend release to AWS Lambda and prove what is serving.

This replaces the GitLab-only release engine for GitHub Actions. It never reports
"verified" from a health probe alone: each component's serving CodeSha256 must equal
the candidate ZIP digest, and an alias cutover (plus its revision precondition) is
required so a rollback target exists.

Environment:
  TARGET_ENV, AWS_REGION, AWS_ACCOUNT_ID (optional), HEALTH_URL (optional)
  SERVER_FUNCTION, SERVER_ALIAS, SERVER_ZIP, SERVER_MANIFEST, SERVER_OBJECT
  WORKER_FUNCTION, WORKER_ALIAS, WORKER_ZIP, WORKER_MANIFEST, WORKER_OBJECT
  REPORT_PATH, SOURCE_SHA (optional), SOURCE_TREE_SHA (optional)
  MC_TEST_AWS_EXECUTABLE (offline tests only)

Usage: deploy_release.py deploy | rollback
"""

import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

HEX = re.compile(r"[0-9a-f]{64}\Z")
SHA = re.compile(r"[0-9a-f]{40}\Z")
TARGET_ENV = os.environ.get("TARGET_ENV", "").strip()
REGION = os.environ.get("AWS_REGION", "us-east-2").strip()
REPORT_PATH = Path(os.environ.get("REPORT_PATH", "artifacts/deployment-report.json"))
STARTED = datetime.now(timezone.utc).isoformat()


class Failure(Exception):
    def __init__(self, code, phase):
        super().__init__(code)
        self.code = code
        self.phase = phase


def require(condition, code, phase):
    if not condition:
        raise Failure(code, phase)


def value(name, phase="source", required=True, default=""):
    result = os.environ.get(name, default).strip()
    require(not required or bool(result), f"missing_{name.lower()}", phase)
    return result


def read_json(path, code, phase):
    try:
        return json.loads(Path(path).read_text())
    except (OSError, ValueError):
        raise Failure(code, phase) from None


def aws_raw(*args, code, phase, timeout=120):
    exe = os.environ.get("MC_TEST_AWS_EXECUTABLE", "aws")
    try:
        completed = subprocess.run(
            [exe, *args, "--region", REGION, "--output", "json"],
            capture_output=True, text=True, timeout=timeout, check=False)
    except (OSError, subprocess.TimeoutExpired):
        raise Failure(code, phase) from None
    # Provider stderr/stdout can contain account or connection details. Never echo it.
    if completed.returncode != 0:
        raise Failure(code, phase)
    return completed.stdout


def aws(*args, code, phase, timeout=120):
    raw = aws_raw(*args, code=code, phase=phase, timeout=timeout)
    try:
        return json.loads(raw or "{}")
    except ValueError:
        raise Failure(code + "_invalid_response", phase) from None


def component(name, function_env, alias_env, zip_env, manifest_env, object_env):
    return {
        "name": name,
        "function": value(function_env, "source"),
        "alias": value(alias_env, "source"),
        "zip": Path(value(zip_env, "source")),
        "manifest_path": Path(value(manifest_env, "source")),
        "object_path": Path(value(object_env, "source")),
    }


def validate_component(spec, source_sha, tree_sha):
    phase = "artifact"
    data = spec["zip"].read_bytes()
    digest = hashlib.sha256(data).digest()
    hex_digest = digest.hex()
    b64_digest = base64.b64encode(digest).decode()
    manifest = read_json(spec["manifest_path"], "manifest_unavailable", phase)
    obj = read_json(spec["object_path"], "release_object_unavailable", phase)

    require(manifest.get("source_sha") == source_sha and manifest.get("tree_sha") == tree_sha,
            "manifest_source_mismatch", phase)
    require(SHA.fullmatch(source_sha or "") and SHA.fullmatch(tree_sha or ""),
            "invalid_source_identity", phase)
    require(manifest.get("zip_sha256") == hex_digest and
            manifest.get("zip_sha256_base64") == b64_digest and
            manifest.get("zip_bytes") == len(data), "manifest_zip_mismatch", phase)
    require(obj.get("zip_sha256") == hex_digest and
            obj.get("zip_sha256_base64") == b64_digest and
            obj.get("zip_bytes") == len(data), "release_object_mismatch", phase)
    require(isinstance(obj.get("version_id"), str) and obj["version_id"] not in ("", "null"),
            "release_object_unversioned", phase)
    require(isinstance(obj.get("bucket"), str) and obj["bucket"], "release_bucket_missing", phase)
    expected_prefix = f"lambda/{spec['name']}/{source_sha}/{hex_digest}/"
    require(isinstance(obj.get("key"), str) and obj["key"].startswith(expected_prefix),
            "release_object_key_mismatch", phase)

    return {
        "function": spec["function"],
        "alias": spec["alias"],
        "zip_sha256": hex_digest,
        "code_sha256_base64": b64_digest,
        "zip_bytes": len(data),
        "bucket": obj["bucket"],
        "key": obj["key"],
        "object_version": obj["version_id"],
        "source_sha": source_sha,
        "tree_sha": tree_sha,
        "pre_code_sha256": None,
        "post_code_sha256": None,
        "pre_version": None,
        "post_version": None,
        "previous_alias_version": None,
        "previous_code_sha256": None,
        "action": None,
    }


def verify_identity(account_id):
    phase = "target"
    identity = aws("sts", "get-caller-identity", code="aws_identity_unavailable", phase=phase)
    if account_id:
        require(identity.get("Account") == account_id, "aws_account_mismatch", phase)
    return identity.get("Account")


def get_configuration(function, phase="target"):
    config = aws("lambda", "get-function-configuration", "--function-name", function,
                 code="lambda_configuration_unavailable", phase=phase)
    require(config.get("State") == "Active", "lambda_not_active", phase)
    require(config.get("LastUpdateStatus") in (None, "Successful"), "lambda_update_in_progress", phase)
    require(isinstance(config.get("CodeSha256"), str) and config["CodeSha256"], "lambda_code_sha_missing", phase)
    require(isinstance(config.get("RevisionId"), str) and config["RevisionId"], "lambda_revision_missing", phase)
    return config


def get_served_code(function, qualifier):
    response = aws("lambda", "get-function", "--function-name", function, "--qualifier", qualifier,
                   code="served_function_unavailable", phase="runtime")
    config = response.get("Configuration", {})
    require(isinstance(config.get("CodeSha256"), str) and config["CodeSha256"],
            "served_code_sha_missing", "runtime")
    return config


def deploy_component(spec, proof):
    phase = "activation"
    function = spec["function"]
    alias_name = spec["alias"]
    before = get_configuration(function)
    proof["pre_code_sha256"] = before["CodeSha256"]
    proof["pre_version"] = before.get("Version")

    if before["CodeSha256"] != proof["code_sha256_base64"]:
        updated = aws("lambda", "update-function-code",
                      "--function-name", function,
                      "--s3-bucket", proof["bucket"],
                      "--s3-key", proof["key"],
                      "--s3-object-version", proof["object_version"],
                      "--revision-id", before["RevisionId"],
                      code="lambda_update_rejected", phase=phase)
        require(updated.get("CodeSha256") == proof["code_sha256_base64"],
                "lambda_update_hash_mismatch", phase)
        aws_raw("lambda", "wait", "function-updated", "--function-name", function,
                code="lambda_wait_failed", phase=phase, timeout=360)
        after = get_configuration(function, phase="runtime")
        require(after["CodeSha256"] == proof["code_sha256_base64"],
                "lambda_post_update_hash_mismatch", "runtime")
        proof["action"] = "updated"
    else:
        after = before
        proof["action"] = "already_serving"

    alias = aws("lambda", "get-alias", "--function-name", function, "--name", alias_name,
                code="alias_unavailable", phase=phase)
    require(isinstance(alias.get("RevisionId"), str) and alias["RevisionId"],
            "alias_revision_missing", phase)
    proof["previous_alias_version"] = alias.get("FunctionVersion")
    require(isinstance(proof["previous_alias_version"], str) and proof["previous_alias_version"].isdigit(),
            "previous_alias_version_invalid", phase)
    proof["previous_code_sha256"] = get_served_code(
        function, proof["previous_alias_version"]).get("CodeSha256")

    alias_served = get_served_code(function, alias["FunctionVersion"])
    if alias_served.get("CodeSha256") == proof["code_sha256_base64"]:
        version = alias["FunctionVersion"]
    else:
        published = aws("lambda", "publish-version", "--function-name", function,
                        "--code-sha256", proof["code_sha256_base64"],
                        "--revision-id", after["RevisionId"],
                        code="candidate_publish_failed", phase=phase)
        version = published.get("Version")
        require(isinstance(version, str) and version.isdigit(), "candidate_version_invalid", phase)
        changed = aws("lambda", "update-alias", "--function-name", function,
                      "--name", alias_name, "--function-version", version,
                      "--revision-id", alias["RevisionId"],
                      code="alias_cutover_failed", phase=phase)
        require(changed.get("FunctionVersion") == version, "alias_cutover_mismatch", phase)

    confirmed = aws("lambda", "get-alias", "--function-name", function, "--name", alias_name,
                    code="alias_confirmation_unavailable", phase="runtime")
    require(confirmed.get("FunctionVersion") == version, "alias_confirmation_mismatch", "runtime")
    served = get_served_code(function, version)
    require(served.get("CodeSha256") == proof["code_sha256_base64"],
            "served_digest_mismatch", "runtime")

    proof["post_code_sha256"] = served["CodeSha256"]
    proof["post_version"] = version
    return proof


def check_health(base_url):
    if not base_url:
        return {"status": "not_checked"}
    require(base_url.startswith("https://"), "health_url_must_be_https", "health")
    results = {}
    for path in ("/health/live", "/health/ready"):
        last = None
        for _ in range(12):
            try:
                with urllib.request.urlopen(base_url.rstrip("/") + path, timeout=10) as response:
                    if response.status == 200:
                        results[path] = 200
                        last = None
                        break
                    last = response.status
            except (urllib.error.URLError, TimeoutError, OSError) as error:
                last = type(error).__name__
            time.sleep(5)
        if last is not None:
            raise Failure(f"health_check_failed_{path.rsplit('/', 1)[-1]}", "health")
    return {"status": "passed", "checks": results}


def write_report(report):
    report["finished_at_utc"] = datetime.now(timezone.utc).isoformat()
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    content = (json.dumps(report, indent=2, sort_keys=True) + "\n").encode()
    with tempfile.NamedTemporaryFile(dir=REPORT_PATH.parent, prefix=".deployment-report-", delete=False) as handle:
        handle.write(content)
        temporary = Path(handle.name)
    temporary.replace(REPORT_PATH)


def source_identity():
    source = os.environ.get("SOURCE_SHA", "").strip() or os.environ.get("GITHUB_SHA", "").strip()
    tree = os.environ.get("SOURCE_TREE_SHA", "").strip()
    if not tree:
        try:
            tree = subprocess.check_output(["git", "rev-parse", "HEAD^{tree}"], text=True).strip()
        except (OSError, subprocess.CalledProcessError):
            tree = ""
    return source, tree


def deploy():
    report = {
        "contract_version": 2,
        "status": "blocked",
        "phase": "source",
        "failure_code": "unverified",
        "environment": TARGET_ENV,
        "region": REGION,
        "source_sha": None,
        "tree_sha": None,
        "components": {},
        "health": {"status": "not_checked"},
        "mutation_attempted": False,
        "recovery": "none",
        "started_at_utc": STARTED,
        "finished_at_utc": None,
    }

    try:
        require(TARGET_ENV == "beta", "target_environment_must_be_beta", "source")
        require(REGION == "us-east-2", "region_mismatch", "source")
        if os.environ.get("GITHUB_EVENT_NAME"):
            require(os.environ["GITHUB_EVENT_NAME"] == "workflow_dispatch",
                    "manual_release_required", "source")
        if os.environ.get("GITHUB_REPOSITORY"):
            require(os.environ["GITHUB_REPOSITORY"] == "denis-petruk/MuscleCuties",
                    "repository_mismatch", "source")

        source_sha, tree_sha = source_identity()
        require(SHA.fullmatch(source_sha or ""), "invalid_source_sha", "source")
        require(SHA.fullmatch(tree_sha or ""), "invalid_tree_sha", "source")
        report["source_sha"] = source_sha
        report["tree_sha"] = tree_sha
        report["account_id"] = verify_identity(os.environ.get("AWS_ACCOUNT_ID", "").strip())

        specs = [
            ("server", component("Server", "SERVER_FUNCTION", "SERVER_ALIAS",
                                 "SERVER_ZIP", "SERVER_MANIFEST", "SERVER_OBJECT")),
            ("worker", component("WorkoutReconciliation", "WORKER_FUNCTION", "WORKER_ALIAS",
                                 "WORKER_ZIP", "WORKER_MANIFEST", "WORKER_OBJECT")),
        ]

        report["phase"] = "artifact"
        proofs = [(name, spec, validate_component(spec, source_sha, tree_sha))
                  for name, spec in specs]

        for name, spec, proof in proofs:
            report["components"][name] = proof
            report["phase"] = "activation"
            report["mutation_attempted"] = True
            deploy_component(spec, proof)

        report["phase"] = "health"
        report["health"] = check_health(os.environ.get("HEALTH_URL", "").strip())

        report["status"] = "verified"
        report["failure_code"] = "none"
        report["phase"] = "complete"
    except Failure as error:
        report["failure_code"] = error.code
        report["phase"] = error.phase
        report["status"] = "partial" if report["mutation_attempted"] else "blocked"
        if report["mutation_attempted"]:
            report["recovery"] = "manual_rollback_required"
    except Exception:
        report["failure_code"] = "internal_contract_error"
        report["status"] = "partial" if report["mutation_attempted"] else "blocked"
        if report["mutation_attempted"]:
            report["recovery"] = "manual_rollback_required"

    write_report(report)
    print("release_status=" + report["status"] + " code=" + report["failure_code"])
    return 0 if report["status"] == "verified" else 1


def rollback():
    report = read_json(REPORT_PATH, "deployment_report_unavailable", "rollback")
    require(report.get("contract_version") == 2, "deployment_report_contract_invalid", "rollback")
    components = report.get("components", {})
    require(bool(components), "deployment_report_has_no_components", "rollback")
    rollback_results = {}
    try:
        for name, component_report in components.items():
            function = component_report["function"]
            alias_name = component_report["alias"]
            previous = component_report.get("previous_alias_version")
            previous_code = component_report.get("previous_code_sha256")
            require(isinstance(previous, str) and previous.isdigit(),
                    f"no_rollback_target_{name}", "rollback")
            require(isinstance(previous_code, str) and previous_code,
                    f"no_rollback_digest_{name}", "rollback")
            alias = aws("lambda", "get-alias", "--function-name", function, "--name", alias_name,
                        code="alias_unavailable", phase="rollback")
            changed = aws("lambda", "update-alias", "--function-name", function,
                          "--name", alias_name, "--function-version", previous,
                          "--revision-id", alias["RevisionId"],
                          code="rollback_alias_update_failed", phase="rollback")
            require(changed.get("FunctionVersion") == previous, "rollback_alias_mismatch", "rollback")
            served = get_served_code(function, previous)
            require(served.get("CodeSha256") == previous_code, "rollback_digest_mismatch", "rollback")
            rollback_results[name] = {"alias": alias_name, "restored_version": previous,
                                      "restored_code_sha256": served["CodeSha256"]}
        report["status"] = "rolled_back"
        report["failure_code"] = "none"
        report["phase"] = "rollback_complete"
        report["rollback"] = rollback_results
        report["rollback_finished_at_utc"] = datetime.now(timezone.utc).isoformat()
    except Failure as error:
        report["rollback_failure_code"] = error.code
        report["status"] = "rollback_failed"
        write_report(report)
        print("rollback_status=failed code=" + error.code)
        return 1

    write_report(report)
    print("rollback_status=verified")
    return 0


def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ("deploy", "rollback"):
        print("Usage: deploy_release.py deploy | rollback", file=sys.stderr)
        return 2
    return deploy() if sys.argv[1] == "deploy" else rollback()


if __name__ == "__main__":
    sys.exit(main())
