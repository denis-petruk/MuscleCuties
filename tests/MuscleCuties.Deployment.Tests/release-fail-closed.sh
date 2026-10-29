#!/usr/bin/env bash
# Failure-injection tests: no AWS or DB calls are allowed without the shared lock.
set -euo pipefail
root=$(git rev-parse --show-toplevel)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/artifacts" "$tmp/bin"
cat > "$tmp/bin/aws" <<'MOCK'
#!/usr/bin/env bash
touch "$MOCK_AWS_CALLED"
exit 99
MOCK
chmod +x "$tmp/bin/aws"
export PATH="$tmp/bin:$PATH" MOCK_AWS_CALLED="$tmp/aws-called"
export CI=true CI_COMMIT_REF_PROTECTED=true CI_PIPELINE_SOURCE=web
export CI_COMMIT_BRANCH=test-flight-beta CI_PROJECT_DIR="$tmp"
export CI_COMMIT_SHA=$(git rev-parse HEAD) CI_COMMIT_TREE_SHA=$(git rev-parse 'HEAD^{tree}')
sha=$(printf 'a%.0s' {1..64})
b64=$(printf 'A%.0s' {1..43})=
jq -n --arg source "$CI_COMMIT_SHA" --arg tree "$CI_COMMIT_TREE_SHA" --arg sha "$sha" --arg b64 "$b64" \
  '{source_sha:$source,tree_sha:$tree,zip_sha256:$sha,zip_sha256_base64:$b64,zip_bytes:1,runtime:"net10.0",architecture:"arm64",handler:"MuscleCuties.Server"}' > "$tmp/artifacts/build-manifest.json"
jq -n --arg source "$CI_COMMIT_SHA" --arg sha "$sha" --arg b64 "$b64" \
  '{bucket:"release-bucket",key:("lambda/MuscleCuties.Server/"+$source+"/"+$sha+"/MuscleCuties.Server.zip"),version_id:"v1",zip_sha256:$sha,zip_sha256_base64:$b64,zip_bytes:1}' > "$tmp/artifacts/release-object.json"
check_blocked() {
    local expected=$1
    if bash "$root/tools/deployment/release.sh" >"$tmp/output" 2>&1; then
        printf '%s\n' 'release unexpectedly succeeded' >&2; exit 1
    fi
    jq -e --arg reason "$expected" '.status == "blocked" and .failure_code == $reason and .mutation_attempted == false' "$tmp/artifacts/deployment-report.json" >/dev/null
    test ! -e "$MOCK_AWS_CALLED"
}
check_blocked database_session_lock_contract_unavailable
check_blocked database_session_lock_contract_unavailable # retry cannot mutate either
CI_COMMIT_REF_PROTECTED=false check_blocked untrusted_pipeline
CI_COMMIT_SHA=0000000000000000000000000000000000000000 check_blocked source_identity_mismatch
jq '.version_id = "null"' "$tmp/artifacts/release-object.json" > "$tmp/changed"
mv "$tmp/changed" "$tmp/artifacts/release-object.json"
check_blocked invalid_release_object
rm "$tmp/artifacts/deployment-report.json"
mkdir "$tmp/artifacts/deployment-report.json"
if bash "$root/tools/deployment/release.sh" >"$tmp/output" 2>&1; then
    printf '%s\n' 'report failure unexpectedly succeeded' >&2; exit 1
fi
grep -q DEPLOYMENT_REPORT_WRITE_FAILED "$tmp/output"
test ! -e "$MOCK_AWS_CALLED"
if bash "$root/tools/deployment/verify-runtime.sh" >"$tmp/output" 2>&1; then
    printf '%s\n' 'runtime verification unexpectedly succeeded' >&2; exit 1
fi
printf '%s\n' 'release failure-injection checks passed'
