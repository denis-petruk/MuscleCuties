#!/usr/bin/env python3
"""Guarded Lambda rollout. A trusted database lock owner must run this process.

The entry point in release.sh does not invoke this engine until the separate
DbMaintenance lock-owner contract is installed. This module is exercised with
fake provider executables in offline tests; it never supplies credentials.
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
from datetime import datetime, timezone


EXPECTED_PROJECT = "denis-petruk/musclecuties-testflight-beta"
EXPECTED_REGION = "us-east-2"
HASH = re.compile(r"[0-9a-f]{64}\Z")
SHA = re.compile(r"[0-9a-f]{40}\Z")


class Blocked(Exception):
    def __init__(self, code):
        super().__init__(code)
        self.code = code


def require(condition, code):
    if not condition:
        raise Blocked(code)


def read_json(path, code):
    try:
        return json.loads(Path(path).read_text())
    except (OSError, ValueError):
        raise Blocked(code) from None


def digest(path):
    try:
        return hashlib.sha256(Path(path).read_bytes()).hexdigest()
    except OSError:
        raise Blocked("requirements_unavailable") from None


def command(argv, code, timeout=90):
    try:
        completed = subprocess.run(argv, text=True, capture_output=True, timeout=timeout, check=False)
    except (OSError, subprocess.TimeoutExpired):
        raise Blocked(code) from None
    # Never copy provider stderr/stdout into release artifacts or CI logs.
    if completed.returncode != 0:
        raise Blocked(code)
    return completed.stdout


def command_json(argv, code, timeout=90):
    raw = command(argv, code, timeout)
    try:
        return json.loads(raw)
    except ValueError:
        raise Blocked(code + "_invalid_response") from None


class Release:
    def __init__(self, policy_path, manifest_path, object_path, report_path):
        self.policy = read_json(policy_path, "policy_unavailable")
        self.manifest = read_json(manifest_path, "manifest_unavailable")
        self.object = read_json(object_path, "object_unavailable")
        self.report_path = Path(report_path)
        self.report = {
            "contract_version": 1,
            "status": "blocked",
            "phase": "source",
            "failure_code": "unverified",
            "source_sha": self.manifest.get("source_sha"),
            "tree_sha": self.manifest.get("tree_sha"),
            "zip_sha256": self.manifest.get("zip_sha256"),
            "pipeline_id": os.environ.get("CI_PIPELINE_ID"),
            "job_id": os.environ.get("CI_JOB_ID"),
            "target_id": self.policy.get("target_id"),
            "artifact_bucket": self.object.get("bucket"),
            "artifact_key": self.object.get("key"),
            "artifact_version": self.object.get("version_id"),
            "pre_revision": None,
            "post_revision": None,
            "pre_code_sha256": None,
            "post_code_sha256": None,
            "db_fingerprint_sha256": None,
            "migration_history_mode": None,
            "backup_snapshot_id": None,
            "mutation_attempted": False,
            "recovery": "none",
            "started_at_utc": datetime.now(timezone.utc).isoformat(),
            "finished_at_utc": None,
        }
        self.mutated = False
        self.before = None
        self.after = None
        os.environ["MC_RELEASE_LOG_START_MS"] = str(int(datetime.now(timezone.utc).timestamp() * 1000))
        self.aws_exe = "aws"
        if os.environ.get("MC_RELEASE_OFFLINE_TEST") == "1":
            require(os.environ.get("CI") != "true", "mock_mode_forbidden_in_ci")
            self.aws_exe = os.environ.get("MC_TEST_AWS_EXECUTABLE", "")
            require(Path(self.aws_exe).is_file(), "mock_aws_unavailable")

    def aws(self, service, operation, code, *args, timeout=90):
        return command_json([self.aws_exe, service, operation, *args,
                             "--region", EXPECTED_REGION, "--output", "json"], code, timeout)

    def aws_wait(self):
        command([self.aws_exe, "lambda", "wait", "function-updated",
                 "--function-name", self.policy["function_arn"],
                 "--region", EXPECTED_REGION], "lambda_wait_failed", 360)

    def validate_inputs(self):
        p, m, o = self.policy, self.manifest, self.object
        self.report["phase"] = "source"
        require(os.environ.get("CI_PROJECT_PATH") == EXPECTED_PROJECT and
                os.environ.get("CI_PIPELINE_SOURCE") == "web" and
                os.environ.get("CI_COMMIT_REF_PROTECTED") == "true" and
                os.environ.get("CI_DEBUG_TRACE", "false").lower() != "true",
                "untrusted_ci_context")
        require(p.get("contract_version") == 1 and p.get("target_id") == os.environ.get("CI_ENVIRONMENT_NAME"),
                "target_policy_mismatch")
        branch = os.environ.get("CI_COMMIT_BRANCH")
        require((p["target_id"] == "staging" and branch == "codex/plan-generation-checkpoint") or
                (p["target_id"] == "production" and branch == "test-flight-beta"),
                "target_ref_mismatch")
        require(p.get("region") == EXPECTED_REGION and
                re.fullmatch(r"[0-9]{12}", p.get("account_id", "")) and
                p.get("function_arn") == f"arn:aws:lambda:{EXPECTED_REGION}:{p['account_id']}:function:{p.get('function_name')}",
                "target_identity_invalid")
        require(p.get("approved_source_sha") == m.get("source_sha") == os.environ.get("CI_COMMIT_SHA") and
                p.get("approved_tree_sha") == m.get("tree_sha") == os.environ.get("CI_COMMIT_TREE_SHA") and
                SHA.fullmatch(m.get("source_sha", "")) and SHA.fullmatch(m.get("tree_sha", "")),
                "source_not_approved")
        require(p.get("release_bucket") == o.get("bucket") and
                o.get("key") == f"lambda/MuscleCuties.Server/{m['source_sha']}/{m.get('zip_sha256')}/MuscleCuties.Server.zip" and
                isinstance(o.get("version_id"), str) and o["version_id"] not in ("", "null") and
                HASH.fullmatch(m.get("zip_sha256", "")) and
                o.get("zip_sha256") == m.get("zip_sha256") and
                o.get("zip_sha256_base64") == m.get("zip_sha256_base64") and
                o.get("zip_bytes") == m.get("zip_bytes") and
                m.get("runtime") == "net10.0" and m.get("architecture") == "arm64" and
                m.get("handler") == "MuscleCuties.Server", "artifact_identity_invalid")
        require(m.get("requirements_sha256") == p.get("candidate_requirements_sha256") and
                HASH.fullmatch(p.get("candidate_requirements_sha256", "")) and
                digest(p.get("candidate_requirements_path", "")) == p["candidate_requirements_sha256"],
                "candidate_requirements_invalid")
        require(HASH.fullmatch(p.get("serving_requirements_sha256", "")) and
                digest(p.get("serving_requirements_path", "")) == p["serving_requirements_sha256"],
                "serving_requirements_invalid")
        require(p.get("integration_mode") in ("unqualified", "alias") and
                p.get("maintenance_window_approved") is True and
                p.get("backup_approved") is True and
                p.get("restore_drill_evidence_sha256") and
                HASH.fullmatch(p["restore_drill_evidence_sha256"]), "release_approval_missing")
        if p["target_id"] == "production":
            require(p.get("production_approval_contract") == "enforced", "production_approval_unavailable")
        require(p.get("reconciliation") in (None, "none"), "reconciliation_contract_unavailable")

    def verify_identity_and_target(self):
        self.report["phase"] = "target"
        p = self.policy
        identity = self.aws("sts", "get-caller-identity", "aws_identity_unavailable")
        require(identity.get("Account") == p["account_id"], "aws_account_mismatch")
        rds = self.aws("rds", "describe-db-instances", "rds_identity_unavailable",
                       "--db-instance-identifier", p["database_identifier"])
        instances = rds.get("DBInstances", [])
        require(len(instances) == 1, "rds_identity_mismatch")
        db = instances[0]
        require(db.get("DBInstanceIdentifier") == p["database_identifier"] and
                db.get("DBName") == p.get("database_name") and
                db.get("Endpoint", {}).get("Address") == p.get("database_host") and
                db.get("DBInstanceStatus") == "available", "rds_identity_mismatch")
        backup = self.aws("rds", "describe-db-snapshots", "backup_unavailable",
                          "--db-snapshot-identifier", p["backup_snapshot_id"])
        snapshots = backup.get("DBSnapshots", [])
        require(len(snapshots) == 1 and snapshots[0].get("Status") == "available" and
                snapshots[0].get("DBInstanceIdentifier") == p["database_identifier"],
                "backup_incomplete")
        self.report["backup_snapshot_id"] = p["backup_snapshot_id"]
        self.verify_api_integration()
        config = self.configuration()
        self.assert_config(config)
        self.before = config
        self.report["pre_revision"] = config["RevisionId"]
        self.report["pre_code_sha256"] = config["CodeSha256"]

    def verify_api_integration(self):
        p = self.policy
        integrations = self.aws("apigatewayv2", "get-integrations", "api_integration_unavailable",
                                "--api-id", p["api_id"])
        uris = [v.get("IntegrationUri") for v in integrations.get("Items", [])
                if v.get("IntegrationType") == "AWS_PROXY"]
        expected = p.get("approved_integration_uri")
        expected_function = p["function_arn"] + (":" + p.get("alias", "") if p["integration_mode"] == "alias" else "")
        require(isinstance(expected, str) and expected_function in expected and
                len(uris) == 1 and uris[0] == expected, "api_integration_mismatch")

    def configuration(self):
        return self.aws("lambda", "get-function-configuration", "lambda_configuration_unavailable",
                        "--function-name", self.policy["function_arn"])

    def assert_config(self, c):
        p = self.policy
        require(c.get("FunctionArn") == p["function_arn"] and
                c.get("Runtime") == "dotnet10" and c.get("Architectures") == ["arm64"] and
                c.get("PackageType") == "Zip" and c.get("Handler") == "MuscleCuties.Server" and
                c.get("State") == "Active" and c.get("LastUpdateStatus") == "Successful" and
                isinstance(c.get("RevisionId"), str) and c["RevisionId"] and
                isinstance(c.get("CodeSha256"), str) and c["CodeSha256"], "lambda_configuration_mismatch")
        # The actual connection is inspected only in memory; never include it in a
        # report, exception, command argument, or log.
        connection = c.get("Environment", {}).get("Variables", {}).get("ConnectionStrings__PostgreSql", "")
        fields = {}
        for item in connection.split(";"):
            if "=" in item:
                k, value = item.split("=", 1)
                fields[k.strip().lower()] = value.strip()
        require(fields.get("host") == p.get("database_host") and
                fields.get("database") == p.get("database_name") and
                fields.get("ssl mode", fields.get("sslmode", "")).lower() in ("verifyfull", "verify full"),
                "lambda_database_binding_mismatch")
        certificate = fields.get("root certificate", fields.get("rootcertificate", ""))
        if certificate:
            # Lambda deployment content is rooted at /var/task. An absolute
            # certificate path outside the package cannot be justified here.
            prefix = "/var/task/"
            require(certificate.startswith(prefix) and
                    certificate[len(prefix):] in {f.get("path") for f in self.manifest.get("files", [])},
                    "lambda_root_certificate_missing")

    def verify_object(self):
        self.report["phase"] = "artifact"
        o = self.object
        head = self.aws("s3api", "head-object", "release_object_unavailable",
                        "--bucket", o["bucket"], "--key", o["key"],
                        "--version-id", o["version_id"], "--checksum-mode", "ENABLED")
        require(head.get("VersionId") == o["version_id"] and
                head.get("ContentLength") == o["zip_bytes"] and
                head.get("ChecksumSHA256") == o["zip_sha256_base64"] and
                head.get("Metadata", {}).get("zip-sha256") == o["zip_sha256"] and
                head.get("Metadata", {}).get("source-sha") == self.manifest["source_sha"],
                "release_object_mismatch")

    def maintenance(self, verb, path, expected_hash):
        project = "src/MuscleCuties.DbMaintenance/MuscleCuties.DbMaintenance.csproj"
        argv = ["dotnet", "run", "--project", project, "--configuration", "Release",
                "--no-build", "--no-restore", "--", verb, path, expected_hash]
        if os.environ.get("MC_RELEASE_OFFLINE_TEST") == "1":
            argv = [os.environ["MC_TEST_MAINTENANCE_EXECUTABLE"], verb, path, expected_hash]
        raw = command_json(argv, "schema_" + verb.replace("-", "_") + "_failed", 120)
        require(raw.get("contract_version") == "1.0.0" and raw.get("status") == "compatible" and
                raw.get("requirements_sha256") == expected_hash and
                HASH.fullmatch(raw.get("db_fingerprint_sha256", "")) and
                isinstance(raw.get("migration_history_mode"), str) and raw["migration_history_mode"] and
                raw.get("failure_codes") == [], "schema_contract_invalid")
        return raw

    def compatibility(self):
        self.report["phase"] = "database"
        p = self.policy
        serving = self.maintenance("schema-preflight", p["serving_requirements_path"],
                                   p["serving_requirements_sha256"])
        candidate = self.maintenance("schema-preflight", p["candidate_requirements_path"],
                                     p["candidate_requirements_sha256"])
        require(serving["db_fingerprint_sha256"] == candidate["db_fingerprint_sha256"] and
                serving["migration_history_mode"] == candidate["migration_history_mode"],
                "schema_proof_inconsistent")
        self.report["db_fingerprint_sha256"] = candidate["db_fingerprint_sha256"]
        self.report["migration_history_mode"] = candidate["migration_history_mode"]
        return candidate

    def verify_compatibility_again(self, prior):
        p = self.policy
        for path, expected in ((p["serving_requirements_path"], p["serving_requirements_sha256"]),
                               (p["candidate_requirements_path"], p["candidate_requirements_sha256"])):
            result = self.maintenance("schema-verify", path, expected)
            require(result["db_fingerprint_sha256"] == prior["db_fingerprint_sha256"] and
                    result["migration_history_mode"] == prior["migration_history_mode"],
                    "schema_changed_under_release")

    def update_lambda(self, o, revision, expected_hash):
        response = self.aws("lambda", "update-function-code", "lambda_update_rejected",
                            "--function-name", self.policy["function_arn"],
                            "--s3-bucket", o["bucket"], "--s3-key", o["key"],
                            "--s3-object-version", o["version_id"],
                            "--revision-id", revision)
        require(response.get("CodeSha256") == expected_hash, "lambda_update_hash_mismatch")
        self.aws_wait()
        updated = self.configuration()
        self.assert_config(updated)
        require(updated["CodeSha256"] == expected_hash, "lambda_post_update_hash_mismatch")
        return updated

    def verify_runtime(self, expected_hash, qualifier=None):
        self.report["phase"] = "runtime"
        argv = [str(Path(__file__).with_name("verify-runtime.sh")), self.policy["function_arn"],
                expected_hash, self.policy["api_id"]]
        if qualifier:
            argv.append(qualifier)
        if os.environ.get("MC_RELEASE_OFFLINE_TEST") == "1":
            argv = [os.environ["MC_TEST_RUNTIME_EXECUTABLE"], expected_hash, qualifier or "serving"]
        command(argv, "runtime_verification_failed", 180)

    def deploy(self):
        self.validate_inputs()
        self.verify_identity_and_target()
        self.verify_object()
        proof = self.compatibility()
        self.verify_compatibility_again(proof)
        self.report["phase"] = "stale_guard"
        current = self.configuration()
        self.assert_config(current)
        require(current["RevisionId"] == self.before["RevisionId"], "stale_lambda_revision")
        expected_hash = self.manifest["zip_sha256_base64"]
        if current["CodeSha256"] == expected_hash:
            self.after = current
            self.report["phase"] = "duplicate"
            self.verify_runtime(expected_hash)
            self.report["status"] = "verified"
            self.report["failure_code"] = "already_serving_verified"
            return
        self.report["phase"] = "activation"
        self.mutated = True
        self.report["mutation_attempted"] = True
        updated = self.update_lambda(self.object, current["RevisionId"], expected_hash)
        self.after = updated
        self.report["post_revision"] = updated["RevisionId"]
        self.report["post_code_sha256"] = updated["CodeSha256"]
        if self.policy["integration_mode"] == "alias":
            self.report["phase"] = "candidate"
            published = self.aws("lambda", "publish-version", "candidate_publish_failed",
                                 "--function-name", self.policy["function_arn"],
                                 "--code-sha256", expected_hash,
                                 "--revision-id", updated["RevisionId"])
            version = published.get("Version")
            require(isinstance(version, str) and version.isdecimal(), "candidate_version_invalid")
            self.verify_runtime(expected_hash, version)
            alias = self.aws("lambda", "get-alias", "alias_unavailable",
                             "--function-name", self.policy["function_arn"],
                             "--name", self.policy["alias"])
            require(alias.get("FunctionVersion") == self.policy.get("expected_serving_version") and
                    alias.get("RevisionId"), "alias_stale")
            changed = self.aws("lambda", "update-alias", "alias_cutover_failed",
                               "--function-name", self.policy["function_arn"],
                               "--name", self.policy["alias"], "--function-version", version,
                               "--revision-id", alias["RevisionId"])
            require(changed.get("FunctionVersion") == version, "alias_cutover_mismatch")
            self.report["candidate_version"] = version
            self.verify_api_integration()
        # Unqualified API traffic changed when UpdateFunctionCode ran; this is
        # permissible only inside the approved maintenance window.
        self.verify_runtime(expected_hash)
        self.verify_compatibility_again(proof)
        self.report["status"] = "verified"
        self.report["failure_code"] = "none"
        self.report["phase"] = "complete"

    def recover(self):
        self.report["phase"] = "recovery"
        good = self.policy.get("known_good")
        if not isinstance(good, dict) or good.get("runtime_verified") is not True or \
                good.get("db_fingerprint_sha256") != self.report["db_fingerprint_sha256"] or \
                good.get("migration_history_mode") != self.report["migration_history_mode"]:
            self.report["recovery"] = "manual_required"
            return
        try:
            requirements_path = good.get("requirements_path", "")
            requirements_hash = good.get("requirements_sha256", "")
            require(HASH.fullmatch(requirements_hash) and digest(requirements_path) == requirements_hash,
                    "unsafe_recovery_requirements")
            proof = self.maintenance("schema-verify", requirements_path, requirements_hash)
            require(proof["db_fingerprint_sha256"] == self.report["db_fingerprint_sha256"] and
                    proof["migration_history_mode"] == self.report["migration_history_mode"],
                    "unsafe_recovery_schema")
            if self.policy["integration_mode"] == "alias":
                require(good.get("version") and good.get("alias") == self.policy.get("alias"),
                        "unsafe_recovery_target")
                alias = self.aws("lambda", "get-alias", "recovery_alias_unavailable",
                                 "--function-name", self.policy["function_arn"],
                                 "--name", self.policy["alias"])
                require(alias.get("RevisionId") and alias.get("FunctionVersion") == self.report.get("candidate_version"),
                        "recovery_alias_raced")
                self.aws("lambda", "update-alias", "recovery_alias_update_failed",
                         "--function-name", self.policy["function_arn"],
                         "--name", self.policy["alias"], "--function-version", good["version"],
                         "--revision-id", alias["RevisionId"])
                self.verify_runtime(good["code_sha256_base64"])
            else:
                require(all(good.get(k) for k in ("bucket", "key", "version_id", "code_sha256_base64", "zip_sha256", "source_sha")) and
                        good["bucket"] == self.policy["release_bucket"] and
                        HASH.fullmatch(good["zip_sha256"]) and SHA.fullmatch(good["source_sha"]),
                        "unsafe_recovery_target")
                head = self.aws("s3api", "head-object", "recovery_object_unavailable",
                                "--bucket", good["bucket"], "--key", good["key"],
                                "--version-id", good["version_id"], "--checksum-mode", "ENABLED")
                require(head.get("VersionId") == good["version_id"] and
                        head.get("ChecksumSHA256") == good["code_sha256_base64"] and
                        head.get("Metadata", {}).get("zip-sha256") == good["zip_sha256"] and
                        head.get("Metadata", {}).get("source-sha") == good["source_sha"],
                        "unsafe_recovery_object")
                current = self.configuration()
                self.assert_config(current)
                require(current["CodeSha256"] == self.manifest["zip_sha256_base64"],
                        "recovery_target_changed")
                self.update_lambda(good, current["RevisionId"], good["code_sha256_base64"])
                self.verify_runtime(good["code_sha256_base64"])
            self.report["recovery"] = "verified_known_good"
        except Blocked:
            self.report["recovery"] = "failed_manual_required"

    def save_report(self):
        self.report["finished_at_utc"] = datetime.now(timezone.utc).isoformat()
        self.report_path.parent.mkdir(parents=True, exist_ok=True)
        content = (json.dumps(self.report, sort_keys=True, indent=2) + "\n").encode()
        with tempfile.NamedTemporaryFile(dir=self.report_path.parent, prefix=".release-report-", delete=False) as handle:
            handle.write(content)
            temporary = Path(handle.name)
        temporary.replace(self.report_path)
        if self.mutated:
            key = f"reports/{self.policy['target_id']}/{self.manifest['source_sha']}/{os.environ.get('CI_PIPELINE_ID','unknown')}-{os.environ.get('CI_JOB_ID','unknown')}.json"
            checksum = base64.b64encode(hashlib.sha256(content).digest()).decode()
            # Conditional write preserves an earlier report from the same attempt.
            self.aws("s3api", "put-object", "durable_report_failed",
                     "--bucket", self.policy["release_bucket"], "--key", key,
                     "--body", str(self.report_path), "--if-none-match", "*",
                     "--checksum-algorithm", "SHA256", "--checksum-sha256", checksum)
            head = self.aws("s3api", "head-object", "durable_report_failed",
                            "--bucket", self.policy["release_bucket"], "--key", key,
                            "--checksum-mode", "ENABLED")
            require(head.get("ChecksumSHA256") == checksum and head.get("VersionId"),
                    "durable_report_failed")


def main():
    if len(sys.argv) != 5:
        print("release engine requires POLICY MANIFEST OBJECT REPORT", file=sys.stderr)
        return 2
    release = Release(*sys.argv[1:])
    try:
        release.deploy()
    except Blocked as error:
        release.report["failure_code"] = error.code
        release.report["status"] = "partial" if release.mutated else "blocked"
        if release.mutated:
            release.recover()
    except Exception:
        # Fail closed on an unexpected provider or contract shape. Never expose
        # raw exception text, which may contain connection details.
        release.report["failure_code"] = "internal_contract_error"
        release.report["status"] = "partial" if release.mutated else "blocked"
        if release.mutated:
            release.recover()
    try:
        release.save_report()
    except (Blocked, OSError):
        release.report["status"] = "partial" if release.mutated else "blocked"
        release.report["failure_code"] = "durable_report_failed"
        try:
            release.report_path.write_text(json.dumps(release.report, sort_keys=True, indent=2) + "\n")
        except OSError:
            pass
        print("DURABLE_REPORT_FAILED", file=sys.stderr)
        return 1
    print("release_status=" + release.report["status"] + " code=" + release.report["failure_code"])
    return 0 if release.report["status"] == "verified" else 1


if __name__ == "__main__":
    sys.exit(main())
