using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionDiaryCommandEventFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nutrition_diary_command_events",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cursor = table.Column<long>(type: "bigint", nullable: false),
                    diary_date = table.Column<DateOnly>(type: "date", nullable: false),
                    command_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_diary_command_events", x => x.id);
                    table.UniqueConstraint("uq_nutrition_diary_command_events_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_nutrition_diary_command_events_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_diary_event_cursors",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_cursor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_diary_event_cursors", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_nutrition_diary_event_cursors_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_diary_command_events_user_id",
                schema: "app",
                table: "nutrition_diary_command_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_nutrition_diary_command_events_owner_cursor",
                schema: "app",
                table: "nutrition_diary_command_events",
                columns: new[] { "user_id", "cursor" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE app.nutrition_diary_command_events ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.nutrition_diary_command_events FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON app.nutrition_diary_command_events
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                ALTER TABLE app.nutrition_diary_event_cursors ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.nutrition_diary_event_cursors FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON app.nutrition_diary_event_cursors
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON app.nutrition_diary_command_events;
                ALTER TABLE app.nutrition_diary_command_events NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.nutrition_diary_command_events DISABLE ROW LEVEL SECURITY;
                DROP POLICY tenant_isolation ON app.nutrition_diary_event_cursors;
                ALTER TABLE app.nutrition_diary_event_cursors NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.nutrition_diary_event_cursors DISABLE ROW LEVEL SECURITY;
                """);
            migrationBuilder.DropTable(
                name: "nutrition_diary_command_events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "nutrition_diary_event_cursors",
                schema: "app");
        }
    }
}
