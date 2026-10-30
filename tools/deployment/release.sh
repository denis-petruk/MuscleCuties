#!/usr/bin/env bash
# Serialized deployment entry point. The separate DbMaintenance lock owner must
# continuously hold one advisory-lock session while the inner engine runs.
set -uo pipefail
set +x
umask 077

report_dir="${CI_PROJECT_DIR:-.}/artifacts"
report_file="$report_dir/deployment-report.json"
phase=source
reason=internal_error
status=blocked
mutation_attempted=false
recovery=none
engine_verified=false
source_sha=""
tree_sha=""
artifact_sha=""

write_report() {
    mkdir -p "$report_dir" || return 1
    local temporary
    temporary=$(mktemp "$report_dir/.deployment-report.XXXXXX") || return 1
    if [ -d "$report_file" ] || [ -L "$report_file" ]; then
        rm -f "$temporary"
        return 1
    fi
    if ! jq -n --arg status "$status" --arg phase "$phase" --arg reason "$reason" \
        --arg source_sha "$source_sha" --arg tree_sha "$tree_sha" --arg artifact_sha "$artifact_sha" \
        --arg recovery "$recovery" --argjson mutation "$mutation_attempted" \
        '{contract_version: 1, status: $status, phase: $phase, failure_code: $reason,
          source_sha: $source_sha, tree_sha: $tree_sha, artifact_sha256: $artifact_sha,
          recovery: $recovery, mutation_attempted: $mutation}' > "$temporary"; then
        rm -f "$temporary"
        return 1
    fi
    mv -f "$temporary" "$report_file"
}
finish() {
    local exit_code=$?
    trap - EXIT
    if [ "$engine_verified" = true ] && [ "$exit_code" -eq 0 ] &&
       jq -e '.status == "verified" and .contract_version == 1' "$report_file" >/dev/null 2>&1; then
        printf '%s\n' 'Release verified; see sanitized deployment report.'
        exit 0
    fi
    if ! write_report; then
        printf '%s\n' 'DEPLOYMENT_REPORT_WRITE_FAILED' >&2
        exit 1
    fi
    printf 'Release %s: %s\n' "$status" "$reason" >&2
    exit "$exit_code"
}
trap finish EXIT

# Protected GitLab context is necessary but is not sufficient approval for a release.
if [ "${CI:-}" != true ] || [ "${CI_COMMIT_REF_PROTECTED:-}" != true ] || \
   [ "${CI_PIPELINE_SOURCE:-}" != web ]; then
    reason=untrusted_pipeline
    exit 1
fi
case "${CI_COMMIT_BRANCH:-}" in
    codex/plan-generation-checkpoint|test-flight-beta) ;;
    *) reason=untrusted_ref; exit 1 ;;
esac
if [[ ! ${CI_COMMIT_SHA:-} =~ ^[0-9a-f]{40}$ ]] ||
   [[ ! ${CI_COMMIT_TREE_SHA:-} =~ ^[0-9a-f]{40}$ ]]; then
    reason=invalid_source_identity
    exit 1
fi
source_sha=$CI_COMMIT_SHA
tree_sha=$CI_COMMIT_TREE_SHA
if [ "$(git rev-parse HEAD 2>/dev/null)" != "$source_sha" ] ||
   [ "$(git rev-parse 'HEAD^{tree}' 2>/dev/null)" != "$tree_sha" ]; then
    reason=source_identity_mismatch
    exit 1
fi

phase=artifact
manifest="${CI_PROJECT_DIR:-.}/artifacts/build-manifest.json"
object="${CI_PROJECT_DIR:-.}/artifacts/release-object.json"
if ! jq -e --arg source "$source_sha" --arg tree "$tree_sha" '
    .source_sha == $source and .tree_sha == $tree and
    (.zip_sha256 | type == "string" and test("^[0-9a-f]{64}$")) and
    (.zip_sha256_base64 | type == "string" and test("^[A-Za-z0-9+/]{43}=$")) and
    (.zip_bytes | type == "number" and . > 0 and floor == .) and
    .runtime == "net10.0" and .architecture == "arm64" and
    (.handler | type == "string" and length > 0)
    ' "$manifest" >/dev/null 2>&1; then
    reason=invalid_build_manifest
    exit 1
fi
artifact_sha=$(jq -r '.zip_sha256' "$manifest")
if ! jq -e --slurpfile build "$manifest" '
    .zip_sha256 == $build[0].zip_sha256 and
    .zip_sha256_base64 == $build[0].zip_sha256_base64 and
    .zip_bytes == $build[0].zip_bytes and
    (.bucket | type == "string" and length > 0) and
    (.version_id | type == "string" and length > 0 and . != "null") and
    .key == ("lambda/MuscleCuties.Server/" + $build[0].source_sha + "/" + $build[0].zip_sha256 + "/MuscleCuties.Server.zip")
    ' "$object" >/dev/null 2>&1; then
    reason=invalid_release_object
    exit 1
fi

phase=coordination
# The runner is supplied by Orchestrator 2's reviewed DbMaintenance integration.
# It must acquire the advisory lock before starting the child, supervise the DB
# session continuously, terminate the child on session loss, and hold the lock
# through verification, recovery, and durable report persistence.
lock_runner="${MC_DB_LOCK_RUNNER:-}"
policy="${MC_RELEASE_POLICY_FILE:-}"
if [ ! -x "$lock_runner" ]; then
    reason=database_session_lock_contract_unavailable
    exit 1
fi
if [ ! -f "$policy" ] ||
   [ "${CI_PROJECT_PATH:-}" != 'denis-petruk/musclecuties-testflight-beta' ] ||
   [ "${CI_DEBUG_TRACE:-false}" = true ]; then
    reason=release_policy_contract_unavailable
    exit 1
fi
case "$(realpath "$lock_runner")" in
    "$(realpath "${CI_PROJECT_DIR:-.}")"/src/MuscleCuties.DbMaintenance/*) ;;
    *) reason=untrusted_lock_runner; exit 1 ;;
esac
# A failed wrapper exit means lock ownership or release verification was not
# maintained. Do not pass through an apparent success report.
if ! "$lock_runner" -- \
    python3 "$(dirname "$0")/release-engine.py" "$policy" "$manifest" "$object" "$report_file"; then
    reason=lock_owner_or_release_failed
    status=partial
    exit 1
fi
if ! jq -e '.status == "verified" and .contract_version == 1' "$report_file" >/dev/null 2>&1; then
    reason=release_report_unverified
    status=partial
    exit 1
fi
engine_verified=true
exit 0
