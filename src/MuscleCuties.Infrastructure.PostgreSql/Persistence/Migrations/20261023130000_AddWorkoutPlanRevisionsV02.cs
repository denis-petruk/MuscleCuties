using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261023130000_AddWorkoutPlanRevisionsV02")]
public partial class AddWorkoutPlanRevisionsV02 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "workout_plan_proposals_v02",
            schema: "app",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                expected_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                ruleset_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                training_policy_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                input_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                proposal_json = table.Column<string>(type: "jsonb", nullable: false),
                reasons_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                accepted_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                concurrency_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_workout_plan_proposals_v02", x => x.id);
                table.UniqueConstraint("uq_workout_plan_proposals_v02_user_id_id", x => new { x.user_id, x.id });
                table.ForeignKey("fk_workout_plan_proposals_v02_users_user_id", x => x.user_id,
                    principalSchema: "app", principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("ck_workout_plan_proposals_v02_status",
                    "status IN ('Proposed', 'Accepted', 'Rejected', 'Expired')");
                table.CheckConstraint("ck_workout_plan_proposals_v02_decision_pair",
                    "(status = 'Proposed' AND decided_at_utc IS NULL AND accepted_revision_id IS NULL) OR " +
                    "(status = 'Rejected' AND decided_at_utc IS NOT NULL AND accepted_revision_id IS NULL) OR " +
                    "(status = 'Accepted' AND decided_at_utc IS NOT NULL AND accepted_revision_id IS NOT NULL) OR " +
                    "(status = 'Expired' AND decided_at_utc IS NOT NULL AND accepted_revision_id IS NULL)");
                table.CheckConstraint("ck_workout_plan_proposals_v02_concurrency", "concurrency_version > 0");
            });

        migrationBuilder.CreateTable(
            name: "workout_plan_revisions_v02",
            schema: "app",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                revision_number = table.Column<long>(type: "bigint", nullable: false),
                source_proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                ruleset_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                training_policy_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                plan_json = table.Column<string>(type: "jsonb", nullable: false),
                input_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                accepted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_workout_plan_revisions_v02", x => x.id);
                table.UniqueConstraint("uq_workout_plan_revisions_v02_user_id_id", x => new { x.user_id, x.id });
                table.UniqueConstraint("uq_workout_plan_revisions_v02_owner_plan_number",
                    x => new { x.user_id, x.plan_id, x.revision_number });
                table.ForeignKey("fk_workout_plan_revisions_v02_users_user_id", x => x.user_id,
                    principalSchema: "app", principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("fk_workout_plan_revisions_v02_proposals_user_id_source_proposal_id",
                    x => new { x.user_id, x.source_proposal_id }, principalSchema: "app",
                    principalTable: "workout_plan_proposals_v02", principalColumns: new[] { "user_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_workout_plan_revisions_v02_number", "revision_number > 0");
            });

        migrationBuilder.CreateIndex(
            name: "ix_workout_plan_proposals_v02_user_id",
            schema: "app", table: "workout_plan_proposals_v02", column: "user_id");
        migrationBuilder.CreateIndex(
            name: "ix_workout_plan_proposals_v02_owner_plan_created",
            schema: "app", table: "workout_plan_proposals_v02",
            columns: new[] { "user_id", "plan_id", "created_at_utc" }, descending: new[] { false, false, true });
        migrationBuilder.CreateIndex(
            name: "ix_workout_plan_revisions_v02_user_id",
            schema: "app", table: "workout_plan_revisions_v02", column: "user_id");
        migrationBuilder.CreateIndex(
            name: "ix_workout_plan_revisions_v02_owner_plan_accepted",
            schema: "app", table: "workout_plan_revisions_v02",
            columns: new[] { "user_id", "plan_id", "accepted_at_utc" }, descending: new[] { false, false, true });

        migrationBuilder.Sql("""
            ALTER TABLE app.workout_plan_proposals_v02 ENABLE ROW LEVEL SECURITY;
            ALTER TABLE app.workout_plan_proposals_v02 FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON app.workout_plan_proposals_v02
                USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
            ALTER TABLE app.workout_plan_revisions_v02 ENABLE ROW LEVEL SECURITY;
            ALTER TABLE app.workout_plan_revisions_v02 FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON app.workout_plan_revisions_v02
                USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "workout_plan_revisions_v02", schema: "app");
        migrationBuilder.DropTable(name: "workout_plan_proposals_v02", schema: "app");
    }
}
