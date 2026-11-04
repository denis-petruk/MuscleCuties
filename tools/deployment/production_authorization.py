#!/usr/bin/env python3
"""Verify an owner-issued, commit-bound production release authorization."""

import hashlib
import hmac
import os
import re
import sys


SOURCE_SHA = re.compile(r"[0-9a-f]{40}\Z")
SIGNATURE = re.compile(r"[0-9a-fA-F]{64}\Z")
MINIMUM_KEY_BYTES = 32


def authorized(source_sha, tree_sha):
    key = os.environ.get("PRODUCTION_RELEASE_AUTHORIZATION_KEY", "")
    signature = os.environ.get("PRODUCTION_RELEASE_SIGNATURE", "")
    if not (isinstance(source_sha, str) and SOURCE_SHA.fullmatch(source_sha) and
            isinstance(tree_sha, str) and SOURCE_SHA.fullmatch(tree_sha) and
            isinstance(signature, str) and SIGNATURE.fullmatch(signature)):
        return False
    key_bytes = key.encode("utf-8")
    if len(key_bytes) < MINIMUM_KEY_BYTES:
        return False
    message = f"{source_sha}:{tree_sha}:production".encode("ascii")
    expected = hmac.new(key_bytes, message, hashlib.sha256).hexdigest()
    return hmac.compare_digest(expected, signature.lower())


if __name__ == "__main__":
    # No secret or supplied signature is written to stdout or stderr.
    sys.exit(0 if len(sys.argv) == 3 and authorized(sys.argv[1], sys.argv[2]) else 1)
