#!/usr/bin/env python3
"""Assign a freshly uploaded TestFlight build to its beta tester groups.

The mobile release workflow uploads the signed .ipa with altool, then runs this tool with
the same App Store Connect API key. It resolves the app by bundle id, waits until App Store
Connect finishes processing the build number, and reports the selected beta groups. Internal
groups receive every build automatically, so only external groups are explicitly assigned;
external groups also require Beta App Review, so a build that reaches one is submitted for it.
Every group of the app is selected by default; TESTFLIGHT_GROUP (or --group) targets one named
group instead.
"""

import argparse
import base64
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

DEFAULT_API_BASE = "https://api.appstoreconnect.apple.com"
DEFAULT_BUNDLE_ID = "com.musclecuties.app"
TOKEN_LIFETIME_SECONDS = 1_200


class ApiError(RuntimeError):
    """An App Store Connect response outside the 2xx range."""

    def __init__(self, status, body):
        """Keep the status available so callers can treat 409 as already-assigned."""
        self.status = status
        super().__init__(f"App Store Connect returned {status}: {body}")


def b64url(raw):
    """Encode bytes as unpadded base64url, the JWT segment encoding."""
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode("ascii")


def der_length(data, offset):
    """Read a DER length at offset and return (length, index of the first value byte)."""
    first = data[offset]
    if first < 0x80:
        return first, offset + 1
    count = first & 0x7F
    if count == 0 or count > 4:
        raise ValueError("unsupported DER length encoding")
    value = int.from_bytes(data[offset + 1:offset + 1 + count], "big")
    return value, offset + 1 + count


def der_to_raw(signature):
    """Convert an openssl DER ECDSA signature into the fixed-width r||s pair JWT expects."""
    if not signature or signature[0] != 0x30:
        raise ValueError("not a DER sequence")
    length, offset = der_length(signature, 1)
    end = offset + length
    if end != len(signature):
        raise ValueError("DER sequence length does not match the signature")
    parts = []
    for _ in range(2):
        if offset >= end or signature[offset] != 0x02:
            raise ValueError("expected a DER integer")
        integer_length, offset = der_length(signature, offset + 1)
        integer = int.from_bytes(signature[offset:offset + integer_length], "big")
        parts.append(integer.to_bytes(32, "big"))
        offset += integer_length
    if offset != end:
        raise ValueError("trailing bytes after the DER signature")
    return parts[0] + parts[1]


def make_token(key_path, key_id, issuer, lifetime=TOKEN_LIFETIME_SECONDS):
    """Create the short-lived ES256 bearer token App Store Connect requires."""
    now = int(time.time())
    header = {"alg": "ES256", "kid": key_id, "typ": "JWT"}
    payload = {"iss": issuer, "iat": now, "exp": now + lifetime, "aud": "appstoreconnect-v1"}
    signing_input = ".".join((
        b64url(json.dumps(header, separators=(",", ":")).encode("utf-8")),
        b64url(json.dumps(payload, separators=(",", ":")).encode("utf-8")),
    ))
    signature = subprocess.run(
        ["openssl", "dgst", "-sha256", "-sign", key_path, "-binary"],
        input=signing_input.encode("ascii"),
        check=True,
        capture_output=True,
    ).stdout
    return f"{signing_input}.{b64url(der_to_raw(signature))}"


def api_request(base, token, method, path, query=None, body=None):
    """Call one App Store Connect endpoint and return (status, decoded JSON or None)."""
    url = base + path
    if query:
        url += "?" + urllib.parse.urlencode(query)
    data = None if body is None else json.dumps(body).encode("utf-8")
    request = urllib.request.Request(url, data=data, method=method)
    request.add_header("Authorization", f"Bearer {token}")
    request.add_header("Accept", "application/json")
    if data is not None:
        request.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")
        raise ApiError(error.code, detail) from error


def resolve_app_id(base, token, bundle_id):
    """Return the App Store Connect app id for the bundle identifier."""
    _, payload = api_request(
        base, token, "GET", "/v1/apps",
        query={"filter[bundleId]": bundle_id, "limit": "1"},
    )
    data = (payload or {}).get("data", [])
    if not data:
        raise SystemExit(f"no App Store Connect app found for bundle id {bundle_id}")
    return data[0]["id"]


def list_groups(base, token, app_id):
    """Return every beta group of the app with its name and internal flag."""
    _, payload = api_request(
        base, token, "GET", "/v1/betaGroups",
        query={"filter[app]": app_id, "fields[betaGroups]": "name,isInternalGroup", "limit": "200"},
    )
    return (payload or {}).get("data", [])


def choose_groups(groups, name):
    """Return the target beta groups: the named group, or every group of the app by default.

    Internal groups receive every build automatically, so assignment only posts external groups.
    """
    if name:
        wanted = name.strip().casefold()
        matches = [g for g in groups if g.get("attributes", {}).get("name", "").strip().casefold() == wanted]
        if not matches:
            available = ", ".join(sorted(g.get("attributes", {}).get("name", "") for g in groups)) or "none"
            raise SystemExit(f"TESTFLIGHT_GROUP '{name}' does not match a beta group; available: {available}")
        return matches
    if not groups:
        raise SystemExit(
            "the app has no beta group; create one in App Store Connect, or set the repository "
            "variable TESTFLIGHT_GROUP to the group name that should receive the build"
        )
    return groups


