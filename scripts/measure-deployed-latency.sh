#!/usr/bin/env bash
# Measure observed deployed Lambda latency from CloudWatch. This is read-only against AWS:
# it reads function/API identity, REPORT log lines and API Gateway metrics, and makes only
# unauthenticated GET /health/live and /health/ready probes. It never invokes the function
# directly, changes configuration, deploys code or sends synthetic user writes.
#
# Usage:
#   MC_WRITE_DEPLOYED_LATENCY_REPORT=1 bash scripts/measure-deployed-latency.sh
#
# The report is staging evidence unless the deployed function explicitly reports the
# Production environment. A missing production deployment is recorded, not papered over.
set -euo pipefail
umask 077

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPORT="$ROOT/docs/performance/deployed-latency-report.json"
REGION="${AWS_REGION:-us-east-2}"
FUNCTION="${MC_DEPLOYED_FUNCTION:-musclecuties-backend-api}"
API_ID="${MC_DEPLOYED_API_ID:-y2ejsgrc6l}"
ACCOUNT="${MC_DEPLOYED_ACCOUNT:-028417007003}"
WINDOW_DAYS="${MC_DEPLOYED_WINDOW_DAYS:-30}"

case "$WINDOW_DAYS" in
  ''|*[!0-9]*) echo "MC_DEPLOYED_WINDOW_DAYS must be a positive integer" >&2; exit 2 ;;
esac
[ "$WINDOW_DAYS" -ge 1 ] || { echo "MC_DEPLOYED_WINDOW_DAYS must be at least 1" >&2; exit 2; }

python3 - "$REGION" "$FUNCTION" "$API_ID" "$ACCOUNT" "$WINDOW_DAYS" "$REPORT" <<'PY'
import json
import os
import pathlib
import ssl
import statistics
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

