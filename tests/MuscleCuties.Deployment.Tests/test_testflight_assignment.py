"""Tests for the TestFlight build-assignment step and its workflow wiring.

The App Store Connect calls themselves are owner-only; these tests cover the pure parts
(der signature encoding, group selection) and assert the workflow always assigns the build
after upload while the API key is still staged.
"""

import base64
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

ROOT = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "deployment"))

import assign_testflight_build as tool  # noqa: E402


def group(identifier, name, internal):
    """Build one betaGroups resource shaped like the App Store Connect response."""
    return {"id": identifier, "type": "betaGroups", "attributes": {"name": name, "isInternalGroup": internal}}


class DerEncodingTests(unittest.TestCase):
    """The openssl signature is DER; App Store Connect needs the raw r||s pair."""

    def test_short_form_integers_are_left_padded_to_32_bytes(self):
        der = bytes([0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0x02])
        raw = tool.der_to_raw(der)
        self.assertEqual(64, len(raw))
        self.assertEqual(1, raw[31])
        self.assertEqual(2, raw[63])

    def test_non_sequence_input_is_rejected(self):
        with self.assertRaises(ValueError):
            tool.der_to_raw(bytes([0x31, 0x00]))


class GroupSelectionTests(unittest.TestCase):
    """Without configuration every group is selected, internal and external; a name overrides that."""

    def test_defaults_to_every_group(self):
        groups = [group("1", "Internal", True), group("2", "External", False), group("3", "Team", True)]
        self.assertEqual(["1", "2", "3"], [g["id"] for g in tool.choose_groups(groups, None)])

    def test_named_group_matches_case_insensitively(self):
        groups = [group("1", "Internal", True), group("2", "External", False)]
        self.assertEqual(["2"], [g["id"] for g in tool.choose_groups(groups, "  external ")])

    def test_unknown_name_lists_available_groups(self):
        groups = [group("1", "Internal", True)]
        with self.assertRaises(SystemExit) as caught:
            tool.choose_groups(groups, "Missing")
        self.assertIn("Internal", str(caught.exception))

    def test_no_group_requires_explicit_configuration(self):
        with self.assertRaises(SystemExit) as caught:
            tool.choose_groups([], None)
        self.assertIn("TESTFLIGHT_GROUP", str(caught.exception))


class AssignmentTests(unittest.TestCase):
    """Internal groups receive every build automatically; only external groups need a POST."""

    def test_internal_group_is_not_posted(self):
        with mock.patch.object(tool, "api_request") as request:
            assigned = tool.assign_build(
                "https://api.example", "token", [group("1", "Internal", True)], "build-1"
            )
        self.assertEqual(0, assigned)
        request.assert_not_called()

    def test_external_group_is_posted(self):
        with mock.patch.object(tool, "api_request", return_value=(200, None)) as request:
            assigned = tool.assign_build(
                "https://api.example", "token", [group("2", "External", False)], "build-1"
            )
        self.assertEqual(1, assigned)
        request.assert_called_once()
        args, kwargs = request.call_args
        self.assertEqual("/v1/betaGroups/2/relationships/builds", args[3])
        self.assertEqual({"data": [{"type": "builds", "id": "build-1"}]}, kwargs["body"])

    def test_mixed_groups_only_post_external(self):
        with mock.patch.object(tool, "api_request", return_value=(200, None)) as request:
            assigned = tool.assign_build(
                "https://api.example", "token",
                [group("1", "Internal", True), group("2", "External", False)], "build-1",
            )
        self.assertEqual(1, assigned)
        self.assertEqual(1, request.call_count)
        args, _ = request.call_args
        self.assertEqual("/v1/betaGroups/2/relationships/builds", args[3])


class BetaReviewSubmissionTests(unittest.TestCase):
    """External testers only receive a build after Apple reviews it, so it is submitted once."""

    def test_missing_submission_is_created(self):
        responses = [(200, {"data": []}), (201, {"data": {"attributes": {"betaReviewState": "WAITING_FOR_REVIEW"}}})]
        with mock.patch.object(tool, "api_request", side_effect=responses) as request:
            state = tool.submit_for_beta_review("https://api.example", "token", "build-1")
        self.assertEqual("WAITING_FOR_REVIEW", state)
        self.assertEqual(2, request.call_count)
        args, kwargs = request.call_args
        self.assertEqual("POST", args[2])
        self.assertEqual("/v1/betaAppReviewSubmissions", args[3])
        self.assertEqual(
            {
                "data": {
                    "type": "betaAppReviewSubmissions",
                    "relationships": {"build": {"data": {"type": "builds", "id": "build-1"}}},
                }
            },
            kwargs["body"],
        )

    def test_existing_submission_is_reported_not_repeated(self):
        responses = [(200, {"data": [{"id": "s", "attributes": {"betaReviewState": "IN_REVIEW"}}]})]
        with mock.patch.object(tool, "api_request", side_effect=responses) as request:
            state = tool.submit_for_beta_review("https://api.example", "token", "build-1")
        self.assertEqual("IN_REVIEW", state)
        self.assertEqual(1, request.call_count)

    def test_conflict_means_already_submitted(self):
        error = tool.ApiError(409, "already submitted")
        with mock.patch.object(tool, "api_request", side_effect=[(200, {"data": []}), error]):
            self.assertEqual("SUBMITTED", tool.submit_for_beta_review("https://api.example", "token", "build-1"))


@unittest.skipUnless(shutil.which("openssl"), "openssl is required to sign the App Store Connect token")
class TokenTests(unittest.TestCase):
    """The generated JWT uses ES256 and carries the issuer and audience Apple expects."""

    def test_token_header_and_claims(self):
        with tempfile.TemporaryDirectory() as directory:
            key = pathlib.Path(directory) / "AuthKey_TEST.p8"
            subprocess.run(
                ["openssl", "ecparam", "-name", "prime256v1", "-genkey", "-noout", "-out", str(key)],
                check=True, capture_output=True,
            )
            token = tool.make_token(str(key), "KEYID", "issuer-id")
        header_part, payload_part, signature_part = token.split(".")
        header = json.loads(base64.urlsafe_b64decode(header_part + "=="))
        payload = json.loads(base64.urlsafe_b64decode(payload_part + "=="))
        self.assertEqual({"alg": "ES256", "kid": "KEYID", "typ": "JWT"}, header)
        self.assertEqual("issuer-id", payload["iss"])
        self.assertEqual("appstoreconnect-v1", payload["aud"])
        self.assertEqual(64, len(base64.urlsafe_b64decode(signature_part + "==")))


class WorkflowWiringTests(unittest.TestCase):
    """The release workflow must assign the build after upload, before the key is deleted."""

    def setUp(self):
        self.workflow = (ROOT / ".github" / "workflows" / "mobile-release.yml").read_text()

    def test_assignment_runs_after_upload(self):
        upload = self.workflow.index("Upload to TestFlight")
        assignment = self.workflow.index("assign_testflight_build.py")
        self.assertGreater(assignment, upload)
        self.assertIn("vars.TESTFLIGHT_GROUP", self.workflow)

    def test_assignment_targets_every_group_by_default(self):
        self.assertIn("TESTFLIGHT_GROUP: ${{ vars.TESTFLIGHT_GROUP }}", self.workflow)
        self.assertNotIn("TESTFLIGHT_GROUP ||", self.workflow)

    def test_key_is_still_staged_when_assignment_runs(self):
        assignment = self.workflow.index("assign_testflight_build.py")
        cleanup = self.workflow.index("Remove the signing material")
        self.assertGreater(cleanup, assignment)


if __name__ == "__main__":
    unittest.main()
