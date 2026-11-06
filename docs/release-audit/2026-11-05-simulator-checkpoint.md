# Simulator acceptance checkpoint

Recorded November 5, 2026 for the MuscleCuties Rider checkout. The owner scoped this pass to `docs/` and set local iPhone simulator acceptance ahead of TestFlight. The owner selected the newer corrective Beta candidate as the plan's review baseline. This selection is for planning. Reconfirm the checkout and installed app before a future execution pass.

## Evidence and current gate

| Item | Observed result |
| --- | --- |
| Rider checkout | `main` at `d840d62745337a604f56f4ba32568fb66ae53158`. The November 4 release-audit folder was already untracked. |
| Selected review baseline | `continue_musclecuties_beta_plan` at `6e38535756494ab3e40a75cc74109ad247727213`, a local descendant of Rider `main`. Recheck its identity and any concurrent changes before work. |
| Failing build | Owner reported Rider Debug in the iPhone simulator, with data loading returning HTTP 403 ("The data is not available, try again"). |
| Booted simulator & app | iPhone 16 (`60DD8563-A1D6-4FE5-9F4F-2BFB975B194B`). Binary is `MuscleCuties.UI.app` built at 08:23 from Rider checkout `d840d627`. Data container preferences empty (no override set). |
| Default endpoint | Default Debug address is `http://127.0.0.1:5000`. |
| Read-only reproduction | macOS ControlCenter (PID 637) listens on port 5000. `curl -i http://127.0.0.1:5000/health/live` returned `HTTP/1.1 403 Forbidden` (`Server: AirTunes/960.13.1`). Port 5080 is verified free. |
| CloudWatch comparison | Beta's default HTTP API route forwards to its backend Lambda with gateway authorization set to `NONE`. The stage has no access-log destination. The two-hour Lambda Logs Insights query scanned zero records. |
| Local prerequisites | .NET 10 and booted iPhone 16 simulator present. No PostgreSQL process listening on 5432; Docker engine unreachable. |
| Remote AWS API Gateway | Verified `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`. `/health/live` returns HTTP 200 (warm ~124ms). `/health/ready` returns HTTP 200 (warm ~155ms), confirming RDS PostgreSQL connectivity. Registration quiz endpoint returns HTTP 200 (5.9KB, ~128ms). Anonymous session returns HTTP 401 (~118ms). |
| Simulator Remote Test | Overrode `backend_api_base_url` to AWS API Gateway via `simctl defaults write`. App launched cleanly to login screen. Network activity log: success. Screenshot recorded. |

AirPlay is the leading explanation if the installed app used the default address. Local simulator acceptance remains open until the app's effective endpoint and a successful synthetic data load are verified.

## Resume the simulator package

1. Read the repository operating instructions and verification gates. Capture current source, worktree status, intended API endpoint, installed app identity and exact authorized scope. Review concurrent candidate changes before adopting any of them.
2. When local implementation and simulator execution are authorized, use a confirmed free loopback port for both API and Debug app. Verify the app's effective endpoint. The current override is a MAUI preference; a server environment variable alone changes only the server listener.
3. Start isolated, disposable PostgreSQL. Use a generated local `ConnectionStrings__PostgreSql`, a fresh local `Authentication__SigningKey` of at least 32 UTF-8 bytes, and a synthetic account. Record configuration key names and outcomes in evidence. Store secret values in the local development secret mechanism.
4. Require HTTP 200 from `/health/live` and `/health/ready`, HTTP 401 from anonymous `/api/v1/session`, and a successful synthetic sign-in and data load on the simulator. Record the actual loopback host and port, source and binary, route/status, test results and failure evidence.
5. Hand the local results to the owner for review. TestFlight follows accepted simulator evidence and its own approved signed candidate and target checks.

Before reaching a session or context limit, save the current package, exact checkout and SHA, scope and reservations, artifact and endpoint identity, evidence already gathered, outstanding checks, authorization status, and the next command or decision in the repository handoff. Resume through a supported session handoff and recheck drift before acting.
