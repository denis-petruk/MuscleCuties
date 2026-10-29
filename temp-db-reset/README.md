# Temporary Database Reset & In-Place Schema Fix Tool

This folder is a standalone one-off utility (not part of `MuscleCuties.sln`) that:
1. Removes all users, user profiles, and tenant/authentication records from the `app` schema.
2. Reconciles the database schema in-place to match the single consolidated baseline (`20261017003443_InitialPostgreSqlApplication`) and resets `public."__EFMigrationsHistory"` to that single baseline entry.
3. Re-verifies/seeds the canonical catalog (`CanonicalCatalogManifest.json`).

## Usage

Set `ConnectionStrings__PostgreSql` (or pass the connection string as the first argument) and run:

```bash
./temp-db-reset/run-reset.sh
```

Or to drop and recreate `app`, `catalog`, `ops`, and `__EFMigrationsHistory` from scratch using the consolidated baseline:

```bash
./temp-db-reset/run-reset.sh --recreate
```

Once finished, delete the `temp-db-reset` folder:

```bash
rm -rf temp-db-reset
```