def wait_for_build(base, token_provider, app_id, build_number, timeout_seconds, poll_seconds):
    """Poll until the uploaded build number is VALID and return its App Store Connect id.

    The token provider is called on every poll because processing can outlive the 20-minute
    lifetime of a single App Store Connect token.
    """
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        _, payload = api_request(
            base, token_provider(), "GET", "/v1/builds",
            query={
                "filter[app]": app_id,
                "filter[version]": build_number,
                "sort": "-uploadedDate",
                "limit": "1",
            },
        )
        data = (payload or {}).get("data", [])
        if data:
            build = data[0]
            state = build.get("attributes", {}).get("processingState")
            if state == "VALID":
                return build["id"]
            if state in ("FAILED", "INVALID"):
                raise SystemExit(f"App Store Connect rejected build {build_number} with state {state}")
            print(f"build {build_number} is {state}; waiting", flush=True)
        else:
            print(f"build {build_number} is not visible yet; waiting", flush=True)
        time.sleep(poll_seconds)
    raise SystemExit(f"timed out after {timeout_seconds}s waiting for build {build_number} to process")


def assign_build(base, token, groups, build_id):
    """Add the build to each external group; internal groups already receive every build."""
    assigned = 0
    for group in groups:
        group_id = group["id"]
        attributes = group.get("attributes", {})
        name = attributes.get("name", group_id)
        if attributes.get("isInternalGroup"):
            print(f"internal group '{name}' receives every build automatically; nothing to assign", flush=True)
            continue
        try:
            api_request(
                base, token, "POST",
                f"/v1/betaGroups/{group_id}/relationships/builds",
                body={"data": [{"type": "builds", "id": build_id}]},
            )
            assigned += 1
            print(f"assigned build {build_id} to group '{name}' ({group_id})", flush=True)
        except ApiError as error:
            if error.status == 409:
                print(f"build {build_id} is already assigned to group '{name}' ({group_id})", flush=True)
                continue
            raise
    return assigned


def submit_for_beta_review(base, token, build_id):
    """Submit the build for Beta App Review so external testers can install it.

    External groups receive a build only after Apple reviews it. An existing submission is
    reported instead of repeated, and a 409 (one created concurrently) counts as submitted.
    """
    _, payload = api_request(
        base, token, "GET", "/v1/betaAppReviewSubmissions",
        query={"filter[build]": build_id, "limit": "1"},
    )
    existing = (payload or {}).get("data", [])
    if existing:
        state = existing[0].get("attributes", {}).get("betaReviewState") or "SUBMITTED"
        print(f"build {build_id} already has a Beta App Review submission ({state})", flush=True)
        return state
    try:
        _, created = api_request(
            base, token, "POST", "/v1/betaAppReviewSubmissions",
            body={
                "data": {
                    "type": "betaAppReviewSubmissions",
                    "relationships": {"build": {"data": {"type": "builds", "id": build_id}}},
                }
            },
        )
    except ApiError as error:
        if error.status != 409:
            raise
        print(f"build {build_id} was already submitted for Beta App Review", flush=True)
        return "SUBMITTED"
    state = (
        ((created or {}).get("data") or {}).get("attributes", {}).get("betaReviewState")
        or "WAITING_FOR_REVIEW"
    )
    print(f"submitted build {build_id} for Beta App Review ({state})", flush=True)
    return state
    """Read the API key, build number and optional target group from flags or environment."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--key", default=os.environ.get("ASC_API_KEY_PATH"), help="path to the AuthKey .p8")
    parser.add_argument("--key-id", default=os.environ.get("ASC_API_KEY_ID"))
    parser.add_argument("--issuer", default=os.environ.get("ASC_API_ISSUER_ID"))
    parser.add_argument("--build-number", default=os.environ.get("BUILD_NUMBER"))
    parser.add_argument("--group", default=os.environ.get("TESTFLIGHT_GROUP") or None)
    parser.add_argument("--bundle-id", default=DEFAULT_BUNDLE_ID)
    parser.add_argument("--api-base", default=DEFAULT_API_BASE)
    parser.add_argument("--timeout-seconds", type=int, default=1_800)
    parser.add_argument("--poll-seconds", type=int, default=20)
    return parser.parse_args(argv)


def main(argv=None):
    """Resolve the app, wait for the build to process, and assign it to its testers."""
    args = parse_args(argv)
    missing = [name for name in ("key", "key_id", "issuer", "build_number") if not getattr(args, name)]
    if missing:
        raise SystemExit(f"missing required input: {', '.join('--' + name.replace('_', '-') for name in missing)}")
    def token_provider():
        """Mint a fresh short-lived token for each App Store Connect call that may wait."""
        return make_token(args.key, args.key_id, args.issuer)

    token = token_provider()
    app_id = resolve_app_id(args.api_base, token, args.bundle_id)
    groups = list_groups(args.api_base, token, app_id)
    selected = choose_groups(groups, args.group)
    build_id = wait_for_build(
        args.api_base, token_provider, app_id, args.build_number, args.timeout_seconds, args.poll_seconds
    )
    assigned = assign_build(args.api_base, token_provider(), selected, build_id)
    internal = sum(1 for group in selected if group.get("attributes", {}).get("isInternalGroup"))
    external = len(selected) - internal
    if external:
        submit_for_beta_review(args.api_base, token_provider(), build_id)
    print(
        f"build {args.build_number} is available to {len(selected)} tester group(s): "
        f"{internal} internal automatically; {assigned} of {external} external assignment(s) made",
        flush=True,
    )


if __name__ == "__main__":
    main()
