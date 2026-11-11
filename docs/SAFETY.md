# Safety, privacy, and security

Read this reference for health-related behavior, sensitive data, authentication, storage, or security changes. These are constraints on implementation and data handling; approved product-flow behavior is owned by [docs/PRODUCT_WORKFLOWS.md](PRODUCT_WORKFLOWS.md).

## Health and workout safety

- Keep ReadinessEngine, CyclePrediction, and related calculations deterministic for empty histories, missing values, invalid durations, and numerical edge cases.
- Do not fabricate cycle dates, readiness measurements, injury details, or physiological readings. Represent uncertainty explicitly.
- Missing, stale, or contradictory safety information must not silently remove a restriction or increase workout load or intensity.
- Apply established injury restrictions and safe alternatives. If the existing rules cannot determine a safe result, request the needed information or withhold the unsafe prescription.
- Keep health synchronization disabled unless the user explicitly authorizes a defined integration and data scope. Apple Health read-only sync is authorized (2026-11-10) for steps, active energy, resting heart rate, HRV and sleep, with backend storage of step and sleep averages owner-approved; see docs/PRODUCT_WORKFLOWS.md. Any approved HealthKit integration must stay within the approved permissions.

## Privacy and data handling

- Keep authoritative personal and health data in first-party PostgreSQL and private object storage.
- Never send health records, cycle data, injuries, measurements, workout history, identifiable photos, credentials, or secrets to external AI or third-party services. Approved generic, non-identifying queries are a separate integration decision.
- Use synthetic fixtures for tests. Never place real health records, tokens, passwords, secrets, or photo contents in logs, errors, test fixtures, or documentation.
- Keep object storage private. Use short-lived, narrowly scoped presigned URLs where needed.
- Require authenticated encrypted transport and encrypted storage. Verify deployed settings before claiming a particular TLS version or encryption algorithm; source configuration alone does not prove deployment compliance.
- Enforce identity and ownership at the server boundary. Avoid exposing sensitive values in validation, logs, or exception messages.

## Implementation discipline

Safety thresholds, offsets, and multipliers belong in descriptive named constants or reviewed configuration, not scattered literals. Preserve data minimization and deletion boundaries. Any change to health-data scope, external integrations, clinical rules, or live security controls requires the applicable verification gates and authorization.
