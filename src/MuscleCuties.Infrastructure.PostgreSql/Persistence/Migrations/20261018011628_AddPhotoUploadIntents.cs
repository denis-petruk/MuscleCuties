using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoUploadIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photo_upload_intents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    s3_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    declared_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_upload_intents", x => x.id);
                    table.UniqueConstraint("uq_photo_upload_intents_user_id_id", x => new { x.user_id, x.id });
                    table.CheckConstraint("ck_photo_upload_intents_expiry", "expires_at_utc > created_at_utc");
                    table.CheckConstraint("ck_photo_upload_intents_size_positive", "declared_size_bytes > 0");
                    table.CheckConstraint("ck_photo_upload_intents_state", "state IN ('Pending', 'Committed', 'Cancelled', 'Expired')");
                    table.ForeignKey(
                        name: "FK_photo_upload_intents_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photo_upload_intents_owner_expiry",
                schema: "app",
                table: "photo_upload_intents",
                columns: new[] { "user_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_photo_upload_intents_user_id",
                schema: "app",
                table: "photo_upload_intents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_photo_upload_intents_owner_idempotency",
                schema: "app",
                table: "photo_upload_intents",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_photo_upload_intents_s3_key",
                schema: "app",
                table: "photo_upload_intents",
                column: "s3_key",
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE app.photo_upload_intents ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.photo_upload_intents FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON app.photo_upload_intents
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON app.photo_upload_intents;
                ALTER TABLE app.photo_upload_intents NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.photo_upload_intents DISABLE ROW LEVEL SECURITY;
                """);
            migrationBuilder.DropTable(
                name: "photo_upload_intents",
                schema: "app");
        }
    }
}
