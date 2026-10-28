using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261028100000_AddHiitEvidenceCaptureChallenges")]
public sealed class AddHiitEvidenceCaptureChallenges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE app.hiit_evidence_capture_challenges (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            issued_at_utc timestamp with time zone NOT NULL, expires_at_utc timestamp with time zone NOT NULL,
            issued_local_date date NOT NULL, time_zone_version bigint NOT NULL, consumed_at_utc timestamp with time zone NULL,
            consumed_request_hash text NULL, result_answer_version bigint NULL, result_safety_plan_changed boolean NULL,
            CONSTRAINT uq_hiit_evidence_capture_challenges_user_id_id UNIQUE (user_id, id)
        );
        CREATE INDEX ix_hiit_evidence_capture_challenges_user_id ON app.hiit_evidence_capture_challenges(user_id);
        CREATE INDEX ix_hiit_evidence_capture_challenges_user_id_expires_at_utc ON app.hiit_evidence_capture_challenges(user_id, expires_at_utc);
        ALTER TABLE app.hiit_evidence_capture_challenges ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.hiit_evidence_capture_challenges FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON app.hiit_evidence_capture_challenges
            USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
            WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.hiit_evidence_capture_challenges TO musclecuties_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("hiit_evidence_capture_challenges", "app");
}
