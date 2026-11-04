## Summary & Scope
<!-- Provide a clear description of the change, the package ID, and the issue/objective it addresses -->

- **Package ID:**
- **Logical Role:** [ ] Product (A)  [ ] Platform (B)  [ ] Integration Authority (I)
- **Base SHA:**
- **Head SHA:**

## Architectural & Safety Contracts
- [ ] Preserves clean architecture dependency directions (Core has no outer dependencies).
- [ ] No direct modifications to shared DTOs/migrations without exclusive Integration reservation.
- [ ] Workout generation/acceptance, tri-state injury, hydration, and history semantics preserved.
- [ ] No hardcoded secrets, connection strings, or cloud credentials.
- [ ] No unauthorized cloud mutation, shared DB alterations, or deployment triggers.

## Verification Evidence
- [ ] Local build (`dotnet build`) succeeds without warnings/errors.
- [ ] Applicable test suites executed with clean results.
- [ ] Disposable synthetic fixtures used for all integration tests.
- [ ] Test commands and TRX / output attached:

## Remaining External Gates
<!-- Identify any required manual approvals, cloud migrations, or owner review items before merge -->
- [ ] Owner PR merge approval
- [ ] Branch protection readback verified
