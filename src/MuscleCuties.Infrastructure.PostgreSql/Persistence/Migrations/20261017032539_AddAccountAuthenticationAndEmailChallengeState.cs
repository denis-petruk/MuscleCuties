using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261017032539_AddAccountAuthenticationAndEmailChallengeState")]
public sealed class AddAccountAuthenticationAndEmailChallengeState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE app.refresh_sessions (
            id uuid PRIMARY KEY,
            user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            family_id uuid NOT NULL,
            token_hash character varying(64) NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            expires_at_utc timestamp with time zone NOT NULL,
            revoked_at_utc timestamp with time zone NULL,
            replaced_at_utc timestamp with time zone NULL,
            CONSTRAINT ck_refresh_sessions_expiry CHECK (expires_at_utc > created_at_utc)
        );
        CREATE UNIQUE INDEX uq_refresh_sessions_token_hash ON app.refresh_sessions(token_hash);
        CREATE INDEX ix_refresh_sessions_family_revoked ON app.refresh_sessions(family_id, revoked_at_utc);

        CREATE TABLE app.apple_sign_in_nonces (
            id uuid PRIMARY KEY,
            nonce_hash character varying(64) NOT NULL UNIQUE,
            expires_at_utc timestamp with time zone NOT NULL,
            redeemed_at_utc timestamp with time zone NULL
        );
        CREATE INDEX ix_apple_sign_in_nonces_expiry ON app.apple_sign_in_nonces(expires_at_utc);

        CREATE TABLE app.revoked_access_tokens (
            id uuid PRIMARY KEY,
            user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            token_id character varying(128) NOT NULL UNIQUE,
            expires_at_utc timestamp with time zone NOT NULL,
            revoked_at_utc timestamp with time zone NOT NULL
        );
        CREATE INDEX ix_revoked_access_tokens_expiry ON app.revoked_access_tokens(expires_at_utc);

        CREATE TABLE app.email_challenge_sessions (
            id uuid PRIMARY KEY,
            normalized_email character varying(320) NOT NULL,
            purpose integer NOT NULL,
            hmac_key_id character varying(64) NOT NULL,
            code_mac character varying(64) NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            expires_at_utc timestamp with time zone NOT NULL,
            resend_available_at_utc timestamp with time zone NOT NULL,
            consumed_at_utc timestamp with time zone NULL,
            superseded_at_utc timestamp with time zone NULL,
            locked_at_utc timestamp with time zone NULL,
            failed_attempts integer NOT NULL DEFAULT 0,
            receipt_id character varying(128) NULL,
            CONSTRAINT ck_email_challenge_session_purpose CHECK (purpose IN (1, 2)),
            CONSTRAINT ck_email_challenge_session_attempts CHECK (failed_attempts >= 0),
            CONSTRAINT ck_email_challenge_session_expiry CHECK (expires_at_utc > created_at_utc)
        );
        CREATE UNIQUE INDEX uq_email_challenge_sessions_receipt_id
            ON app.email_challenge_sessions(receipt_id) WHERE receipt_id IS NOT NULL;
        CREATE INDEX ix_email_challenge_sessions_email_created
            ON app.email_challenge_sessions(normalized_email, created_at_utc DESC);
        CREATE INDEX ix_email_challenge_sessions_expiry
            ON app.email_challenge_sessions(expires_at_utc);

        CREATE TABLE app.source_account_claim_nonces (
            id uuid PRIMARY KEY,
            destination_user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            source_installation_key_hash character varying(64) NOT NULL,
            source_account_key_hash character varying(64) NOT NULL,
            nonce_hash character varying(64) NOT NULL UNIQUE,
            created_at_utc timestamp with time zone NOT NULL,
            expires_at_utc timestamp with time zone NOT NULL,
            redeemed_at_utc timestamp with time zone NULL,
            CONSTRAINT ck_source_account_claim_expiry CHECK (expires_at_utc > created_at_utc)
        );
        CREATE INDEX ix_source_account_claim_nonces_expiry ON app.source_account_claim_nonces(expires_at_utc);

        CREATE TABLE app.source_account_mappings (
            id uuid PRIMARY KEY,
            claim_id uuid NOT NULL REFERENCES app.source_account_claim_nonces(id) ON DELETE RESTRICT,
            destination_user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            source_installation_key_hash character varying(64) NOT NULL,
            source_account_key_hash character varying(64) NOT NULL,
            created_at_utc timestamp with time zone NOT NULL
        );
        CREATE UNIQUE INDEX uq_source_account_mappings_source
            ON app.source_account_mappings(source_installation_key_hash, source_account_key_hash);
        CREATE UNIQUE INDEX uq_source_account_mappings_claim_id ON app.source_account_mappings(claim_id);
        CREATE INDEX ix_source_account_mappings_destination_user_id ON app.source_account_mappings(destination_user_id);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS app.email_challenge_sessions;
        DROP TABLE IF EXISTS app.source_account_mappings;
        DROP TABLE IF EXISTS app.source_account_claim_nonces;
        DROP TABLE IF EXISTS app.revoked_access_tokens;
        DROP TABLE IF EXISTS app.apple_sign_in_nonces;
        DROP TABLE IF EXISTS app.refresh_sessions;
        """);
}