region, function, api_id, expected_account, window_days_raw, report_path = sys.argv[1:]
window_days = int(window_days_raw)
log_group = f"/aws/lambda/{function}"
end = int(time.time())
start = end - window_days * 24 * 60 * 60
start_iso = datetime.fromtimestamp(start, tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
end_iso = datetime.fromtimestamp(end, tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def fail(code):
    raise SystemExit(code)


def aws_json(*args):
    try:
        result = subprocess.run(
            ["aws", *args, "--region", region, "--output", "json"],
            capture_output=True, text=True, timeout=120)
    except (OSError, subprocess.TimeoutExpired):
        fail("AWS_READ_FAILED")
    if result.returncode != 0:
        # Do not print the provider response: deployment configuration can contain secrets.
        fail(f"AWS_READ_FAILED:{args[0]}:{args[1]}")
    try:
        return json.loads(result.stdout)
    except ValueError:
        fail("AWS_RESPONSE_INVALID")


def log_stats(query):
    result = aws_json(
        "logs", "start-query",
        "--log-group-name", log_group,
        "--start-time", str(start),
        "--end-time", str(end),
        "--query-string", query)
    query_id = result.get("queryId")
    if not query_id:
        fail("LOG_QUERY_START_FAILED")
    for _ in range(60):
        result = aws_json("logs", "get-query-results", "--query-id", query_id)
        status = result.get("status")
        if status == "Complete":
            rows = result.get("results") or []
            if not rows:
                return {}
            return {item["field"]: item["value"] for item in rows[0]}
        if status in ("Failed", "Cancelled", "Timeout"):
            fail("LOG_QUERY_FAILED")
        time.sleep(2)
    fail("LOG_QUERY_TIMEOUT")


def number(row, field):
    value = row.get(field)
    return None if value in (None, "") else float(value)


def metric_daily_p99(metric_name):
    data = aws_json(
        "cloudwatch", "get-metric-statistics",
        "--namespace", "AWS/ApiGateway",
        "--metric-name", metric_name,
        "--dimensions", f"Name=ApiId,Value={api_id}",
        "--start-time", start_iso,
        "--end-time", end_iso,
        "--period", "86400",
        "--extended-statistics", "p99")
    values = sorted(
        float(point["ExtendedStatistics"]["p99"])
        for point in data.get("Datapoints", [])
        if point.get("ExtendedStatistics", {}).get("p99") is not None)
    if not values:
        return {"Samples": 0, "Min": None, "Median": None, "Max": None}
    return {
        "Samples": len(values),
        "Min": round(values[0], 2),
        "Median": round(statistics.median(values), 2),
        "Max": round(values[-1], 2),
    }


def probe(path):
    context = ssl.create_default_context()
    url = f"https://{api_id}.execute-api.{region}.amazonaws.com{path}"
    samples = []
    for _ in range(3):
        started = time.perf_counter()
        try:
            request = urllib.request.Request(
                url, headers={"User-Agent": "musclecuties-deployed-latency-verifier"})
            with urllib.request.urlopen(request, timeout=20, context=context) as response:
                response.read(1)
                status = response.status
            error = None
        except urllib.error.HTTPError as exc:
            status = exc.code
            error = "http_error"
        except (OSError, ValueError):
            status = None
            error = "request_failed"
        samples.append({
            "StatusCode": status,
            "TotalMs": round((time.perf_counter() - started) * 1000, 2),
            "Error": error,
        })
    totals = [sample["TotalMs"] for sample in samples if sample["Error"] is None]
    return {
        "Path": path,
        "Samples": samples,
        "MedianMs": round(statistics.median(totals), 2) if totals else None,
    }


identity = aws_json("sts", "get-caller-identity")
if identity.get("Account") != expected_account:
    fail("AWS_ACCOUNT_MISMATCH")

config = aws_json(
    "lambda", "get-function-configuration", "--function-name", function,
    "--query",
    "{State:State,LastUpdateStatus:LastUpdateStatus,Version:Version,CodeSha256:CodeSha256,"
    "Runtime:Runtime,Architectures:Architectures,MemorySize:MemorySize,Timeout:Timeout,"
    "Environment:Environment.Variables.ASPNETCORE_ENVIRONMENT}")
if config.get("State") != "Active" or config.get("LastUpdateStatus") != "Successful":
    fail("FUNCTION_NOT_READY")
environment = config.get("Environment") or "Unknown"
if environment.lower() == "production":
    production_deployment = True
else:
    production_deployment = False

integrations = aws_json("apigatewayv2", "get-integrations", "--api-id", api_id).get("Items", [])
if not any(
        integration.get("IntegrationType") == "AWS_PROXY" and
        f":function:{function}" in integration.get("IntegrationUri", "")
        for integration in integrations):
    fail("API_FUNCTION_BINDING_MISMATCH")

all_row = log_stats(
    "fields @timestamp, @message "
    "| filter @message like /^REPORT RequestId:/ "
    "| parse @message /Duration:\\s*(?<duration>[0-9.]+)\\s*ms/ "
    "| stats count() as invocations, pct(duration,50) as p50_ms, "
    "pct(duration,95) as p95_ms, pct(duration,99) as p99_ms, max(duration) as max_ms")

warm_row = log_stats(
    "fields @timestamp, @message "
    "| filter @message like /^REPORT RequestId:/ and @message not like /Init Duration:/ "
    "| parse @message /Duration:\\s*(?<duration>[0-9.]+)\\s*ms/ "
    "| stats count() as invocations, pct(duration,50) as p50_ms, "
    "pct(duration,95) as p95_ms, pct(duration,99) as p99_ms, max(duration) as max_ms")

cold_row = log_stats(
    "fields @timestamp, @message "
    "| filter @message like /^REPORT RequestId:/ "
    "| parse @message /Duration:\\s*(?<duration>[0-9.]+)\\s*ms/ "
    "| parse @message /Init Duration:\\s*(?<init>[0-9.]+)\\s*ms/ "
    "| filter ispresent(init) "
    "| stats count() as cold_starts, pct(init,50) as init_p50_ms, "
    "pct(init,95) as init_p95_ms, pct(init,99) as init_p99_ms, max(init) as init_max_ms, "
    "pct(duration,99) as p99_duration_ms, max(duration) as max_duration_ms")

invocations = int(float(all_row.get("invocations", 0)))
cold_starts = int(float(cold_row.get("cold_starts", 0)))
if invocations == 0:
    fail("NO_DEPLOYED_INVOCATIONS")
if cold_starts == 0:
    fail("NO_OBSERVED_COLD_START")

all_p99 = number(all_row, "p99_ms")
warm_p99 = number(warm_row, "p99_ms")
cold_init_p99 = number(cold_row, "init_p99_ms")
budget_ms = 1000
failures = []
if all_p99 is not None and all_p99 > budget_ms:
    failures.append(f"overall p99 {all_p99:.2f} ms exceeds {budget_ms} ms")
if warm_p99 is not None and warm_p99 > budget_ms:
    failures.append(f"warm p99 {warm_p99:.2f} ms exceeds {budget_ms} ms")
if cold_init_p99 is not None and cold_init_p99 > budget_ms:
    failures.append(f"cold-start init p99 {cold_init_p99:.2f} ms exceeds {budget_ms} ms")

report = {
    "SchemaVersion": 1,
    "Protocol": "aws-cloudwatch-observed-deployed-latency-v1",
    "MeasuredAtUtc": datetime.now(tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "Region": region,
    "Account": expected_account,
    "Function": {
        "Name": function,
        "Version": config.get("Version"),
        "CodeSha256": config.get("CodeSha256"),
        "Runtime": config.get("Runtime"),
        "Architectures": config.get("Architectures"),
        "MemorySizeMb": config.get("MemorySize"),
        "TimeoutSeconds": config.get("Timeout"),
        "Environment": environment,
    },
    "ApiGateway": {
        "ApiId": api_id,
        "BaseUrl": f"https://{api_id}.execute-api.{region}.amazonaws.com",
        "LogGroup": log_group,
    },
    "Window": {
        "Days": window_days,
        "StartUtc": start_iso,
        "EndUtc": end_iso,
    },
    "Invocations": {
        "Count": invocations,
        "DurationMs": {
            "P50": number(all_row, "p50_ms"),
            "P95": number(all_row, "p95_ms"),
            "P99": all_p99,
            "Max": number(all_row, "max_ms"),
        },
    },
    "WarmInvocations": {
        "Count": int(float(warm_row.get("invocations", 0))),
        "DurationMs": {
            "P50": number(warm_row, "p50_ms"),
            "P95": number(warm_row, "p95_ms"),
            "P99": warm_p99,
            "Max": number(warm_row, "max_ms"),
        },
    },
    "ColdStarts": {
        "Count": cold_starts,
        "InitMs": {
            "P50": number(cold_row, "init_p50_ms"),
            "P95": number(cold_row, "init_p95_ms"),
            "P99": cold_init_p99,
            "Max": number(cold_row, "init_max_ms"),
        },
        "DurationMs": {
            "P99": number(cold_row, "p99_duration_ms"),
            "Max": number(cold_row, "max_duration_ms"),
        },
    },
    "ApiGatewayLatencyDailyP99Ms": metric_daily_p99("Latency"),
    "ApiGatewayIntegrationLatencyDailyP99Ms": metric_daily_p99("IntegrationLatency"),
    "Probe": {
        "Live": probe("/health/live"),
        "Ready": probe("/health/ready"),
    },
    "ProductionDeployment": production_deployment,
    "ProductionP99Available": production_deployment,
    "DiagnosticBudgetMs": budget_ms,
    "BudgetBasis": "local 1000 ms handler budget used as a diagnostic comparison, not a production SLA",
    "Result": "fail" if failures else "pass",
    "Failures": failures,
    "Notes": [
        "Cold starts are observed Init Duration values from CloudWatch REPORT lines; none were forced.",
        "No direct Lambda invoke, configuration write, deployment or synthetic user write was performed.",
        "API Gateway daily p99 values are per-day percentiles; they are summarised, not re-aggregated into a single percentile.",
    ],
}

if production_deployment:
    report["Notes"].append("The deployed function reports the Production environment.")
else:
    report["Notes"].append(
        "No separate production deployment was found; these are deployed staging measurements, not production p99.")

if not failures:
    report["Failures"] = []

print(f"function={function}")
print(f"environment={environment}")
print(f"invocations={invocations}")
print(f"warm_p99_ms={warm_p99}")
print(f"cold_starts={cold_starts}")
print(f"cold_init_p99_ms={cold_init_p99}")
print(f"api_gateway_daily_p99_median_ms={report['ApiGatewayLatencyDailyP99Ms']['Median']}")
print(f"production_deployment={production_deployment}")
print(f"result={report['Result']}")
if failures:
    print("failures=" + " | ".join(failures))

if os.environ.get("MC_WRITE_DEPLOYED_LATENCY_REPORT") == "1":
    pathlib.Path(report_path).parent.mkdir(parents=True, exist_ok=True)
    pathlib.Path(report_path).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"report={report_path}")
else:
    print("report_not_written=set MC_WRITE_DEPLOYED_LATENCY_REPORT=1")

if report["Result"] != "pass":
    sys.exit(3)
PY
