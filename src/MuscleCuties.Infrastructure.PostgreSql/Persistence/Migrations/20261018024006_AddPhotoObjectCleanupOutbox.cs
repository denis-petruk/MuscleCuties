using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoObjectCleanupOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_photo_upload_intents_state",
                schema: "app",
                table: "photo_upload_intents");

            migrationBuilder.CreateTable(
                name: "photo_object_cleanups",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    s3_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    not_before_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_generation = table.Column<long>(type: "bigint", nullable: false),
                    lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_failure_category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_object_cleanups", x => x.id);
                    table.UniqueConstraint("uq_photo_object_cleanups_user_id_id", x => new { x.user_id, x.id });
                    table.CheckConstraint("ck_photo_object_cleanups_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_photo_object_cleanups_lease", "(state = 'Leased' AND lease_token IS NOT NULL AND lease_expires_at_utc IS NOT NULL) OR (state <> 'Leased' AND lease_token IS NULL AND lease_expires_at_utc IS NULL)");
                    table.CheckConstraint("ck_photo_object_cleanups_reason", "reason IN ('CancelledUpload', 'ExpiredUpload', 'CommittedPhoto')");
                    table.CheckConstraint("ck_photo_object_cleanups_state", "state IN ('Pending', 'Leased', 'Retryable', 'Completed')");
                    table.ForeignKey(
                        name: "FK_photo_object_cleanups_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_photo_object_cleanups_failure_category",
                schema: "app",
                table: "photo_object_cleanups",
                sql: "last_failure_category IS NULL OR last_failure_category IN ('Transient', 'Permission', 'Configuration', 'Provider', 'Unknown')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_photo_upload_intents_state",
                schema: "app",
                table: "photo_upload_intents",
                sql: "state IN ('Pending', 'Committed', 'Cancelled', 'Expired', 'Deleted')");

            migrationBuilder.CreateIndex(
                name: "ix_photo_object_cleanups_due",
                schema: "app",
                table: "photo_object_cleanups",
                columns: new[] { "not_before_utc", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_photo_object_cleanups_user_id",
                schema: "app",
                table: "photo_object_cleanups",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_photo_object_cleanups_owner_photo_reason",
                schema: "app",
                table: "photo_object_cleanups",
                columns: new[] { "user_id", "photo_id", "reason" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE app.photo_object_cleanups ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.photo_object_cleanups FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON app.photo_object_cleanups
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_photo_object_cleanups_failure_category",
                schema: "app",
                table: "photo_object_cleanups");
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON app.photo_object_cleanups;
                ALTER TABLE app.photo_object_cleanups NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.photo_object_cleanups DISABLE ROW LEVEL SECURITY;
                """);
            migrationBuilder.DropTable(
                name: "photo_object_cleanups",
                schema: "app");

            migrationBuilder.DropCheckConstraint(
                name: "ck_photo_upload_intents_state",
                schema: "app",
                table: "photo_upload_intents");

            migrationBuilder.AddCheckConstraint(
                name: "ck_photo_upload_intents_state",
                schema: "app",
                table: "photo_upload_intents",
                sql: "state IN ('Pending', 'Committed', 'Cancelled', 'Expired')");
        }
    }
}
